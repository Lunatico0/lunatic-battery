using BatteryDisplay.Core;

namespace BatteryDisplay.App.Polling;

/// <summary>
/// One tracked device as of the last refresh. DisplayName is the user's name for it, or Name when none was given.
/// Status is null when the device gave no reading this time; LastReadAt is when it last gave one (null if never
/// since it appeared).
/// </summary>
public sealed record DeviceSnapshot(
    string Id, string Name, string DisplayName, DeviceKind Kind, string Source, BatteryStatus? Status, DateTimeOffset? LastReadAt);

/// <summary>Read side of the monitor for the UI: the current devices, in order of first appearance, and a change event.</summary>
public interface IDeviceSnapshots
{
    /// <summary>Immutable; replaced as a whole on every change.</summary>
    IReadOnlyList<DeviceSnapshot> Devices { get; }

    /// <summary>Raised with the new list on the thread that ran the refresh or saved the settings (the UI thread in the app).</summary>
    event EventHandler<IReadOnlyList<DeviceSnapshot>>? DevicesChanged;
}
