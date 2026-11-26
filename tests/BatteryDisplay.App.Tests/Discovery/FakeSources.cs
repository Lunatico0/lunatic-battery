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

/// <summary>
/// Blocks inside Discover until Release, like an enumeration stuck on the PnP lock. The first FreeCalls calls
/// answer at once.
/// </summary>
internal sealed class BlockingSource(params IBatteryProvider[] providers) : IDeviceSource, IDisposable
{
    private readonly ManualResetEventSlim _gate = new();
    public int Calls;
    public int FreeCalls { get; init; }

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        if (Interlocked.Increment(ref Calls) > FreeCalls)
            _gate.Wait(TimeSpan.FromSeconds(30));
        return providers;
    }

    public void Release() => _gate.Set();

    public void Dispose() => _gate.Set();
}

internal sealed class ThrowingSource : IDeviceSource
{
    public IReadOnlyList<IBatteryProvider> Discover() => throw new InvalidOperationException("enumeration exploded");
}
