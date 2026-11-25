using System.Drawing;
using BatteryDisplay.App.Presentation;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// The ring and silhouette sit on a near-black disc, so their contrast does not depend on the taskbar theme:
/// the disc blends into the dark taskbar (#1C1C1C) and stands out on the light one (#F3F3F3).
/// </summary>
internal static class IconPalette
{
    public static readonly Color Badge = Color.FromArgb(0x1A, 0x1A, 0x1A);
    public static readonly Color UnlitSegment = Color.FromArgb(0x4A, 0x4A, 0x4A);
    public static readonly Color Silhouette = Color.FromArgb(0xE5, 0xE5, 0xE5);
    public static readonly Color SilhouetteUnknown = Color.FromArgb(0x8A, 0x8A, 0x8A);

    public static Color For(LevelColor level) => level switch
    {
        LevelColor.High => Color.FromArgb(0x4A, 0xDE, 0x80),
        LevelColor.Medium => Color.FromArgb(0xFB, 0xBF, 0x24),
        LevelColor.Low => Color.FromArgb(0xF8, 0x71, 0x71),
        LevelColor.Charging => Color.FromArgb(0x22, 0xD3, 0xEE),
        _ => UnlitSegment,
    };
}
