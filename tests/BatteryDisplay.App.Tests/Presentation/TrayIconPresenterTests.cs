using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

public class TrayIconPresenterTests
{
    [Fact]
    public void NullStatus_HidesIcon()
    {
        Assert.False(TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, null).Visible);
    }

    [Fact]
    public void Reading_ShowsIconWithKindRingAndTooltip()
    {
        var model = TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, new BatteryStatus(78, ChargeState.Discharging));

        Assert.Equal(new TrayIconModel(true, DeviceKind.Mouse, 7, LevelColor.High, true, "Mouse: 78%"), model);
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void CarriesTheDeviceKind(DeviceKind kind)
    {
        Assert.Equal(kind, TrayIconPresenter.Present("x", kind, new BatteryStatus(60, ChargeState.Discharging)).Kind);
    }

    [Theory]
    [InlineData(100, ChargeState.Full, 8, LevelColor.High)]
    [InlineData(75, ChargeState.Discharging, 6, LevelColor.High)]
    [InlineData(50, ChargeState.Discharging, 4, LevelColor.Medium)]
    [InlineData(30, ChargeState.Discharging, 3, LevelColor.Medium)]
    [InlineData(15, ChargeState.Discharging, 2, LevelColor.Low)]
    [InlineData(24, ChargeState.Charging, 2, LevelColor.Charging)]
    public void LitSegments_TakeTheZoneColor_OrChargingWhileCharging(int percent, ChargeState charge, int lit, LevelColor color)
    {
        var model = TrayIconPresenter.Present("x", DeviceKind.Headset, new BatteryStatus(percent, charge));

        Assert.Equal((lit, color, true), (model.LitSegments, model.Color, model.LevelKnown));
    }

    [Fact]
    public void ConnectedWithUnknownPercent_StaysVisible_WithEveryLitSegmentOff()
    {
        var model = TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, new BatteryStatus(null, ChargeState.Unknown));

        Assert.True(model.Visible);
        Assert.Equal((0, LevelColor.Unknown, false), (model.LitSegments, model.Color, model.LevelKnown));
        Assert.Equal("Mouse: nivel desconocido", model.Tooltip);
    }

    [Fact]
    public void UnknownPercent_WhileCharging_IsStillUnknownLevel()
    {
        var model = TrayIconPresenter.Present("Mouse", DeviceKind.Mouse, new BatteryStatus(null, ChargeState.Charging));

        Assert.Equal((0, false), (model.LitSegments, model.LevelKnown));
    }
}
