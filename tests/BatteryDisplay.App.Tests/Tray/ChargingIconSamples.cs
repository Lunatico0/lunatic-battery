using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Tray;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Tray;

/// <summary>
/// Not a check: exports sample icons (16, 24 and 32 px; Dark, Light and high contrast; 20% and 80%, charging or not) plus
/// an 8x contact sheet on each taskbar color, to judge the charging bolt by eye. Remove the Skip and set
/// BATTERY_ICON_SAMPLES_DIR to the output folder to run it; it writes nothing when the variable is unset.
/// </summary>
public class ChargingIconSamples
{
    private const int Zoom = 8;

    [Fact(Skip = "Manual export of sample icons, see the class summary.")]
    public void Export()
    {
        var dir = Environment.GetEnvironmentVariable("BATTERY_ICON_SAMPLES_DIR");
        if (string.IsNullOrEmpty(dir))
            return;
        Directory.CreateDirectory(dir);

        int[] sizes = [16, 24, 32];
        var themes = new (string Name, TrayIconTheme Theme, Color Taskbar)[]
        {
            ("dark", TrayIconTheme.Dark, Color.FromArgb(0x20, 0x20, 0x20)),
            ("light", TrayIconTheme.Light, Color.FromArgb(0xEE, 0xEE, 0xEE)),
            ("hc", TrayIconTheme.HighContrast, Color.Black),
        };
        var cases = new List<(string Name, int Percent, bool Charging, DeviceKind Kind)>();
        foreach (var kind in new[] { DeviceKind.Mouse, DeviceKind.Headset })
            foreach (var percent in new[] { 20, 80 })
                foreach (var charging in new[] { false, true })
                    cases.Add(($"{kind}-{percent}-{(charging ? "charging" : "idle")}".ToLowerInvariant(), percent, charging, kind));

        var cell = sizes.Max() * Zoom + Zoom * 2;
        using var sheet = new Bitmap(cell * cases.Count, cell * sizes.Length * themes.Length, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(sheet);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        for (var t = 0; t < themes.Length; t++)
            for (var si = 0; si < sizes.Length; si++)
            {
                var row = t * sizes.Length + si;
                using var taskbar = new SolidBrush(themes[t].Taskbar);
                g.FillRectangle(taskbar, 0, row * cell, sheet.Width, cell);
                for (var ci = 0; ci < cases.Count; ci++)
                {
                    var c = cases[ci];
                    var model = new TrayIconModel(true, c.Kind, RingSegments.Lit(c.Percent),
                        c.Charging ? LevelColor.Charging : LevelColors.For(new BatteryStatus(c.Percent, ChargeState.Discharging), 20),
                        true, "x", themes[t].Theme, new TrayIconInk(Color.White.ToArgb(), Color.FromArgb(0x3F, 0xF2, 0x3F).ToArgb()));
                    var px = sizes[si];
                    using var icon = BatteryIconRenderer.Draw(model, new Size(px, px));
                    icon.Save(Path.Combine(dir, $"{themes[t].Name}-{px}-{c.Name}.png"), ImageFormat.Png);
                    g.DrawImage(icon, new Rectangle(ci * cell + Zoom, row * cell + Zoom, px * Zoom, px * Zoom));
                }
            }
        sheet.Save(Path.Combine(dir, "sheet.png"), ImageFormat.Png);

        // The size that matters most, larger: 16 px icons at 16x, one row per theme.
        const int zoom16 = 16;
        var cell16 = 16 * zoom16 + zoom16 * 2;
        using var sheet16 = new Bitmap(cell16 * cases.Count, cell16 * themes.Length, PixelFormat.Format32bppArgb);
        using var g16 = Graphics.FromImage(sheet16);
        g16.InterpolationMode = InterpolationMode.NearestNeighbor;
        g16.PixelOffsetMode = PixelOffsetMode.Half;
        for (var t = 0; t < themes.Length; t++)
        {
            using var taskbar = new SolidBrush(themes[t].Taskbar);
            g16.FillRectangle(taskbar, 0, t * cell16, sheet16.Width, cell16);
            for (var ci = 0; ci < cases.Count; ci++)
            {
                using var icon = (Bitmap)Image.FromFile(Path.Combine(dir, $"{themes[t].Name}-16-{cases[ci].Name}.png"));
                g16.DrawImage(icon, new Rectangle(ci * cell16 + zoom16, t * cell16 + zoom16, 16 * zoom16, 16 * zoom16));
            }
        }
        sheet16.Save(Path.Combine(dir, "sheet-16px.png"), ImageFormat.Png);
    }
}
