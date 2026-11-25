using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

/// <summary>
/// Everything a tray icon needs to render one provider reading. Visible is false when the device is absent.
/// LitSegments of <see cref="RingSegments.Count"/> take Color; LevelKnown is false when the device reports no percent.
/// </summary>
public sealed record TrayIconModel(bool Visible, DeviceKind Kind, int LitSegments, LevelColor Color, bool LevelKnown, string Tooltip)
{
    public static readonly TrayIconModel Hidden = new(false, DeviceKind.Mouse, 0, LevelColor.Unknown, false, string.Empty);
}

public static class TrayIconPresenter
{
    public static TrayIconModel Present(string deviceName, DeviceKind kind, BatteryStatus? status) => status is null
        ? TrayIconModel.Hidden
        : new TrayIconModel(true, kind, RingSegments.Lit(status.Percent), LevelColors.For(status), status.Percent is not null, TrayTooltip.For(deviceName, status));
}
