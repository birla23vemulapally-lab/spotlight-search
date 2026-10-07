using System.Diagnostics;
using Microsoft.Data.Sqlite;
using SpotlightNative.Models;

namespace SpotlightNative.Services;

public sealed class FileIndexService : ISearchProvider, IDisposable
{
    private readonly AppConfig _config;
    private readonly string _databasePath;
    private readonly string[] _roots;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Task _initialization;
    private volatile bool _paused;
    private readonly UsageStore _usage = new();

    public FileIndexService(AppConfig config)
    {
        _config = config;
        _databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotlightNative", "index.db");
        _roots = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .Concat(config.IndexedFolders.Where(Directory.Exists))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _initialization = Task.Run(async () =>
        {
            InitializeDatabase();
            foreach (var root in _roots) Watch(root);
            await Task.CompletedTask;
        }, _stop.Token);
        _ = Task.Run(async () =>
        {
            await _initialization;
            await CrawlAsync(_stop.Token);
        }, _stop.Token);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        try
        {
            await _initialization.WaitAsync(cancellationToken);
            await using var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT name, path, kind
                FROM entries
                WHERE name_lower LIKE $contains
                ORDER BY
                    CASE WHEN name_lower = $query THEN 0
                         WHEN name_lower LIKE $starts THEN 1
                         WHEN name_lower LIKE $wordStart THEN 2
                         ELSE 3 END,
                    length(path), launch_count DESC, modified DESC
                LIMIT 20
                """;
            var normalized = query.Trim().ToLowerInvariant();
            command.Parameters.AddWithValue("$query", normalized);
            command.Parameters.AddWithValue("$starts", normalized + "%");
            command.Parameters.AddWithValue("$wordStart", "% " + normalized + "%");
            command.Parameters.AddWithValue("$contains", "%" + normalized[0] + "%");
            var results = new List<SearchResult>(20);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var kind = reader.GetString(2).Equals("folder", StringComparison.OrdinalIgnoreCase)
                    ? SearchResultKind.Folder : SearchResultKind.File;
                results.Add(CreateResult(reader.GetString(0), reader.GetString(1), kind));
            }
            return new FuzzySearchEngine(results, _usage, _config.MaxEditDistance, _config.EnableFuzzy)
                .Search(query, 20);
        }
        catch (SqliteException exception)
        {
            Debug.WriteLine($"File index search failed: {exception.Message}");
            return [];
        }
    }

    public void Rebuild() => _ = Task.Run(async () =>
    {
        await _initialization;
        await CrawlAsync(_stop.Token, true);
    }, _stop.Token);
    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    private async Task CrawlAsync(CancellationToken cancellationToken, bool rebuild = false)
    {
        var previousPriority = Thread.CurrentThread.Priority;
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        try
        {
            if (rebuild)
            {
                await using var clearConnection = new SqliteConnection($"Data Source={_databasePath}");
                await clearConnection.OpenAsync(cancellationToken);
                await using var clear = clearConnection.CreateCommand();
                clear.CommandText = "DELETE FROM entries";
                await clear.ExecuteNonQueryAsync(cancellationToken);
            }
            var batch = new List<(string Name, string Path, string Kind, string Extension, long Modified)>(1000);
            foreach (var root in _roots)
            {
                foreach (var entry in Enumerate(root, cancellationToken))
                {
                    batch.Add(entry);
                    if (batch.Count < 1000) continue;
                    await UpsertBatchAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }
            if (batch.Count > 0) await UpsertBatchAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Debug.WriteLine($"File index crawl failed: {exception.Message}"); }
        finally
        {
            Thread.CurrentThread.Priority = previousPriority;
        }
    }

    private IEnumerable<(string Name, string Path, string Kind, string Extension, long Modified)> Enumerate(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, ReturnSpecialDirectories = false };
        while (pending.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            while (_paused && !cancellationToken.IsCancellationRequested)
                Thread.Sleep(25);
            var folder = pending.Pop();
            if (!Allowed(folder)) continue;
            yield return (Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : folder, folder, "folder", "", LastWriteTicks(folder));
            IEnumerable<string> directories;
            IEnumerable<string> files;
            try
            {
                directories = Directory.EnumerateDirectories(folder, "*", options);
                files = Directory.EnumerateFiles(folder, "*", options);
            }
            catch (IOException) { continue; }
            foreach (var directory in directories) if (Allowed(directory)) pending.Push(directory);
            foreach (var file in files) if (Allowed(file))
                yield return (Path.GetFileNameWithoutExtension(file), file, "file", Path.GetExtension(file), LastWriteTicks(file));
        }
    }

    private async Task UpsertBatchAsync(IReadOnlyList<(string Name, string Path, string Kind, string Extension, long Modified)> batch, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        foreach (var entry in batch)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO entries(name, name_lower, path, kind, extension, modified)
                VALUES($name, $nameLower, $path, $kind, $extension, $modified)
                ON CONFLICT(path) DO UPDATE SET name=$name, name_lower=$nameLower, kind=$kind, extension=$extension, modified=$modified
                """;
            command.Parameters.AddWithValue("$name", entry.Name);
            command.Parameters.AddWithValue("$nameLower", entry.Name.ToLowerInvariant());
            command.Parameters.AddWithValue("$path", entry.Path);
            command.Parameters.AddWithValue("$kind", entry.Kind);
            command.Parameters.AddWithValue("$extension", entry.Extension);
            command.Parameters.AddWithValue("$modified", entry.Modified);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private SearchResult CreateResult(string name, string path, SearchResultKind kind) => new()
    {
        Title = name,
        Subtitle = kind == SearchResultKind.Folder ? $"Folder  {path}" : path,
        Path = path,
        Kind = kind,
        ExecuteAsync = async reveal =>
        {
            if (reveal || kind == SearchResultKind.Folder)
                Process.Start(new ProcessStartInfo("explorer.exe", reveal ? $"/select,\"{path}\"" : $"\"{path}\"") { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            _usage.Record(path);
            await RecordLaunchAsync(path);
        }
    };

    private void Watch(string root)
    {
        try
        {
            var watcher = new FileSystemWatcher(root) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
            watcher.Created += (_, args) => QueuePath(args.FullPath);
            watcher.Changed += (_, args) => QueuePath(args.FullPath);
            watcher.Renamed += (_, args) => QueuePath(args.FullPath);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
        catch (IOException) { }
    }

    private void QueuePath(string path)
    {
        if (!Allowed(path)) return;
        _ = Task.Run(async () =>
        {
            var isFolder = Directory.Exists(path);
            if (!isFolder && !File.Exists(path)) return;
            var entry = (Path.GetFileNameWithoutExtension(path), path, isFolder ? "folder" : "file", isFolder ? "" : Path.GetExtension(path), LastWriteTicks(path));
            await UpsertBatchAsync([entry], _stop.Token);
        }, _stop.Token);
    }

    private void InitializeDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS entries(
                name TEXT NOT NULL, name_lower TEXT NOT NULL, path TEXT PRIMARY KEY,
                kind TEXT NOT NULL, extension TEXT NOT NULL, modified INTEGER NOT NULL,
                launch_count INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_entries_name_lower ON entries(name_lower);
            """;
        command.ExecuteNonQuery();
        TryAddColumn(connection, "name_lower TEXT NOT NULL DEFAULT ''");
        TryAddColumn(connection, "extension TEXT NOT NULL DEFAULT ''");
        using var migrate = connection.CreateCommand();
        migrate.CommandText = "UPDATE entries SET name_lower=lower(name) WHERE name_lower=''";
        migrate.ExecuteNonQuery();
    }

    private static void TryAddColumn(SqliteConnection connection, string definition)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE entries ADD COLUMN {definition}";
            command.ExecuteNonQuery();
        }
        catch (SqliteException) { }
    }

    private async Task RecordLaunchAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE entries SET launch_count = launch_count + 1 WHERE path=$path";
        command.Parameters.AddWithValue("$path", path);
        await command.ExecuteNonQueryAsync();
    }

    private bool Allowed(string path) => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(part => _config.ExcludedFolders.Any(exclude => part.Equals(exclude, StringComparison.OrdinalIgnoreCase)));
    private static long LastWriteTicks(string path) { try { return Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path).Ticks : File.GetLastWriteTimeUtc(path).Ticks; } catch (IOException) { return 0; } }
    public void Dispose() { _stop.Cancel(); foreach (var watcher in _watchers) watcher.Dispose(); }
}
