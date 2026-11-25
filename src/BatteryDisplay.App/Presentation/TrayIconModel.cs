using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

/// <summary>Which taskbar the icon is drawn for: Dark keeps the disc, Light drops it and uses darker colors.</summary>
public enum TrayIconTheme { Dark, Light }

/// <summary>The taskbar's light or dark setting, raising Changed on the UI thread when the user switches it.</summary>
public interface ITaskbarTheme
{
    TrayIconTheme Theme { get; }

    event EventHandler? Changed;
}

/// <summary>
/// Everything a tray icon needs to render one provider reading. Visible is false when the device is absent.
/// LitSegments of <see cref="RingSegments.Count"/> take Color; LevelKnown is false when the device reports no percent.
/// Theme is the taskbar variant, set by <see cref="TrayIconCoordinator"/>, not by the planner.
/// </summary>
public sealed record TrayIconModel(
    bool Visible,
    DeviceKind Kind,
    int LitSegments,
    LevelColor Color,
    bool LevelKnown,
    string Tooltip,
    TrayIconTheme Theme = TrayIconTheme.Dark)
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
