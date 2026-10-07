using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using SpotlightNative.Services;
using Forms = System.Windows.Forms;

namespace SpotlightNative;

public partial class App : System.Windows.Application
{
    private const int AutostartDelayMilliseconds = 7000;
    private Mutex? _mutex;
    private MainWindow? _window;
    private ConfigService? _config;
    private HotkeyService? _hotkey;
    private Forms.NotifyIcon? _tray;
    private readonly AutostartManager _autostart = new();
    private bool _startedByAutostart;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            StartupLogger.Write("Dispatcher unhandled exception.", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            StartupLogger.Write("AppDomain unhandled exception.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLogger.Write("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
        StartupLogger.Write("Application started.");
        _mutex = new Mutex(true, "SpotlightNative.SingleInstance", out var created);
        if (!created)
        {
            StartupLogger.Write("Another instance already owns the single-instance mutex; this process is exiting.");
            Shutdown();
            return;
        }
        StartupLogger.Write("Single-instance mutex acquired.");

        _startedByAutostart = e.Args.Any(argument => argument.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        StartupLogger.Write($"Startup mode: {( _startedByAutostart ? "autostart" : "manual")}; process={Environment.ProcessPath}; base={AppContext.BaseDirectory}");
        if (_startedByAutostart)
        {
            _ = InitializeAsync(true);
            return;
        }

        _ = InitializeAsync(false);
    }

    private async Task InitializeAsync(bool autostart)
    {
        try
        {
            if (autostart)
            {
                SetBelowNormalPriority();
                await Task.Delay(AutostartDelayMilliseconds);
            }

            _config = new ConfigService();
            _autostart.Sync(_config.Current.Autostart);
            StartupLogger.Write($"Autostart registry synchronized: enabled={_config.Current.Autostart}, registered={_autostart.IsEnabled()}");
            _config.Changed += Config_Changed;
            BuildTray();
            StartupLogger.Write("System tray icon created.");
            _window = new MainWindow(_config);
            _window.Closed += (_, _) => _window = null;
            _ = new WindowInteropHelper(_window).EnsureHandle();
            _window.Opacity = 0;
            _window.Show();
            _window.Hide();
            _window.Opacity = 1;
            StartupLogger.Write("Launcher window handle created.");
            StartupLogger.Write("Index services initialized.");
            _hotkey = new HotkeyService(_config.Current.Hotkey);
            _hotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ShowLauncher);
            _hotkey.RegistrationFailed += (_, message) => Dispatcher.Invoke(() =>
            {
                StartupLogger.Write($"Global hotkey registration failed: {message}");
                if (!_startedByAutostart)
                    _tray?.ShowBalloonTip(5000, "Spotlight Native", message, Forms.ToolTipIcon.Warning);
            });
            StartupLogger.Write(_hotkey.IsActive ? "Global hotkey registered." : "Global hotkey registration failed.");
            if (!_hotkey.IsActive)
                _ = RetryHotkeyRegistrationAsync();

            if (!autostart)
                Console.WriteLine($"Spotlight Native is ready. Press {_config.Current.Hotkey} to open the launcher.");
            if (autostart) RestoreNormalPriority();
            StartupLogger.Write("Initialization completed.");
        }
        catch (Exception exception)
        {
            StartupLogger.Write("Startup initialization failed.", exception);
        }
    }

    private async Task RetryHotkeyRegistrationAsync()
    {
        for (var attempt = 1; attempt <= 3 && _hotkey is { IsActive: false }; attempt++)
        {
            await Task.Delay(2000);
            _hotkey.Register(_config?.Current.Hotkey ?? "Alt+Space");
        }
        if (_hotkey is { IsActive: false })
            Debug.WriteLine("Spotlight Native could not register its global hotkey after retries.");
    }

    private void ShowLauncher()
    {
        if (_config is null) return;
        _window?.ShowLauncher();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartupLogger.Write($"Application exiting with code {e.ApplicationExitCode}.");
        if (_config is not null) _config.Changed -= Config_Changed;
        _hotkey?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        _window?.Close();
        _mutex?.Dispose();
        _config?.Dispose();
        base.OnExit(e);
    }

    private void Config_Changed(object? sender, EventArgs e)
    {
        if (_config is null || _hotkey is null) return;
        Dispatcher.Invoke(() =>
        {
            _hotkey.Register(_config.Current.Hotkey);
            _autostart.Sync(_config.Current.Autostart);
            _window?.Hide();
            _tray?.ShowBalloonTip(3000, "Spotlight Native", $"Hotkey reloaded: {_config.Current.Hotkey}", Forms.ToolTipIcon.Info);
        });
    }

    private void BuildTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Spotlight Native",
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowLauncher);
        var menu = _tray.ContextMenuStrip;
        menu.Items.Add("Open", null, (_, _) => Dispatcher.Invoke(ShowLauncher));
        menu.Items.Add("Open config file", null, (_, _) => _config?.Open());
        menu.Items.Add("Reload config", null, (_, _) => _tray?.ShowBalloonTip(3000, "Spotlight Native", "Restart the app to apply hotkey changes.", Forms.ToolTipIcon.Info));
        menu.Items.Add("Rebuild index", null, (_, _) => Dispatcher.Invoke(() =>
        {
            ShowLauncher();
            _window?.RebuildIndex();
            _tray?.ShowBalloonTip(3000, "Spotlight Native", "Index rebuild started.", Forms.ToolTipIcon.Info);
        }));
        var startup = new Forms.ToolStripMenuItem("Start with Windows") { Checked = _config?.Current.Autostart == true && _autostart.IsEnabled() };
        startup.Click += (_, _) =>
        {
            var enabled = !startup.Checked;
            _autostart.Sync(enabled);
            if (_config is not null) { _config.Current.Autostart = enabled; _config.Save(); }
            startup.Checked = enabled && _autostart.IsEnabled();
        };
        menu.Items.Add(startup);
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
    }

    private static void SetBelowNormalPriority()
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch (Exception exception) { Debug.WriteLine($"Could not lower startup priority: {exception}"); }
    }

    private static void RestoreNormalPriority()
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal; }
        catch (Exception exception) { Debug.WriteLine($"Could not restore process priority: {exception}"); }
    }

    internal static void TrimWorkingSet()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        _ = EmptyWorkingSet(Process.GetCurrentProcess().Handle);
    }

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
