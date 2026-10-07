using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;
using SpotlightNative.Models;

namespace SpotlightNative.Services;

/// <summary>Indexes Start Menu .lnk files and launches them through the Windows shell.</summary>
public sealed class AppIndexService : ISearchProvider
{
    private readonly List<(string Name, string Path)> _apps = [];
    private readonly AppConfig _config;
    private readonly ConcurrentDictionary<string, BitmapSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _appsLock = new();
    private readonly UsageStore _usage = new();

    public AppIndexService(AppConfig config)
    {
        _config = config;
        _ = Task.Run(LoadApps);
    }
    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        return Task.Run<IReadOnlyList<SearchResult>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string Name, string Path)[] apps;
            lock (_appsLock) apps = _apps.ToArray();
            var candidates = apps.Select(a => new SearchResult
            {
                Title = a.Name, Subtitle = "Application", Path = a.Path, Kind = SearchResultKind.App,
                ExecuteAsync = _ => { _usage.Record(a.Path); Process.Start(new ProcessStartInfo(a.Path) { UseShellExecute = true }); return Task.FromResult(true); }
            });
            var results = new FuzzySearchEngine(candidates, _usage, _config.MaxEditDistance, _config.EnableFuzzy)
                .Search(query, 20);
            foreach (var result in results)
                if (!string.IsNullOrWhiteSpace(result.Path))
                    result.Icon = _iconCache.GetOrAdd(result.Path, IconFor);
            return results;
        }, cancellationToken);
    }

    private void LoadApps()
    {
        var discovered = new List<(string Name, string Path)>();
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        foreach (var root in roots.Where(Directory.Exists))
        {
            var searchRoot = Directory.Exists(Path.Combine(root, "Programs"))
                ? Path.Combine(root, "Programs") : root;
            try
            {
                foreach (var file in Directory.EnumerateFiles(searchRoot, "*.lnk", SearchOption.AllDirectories))
                {
                    discovered.Add((Path.GetFileNameWithoutExtension(file), file));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        lock (_appsLock)
        {
            _apps.Clear();
            _apps.AddRange(discovered);
        }
    }
    private static int Score(string name, string query, IReadOnlyList<string> aliases)
    {
        if (aliases.Any(alias => name.Equals(alias, StringComparison.OrdinalIgnoreCase))) return 0;
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 2;
        var words = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => word.StartsWith(query, StringComparison.OrdinalIgnoreCase))) return 3;
        var initials = string.Concat(words.Select(word => word[0]));
        if (initials.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 4;
        var subsequence = 0;
        var normalizedName = name.ToLowerInvariant();
        foreach (var character in query)
        {
            var index = normalizedName.IndexOf(char.ToLowerInvariant(character), subsequence);
            if (index < 0) return int.MaxValue;
            subsequence = index + 1;
        }
        return 5;
    }
    private static BitmapSource? IconFor(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            return source;
        }
        catch { return null; }
    }
}
