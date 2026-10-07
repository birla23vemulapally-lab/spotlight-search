using System.Text.Json;
using System.Text.Json.Serialization;
using SpotlightNative.Models;

namespace SpotlightNative.Services;

/// <summary>Loads and persists user configuration in the roaming AppData profile.</summary>
public sealed class ConfigService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotlightNative", "config.json");
    private readonly FileSystemWatcher _watcher;
    public AppConfig Current { get; private set; }
    public event EventHandler? Changed;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigService()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (!File.Exists(_path)) { Current = new AppConfig(); Save(); }
        else Current = Load();

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_path)!, Path.GetFileName(_path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Changed += OnConfigFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    public void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
    public void Open() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_path) { UseShellExecute = true });
    public void Dispose() => _watcher.Dispose();

    private AppConfig Load()
    {
        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), JsonOptions) ?? new AppConfig();
            return config;
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Thread.Sleep(75);
            var updated = Load();
            Current = updated;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (IOException) { }
    }
}
