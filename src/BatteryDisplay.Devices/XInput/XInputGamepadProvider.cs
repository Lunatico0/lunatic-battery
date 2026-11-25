using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.XInput;

public readonly record struct XInputBatteryInformation(XInputBatteryType Type, XInputBatteryLevel Level);

/// <summary>Seam over xinput1_4.dll so the provider logic can be tested without hardware.</summary>
public interface IXInputApi
{
    bool IsConnected(uint userIndex);
    XInputBatteryInformation? GetGamepadBattery(uint userIndex);
}

public sealed class XInputGamepadProvider : IBatteryProvider
{
    private readonly IXInputApi _api;

    public XInputGamepadProvider() : this(new XInputNativeApi()) { }

    public XInputGamepadProvider(IXInputApi api) => _api = api;

    private const uint MaxUsers = 4;

    public string Id => "xinput-gamepad";
    public string Name => "Joystick";
    public DeviceKind Kind => DeviceKind.Gamepad;

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        for (uint i = 0; i < MaxUsers; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (!_api.IsConnected(i))
                continue;

            var info = _api.GetGamepadBattery(i);
            return Task.FromResult(info is { } b ? XInputBatteryMapper.Map(b.Type, b.Level) : null);
        }

        return Task.FromResult<BatteryStatus?>(null);
    }
}
