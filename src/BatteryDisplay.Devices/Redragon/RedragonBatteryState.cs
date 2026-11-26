using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Redragon;

/// <summary>
/// What the pushed frames say so far. With the cable in the keyboard reports an unreliable level (75 then 100 within
/// seconds at a true ~85), so charging shows the last level seen on battery, or the reported one until there is any.
/// A lost link keeps the reading: the charge does not change while the keyboard is off or asleep.
/// </summary>
public sealed record RedragonBatteryState(BatteryStatus? Reading, int? LastOnBattery)
{
    /// <summary>Nothing received yet: the keyboard pushes nothing until its first event.</summary>
    public static RedragonBatteryState Unknown { get; } = new(null, null);

    public RedragonBatteryState Apply(RedragonFrame frame) => frame switch
    {
        RedragonBatteryFrame { CableIn: true } battery =>
            this with { Reading = new BatteryStatus(LastOnBattery ?? battery.Percent, ChargeState.Charging) },
        RedragonBatteryFrame battery =>
            new RedragonBatteryState(new BatteryStatus(battery.Percent, ChargeState.Discharging), battery.Percent),
        _ => this,
    };
}
