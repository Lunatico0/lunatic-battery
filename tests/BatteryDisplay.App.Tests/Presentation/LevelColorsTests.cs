using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

public class LevelColorsTests
{
    [Theory]
    [InlineData(100, LevelColor.High)]
    [InlineData(51, LevelColor.High)]
    [InlineData(50, LevelColor.Medium)]
    [InlineData(21, LevelColor.Medium)]
    [InlineData(20, LevelColor.Low)]
    [InlineData(0, LevelColor.Low)]
    public void Discharging_UsesLevelThresholds(int percent, LevelColor expected)
    {
        Assert.Equal(expected, LevelColors.For(new BatteryStatus(percent, ChargeState.Discharging)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(78)]
    [InlineData(null)]
    public void Charging_AlwaysUsesChargingColor(int? percent)
    {
        Assert.Equal(LevelColor.Charging, LevelColors.For(new BatteryStatus(percent, ChargeState.Charging)));
    }

    [Fact]
    public void Full_IsNotTreatedAsCharging()
    {
        Assert.Equal(LevelColor.High, LevelColors.For(new BatteryStatus(100, ChargeState.Full)));
    }

    [Fact]
    public void UnknownPercent_NotCharging_UsesUnknownColor()
    {
        Assert.Equal(LevelColor.Unknown, LevelColors.For(new BatteryStatus(null, ChargeState.Unknown)));
    }
}
