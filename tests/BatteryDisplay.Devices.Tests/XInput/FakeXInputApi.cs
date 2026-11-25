using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Tests.XInput;

internal sealed class FakeXInputApi(Dictionary<uint, XInputBatteryInformation?> pads) : IXInputApi
{
    public List<uint> BatteryQueries { get; } = [];
    public Exception? Failure { get; init; }

    public bool IsConnected(uint userIndex) => Failure is null ? pads.ContainsKey(userIndex) : throw Failure;

    public XInputBatteryInformation? GetGamepadBattery(uint userIndex)
    {
        BatteryQueries.Add(userIndex);
        return pads[userIndex];
    }
}
