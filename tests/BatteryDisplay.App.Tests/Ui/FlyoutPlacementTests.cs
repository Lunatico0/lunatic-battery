using System.Drawing;
using BatteryDisplay.App.Ui.Flyout;

namespace BatteryDisplay.App.Tests.Ui;

public class FlyoutPlacementTests
{
    private static readonly Rectangle Screen = new(0, 0, 1920, 1080);
    private static readonly Size Flyout = new(360, 400);
    private const int Margin = 12;

    [Theory]
    [InlineData(0, 0, 1920, 1032, TaskbarEdge.Bottom)]
    [InlineData(0, 48, 1920, 1032, TaskbarEdge.Top)]
    [InlineData(48, 0, 1872, 1080, TaskbarEdge.Left)]
    [InlineData(0, 0, 1872, 1080, TaskbarEdge.Right)]
    [InlineData(0, 0, 1920, 1080, TaskbarEdge.Bottom)]
    public void Edge_IsTheSideTheWorkAreaGivesUp(int x, int y, int w, int h, TaskbarEdge edge)
    {
        Assert.Equal(edge, FlyoutPlacement.EdgeOf(Screen, new Rectangle(x, y, w, h)));
    }

    [Fact]
    public void Edge_WorksOnASecondMonitor()
    {
        var bounds = new Rectangle(-1920, -200, 1920, 1080);

        Assert.Equal(TaskbarEdge.Bottom, FlyoutPlacement.EdgeOf(bounds, bounds with { Height = 1032 }));
    }

    [Fact]
    public void BottomTaskbar_GoesBottomRight_AboveIt()
    {
        var workArea = new Rectangle(0, 0, 1920, 1032);

        Assert.Equal(new Point(1920 - 12 - 360, 1032 - 12 - 400), FlyoutPlacement.Place(workArea, TaskbarEdge.Bottom, Flyout, Margin));
    }

    [Fact]
    public void TopTaskbar_GoesTopRight_BelowIt()
    {
        var workArea = new Rectangle(0, 48, 1920, 1032);

        Assert.Equal(new Point(1920 - 12 - 360, 48 + 12), FlyoutPlacement.Place(workArea, TaskbarEdge.Top, Flyout, Margin));
    }

    [Fact]
    public void LeftTaskbar_GoesBottomLeft_NextToIt()
    {
        var workArea = new Rectangle(48, 0, 1872, 1080);

        Assert.Equal(new Point(48 + 12, 1080 - 12 - 400), FlyoutPlacement.Place(workArea, TaskbarEdge.Left, Flyout, Margin));
    }

    [Fact]
    public void RightTaskbar_GoesBottomRight_NextToIt()
    {
        var workArea = new Rectangle(0, 0, 1872, 1080);

        Assert.Equal(new Point(1872 - 12 - 360, 1080 - 12 - 400), FlyoutPlacement.Place(workArea, TaskbarEdge.Right, Flyout, Margin));
    }

    [Fact]
    public void SecondMonitorWithNegativeOrigin_StaysInsideItsWorkArea()
    {
        var workArea = new Rectangle(-1920, -200, 1920, 1032);

        Assert.Equal(new Point(-12 - 360, -200 + 1032 - 12 - 400), FlyoutPlacement.Place(workArea, TaskbarEdge.Bottom, Flyout, Margin));
    }

    [Fact]
    public void TallerThanTheWorkArea_PinsToItsTop()
    {
        var workArea = new Rectangle(0, 0, 1920, 300);

        Assert.Equal(new Point(1920 - 12 - 360, 0), FlyoutPlacement.Place(workArea, TaskbarEdge.Bottom, Flyout, Margin));
    }

    [Fact]
    public void WiderThanTheWorkArea_PinsToItsLeft()
    {
        var workArea = new Rectangle(100, 0, 300, 1032);

        Assert.Equal(100, FlyoutPlacement.Place(workArea, TaskbarEdge.Bottom, Flyout, Margin).X);
    }

    // Mixed DPI: a 100% primary and a 150% monitor to its right, both in physical pixels.
    private static readonly FlyoutMonitor At100 = new(new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1032), 1.0);
    private static readonly FlyoutMonitor At150 = new(new Rectangle(1920, 0, 3840, 2160), new Rectangle(1920, 0, 3840, 2088), 1.5);
    private static readonly FlyoutMonitor[] Monitors = [At100, At150];

    [Fact]
    public void MonitorAt_IsTheMonitorUnderTheClick()
    {
        Assert.Equal(At150, FlyoutPlacement.MonitorAt(Monitors, new Point(5600, 2120)));
    }

    [Fact]
    public void MonitorAt_OutsideEveryMonitor_TakesTheNearest()
    {
        Assert.Equal(At100, FlyoutPlacement.MonitorAt(Monitors, new Point(-5, 500)));
    }

    [Fact]
    public void MonitorAt_NoMonitors_IsNull()
    {
        Assert.Null(FlyoutPlacement.MonitorAt([], new Point(0, 0)));
    }

    [Fact]
    public void MovingTo150_ScalesTheWindowAndTheMarginToThatMonitor()
    {
        // Still on the 100% monitor: WPF resizes it to 540x600 once it lands on the 150% one.
        var corner = FlyoutPlacement.Place(At150, new Size(360, 400), windowScale: 1.0, marginDips: 12);

        Assert.Equal(new Point(1920 + 3840 - 18 - 540, 2088 - 18 - 600), corner);
    }

    [Fact]
    public void AlreadyAtTheMonitorScale_KeepsTheRealWindowSize()
    {
        var corner = FlyoutPlacement.Place(At150, new Size(541, 601), windowScale: 1.5, marginDips: 12);

        Assert.Equal(new Point(1920 + 3840 - 18 - 541, 2088 - 18 - 601), corner);
    }

    [Fact]
    public void MovingBackTo100_UsesThatMonitorsScale()
    {
        var corner = FlyoutPlacement.Place(At100, new Size(540, 600), windowScale: 1.5, marginDips: 12);

        Assert.Equal(new Point(1920 - 12 - 360, 1032 - 12 - 400), corner);
    }

    [Fact]
    public void LeftMonitorAt125_WithNegativeOrigin_StaysInsideItsWorkArea()
    {
        var at125 = new FlyoutMonitor(new Rectangle(-2400, -300, 2400, 1350), new Rectangle(-2400, -300, 2400, 1290), 1.25);

        var corner = FlyoutPlacement.Place(at125, new Size(360, 400), windowScale: 1.0, marginDips: 12);

        Assert.Equal(new Point(-15 - 450, -300 + 1290 - 15 - 500), corner);
    }
}
