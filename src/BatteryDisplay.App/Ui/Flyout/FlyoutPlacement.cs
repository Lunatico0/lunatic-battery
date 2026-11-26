using System.Drawing;

namespace BatteryDisplay.App.Ui.Flyout;

public enum TaskbarEdge { Bottom, Top, Left, Right }

/// <summary>A monitor in physical pixels, with its own scale (1.5 at 150%).</summary>
public readonly record struct FlyoutMonitor(Rectangle Bounds, Rectangle WorkArea, double Scale);

/// <summary>
/// Where the flyout goes, in physical pixels: in the corner of the monitor's work area next to the notification area,
/// a margin away from the taskbar and the screen edge, and always inside the work area. With monitors at different
/// scales, everything is measured at the scale of the monitor that holds the tray click.
/// </summary>
public static class FlyoutPlacement
{
    /// <summary>The side the taskbar takes from the monitor. A hidden or absent taskbar counts as Bottom.</summary>
    public static TaskbarEdge EdgeOf(Rectangle bounds, Rectangle workArea)
    {
        if (workArea.Bottom < bounds.Bottom)
            return TaskbarEdge.Bottom;
        if (workArea.Top > bounds.Top)
            return TaskbarEdge.Top;
        if (workArea.Left > bounds.Left)
            return TaskbarEdge.Left;
        if (workArea.Right < bounds.Right)
            return TaskbarEdge.Right;
        return TaskbarEdge.Bottom;
    }

    /// <summary>Top-left corner for a flyout of <paramref name="flyout"/> size. The notification area is at the right end of a
    /// horizontal taskbar and at the bottom of a vertical one.</summary>
    public static Point Place(Rectangle workArea, TaskbarEdge edge, Size flyout, int margin)
    {
        var x = edge == TaskbarEdge.Left ? workArea.Left + margin : workArea.Right - margin - flyout.Width;
        var y = edge == TaskbarEdge.Top ? workArea.Top + margin : workArea.Bottom - margin - flyout.Height;
        return new Point(Math.Max(x, workArea.Left), Math.Max(y, workArea.Top));
    }

    /// <summary>The monitor that holds <paramref name="anchor"/>, or the nearest one when the point falls outside all of them.</summary>
    public static FlyoutMonitor? MonitorAt(IReadOnlyList<FlyoutMonitor> monitors, Point anchor)
    {
        FlyoutMonitor? nearest = null;
        var best = long.MaxValue;
        foreach (var monitor in monitors)
        {
            var b = monitor.Bounds;
            long dx = Math.Max(0, Math.Max(b.Left - anchor.X, anchor.X - (b.Right - 1)));
            long dy = Math.Max(0, Math.Max(b.Top - anchor.Y, anchor.Y - (b.Bottom - 1)));
            var distance = dx * dx + dy * dy;
            if (distance >= best)
                continue;
            best = distance;
            nearest = monitor;
        }
        return nearest;
    }

    /// <summary>
    /// Top-left corner on <paramref name="monitor"/> for the flyout window, which is now <paramref name="windowPixels"/>
    /// at <paramref name="windowScale"/>. WPF keeps the window's size in DIPs and resizes it when it lands on a monitor
    /// with another scale, so the size and the margin are taken at the target monitor's scale.
    /// </summary>
    public static Point Place(FlyoutMonitor monitor, Size windowPixels, double windowScale, double marginDips)
    {
        var size = windowScale == monitor.Scale
            ? windowPixels
            : new Size(Scale(windowPixels.Width, windowScale, monitor.Scale), Scale(windowPixels.Height, windowScale, monitor.Scale));
        var margin = (int)Math.Round(marginDips * monitor.Scale);
        return Place(monitor.WorkArea, EdgeOf(monitor.Bounds, monitor.WorkArea), size, margin);
    }

    private static int Scale(int pixels, double from, double to) => (int)Math.Round(pixels / from * to);
}
