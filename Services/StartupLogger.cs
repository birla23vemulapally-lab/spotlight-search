using System.Diagnostics;

namespace SpotlightNative.Services;

public static class StartupLogger
{
    private static readonly object Sync = new();
    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpotlightNative",
        "log.txt");

    public static string Path => LogPath;

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);
            lock (Sync)
            {
                File.AppendAllText(LogPath,
                    $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
        }
        catch (Exception loggingException)
        {
            Debug.WriteLine($"Could not write startup log: {loggingException}");
        }
    }
}
