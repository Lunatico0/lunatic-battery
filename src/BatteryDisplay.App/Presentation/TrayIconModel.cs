using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

/// <summary>
/// Which taskbar the icon is drawn for: Dark keeps the disc, Light drops it and uses darker colors. HighContrast is a
/// Windows high contrast theme, which wins over light or dark: no disc, one ink for every level (<see cref="TrayIconInk"/>).
/// </summary>
public enum TrayIconTheme { Dark, Light, HighContrast }

/// <summary>
/// The high contrast theme's own colors, as ARGB: Text for the ring and the glyph, GrayText for a glyph with no reading.
/// Plain values, so a switch between two high contrast themes changes the model and redraws the icon. Empty (default)
/// outside high contrast.
/// </summary>
public readonly record struct TrayIconInk(int Text, int GrayText);

/// <summary>
/// The taskbar's light or dark setting, or a high contrast theme with its ink, raising Changed on the UI thread when
/// either switches.
/// </summary>
public interface ITaskbarTheme
{
    TrayIconTheme Theme { get; }

    /// <summary>The high contrast colors while <see cref="Theme"/> is HighContrast; default otherwise.</summary>
    TrayIconInk Ink { get; }

    event EventHandler? Changed;
}

/// <summary>
/// Everything a tray icon needs to render one provider reading. Visible is false when the device is absent.
/// LitSegments of <see cref="RingSegments.Count"/> take Color; LevelKnown is false when the device reports no percent.
/// Theme and Ink are the taskbar variant, set by <see cref="TrayIconCoordinator"/>, not by the planner.
/// </summary>
public sealed record TrayIconModel(
    bool Visible,
    DeviceKind Kind,
    int LitSegments,
    LevelColor Color,
    bool LevelKnown,
    string Tooltip,
    TrayIconTheme Theme = TrayIconTheme.Dark,
    TrayIconInk Ink = default)
{
    public static readonly TrayIconModel Hidden = new(false, DeviceKind.Mouse, 0, LevelColor.Unknown, false, string.Empty);
}

public static class TrayIconPresenter
{
    /// <summary><paramref name="lowBatteryThreshold"/> is the device's own, so the ring turns red where its row says "Batería baja".</summary>
    public static TrayIconModel Present(string deviceName, DeviceKind kind, BatteryStatus? status, int lowBatteryThreshold) => status is null
        ? TrayIconModel.Hidden
        : new TrayIconModel(true, kind, RingSegments.Lit(status.Percent), LevelColors.For(status, lowBatteryThreshold), status.Percent is not null,
            TrayTooltip.For(deviceName, status));
}
