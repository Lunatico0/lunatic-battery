using BatteryDisplay.App.Settings;

namespace BatteryDisplay.App.Tests.Settings;

/// <summary>In-memory store with the same contract as the JSON one: Current first, then Changed.</summary>
internal sealed class FakeSettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Current { get; private set; } = initial ?? AppSettings.Default;

    public event EventHandler<AppSettings>? Changed;

    public bool Save(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, settings);
        return true;
    }

    public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
}

/// <summary>A clock the test moves by hand.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}
