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

        Assert.True(CountNear(known, IconPalette.Silhouette) > 0);
        Assert.Equal(0, CountNear(unknown, IconPalette.Silhouette));
        Assert.True(CountNear(unknown, IconPalette.SilhouetteUnknown) > 0);
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
