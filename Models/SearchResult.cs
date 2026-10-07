using System.Windows.Media;
using System.ComponentModel;

namespace SpotlightNative.Models;

public enum SearchResultKind { Command, Calculator, App, Folder, File, Google }
public sealed class SearchResult : INotifyPropertyChanged
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "";
    public SearchResultKind Kind { get; init; }
    public string? Path { get; init; }
    public ImageSource? Icon { get; set; }
    public int FuzzyScore { get; set; }
    public IReadOnlyList<int> MatchedIndices { get; set; } = [];
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackground)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool ShowSectionLabel { get; set; }
    public string RowBackground => IsSelected ? "#FF315A9E" : "Transparent";
    public string SectionLabel => Kind switch
    {
        SearchResultKind.App => "Applications",
        SearchResultKind.Folder => "Folders",
        SearchResultKind.File => "Files",
        SearchResultKind.Google => "Web",
        _ => "Commands"
    };
    public string KindGlyph => Kind switch
    {
        SearchResultKind.Command => ">",
        SearchResultKind.Calculator => "=",
        SearchResultKind.App => "A",
        SearchResultKind.Folder => "D",
        SearchResultKind.File => "F",
        SearchResultKind.Google => "W",
        _ => "•"
    };
    public Func<bool, Task>? ExecuteAsync { get; init; }
}
