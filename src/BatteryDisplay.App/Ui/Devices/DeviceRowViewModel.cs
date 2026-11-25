using System.Globalization;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Ui.Devices;

/// <summary>What the row says under the value. Charging wins over Low, and Low wins over Discharging.</summary>
public enum DeviceState { NoData, Discharging, Low, Charging, Charged }

public static class DeviceStates
{
    /// <summary>Low is a known percent at or below the device's threshold, unless the device is charging or full.</summary>
    public static DeviceState For(BatteryStatus? status, int lowBatteryThreshold) => status switch
    {
        null => DeviceState.NoData,
        { Charge: ChargeState.Charging } => DeviceState.Charging,
        { Charge: ChargeState.Full } => DeviceState.Charged,
        { Percent: null } => DeviceState.NoData,
        { Percent: var percent } when percent <= lowBatteryThreshold => DeviceState.Low,
        // Bluetooth reports no charge state; a known level that is not charging reads as discharging.
        _ => DeviceState.Discharging,
    };

    public static string Text(DeviceState state) => state switch
    {
        DeviceState.Low => "Batería baja",
        DeviceState.Charging => "Cargando",
        DeviceState.Charged => "Cargada",
        DeviceState.Discharging => "Descargando",
        _ => "Sin datos",
    };
}

/// <summary>Segoe Fluent Icons code points per device kind. Mouse and headset are the glyphs the tray icon draws.</summary>
public static class DeviceGlyphs
{
    public const string Mouse = "\uE962";
    public const string Headset = "\uE7F6";
    public const string Keyboard = "\uE765";
    public const string Gamepad = "\uE7FC";

    // Other falls back to the gamepad, like the tray icon.
    public static string For(DeviceKind kind) => kind switch
    {
        DeviceKind.Mouse => Mouse,
        DeviceKind.Headset => Headset,
        DeviceKind.Keyboard => Keyboard,
        _ => Gamepad,
    };
}

/// <summary>
/// One device as the flyout and the settings list show it. Tone and LitSegments follow the tray ring
/// (<see cref="LevelColors"/>, <see cref="RingSegments"/>); State follows the device's own low threshold.
/// </summary>
public sealed record DeviceRowViewModel(
    string Id,
    string Name,
    string Source,
    string ValueText,
    DeviceState State,
    string StateText,
    LevelColor Tone,
    int LitSegments,
    bool LevelKnown,
    string Glyph,
    bool Hidden = false)
{
    public const string NoValue = "-";

    public static DeviceRowViewModel From(DeviceSnapshot device, DeviceSettings settings)
    {
        var status = device.Status;
        var percent = status?.Percent;
        var state = DeviceStates.For(status, settings.LowBatteryThreshold);
        return new DeviceRowViewModel(
            device.Id,
            device.DisplayName,
            device.Source,
            percent is { } p ? string.Create(CultureInfo.InvariantCulture, $"{p}%") : NoValue,
            state,
            DeviceStates.Text(state),
            status is null ? LevelColor.Unknown : LevelColors.For(status, settings.LowBatteryThreshold),
            RingSegments.Lit(percent),
            percent is not null,
            DeviceGlyphs.For(device.Kind),
            settings.Hidden);
    }
}
