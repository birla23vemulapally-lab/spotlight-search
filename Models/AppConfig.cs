namespace SpotlightNative.Models;

public sealed class AppConfig
{
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";
    public int MaxResults { get; set; } = 8;
    public int MaxEditDistance { get; set; } = 2;
    public bool EnableFuzzy { get; set; } = true;
    public int MaxApplications { get; set; } = 4;
    public int MaxFolders { get; set; } = 3;
    public int MaxFiles { get; set; } = 3;
    public string CalculatorMode { get; set; } = "deg";
    public List<string> IndexedFolders { get; set; } = DefaultIndexedFolders();
    public List<string> ExcludedFolders { get; set; } =
        ["$Recycle.Bin", "System Volume Information", "Windows", "node_modules", ".git", "Temp", "Cache"];
    public bool Autostart { get; set; } = true;
    public Dictionary<string, string> SearchEngines { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = "https://www.google.com/search?q={query}",
        ["yt"] = "https://www.youtube.com/results?search_query={query}"
    };
    public Dictionary<string, List<string>> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vs"] = ["Visual Studio Code"],
        ["vsc"] = ["Visual Studio Code"],
        ["code"] = ["Visual Studio Code"],
        ["goog"] = ["Google Chrome"],
        ["google"] = ["Google Chrome"],
        ["browser"] = ["Google Chrome", "Microsoft Edge", "Firefox"],
        ["chrome"] = ["Google Chrome"],
        ["pic"] = ["Photos", "Paint"],
        ["photo"] = ["Photos", "Paint"],
        ["image"] = ["Photos", "Paint"],
        ["word"] = ["Microsoft Word"],
        ["doc"] = ["Microsoft Word"],
        ["sheet"] = ["Microsoft Excel"],
        ["excel"] = ["Microsoft Excel"],
        ["ppt"] = ["Microsoft PowerPoint"],
        ["slides"] = ["Microsoft PowerPoint"],
        ["cmd"] = ["Windows Terminal", "Command Prompt"],
        ["terminal"] = ["Windows Terminal", "PowerShell"],
        ["shell"] = ["Windows Terminal", "PowerShell"],
        ["music"] = ["Spotify", "Media Player"],
        ["spotify"] = ["Spotify"],
        ["calc"] = ["Calculator"],
        ["notes"] = ["Notepad"],
        ["settings"] = ["Settings"],
        ["setting"] = ["Settings"],
        ["files"] = ["File Explorer"],
        ["explorer"] = ["File Explorer"]
    };

    private static List<string> DefaultIndexedFolders() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
    ];
}
