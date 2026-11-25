using System.Drawing;
using BatteryDisplay.App.Presentation;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Tray icon colors for each taskbar. Dark draws on its own dark disc, so the level colors are bright.
/// Light has no disc and draws straight on a taskbar of about #EEEEEE to #F3F3F3: every color that carries meaning
/// (ring tones, unlit track, glyph) keeps at least 3:1 against both ends of that range.
/// </summary>
internal sealed record IconPalette(Color? Disc, Color UnlitSegment, Color Silhouette, Color SilhouetteUnknown,
    Color High, Color Medium, Color Low, Color Charging)
{
    public static readonly IconPalette Dark = new(
        Disc: Color.FromArgb(0x1A, 0x1A, 0x1A),
        UnlitSegment: Color.FromArgb(0x4A, 0x4A, 0x4A),
        Silhouette: Color.FromArgb(0xE5, 0xE5, 0xE5),
        SilhouetteUnknown: Color.FromArgb(0x8A, 0x8A, 0x8A),
        High: Color.FromArgb(0x4A, 0xDE, 0x80),
        Medium: Color.FromArgb(0xFB, 0xBF, 0x24),
        Low: Color.FromArgb(0xF8, 0x71, 0x71),
        Charging: Color.FromArgb(0x22, 0xD3, 0xEE));

    public static readonly IconPalette Light = new(
        Disc: null,
        UnlitSegment: Color.FromArgb(0x88, 0x88, 0x88),
        Silhouette: Color.FromArgb(0x1A, 0x1A, 0x1A),
        SilhouetteUnknown: Color.FromArgb(0x70, 0x70, 0x70),
        High: Color.FromArgb(0x15, 0x80, 0x3D),
        Medium: Color.FromArgb(0xB4, 0x53, 0x09),
        Low: Color.FromArgb(0xB9, 0x1C, 0x1C),
        Charging: Color.FromArgb(0x0E, 0x74, 0x90));

    public static IconPalette For(TrayIconTheme theme) => theme == TrayIconTheme.Light ? Light : Dark;

    public Color For(LevelColor level) => level switch
    {
        LevelColor.High => High,
        LevelColor.Medium => Medium,
        LevelColor.Low => Low,
        LevelColor.Charging => Charging,
        _ => UnlitSegment,
    };
}
