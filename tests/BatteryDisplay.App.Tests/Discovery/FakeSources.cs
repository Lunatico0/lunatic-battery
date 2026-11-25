using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Discovery;

/// <summary>Returns whatever Current holds at the time of each Discover call.</summary>
internal sealed class FakeSource(params IBatteryProvider[] initial) : IDeviceSource
{
    public IReadOnlyList<IBatteryProvider> Current { get; set; } = initial;
    public int Calls;
    public Action? OnDiscover { get; set; }

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        Interlocked.Increment(ref Calls);
        OnDiscover?.Invoke();
        return Current;
    }
}

internal sealed class ThrowingSource : IDeviceSource
{
    public IReadOnlyList<IBatteryProvider> Discover() => throw new InvalidOperationException("enumeration exploded");
}
