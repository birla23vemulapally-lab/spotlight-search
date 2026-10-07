using System.Text.Json;

namespace SpotlightNative.Services;

public sealed class UsageStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpotlightNative", "usage.json");
    private readonly Dictionary<string, UsageEntry> _entries;
    private readonly object _gate = new();

    public UsageStore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        try
        {
            _entries = JsonSerializer.Deserialize<Dictionary<string, UsageEntry>>(
                File.Exists(_path) ? File.ReadAllText(_path) : "") ?? [];
        }
        catch (JsonException)
        {
            _entries = [];
        }
    }

    public double GetBoost(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return 0;
        lock (_gate)
        {
            if (!_entries.TryGetValue(path, out var entry)) return 0;
            var ageDays = Math.Max(0, (DateTime.UtcNow - entry.LastOpenedUtc).TotalDays);
            return Math.Min(18, Math.Log2(entry.Count + 1) * 2 + Math.Max(0, 6 - ageDays));
        }
    }

    public void Record(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        lock (_gate)
        {
            _entries.TryGetValue(path, out var entry);
            _entries[path] = new UsageEntry((entry?.Count ?? 0) + 1, DateTime.UtcNow);
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException) { }
        }
    }

    private sealed record UsageEntry(int Count, DateTime LastOpenedUtc);
}
