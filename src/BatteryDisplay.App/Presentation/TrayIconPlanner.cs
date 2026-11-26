using System.Collections.Immutable;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

/// <summary>
/// Pure mapping from the icon mode, the device list and the per-device settings to the tray icon slots.
/// Per-device slots are keyed by device Id; the combined icon by <see cref="CombinedKey"/>. A device without
/// a reading keeps a hidden slot so its icon keeps its place in the tray. A device the user hid is left out entirely.
/// </summary>
public static class TrayIconPlanner
{
    /// <summary>Key of the combined icon. Provider Ids (xinput-N, bt-GUID, catalog entry ids) do not start with '*'.</summary>
    public const string CombinedKey = "*combined";
    public const string NoDevicesTooltip = "Sin dispositivos";
    /// <summary>Glyph of the combined icon when no device has a reading.</summary>
    public const DeviceKind NoDeviceKind = DeviceKind.Other;
    public const string SearchingTooltip = "Lunatic Battery: buscando dispositivos";

    private static readonly TrayIconModel NoDevices = new(true, NoDeviceKind, 0, LevelColor.Unknown, false, NoDevicesTooltip);

    /// <summary>
    /// What the tray shows before the first device list arrives: one icon, under the combined key, so the app is never
    /// invisible while discovery runs (or when it fails) and the menu is reachable right away.
    /// </summary>
    public static IReadOnlyList<TrayIconSlot> Searching() => [new(CombinedKey, NoDevices with { Tooltip = SearchingTooltip })];

    /// <summary>
    /// The app is never invisible: a plan with no visible icon (per device mode with nothing connected, nothing read
    /// or everything hidden) gets the "no devices" icon first, under the combined key; the hidden slots keep their places.
    /// </summary>
    public static IReadOnlyList<TrayIconSlot> EnsureOneVisible(IReadOnlyList<TrayIconSlot> slots) =>
        slots.Any(slot => slot.Model.Visible) ? slots : [new(CombinedKey, NoDevices), .. slots];

    public static IReadOnlyList<TrayIconSlot> Plan(TrayIconMode mode, IReadOnlyList<DeviceSnapshot> devices, ImmutableDictionary<string, DeviceSettings> settings)
    {
        DeviceSettings For(DeviceSnapshot d) => settings.GetValueOrDefault(d.Id) ?? DeviceSettings.Default;
        TrayIconSlot Own(DeviceSnapshot d) => new(d.Id, TrayIconPresenter.Present(d.DisplayName, d.Kind, d.Status, For(d).LowBatteryThreshold));

        // A hidden device gets no slot in any mode, not even a hidden one, and never feeds the combined icon.
        devices = devices.Where(d => !For(d).Hidden).ToArray();

        if (mode == TrayIconMode.PerDevice)
            return devices.Select(Own).ToArray();

        bool IsPinned(DeviceSnapshot d) => mode == TrayIconMode.LowestWithPinned && For(d).Pinned;

        var slots = new List<TrayIconSlot> { new(CombinedKey, Combined(devices.Where(d => !IsPinned(d)).ToArray(), For)) };
        slots.AddRange(devices.Where(IsPinned).Select(Own));
        return slots;
    }

    // Lowest known percent wins (first in order on a tie); with no percent at all, the first device read shows
    // the unknown state the way a single device would. The color uses the low threshold of the device shown.
    private static TrayIconModel Combined(IReadOnlyList<DeviceSnapshot> devices, Func<DeviceSnapshot, DeviceSettings> settingsOf)
    {
        var read = devices.Where(d => d.Status is not null).ToArray();
        var shown = read.Where(d => d.Status!.Percent is not null).MinBy(d => d.Status!.Percent) ?? read.FirstOrDefault();
        if (shown is null)
            return NoDevices;

        var tooltip = TrayTooltip.ForMany(read.Select(d => (d.DisplayName, d.Status!)).ToArray());
        return TrayIconPresenter.Present(shown.DisplayName, shown.Kind, shown.Status, settingsOf(shown).LowBatteryThreshold) with { Tooltip = tooltip };
    }
}
