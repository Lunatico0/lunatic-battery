using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

public enum LevelColor { High, Medium, Low, Charging, Unknown }

public static class LevelColors
{
    public const int HighAbove = 50;
    public const int LowAtOrBelow = 20;

    public static LevelColor For(BatteryStatus status) => status switch
    {
        { Charge: ChargeState.Charging } => LevelColor.Charging,
        { Percent: null } => LevelColor.Unknown,
        { Percent: > HighAbove } => LevelColor.High,
        { Percent: > LowAtOrBelow } => LevelColor.Medium,
        _ => LevelColor.Low,
    };
}
