using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.XInput;

/// <summary>Values of XINPUT_BATTERY_INFORMATION.BatteryType.</summary>
public enum XInputBatteryType : byte
{
    Disconnected = 0x00,
    Wired = 0x01,
    Alkaline = 0x02,
    NiMH = 0x03,
    Unknown = 0xFF,
}

/// <summary>Values of XINPUT_BATTERY_INFORMATION.BatteryLevel.</summary>
public enum XInputBatteryLevel : byte
{
    Empty = 0x00,
    Low = 0x01,
    Medium = 0x02,
    Full = 0x03,
}

public static class XInputBatteryMapper
{
    public static BatteryStatus? Map(XInputBatteryType type, XInputBatteryLevel level) => type switch
    {
        XInputBatteryType.Disconnected => null,
        XInputBatteryType.Alkaline or XInputBatteryType.NiMH =>
            new BatteryStatus(ToPercent(level), ChargeState.Discharging),
        _ => new BatteryStatus(null, ChargeState.Unknown),
    };

    private static int? ToPercent(XInputBatteryLevel level) => level switch
    {
        XInputBatteryLevel.Empty => 0,
        XInputBatteryLevel.Low => 25,
        XInputBatteryLevel.Medium => 50,
        XInputBatteryLevel.Full => 100,
        _ => null,
    };
}
