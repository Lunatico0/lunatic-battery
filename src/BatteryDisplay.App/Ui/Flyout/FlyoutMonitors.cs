using System.Drawing;
using System.Runtime.InteropServices;

namespace BatteryDisplay.App.Ui.Flyout;

/// <summary>
/// The monitors as Windows reports them to a per-monitor DPI aware process: bounds and work area in physical pixels,
/// and each monitor's own effective DPI.
/// </summary>
internal static class FlyoutMonitors
{
    private const double BaseDpi = 96;
    private const uint MdtEffectiveDpi = 0;

    public static IReadOnlyList<FlyoutMonitor> All()
    {
        var monitors = new List<FlyoutMonitor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var scale = GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0 ? dpiX / BaseDpi : 1.0;
                monitors.Add(new FlyoutMonitor(info.Monitor.ToRectangle(), info.Work.ToRectangle(), scale));
            }
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>The scale of the monitor a window is on now (WPF has not always caught up when SizeChanged fires).</summary>
    public static double ScaleOf(IntPtr hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / BaseDpi : 1.0;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("shcore.dll", ExactSpelling = true)]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
