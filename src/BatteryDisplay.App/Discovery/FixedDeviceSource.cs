using BatteryDisplay.Core;

namespace BatteryDisplay.App.Discovery;

/// <summary>A hard-coded list of providers; each one hides its own icon while its device is absent.</summary>
public sealed class FixedDeviceSource(IReadOnlyList<IBatteryProvider> providers) : IDeviceSource
{
    public IReadOnlyList<IBatteryProvider> Discover() => providers;
}
