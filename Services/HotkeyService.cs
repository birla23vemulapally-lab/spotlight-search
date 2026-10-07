using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SpotlightNative.Services;

/// <summary>Registers the configured system-wide hotkey through a message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 1, ModControl = 2, ModShift = 4, ModWin = 8, ModNoRepeat = 0x4000;
    private readonly HwndSource _source;
    private readonly int _id = 0x534E;
    public bool IsRegistered { get; private set; }
    public bool IsActive => IsRegistered;
    public event EventHandler? HotkeyPressed;
    public event EventHandler<string>? RegistrationFailed;
    public HotkeyService(string hotkey)
    {
        _source = new HwndSource(new HwndSourceParameters("SpotlightNativeHotkey")
        {
            Width = 0,
            Height = 0,
            ParentWindow = HwndMessage
        });
        _source.AddHook(WndProc);
        Register(hotkey);
    }
    public bool Register(string hotkey)
    {
        UnregisterHotKey(_source.Handle, _id);
        IsRegistered = TryParse(hotkey, out var modifiers, out var key) &&
                       RegisterHotKey(_source.Handle, _id, modifiers | ModNoRepeat, key);
        if (!IsRegistered)
            RegistrationFailed?.Invoke(this, $"Could not register hotkey '{hotkey}'. Another app may already own it. Change it in config.json.");
        return IsRegistered;
    }
    private static bool TryParse(string text, out uint modifiers, out uint key)
    {
        modifiers = 0; key = 0;
        foreach (var part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModControl;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModShift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModAlt;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModWin;
            else if (part.Length == 1) key = char.ToUpperInvariant(part[0]);
            else if (part.Equals("Space", StringComparison.OrdinalIgnoreCase)) key = 0x20;
        }
        return modifiers != 0 && key != 0;
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _id) { HotkeyPressed?.Invoke(this, EventArgs.Empty); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_source.Handle, _id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private static readonly IntPtr HwndMessage = new(-3);
}
