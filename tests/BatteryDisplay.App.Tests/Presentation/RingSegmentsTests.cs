using BatteryDisplay.App.Presentation;

namespace BatteryDisplay.App.Tests.Presentation;

public class RingSegmentsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(12, 1)]
    [InlineData(13, 2)]
    [InlineData(24, 2)]
    [InlineData(25, 2)]
    [InlineData(26, 3)]
    [InlineData(50, 4)]
    [InlineData(99, 8)]
    [InlineData(100, 8)]
    public void EachSegmentCoversAnEighth_RoundedUp(int percent, int expected)
    {
        Assert.Equal(expected, RingSegments.Lit(percent));
    }

    [Fact]
    public void UnknownLevel_LightsNothing()
    {
        Assert.Equal(0, RingSegments.Lit(null));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(150, RingSegments.Count)]
    public void OutOfRange_IsClamped(int percent, int expected)
    {
        Assert.Equal(expected, RingSegments.Lit(percent));
    }
}
