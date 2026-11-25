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
}
