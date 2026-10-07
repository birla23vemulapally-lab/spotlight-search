using System.Runtime.InteropServices;
using System.Windows;
using System.Diagnostics;

namespace SpotlightNative;

public sealed class WindowPlacementHelper
{
    public const double VerticalOffsetRatio = 0.30;
    private const double DpiBase = 96.0;
    private const uint MonitorDefaultToNearest = 2;

    public PlacementInfo GetPlacement(System.Windows.Point cursorPixels, double windowWidth, double windowHeight)
    {
        var monitor = MonitorFromPoint(new NativePoint((int)cursorPixels.X, (int)cursorPixels.Y), MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
            return new PlacementInfo(0, 0, 800, 600, 1);

        var dpi = GetDpi(monitor);
        var scale = dpi / DpiBase;
        var workLeft = monitorInfo.Work.Left / scale;
        var workTop = monitorInfo.Work.Top / scale;
        var workWidth = (monitorInfo.Work.Right - monitorInfo.Work.Left) / scale;
        var workHeight = (monitorInfo.Work.Bottom - monitorInfo.Work.Top) / scale;
        var width = Math.Clamp(workWidth * 0.40, 600, 900);
        var height = Math.Min(windowHeight > 0 ? windowHeight : workHeight * 0.68, workHeight * 0.68);
        var left = workLeft + Math.Max(0, (workWidth - width) / 2);
        var top = workTop + Math.Max(0, workHeight * VerticalOffsetRatio);
        top = Math.Min(top, workTop + workHeight - height);
        Debug.WriteLine(
            $"Spotlight placement: monitor=({monitorInfo.Monitor.Left},{monitorInfo.Monitor.Top},{monitorInfo.Monitor.Right},{monitorInfo.Monitor.Bottom}), " +
            $"work=({monitorInfo.Work.Left},{monitorInfo.Work.Top},{monitorInfo.Work.Right},{monitorInfo.Work.Bottom}), " +
            $"dpiScale={scale:0.##}, widthDip={width:0.##}, leftDip={left:0.##}, topDip={top:0.##}");
        return new PlacementInfo(left, top, width, Math.Max(240, workHeight * 0.68), scale);
    }

    private static uint GetDpi(IntPtr monitor)
    {
        try
        {
            if (GetDpiForMonitor(monitor, 0, out var dpiX, out _ ) == 0)
                return dpiX;
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return 96;
    }

    public readonly record struct PlacementInfo(double Left, double Top, double Width, double MaxHeight, double DpiScale);
    private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("Shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, uint type, out uint dpiX, out uint dpiY);
}
