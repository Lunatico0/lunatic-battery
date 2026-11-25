using System.Drawing;

namespace BatteryDisplay.App.Ui.Flyout;

public enum TaskbarEdge { Bottom, Top, Left, Right }

/// <summary>
/// Where the flyout goes, in physical pixels: in the corner of the monitor's work area next to the notification area,
/// a margin away from the taskbar and the screen edge, and always inside the work area.
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
}
