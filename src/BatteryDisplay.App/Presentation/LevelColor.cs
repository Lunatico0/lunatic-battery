using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

public enum LevelColor { High, Medium, Low, Charging, Unknown }

/// <summary>
/// The one color rule for the tray ring, the combined icon, the flyout and settings rows and the detail ring.
/// Low is the device's own low battery threshold, so the red ring and the "Batería baja" state always agree.
/// </summary>
public static class LevelColors
{
    public const int MediumAtOrBelow = 50;

    public static LevelColor For(BatteryStatus status, int lowBatteryThreshold) => status switch
    {
        { Charge: ChargeState.Charging } => LevelColor.Charging,
        { Percent: null } => LevelColor.Unknown,
        { Percent: var percent } when percent <= lowBatteryThreshold => LevelColor.Low,
        { Percent: <= MediumAtOrBelow } => LevelColor.Medium,
        _ => LevelColor.High,
    };
}
