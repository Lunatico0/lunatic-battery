using BatteryDisplay.Core;
using BatteryDisplay.Devices.Bluetooth;

namespace BatteryDisplay.Devices.Tests.Bluetooth;

public class BluetoothKindClassifierTests
{
    [Theory]
    [InlineData(0x240404u, DeviceKind.Headset)]  // audio/video, wearable headset
    [InlineData(0x240408u, DeviceKind.Headset)]  // audio/video, hands-free (XION XI-HT325BT)
    [InlineData(0x240418u, DeviceKind.Headset)]  // audio/video, headphones
    [InlineData(0x240414u, DeviceKind.Other)]    // audio/video, loudspeaker
    [InlineData(0x000540u, DeviceKind.Keyboard)] // peripheral, keyboard
    [InlineData(0x000580u, DeviceKind.Mouse)]    // peripheral, pointing device
    [InlineData(0x0005C0u, DeviceKind.Keyboard)] // peripheral, keyboard + pointing combo
    [InlineData(0x002508u, DeviceKind.Gamepad)]  // peripheral, gamepad (DualShock 4)
    [InlineData(0x000504u, DeviceKind.Gamepad)]  // peripheral, joystick
    [InlineData(0x000500u, DeviceKind.Other)]    // peripheral, uncategorized
    [InlineData(0x5A020Cu, DeviceKind.Other)]    // phone, smartphone
    [InlineData(0u, DeviceKind.Other)]
    public void Classifies_class_of_device(uint classOfDevice, DeviceKind expected)
    {
        Assert.Equal(expected, BluetoothKindClassifier.Classify(classOfDevice, null));
    }

    [Theory]
    [InlineData(0x03C1, DeviceKind.Keyboard)]
    [InlineData(0x03C2, DeviceKind.Mouse)]
    [InlineData(0x03C3, DeviceKind.Gamepad)] // joystick
    [InlineData(0x03C4, DeviceKind.Gamepad)]
    [InlineData(0x03C0, DeviceKind.Other)]   // generic HID
    [InlineData(0x03C5, DeviceKind.Other)]   // digitizer tablet
    [InlineData(0x0941, DeviceKind.Headset)] // wearable audio, earbud
    [InlineData(0x0942, DeviceKind.Headset)] // wearable audio, headset
    [InlineData(0x0943, DeviceKind.Headset)] // wearable audio, headphones
    [InlineData(0x0944, DeviceKind.Headset)] // wearable audio, neck band
    [InlineData(0x0841, DeviceKind.Other)]   // audio sink, speaker
    [InlineData(0x0040, DeviceKind.Other)]   // phone
    [InlineData(0x0000, DeviceKind.Other)]
    public void Classifies_appearance(int appearance, DeviceKind expected)
    {
        Assert.Equal(expected, BluetoothKindClassifier.Classify(null, (ushort)appearance));
    }

    [Fact]
    public void Appearance_is_used_when_class_of_device_is_not_conclusive()
    {
        Assert.Equal(DeviceKind.Keyboard, BluetoothKindClassifier.Classify(0x000500, 0x03C1));
    }

    [Fact]
    public void Class_of_device_wins_when_conclusive()
    {
        Assert.Equal(DeviceKind.Headset, BluetoothKindClassifier.Classify(0x240408, 0x03C1));
    }

    [Fact]
    public void Nothing_known_is_other()
    {
        Assert.Equal(DeviceKind.Other, BluetoothKindClassifier.Classify(null, null));
    }
}
