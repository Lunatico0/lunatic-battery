using System.Runtime.InteropServices;

namespace BatteryDisplay.Devices.XInput;

/// <summary>P/Invoke wrapper over xinput1_4.dll (Windows 8+).</summary>
public sealed class XInputNativeApi : IXInputApi
{
    private const uint ErrorSuccess = 0;
    private const uint ErrorDeviceNotConnected = 1167;
    private const byte BatteryDevTypeGamepad = 0x00;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformationNative
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint dwUserIndex, out XInputState pState);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern uint XInputGetBatteryInformation(
        uint dwUserIndex, byte devType, out XInputBatteryInformationNative pBatteryInformation);

    public bool IsConnected(uint userIndex) => XInputGetState(userIndex, out _) != ErrorDeviceNotConnected;

    public XInputBatteryInformation? GetGamepadBattery(uint userIndex)
    {
        if (XInputGetBatteryInformation(userIndex, BatteryDevTypeGamepad, out var info) != ErrorSuccess)
            return null;
        return new XInputBatteryInformation((XInputBatteryType)info.BatteryType, (XInputBatteryLevel)info.BatteryLevel);
    }
}
