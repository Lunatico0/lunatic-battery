namespace BatteryDisplay.Core;

public enum DeviceKind { Mouse, Keyboard, Headset, Gamepad, Other }

public enum ChargeState { Unknown, Discharging, Charging, Full }

/// <summary>Percent is 0..100, or null when the device is connected but does not report a level.</summary>
public sealed record BatteryStatus(int? Percent, ChargeState Charge);

/// <summary>
/// How directly a provider reaches its device, most direct first. When two sources see the same physical device
/// (same <see cref="PhysicalIdentity"/>), discovery keeps the most direct one.
/// </summary>
public enum DeviceChannel { VendorHid, XInput, Bluetooth, Other }

/// <summary>
/// The PnP ContainerId Windows gives every node of one physical device, whatever the bus or driver that exposes it.
/// It is what lets discovery recognize a device seen by two sources.
/// </summary>
public static class PhysicalIdentity
{
    /// <summary>The container of the computer itself: every built-in node shares it, so it identifies no device.</summary>
    public static readonly Guid LocalMachine = new("00000000-0000-0000-ffff-ffffffffffff");

    /// <summary>True when the container names one physical device: not missing, not empty, not the computer's.</summary>
    public static bool Identifies(Guid? containerId) =>
        containerId is { } id && id != Guid.Empty && id != LocalMachine;
}

/// <summary>
/// Port implemented by each device adapter (Razer, SteelSeries, XInput, ...).
/// ReadAsync returns null when the device is not present or is powered off.
/// </summary>
public interface IBatteryProvider
{
    string Id { get; }
    string Name { get; }
    DeviceKind Kind { get; }

    /// <summary>Short Spanish label for how the device is reached ("Bluetooth", "Dongle Razer", ...), shown under its name.</summary>
    string Source => "";

    /// <summary>How the device is reached; decides which provider stays when two sources see the same device.</summary>
    DeviceChannel Channel => DeviceChannel.Other;

    /// <summary>
    /// PnP ContainerId of the physical device, or null when the source cannot tell. Two providers with the same
    /// identifying container (see <see cref="PhysicalIdentity.Identifies"/>) are the same device.
    /// </summary>
    Guid? ContainerId => null;

    Task<BatteryStatus?> ReadAsync(CancellationToken ct);
}
