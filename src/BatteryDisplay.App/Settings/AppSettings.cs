using System.Collections.Immutable;

namespace BatteryDisplay.App.Settings;

/// <summary>
/// What the user chose for one device, keyed by provider Id. DisplayName null means the provider Name.
/// Pinned only matters in <see cref="TrayIconMode.LowestWithPinned"/>; the threshold and the charged notice feed notifications.
/// A Hidden device gets no tray icon, does not count for the combined icon and is left out of the flyout; only the
/// settings window still lists it, so it can be shown again.
/// </summary>
public sealed record DeviceSettings(
    string? DisplayName = null,
    bool Pinned = false,
    int LowBatteryThreshold = DeviceSettings.DefaultLowBatteryThreshold,
    bool NotifyWhenCharged = false,
    bool Hidden = false)
{
    public const int DefaultLowBatteryThreshold = 15;

    public static readonly DeviceSettings Default = new();
}

/// <summary>Immutable user settings: change them with <c>with</c> or <see cref="WithDevice"/> and hand the copy to the store.</summary>
public sealed record AppSettings(TrayIconMode TrayIconMode, ImmutableDictionary<string, DeviceSettings> Devices)
{
    public static readonly AppSettings Default = new(TrayIconMode.PerDevice, ImmutableDictionary.Create<string, DeviceSettings>(StringComparer.Ordinal));

    /// <summary>The device's settings, or the defaults for a device the user never touched.</summary>
    public DeviceSettings For(string deviceId) => Devices.GetValueOrDefault(deviceId) ?? DeviceSettings.Default;

    public AppSettings WithDevice(string deviceId, DeviceSettings settings) => this with { Devices = Devices.SetItem(deviceId, settings) };

    /// <summary>The name the user gave the device, or the provider name when none was given (blank counts as none).</summary>
    public string DisplayNameOf(string deviceId, string providerName) =>
        For(deviceId).DisplayName is { } name && !string.IsNullOrWhiteSpace(name) ? name : providerName;
}
