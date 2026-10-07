using Microsoft.Win32;
using System.Diagnostics;

namespace SpotlightNative.Services;

public sealed class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpotlightNative";

    public void Enable()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath)) return;

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key?.SetValue(ValueName, $"\"{processPath}\" --autostart");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Could not enable Spotlight Native autostart: {exception}");
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Could not disable Spotlight Native autostart: {exception}");
        }
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value &&
                value.Contains("--autostart", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Could not read Spotlight Native autostart: {exception}");
            return false;
        }
    }

    public void Sync(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }
}
