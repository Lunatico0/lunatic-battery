using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Bluetooth;

/// <summary>Maps the Classic Class of Device or the LE GAP Appearance (Bluetooth Assigned Numbers) to a DeviceKind.</summary>
public static class BluetoothKindClassifier
{
    private const uint MajorAudioVideo = 0x04;
    private const uint MajorPeripheral = 0x05;
    private const int AppearanceHid = 0x00F;
    private const int AppearanceWearableAudio = 0x025;

    public static DeviceKind Classify(uint? classOfDevice, ushort? appearance)
    {
        var kind = classOfDevice is { } cod ? FromClassOfDevice(cod) : DeviceKind.Other;
        return kind == DeviceKind.Other && appearance is { } a ? FromAppearance(a) : kind;
    }

    private static DeviceKind FromClassOfDevice(uint cod)
    {
        uint major = (cod >> 8) & 0x1F;
        uint minor = (cod >> 2) & 0x3F;
        return major switch
        {
            // Wearable headset, hands-free, headphones.
            MajorAudioVideo => minor is 1 or 2 or 6 ? DeviceKind.Headset : DeviceKind.Other,
            // Low nibble: joystick or gamepad. High bits: keyboard, pointing, combo.
            MajorPeripheral => (minor & 0x0F) is 1 or 2 ? DeviceKind.Gamepad : (minor >> 4) switch
            {
                1 or 3 => DeviceKind.Keyboard,
                2 => DeviceKind.Mouse,
                _ => DeviceKind.Other,
            },
            _ => DeviceKind.Other,
        };
    }

    private static DeviceKind FromAppearance(ushort appearance)
    {
        int category = appearance >> 6;
        int sub = appearance & 0x3F;
        return category switch
        {
            AppearanceHid => sub switch
            {
                1 => DeviceKind.Keyboard,
                2 => DeviceKind.Mouse,
                3 or 4 => DeviceKind.Gamepad,
                _ => DeviceKind.Other,
            },
            // Earbud, headset, headphones, neck band.
            AppearanceWearableAudio => sub is >= 1 and <= 4 ? DeviceKind.Headset : DeviceKind.Other,
            _ => DeviceKind.Other,
        };
    }
}
