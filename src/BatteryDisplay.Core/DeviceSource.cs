namespace BatteryDisplay.Core;

/// <summary>
/// Port implemented by each detection mechanism (vendor catalog, Bluetooth, XInput, ...).
/// Discover enumerates the devices connected right now and returns one provider per physical device.
/// A provider's Id must be stable across calls for the same physical device, so the tray keeps its icon
/// and settings when the device is rediscovered. Discover must not throw and must only read: it never
/// sends commands to a device.
/// </summary>
public interface IDeviceSource
{
    IReadOnlyList<IBatteryProvider> Discover();
}
