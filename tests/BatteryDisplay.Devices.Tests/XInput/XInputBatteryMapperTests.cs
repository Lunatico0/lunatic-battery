using BatteryDisplay.Core;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Tests.XInput;

public class XInputBatteryMapperTests
{
    [Fact]
    public void Disconnected_returns_null()
    {
        Assert.Null(XInputBatteryMapper.Map(XInputBatteryType.Disconnected, XInputBatteryLevel.Full));
    }

    [Fact]
    public void Wired_returns_unknown_without_percent()
    {
        Assert.Equal(new BatteryStatus(null, ChargeState.Unknown),
            XInputBatteryMapper.Map(XInputBatteryType.Wired, XInputBatteryLevel.Full));
    }

    [Fact]
    public void Unknown_type_returns_unknown_without_percent()
    {
        Assert.Equal(new BatteryStatus(null, ChargeState.Unknown),
            XInputBatteryMapper.Map(XInputBatteryType.Unknown, XInputBatteryLevel.Low));
    }

    [Theory]
    [InlineData(XInputBatteryType.Alkaline, XInputBatteryLevel.Empty, 0)]
    [InlineData(XInputBatteryType.Alkaline, XInputBatteryLevel.Low, 25)]
    [InlineData(XInputBatteryType.Alkaline, XInputBatteryLevel.Medium, 50)]
    [InlineData(XInputBatteryType.Alkaline, XInputBatteryLevel.Full, 100)]
    [InlineData(XInputBatteryType.NiMH, XInputBatteryLevel.Empty, 0)]
    [InlineData(XInputBatteryType.NiMH, XInputBatteryLevel.Low, 25)]
    [InlineData(XInputBatteryType.NiMH, XInputBatteryLevel.Medium, 50)]
    [InlineData(XInputBatteryType.NiMH, XInputBatteryLevel.Full, 100)]
    public void Battery_powered_maps_level_to_percent_and_discharging(
        XInputBatteryType type, XInputBatteryLevel level, int expected)
    {
        Assert.Equal(new BatteryStatus(expected, ChargeState.Discharging), XInputBatteryMapper.Map(type, level));
    }

    [Fact]
    public void Undocumented_type_returns_unknown_without_percent()
    {
        Assert.Equal(new BatteryStatus(null, ChargeState.Unknown),
            XInputBatteryMapper.Map((XInputBatteryType)0x42, XInputBatteryLevel.Full));
    }

    [Fact]
    public void Provider_exposes_identity()
    {
        var provider = new XInputGamepadProvider();
        Assert.Equal("xinput-gamepad", provider.Id);
        Assert.Equal("Joystick", provider.Name);
        Assert.Equal(DeviceKind.Gamepad, provider.Kind);
    }
}
