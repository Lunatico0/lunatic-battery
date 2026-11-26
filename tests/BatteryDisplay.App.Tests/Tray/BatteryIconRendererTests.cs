using System.Drawing;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Tray;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Tray;

public class BatteryIconRendererTests
{
    private static TrayIconModel Model(int lit, DeviceKind kind = DeviceKind.Mouse, bool known = true) =>
        new(true, kind, lit, LevelColor.High, known, "x");

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void Draws_AtRequestedSize_WithTransparentCorners(int px)
    {
        var size = new Size(px, px);
        using var bitmap = BatteryIconRenderer.Draw(Model(8), size);

        Assert.Equal(size, bitmap.Size);
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
        Assert.Equal(0, bitmap.GetPixel(px - 1, px - 1).A);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void FirstEighth_UsesTheLevelColor_OnlyWhenLit(int px)
    {
        using var lit = BatteryIconRenderer.Draw(Model(1), new Size(px, px));
        using var unlit = BatteryIconRenderer.Draw(Model(0), new Size(px, px));

        // The ring is about one pixel thick at 16 px, so its pixels are anti-aliased: check the tint, not the exact color.
        Assert.True(CountGreenish(lit, fromDegrees: 5, toDegrees: 40) > 0);
        Assert.Equal(0, CountGreenish(lit, fromDegrees: 180, toDegrees: 225));
        Assert.Equal(0, CountGreenish(unlit, fromDegrees: 0, toDegrees: 360));
    }

    // The depleted part has no track at all: a colorblind user cannot tell a colored segment from a gray one, but can
    // tell a line from no line.
    [Theory]
    [InlineData(24, 0)]
    [InlineData(24, 4)]
    [InlineData(32, 0)]
    [InlineData(32, 4)]
    public void DepletedPart_DrawsNothing_OnEitherTaskbar(int px, int litSegments)
    {
        using var dark = BatteryIconRenderer.Draw(Model(litSegments), new Size(px, px));
        using var light = BatteryIconRenderer.Draw(Light(Model(litSegments)), new Size(px, px));

        var depleted = RingBandPixels(px, fromDegrees: 190, toDegrees: 350).ToArray();
        Assert.NotEmpty(depleted);
        Assert.All(depleted, p => Assert.Equal(IconPalette.Dark.Disc!.Value.ToArgb(), dark.GetPixel(p.X, p.Y).ToArgb()));
        Assert.All(depleted, p => Assert.Equal(0, light.GetPixel(p.X, p.Y).A));
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void Silhouette_IsBrightWhenKnown_AndDimmerWhenUnknown(DeviceKind kind)
    {
        using var known = BatteryIconRenderer.Draw(Model(4, kind), new Size(24, 24));
        using var unknown = BatteryIconRenderer.Draw(Model(0, kind, known: false), new Size(24, 24));

        Assert.True(CountNear(known, IconPalette.Dark.Silhouette) > 0);
        Assert.Equal(0, CountNear(unknown, IconPalette.Dark.Silhouette));
        Assert.True(CountNear(unknown, IconPalette.Dark.SilhouetteUnknown) > 0);
    }

    // Light taskbar variant

    private static TrayIconModel Light(TrayIconModel model) => model with { Theme = TrayIconTheme.Light };

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void Dark_KeepsTheDisc_BetweenTheGlyphAndTheRing(DeviceKind kind)
    {
        using var dark = BatteryIconRenderer.Draw(Model(4, kind), new Size(24, 24));

        // At 24 px the glyph fits in radius 7 and the ring starts at radius 10.
        var gap = GapPixels(dark).ToArray();
        Assert.NotEmpty(gap);
        Assert.All(gap, p => Assert.Equal(IconPalette.Dark.Disc!.Value.ToArgb(), p.ToArgb()));
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void Light_DrawsNoDisc_TheGapIsTransparent(DeviceKind kind)
    {
        using var light = BatteryIconRenderer.Draw(Light(Model(4, kind)), new Size(24, 24));

        Assert.All(GapPixels(light), p => Assert.Equal(0, p.A));
    }

    [Fact]
    public void Light_KeyboardCut_IsAHole_NotPaintedInTheDiscColor()
    {
        using var dark = BatteryIconRenderer.Draw(Model(4, DeviceKind.Keyboard), new Size(24, 24));
        using var light = BatteryIconRenderer.Draw(Light(Model(4, DeviceKind.Keyboard)), new Size(24, 24));

        // (12, 13) sits inside the space bar cut at 24 px.
        Assert.Equal(IconPalette.Dark.Disc!.Value.ToArgb(), dark.GetPixel(12, 13).ToArgb());
        Assert.Equal(0, light.GetPixel(12, 13).A);
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void Light_GlyphIsDark_AndDimmerWhenUnknown(DeviceKind kind)
    {
        using var known = BatteryIconRenderer.Draw(Light(Model(4, kind)), new Size(24, 24));
        using var unknown = BatteryIconRenderer.Draw(Light(Model(0, kind, known: false)), new Size(24, 24));

        Assert.True(CountNear(known, IconPalette.Light.Silhouette) > 0);
        Assert.Equal(0, CountNear(known, IconPalette.Dark.Silhouette));
        Assert.Equal(0, CountNear(unknown, IconPalette.Light.Silhouette));
        Assert.True(CountNear(unknown, IconPalette.Light.SilhouetteUnknown) > 0);
    }

    [Theory]
    [InlineData(LevelColor.High)]
    [InlineData(LevelColor.Medium)]
    [InlineData(LevelColor.Low)]
    [InlineData(LevelColor.Charging)]
    public void Light_RingUsesTheDarkerLightPalette(LevelColor color)
    {
        var model = new TrayIconModel(true, DeviceKind.Mouse, 8, color, true, "x", TrayIconTheme.Light);

        using var light = BatteryIconRenderer.Draw(model, new Size(24, 24));

        Assert.True(CountNear(light, IconPalette.Light.For(color)) > 0);
        Assert.Equal(0, CountNear(light, IconPalette.Dark.For(color)));
    }

    // High contrast variant: one ink for every level, from the system colors. White on black with the green gray text
    // of High Contrast Black, so no Dark or Light palette color can pass for it.

    private static readonly Color HcText = Color.FromArgb(0xFF, 0xFF, 0xFF);
    private static readonly Color HcGrayText = Color.FromArgb(0x3F, 0xF2, 0x3F);

    private static TrayIconModel HighContrast(TrayIconModel model) =>
        model with { Theme = TrayIconTheme.HighContrast, Ink = new TrayIconInk(HcText.ToArgb(), HcGrayText.ToArgb()) };

    [Theory]
    [InlineData(LevelColor.High)]
    [InlineData(LevelColor.Medium)]
    [InlineData(LevelColor.Low)]
    [InlineData(LevelColor.Charging)]
    public void HighContrast_RingUsesTheSystemTextColor_ForEveryLevel(LevelColor color)
    {
        var model = HighContrast(new TrayIconModel(true, DeviceKind.Mouse, 8, color, true, "x"));

        using var bitmap = BatteryIconRenderer.Draw(model, new Size(24, 24));

        var band = RingBandPixels(24, fromDegrees: 0, toDegrees: 360).Select(p => bitmap.GetPixel(p.X, p.Y)).ToArray();
        Assert.Contains(band, p => Near(p, HcText));
        Assert.DoesNotContain(band, p => Near(p, IconPalette.Dark.For(color)) || Near(p, IconPalette.Light.For(color)));
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void HighContrast_DrawsNoDisc_TheGapAndTheDepletedPartAreTransparent(DeviceKind kind)
    {
        using var bitmap = BatteryIconRenderer.Draw(HighContrast(Model(4, kind)), new Size(24, 24));

        Assert.All(GapPixels(bitmap), p => Assert.Equal(0, p.A));
        Assert.All(RingBandPixels(24, fromDegrees: 190, toDegrees: 350), p => Assert.Equal(0, bitmap.GetPixel(p.X, p.Y).A));
    }

    [Theory]
    [InlineData(DeviceKind.Mouse)]
    [InlineData(DeviceKind.Headset)]
    [InlineData(DeviceKind.Keyboard)]
    [InlineData(DeviceKind.Gamepad)]
    public void HighContrast_GlyphUsesTheTextColor_AndGrayTextWithoutAReading(DeviceKind kind)
    {
        using var known = BatteryIconRenderer.Draw(HighContrast(Model(4, kind)), new Size(24, 24));
        using var unknown = BatteryIconRenderer.Draw(HighContrast(Model(0, kind, known: false)), new Size(24, 24));

        Assert.True(CountNear(known, HcText) > 0);
        Assert.Equal(0, CountNear(known, HcGrayText));
        Assert.Equal(0, CountNear(unknown, HcText));
        Assert.True(CountNear(unknown, HcGrayText) > 0);
    }

    // Charging: a colorblind user cannot tell the cyan ring from the green one, so charging also swaps the device glyph
    // for a lightning bolt in the glyph's own color. The bolt is solid across its middle, where the mouse and headset
    // glyphs are hollow, so that band tells the two shapes apart without looking at any hue.

    public static TheoryData<TrayIconTheme, int> ThemesAndSizes()
    {
        var data = new TheoryData<TrayIconTheme, int>();
        foreach (var theme in new[] { TrayIconTheme.Dark, TrayIconTheme.Light, TrayIconTheme.HighContrast })
            foreach (var px in new[] { 16, 32 })
                data.Add(theme, px);
        return data;
    }

    [Theory]
    [MemberData(nameof(ThemesAndSizes))]
    public void Charging_DrawsABoltInTheGlyphColor(TrayIconTheme theme, int px)
    {
        foreach (var kind in new[] { DeviceKind.Mouse, DeviceKind.Headset })
            foreach (var lit in new[] { 2, 7 })
            {
                using var bitmap = BatteryIconRenderer.Draw(Themed(Charging(lit, kind), theme), new Size(px, px));

                var core = BoltCorePixels(px).ToArray();
                Assert.NotEmpty(core);
                Assert.All(core, p => Assert.True(Near(bitmap.GetPixel(p.X, p.Y), GlyphColor(theme)),
                    $"{kind} {lit}/8 at {p}: {bitmap.GetPixel(p.X, p.Y)}"));
            }
    }

    [Theory]
    [MemberData(nameof(ThemesAndSizes))]
    public void NotCharging_DrawsNoBolt(TrayIconTheme theme, int px)
    {
        foreach (var kind in new[] { DeviceKind.Mouse, DeviceKind.Headset })
            foreach (var lit in new[] { 2, 7 })
            {
                using var bitmap = BatteryIconRenderer.Draw(Themed(Model(lit, kind), theme), new Size(px, px));

                Assert.All(BoltCorePixels(px), p => Assert.False(Near(bitmap.GetPixel(p.X, p.Y), GlyphColor(theme)),
                    $"{kind} {lit}/8 at {p}: {bitmap.GetPixel(p.X, p.Y)}"));
            }
    }

    // The ring band probe needs a ring wider than one pixel, so this guard runs at 24 and 32 px.
    [Theory]
    [InlineData(TrayIconTheme.Dark, 24)]
    [InlineData(TrayIconTheme.Dark, 32)]
    [InlineData(TrayIconTheme.Light, 24)]
    [InlineData(TrayIconTheme.Light, 32)]
    [InlineData(TrayIconTheme.HighContrast, 24)]
    [InlineData(TrayIconTheme.HighContrast, 32)]
    public void Charging_KeepsTheLevelAsRingLength(TrayIconTheme theme, int px)
    {
        using var bitmap = BatteryIconRenderer.Draw(Themed(Charging(2, DeviceKind.Mouse), theme), new Size(px, px));

        // Two eighths lit: the ring runs from 12 to 3 o'clock and nothing is drawn on the depleted part.
        Assert.Contains(RingBandPixels(px, fromDegrees: 10, toDegrees: 80), p => bitmap.GetPixel(p.X, p.Y).A == 255);
        Assert.All(RingBandPixels(px, fromDegrees: 190, toDegrees: 350), p => Assert.True(IsBackground(bitmap.GetPixel(p.X, p.Y), theme)));
    }

    private static TrayIconModel Charging(int lit, DeviceKind kind) => new(true, kind, lit, LevelColor.Charging, true, "x");

    private static TrayIconModel Themed(TrayIconModel model, TrayIconTheme theme) => theme switch
    {
        TrayIconTheme.Light => Light(model),
        TrayIconTheme.HighContrast => HighContrast(model),
        _ => model,
    };

    private static Color GlyphColor(TrayIconTheme theme) => theme switch
    {
        TrayIconTheme.Light => IconPalette.Light.Silhouette,
        TrayIconTheme.HighContrast => HcText,
        _ => IconPalette.Dark.Silhouette,
    };

    // What an empty spot inside the ring holds: the disc on Dark, nothing elsewhere.
    private static bool IsBackground(Color pixel, TrayIconTheme theme) =>
        theme == TrayIconTheme.Dark ? pixel.ToArgb() == IconPalette.Dark.Disc!.Value.ToArgb() : pixel.A == 0;

    // The bolt fills the glyph's box (1.5 radii wide, 2 tall, centered). Its widest part is the band from 40% to 50% of
    // the box height, solid from 30% to 80% of the box width; only pixels wholly inside that band are probed, so
    // anti-aliased edges stay out (16 px: row 7, columns 7..9).
    private static IEnumerable<Point> BoltCorePixels(int px)
    {
        var r = BatteryIconRenderer.RingGeometry.For(px).SilhouetteRadius;
        var c = px / 2.0;
        double left = c - 0.75 * r, top = c - r, width = 1.5 * r, height = 2.0 * r;
        for (var y = (int)Math.Ceiling(top + 0.40 * height); y + 1 <= top + 0.50 * height; y++)
            for (var x = (int)Math.Ceiling(left + 0.30 * width); x + 1 <= left + 0.80 * width; x++)
                yield return new Point(x, y);
    }

    // At 24 px the glyph's ink box is 14 px tall (rows 5..18) and the ring starts at radius 10, so rows 3 and 20 near the
    // middle column are always clear of both; the box corners can reach past radius 7, so the sides are not probed.
    private static IEnumerable<Color> GapPixels(Bitmap bitmap)
    {
        foreach (var y in new[] { 3, 20 })
            for (var x = 10; x <= 13; x++)
                yield return bitmap.GetPixel(x, y);
    }

    // Pixels fully inside the ring band (and the disc) whose center lies in the wedge, clockwise from 12 o'clock.
    // The band runs from the inner radius to the outer one; a 0.3 px margin keeps anti-aliased edge pixels out.
    private static IEnumerable<Point> RingBandPixels(int px, double fromDegrees, double toDegrees)
    {
        var ring = BatteryIconRenderer.RingGeometry.For(px);
        var c = px / 2.0;
        for (var y = 0; y < px; y++)
            for (var x = 0; x < px; x++)
            {
                var dx = x + 0.5 - c;
                var dy = c - (y + 0.5);
                var r = Math.Sqrt(dx * dx + dy * dy);
                var angle = (Math.Atan2(dx, dy) * 180 / Math.PI + 360) % 360;
                if (r >= ring.Inner + 0.3 && r <= ring.Outer - 0.7 && angle >= fromDegrees && angle <= toDegrees)
                    yield return new Point(x, y);
            }
    }

    // Pixels with a clear green tint whose center lies in the wedge, measured clockwise from 12 o'clock.
    private static int CountGreenish(Bitmap bitmap, double fromDegrees, double toDegrees)
    {
        var c = bitmap.Width / 2.0;
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var angle = (Math.Atan2(x + 0.5 - c, c - (y + 0.5)) * 180 / Math.PI + 360) % 360;
                var p = bitmap.GetPixel(x, y);
                if (angle >= fromDegrees && angle <= toDegrees && p.A > 0 && p.G - Math.Max(p.R, p.B) > 30)
                    count++;
            }
        return count;
    }

    private static bool Near(Color a, Color b) =>
        a.A == 255 && Math.Abs(a.R - b.R) <= 24 && Math.Abs(a.G - b.G) <= 24 && Math.Abs(a.B - b.B) <= 24;

    private static int CountNear(Bitmap bitmap, Color target)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (Near(bitmap.GetPixel(x, y), target))
                    count++;
        return count;
    }
}
