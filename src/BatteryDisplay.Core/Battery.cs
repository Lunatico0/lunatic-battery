namespace BatteryDisplay.Core;

public enum DeviceKind { Mouse, Keyboard, Headset, Gamepad }

public enum ChargeState { Unknown, Discharging, Charging, Full }

/// <summary>Percent is 0..100, or null when the device is connected but does not report a level.</summary>
public sealed record BatteryStatus(int? Percent, ChargeState Charge);

/// <summary>
/// Port implemented by each device adapter (Razer, SteelSeries, XInput, ...).
/// ReadAsync returns null when the device is not present or is powered off.
/// </summary>
public interface IBatteryProvider
{
    string Id { get; }
    string Name { get; }
    DeviceKind Kind { get; }
    Task<BatteryStatus?> ReadAsync(CancellationToken ct);
}
