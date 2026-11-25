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

    // At 24 px the glyph's ink box is 14 px tall (rows 5..18) and the ring starts at radius 10, so rows 3 and 20 near the
    // middle column are always clear of both; the box corners can reach past radius 7, so the sides are not probed.
    private static IEnumerable<Color> GapPixels(Bitmap bitmap)
    {
        foreach (var y in new[] { 3, 20 })
            for (var x = 10; x <= 13; x++)
                yield return bitmap.GetPixel(x, y);
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
