using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using SpotlightNative.Models;
using SpotlightNative.Services;

namespace SpotlightNative.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ConfigService _config;
    private readonly ISearchProvider _calculator;
    private readonly ISearchProvider _google;
    private readonly ISearchProvider _apps;
    private readonly ISearchProvider _files;
    private CancellationTokenSource? _searchCancellation;
    private string _query = "";
    private int _selectedIndex;
    public ObservableCollection<SearchResult> Results { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            _query = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsQueryEmpty));
            DebounceSearch();
        }
    }
    public bool IsQueryEmpty => string.IsNullOrEmpty(_query);
    public MainViewModel(ConfigService config)
    {
        _config = config;
        _google = new GoogleSearchProvider(config.Current.SearchEngines);
        _apps = new AppIndexService(config.Current);
        _calculator = new CalculatorService(config.Current.CalculatorMode);
        _files = new FileIndexService(config.Current);
    }
    private async void DebounceSearch()
    {
        _searchCancellation?.Cancel();
        var cts = _searchCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(30, cts.Token);
            var query = Query.Trim();
            var command = CreateCommand(query);
            var groups = await Task.WhenAll(
                SearchProviderSafelyAsync(_calculator, query, cts.Token),
                SearchProviderSafelyAsync(_apps, query, cts.Token),
                SearchProviderSafelyAsync(_files, query, cts.Token),
                SearchProviderSafelyAsync(_google, query, cts.Token));
            if (cts.IsCancellationRequested) return;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Results.Clear();
                var all = groups.SelectMany(x => x).ToList();
                var calculator = all.FirstOrDefault(result => result.Kind == SearchResultKind.Calculator);
                var google = all.LastOrDefault(result => result.Kind == SearchResultKind.Google);
                if (command is not null) Results.Add(command);
                if (calculator is not null) Results.Add(calculator);
                foreach (var result in all.Where(result => result.Kind == SearchResultKind.App).Take(Math.Min(_config.Current.MaxApplications, _config.Current.MaxResults))) Results.Add(result);
                foreach (var result in all.Where(result => result.Kind == SearchResultKind.Folder).Take(Math.Min(_config.Current.MaxFolders, _config.Current.MaxResults))) Results.Add(result);
                foreach (var result in all.Where(result => result.Kind == SearchResultKind.File).Take(Math.Min(_config.Current.MaxFiles, _config.Current.MaxResults))) Results.Add(result);
                if (google is not null && !string.IsNullOrWhiteSpace(query)) Results.Add(google);
                SearchResultKind? previousKind = null;
                foreach (var result in Results)
                {
                    result.ShowSectionLabel = result.Kind is not SearchResultKind.Calculator and not SearchResultKind.Command
                        && result.Kind != previousKind;
                    previousKind = result.Kind;
                }
                var isWebPrefix = query.StartsWith("g ", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("yt ", StringComparison.OrdinalIgnoreCase);
                _selectedIndex = Results.Count == 0
                    ? -1
                    : isWebPrefix && google is not null
                        ? Results.IndexOf(google)
                        : 0;
                UpdateSelection();
                StartupLogger.Write($"Search query='{query}' results={Results.Count} web={google is not null}");
                Debug.WriteLine($"Spotlight search query='{query}' results={Results.Count} web={google is not null}");
                Debug.WriteLine($"Spotlight search working set: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
            });
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException exception)
        {
            Debug.WriteLine($"Search failed: {exception.Message}");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Search failed: {exception}");
        }
    }
    private static async Task<IReadOnlyList<SearchResult>> SearchProviderSafelyAsync(
        ISearchProvider provider, string query, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Search provider failed: {exception.Message}");
            return [];
        }
    }
    public void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        _selectedIndex = (_selectedIndex + delta + Results.Count) % Results.Count;
        UpdateSelection();
        StartupLogger.Write($"Selection moved to index {_selectedIndex}.");
        Debug.WriteLine($"Spotlight selection index={_selectedIndex}");
    }
    public void SelectFirst()
    {
        if (Results.Count == 0) return;
        _selectedIndex = 0;
        UpdateSelection();
    }
    public void SelectLast()
    {
        if (Results.Count == 0) return;
        _selectedIndex = Results.Count - 1;
        UpdateSelection();
    }
    public async Task ExecuteSelected(bool reveal)
    {
        if (_selectedIndex < 0 || _selectedIndex >= Results.Count) return;
        await (Results[_selectedIndex].ExecuteAsync?.Invoke(reveal) ?? Task.CompletedTask);
    }
    public Task ExecuteAt(int index, bool reveal) { _selectedIndex = index; return ExecuteSelected(reveal); }
    public void RebuildIndex() => (_files as FileIndexService)?.Rebuild();
    public void PauseIndex() => (_files as FileIndexService)?.Pause();
    public void ResumeIndex() => (_files as FileIndexService)?.Resume();
    private static SearchResult? CreateCommand(string query) =>
        query.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        query.Equals("exit", StringComparison.OrdinalIgnoreCase)
            ? new SearchResult
            {
                Title = "Quit Spotlight Native",
                Subtitle = "Command",
                Kind = SearchResultKind.Command,
                ExecuteAsync = _ =>
                {
                    System.Windows.Application.Current.Shutdown();
                    return Task.FromResult(true);
                }
            }
            : null;
    private void UpdateSelection()
    {
        for (var i = 0; i < Results.Count; i++) Results[i].IsSelected = i == _selectedIndex;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Dispose()
    {
        _searchCancellation?.Cancel();
        (_files as IDisposable)?.Dispose();
    }
}
