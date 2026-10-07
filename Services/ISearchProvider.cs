using SpotlightNative.Models;

namespace SpotlightNative.Services;

public interface ISearchProvider
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
