using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

public class TrayIconPresenterTests
{
    private const int Threshold = DeviceSettings.DefaultLowBatteryThreshold;

    [Fact]
    public void NullStatus_HidesIcon()
    {
        Assert.False(TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, null, Threshold).Visible);
    }

    [Fact]
    public void Reading_ShowsIconWithKindRingAndTooltip()
    {
        var model = TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, new BatteryStatus(78, ChargeState.Discharging), Threshold);

        Assert.Equal(new TrayIconModel(true, DeviceKind.Mouse, 7, LevelColor.High, true, "Mouse: 78%"), model);
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void CarriesTheDeviceKind(DeviceKind kind)
    {
        Assert.Equal(kind, TrayIconPresenter.Present("x", kind, new BatteryStatus(60, ChargeState.Discharging), Threshold).Kind);
    }

    [Theory]
    [InlineData(100, ChargeState.Full, 8, LevelColor.High)]
    [InlineData(75, ChargeState.Discharging, 6, LevelColor.High)]
    [InlineData(50, ChargeState.Discharging, 4, LevelColor.Medium)]
    [InlineData(30, ChargeState.Discharging, 3, LevelColor.Medium)]
    [InlineData(16, ChargeState.Discharging, 2, LevelColor.Medium)]
    [InlineData(15, ChargeState.Discharging, 2, LevelColor.Low)]
    [InlineData(24, ChargeState.Charging, 2, LevelColor.Charging)]
    public void LitSegments_TakeTheZoneColor_OrChargingWhileCharging(int percent, ChargeState charge, int lit, LevelColor color)
    {
        var model = TrayIconPresenter.Present("x", DeviceKind.Headset, new BatteryStatus(percent, charge), Threshold);

        Assert.Equal((lit, color, true), (model.LitSegments, model.Color, model.LevelKnown));
    }

    [Theory]
    [InlineData(25, LevelColor.Low)]
    [InlineData(30, LevelColor.Low)]
    [InlineData(31, LevelColor.Medium)]
    public void Color_UsesTheDevicesOwnThreshold(int percent, LevelColor color)
    {
        var model = TrayIconPresenter.Present("x", DeviceKind.Mouse, new BatteryStatus(percent, ChargeState.Discharging), lowBatteryThreshold: 30);

        Assert.Equal(color, model.Color);
    }

    [Fact]
    public void ConnectedWithUnknownPercent_StaysVisible_WithEveryLitSegmentOff()
    {
        var model = TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, new BatteryStatus(null, ChargeState.Unknown), Threshold);

        Assert.True(model.Visible);
        Assert.Equal((0, LevelColor.Unknown, false), (model.LitSegments, model.Color, model.LevelKnown));
        Assert.Equal("Mouse: nivel desconocido", model.Tooltip);
    }

    [Fact]
    public void UnknownPercent_WhileCharging_IsStillUnknownLevel()
    {
        var model = TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, new BatteryStatus(null, ChargeState.Charging), Threshold);

        Assert.Equal((0, false), (model.LitSegments, model.LevelKnown));
    }
}
