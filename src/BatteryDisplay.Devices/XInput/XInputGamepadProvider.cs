using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.XInput;

public readonly record struct XInputBatteryInformation(XInputBatteryType Type, XInputBatteryLevel Level);

/// <summary>Seam over xinput1_4.dll so the provider logic can be tested without hardware.</summary>
public interface IXInputApi
{
    bool IsConnected(uint userIndex);
    XInputBatteryInformation? GetGamepadBattery(uint userIndex);
}

/// <summary>Gamepad in one XInput slot (user index 0..3).</summary>
public sealed class XInputGamepadProvider : IBatteryProvider
{
    public const uint MaxSlots = 4;

    private readonly IXInputApi _api;
    private readonly uint _slot;

    public XInputGamepadProvider(IXInputApi api, uint slot)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, MaxSlots);
        _api = api;
        _slot = slot;
    }

    public string Id => $"xinput-{_slot}";
    public string Name => $"Control {_slot + 1}";
    public DeviceKind Kind => DeviceKind.Gamepad;
    public string Source => "XInput";

    // No ContainerId: XInput gives a slot number and no device path, so it cannot name the device in the slot.
    public DeviceChannel Channel => DeviceChannel.XInput;

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_api.IsConnected(_slot))
            return Task.FromResult<BatteryStatus?>(null);

        var info = _api.GetGamepadBattery(_slot);
        return Task.FromResult(info is { } b ? XInputBatteryMapper.Map(b.Type, b.Level) : null);
    }
}
