using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using SpotlightNative.Services;
using SpotlightNative.ViewModels;

namespace SpotlightNative;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _closeTimer;
    private readonly WindowPlacementHelper _placement = new();
    private bool _isHiding;

    public MainWindow(ConfigService config)
    {
        InitializeComponent();
        DataContext = new MainViewModel(config);
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _closeTimer.Tick += (_, _) => CloseLauncher();
    }

    public void ShowLauncher()
    {
        _closeTimer.Stop();
        (DataContext as MainViewModel)?.PauseIndex();
        var cursor = System.Windows.Forms.Cursor.Position;
        Opacity = 0;
        Show();
        var cursorPoint = new System.Windows.Point(cursor.X, cursor.Y);
        var metrics = _placement.GetPlacement(cursorPoint, Width, 0);
        Width = metrics.Width;
        MaxHeight = metrics.MaxHeight;
        UpdateLayout();
        var placement = _placement.GetPlacement(cursorPoint, Width, ActualHeight);
        Left = placement.Left;
        Top = placement.Top;
        UpdateLayout();
        Activate();
        SearchBox.Clear();
        SearchBox.Focus();
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(135))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    public void RebuildIndex() => (DataContext as MainViewModel)?.RebuildIndex();

    private void CloseLauncher()
    {
        _closeTimer.Stop();
        (DataContext as MainViewModel)?.ResumeIndex();
        if (IsVisible) Hide();
        App.TrimWorkingSet();
    }

    private async void HideLauncher()
    {
        if (!IsVisible || _isHiding) return;
        _isHiding = true;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(85)));
        await Task.Delay(90);
        if (IsVisible) Hide();
        Opacity = 1;
        _isHiding = false;
        _closeTimer.Start();
    }

    private void Window_Deactivated(object sender, EventArgs e) => HideLauncher();
    private async void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not MainViewModel model) return;
        if (e.Key == Key.Down) { model.MoveSelection(1); e.Handled = true; }
        else if (e.Key == Key.Up) { model.MoveSelection(-1); e.Handled = true; }
        else if (e.Key == Key.Home) { model.SelectFirst(); e.Handled = true; }
        else if (e.Key == Key.End) { model.SelectLast(); e.Handled = true; }
        else if (e.Key == Key.Enter)
        {
            await model.ExecuteSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            e.Handled = true;
        }
    }

    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not MainViewModel model) return;
        if (e.Key == Key.Escape) { HideLauncher(); e.Handled = true; }
        else if (e.Key == Key.Down) { model.MoveSelection(1); e.Handled = true; }
        else if (e.Key == Key.Up) { model.MoveSelection(-1); e.Handled = true; }
        else if (e.Key == Key.Home) { model.SelectFirst(); e.Handled = true; }
        else if (e.Key == Key.End) { model.SelectLast(); e.Handled = true; }
        else if (e.Key == Key.Tab) { model.MoveSelection(1); e.Handled = true; }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key >= Key.D1 && e.Key <= Key.D8)
        {
            await model.ExecuteAt(e.Key - Key.D1, false);
            HideLauncher();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            await model.ExecuteSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            HideLauncher();
            e.Handled = true;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var cornerPreference = 2;
        _ = DwmSetWindowAttribute(hwnd, 33, ref cornerPreference, sizeof(int));
        var backdrop = 2;
        _ = DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
        ApplyRoundedClip();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyRoundedClip();

    private void ApplyRoundedClip() =>
        Container.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, ActualWidth, ActualHeight), 16, 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
