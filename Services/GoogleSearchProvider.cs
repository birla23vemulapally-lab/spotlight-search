using System.Diagnostics;
using SpotlightNative.Models;

namespace SpotlightNative.Services;

public sealed class GoogleSearchProvider : ISearchProvider
{
    private readonly IReadOnlyDictionary<string, string> _engines;
    public GoogleSearchProvider(IReadOnlyDictionary<string, string>? engines = null)
    {
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["g"] = "https://www.google.com/search?q={query}",
            ["yt"] = "https://www.youtube.com/results?search_query={query}"
        };
        if (engines is not null)
            foreach (var pair in engines)
                defaults[pair.Key] = pair.Value;
        _engines = defaults;
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SearchResult>>(CreateResult(query));

    private IReadOnlyList<SearchResult> CreateResult(string query)
    {
        var prefix = "";
        var text = query;
        var firstSpace = query.IndexOf(' ');
        if (firstSpace > 0)
        {
            prefix = query[..firstSpace];
            if (_engines.ContainsKey(prefix)) text = query[(firstSpace + 1)..].Trim();
        }

        var engine = prefix.Length > 0 && _engines.TryGetValue(prefix, out var template)
            ? (Template: template, Name: prefix.Equals("yt", StringComparison.OrdinalIgnoreCase) ? "YouTube" : "Google")
            : (Template: _engines.TryGetValue("g", out var google) ? google : "https://www.google.com/search?q={query}", Name: "Google");
        var url = engine.Template.Replace("{query}", Uri.EscapeDataString(text), StringComparison.Ordinal);
        var title = engine.Name == "Google"
            ? $"Search Google for '{text}'"
            : $"Search {engine.Name} for '{text}'";
        return [new SearchResult
        {
            Title = title, Subtitle = engine.Name, Kind = SearchResultKind.Google,
            ExecuteAsync = _ => { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); return Task.FromResult(true); }
        }];
    }
}
