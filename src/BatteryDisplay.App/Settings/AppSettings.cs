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

/// <summary>
/// Immutable user settings: change them with <c>with</c> or <see cref="WithDevice"/> and hand the copy to the store.
/// NotifyLowBattery is the global switch for the low battery notice; each device keeps its own threshold.
/// Appearance picks how the flyout and the settings window look (Fluent by default).
/// DeviceAliases sends the Id one source gave a physical device to the Id it is shown and configured under, learned
/// when discovery merged both sources, so the device keeps its Id and settings when only the other source sees it.
/// </summary>
public sealed record AppSettings(
    TrayIconMode TrayIconMode,
    ImmutableDictionary<string, DeviceSettings> Devices,
    bool NotifyLowBattery = true,
    UiAppearance Appearance = UiAppearance.Fluent)
{
    // Declared before Default, which needs it already set.
    private static readonly ImmutableDictionary<string, string> NoAliases = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);

    public static readonly AppSettings Default = new(TrayIconMode.PerDevice, ImmutableDictionary.Create<string, DeviceSettings>(StringComparer.Ordinal));

    /// <summary>Alias Id to canonical Id. A canonical Id is never itself an alias, so one lookup always lands.</summary>
    public ImmutableDictionary<string, string> DeviceAliases { get; init; } = NoAliases;

    /// <summary>The device's settings, or the defaults for a device the user never touched.</summary>
    public DeviceSettings For(string deviceId) => Devices.GetValueOrDefault(deviceId) ?? DeviceSettings.Default;

    public AppSettings WithDevice(string deviceId, DeviceSettings settings) => this with { Devices = Devices.SetItem(deviceId, settings) };

    /// <summary>The Id the device is shown and configured under: its canonical Id when it is an alias, else itself.</summary>
    public string CanonicalIdOf(string deviceId) => DeviceAliases.GetValueOrDefault(deviceId) ?? deviceId;

    /// <summary>
    /// Records that <paramref name="aliasId"/> is the same device as <paramref name="canonicalId"/>. The alias's settings
    /// move to the canonical Id when it has none (the canonical ones win otherwise), aliases of the alias are repointed,
    /// and a canonical Id that was an alias stops being one, so no chain or cycle forms. Same instance when already known.
    /// </summary>
    public AppSettings WithAlias(string aliasId, string canonicalId)
    {
        if (aliasId == canonicalId)
            return this;
        if (DeviceAliases.GetValueOrDefault(aliasId) == canonicalId && !DeviceAliases.ContainsKey(canonicalId) && !Devices.ContainsKey(aliasId))
            return this;

        var aliases = DeviceAliases.Remove(canonicalId).SetItem(aliasId, canonicalId);
        foreach (var (alias, target) in aliases)
        {
            if (target == aliasId)
                aliases = aliases.SetItem(alias, canonicalId);
        }

        var devices = Devices;
        if (devices.TryGetValue(aliasId, out var moved))
            devices = (devices.ContainsKey(canonicalId) ? devices : devices.SetItem(canonicalId, moved)).Remove(aliasId);
        return this with { DeviceAliases = aliases, Devices = devices };
    }

    /// <summary>The name the user gave the device, or the provider name when none was given (blank counts as none).</summary>
    public string DisplayNameOf(string deviceId, string providerName) =>
        For(deviceId).DisplayName is { } name && !string.IsNullOrWhiteSpace(name) ? name : providerName;
}
