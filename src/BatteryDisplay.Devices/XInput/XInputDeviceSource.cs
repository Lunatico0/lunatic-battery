using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.XInput;

/// <summary>One gamepad provider per connected XInput slot.</summary>
public sealed class XInputDeviceSource(IXInputApi api) : IDeviceSource
{
    // Same instance per slot across calls, so rediscovery keeps the provider.
    private readonly XInputGamepadProvider?[] _providers = new XInputGamepadProvider?[XInputGamepadProvider.MaxSlots];

    public XInputDeviceSource() : this(new XInputNativeApi()) { }

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        try
        {
            var providers = new List<IBatteryProvider>();
            for (uint slot = 0; slot < XInputGamepadProvider.MaxSlots; slot++)
            {
                if (api.IsConnected(slot))
                    providers.Add(_providers[slot] ??= new XInputGamepadProvider(api, slot));
            }
            return providers;
        }
        catch (Exception)
        {
            // xinput1_4.dll missing or failing: no gamepads.
            return [];
        }
    }
}
