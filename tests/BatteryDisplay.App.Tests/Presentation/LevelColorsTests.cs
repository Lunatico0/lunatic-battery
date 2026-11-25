using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

public class LevelColorsTests
{
    private const int DefaultThreshold = DeviceSettings.DefaultLowBatteryThreshold;

    [Theory]
    [InlineData(100, LevelColor.High)]
    [InlineData(51, LevelColor.High)]
    [InlineData(50, LevelColor.Medium)]
    [InlineData(16, LevelColor.Medium)]
    [InlineData(15, LevelColor.Low)]
    [InlineData(0, LevelColor.Low)]
    public void Discharging_IsLowAtOrBelowTheDeviceThreshold_AndMediumUpTo50(int percent, LevelColor expected)
    {
        Assert.Equal(expected, LevelColors.For(new BatteryStatus(percent, ChargeState.Discharging), DefaultThreshold));
    }

    [Theory]
    [InlineData(31, LevelColor.Medium)]
    [InlineData(30, LevelColor.Low)]
    [InlineData(20, LevelColor.Low)]
    [InlineData(50, LevelColor.Medium)]
    [InlineData(51, LevelColor.High)]
    public void CustomThreshold_MovesOnlyTheLowBoundary(int percent, LevelColor expected)
    {
        Assert.Equal(expected, LevelColors.For(new BatteryStatus(percent, ChargeState.Discharging), 30));
    }

    [Fact]
    public void BelowTheOldFixedThreshold_ButAboveTheDevicesOwn_IsMedium()
    {
        Assert.Equal(LevelColor.Medium, LevelColors.For(new BatteryStatus(20, ChargeState.Discharging), 10));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(78)]
    [InlineData(null)]
    public void Charging_AlwaysUsesChargingColor_EvenAtOrBelowTheThreshold(int? percent)
    {
        Assert.Equal(LevelColor.Charging, LevelColors.For(new BatteryStatus(percent, ChargeState.Charging), DefaultThreshold));
    }

    [Fact]
    public void Full_IsNotTreatedAsCharging()
    {
        Assert.Equal(LevelColor.High, LevelColors.For(new BatteryStatus(100, ChargeState.Full), DefaultThreshold));
    }

    [Fact]
    public void UnknownPercent_NotCharging_UsesUnknownColor()
    {
        Assert.Equal(LevelColor.Unknown, LevelColors.For(new BatteryStatus(null, ChargeState.Unknown), DefaultThreshold));
    }
}
