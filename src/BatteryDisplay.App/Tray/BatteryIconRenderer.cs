using System.Drawing;
using System.Drawing.Drawing2D;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Draws a level ring (filled in eighths) around a device silhouette, all as vector paths at the requested size, so the
/// icon stays crisp at every DPI scale. On a dark taskbar it sits on a dark disc; on a light one there is no disc, the
/// colors come from <see cref="IconPalette.Light"/> and the glyph cuts are holes that show the taskbar. High contrast
/// draws the same way, with no disc, in the theme's text colors (<see cref="IconPalette.HighContrast"/>).
/// While charging, a lightning bolt takes the device silhouette's place.
/// </summary>
internal static class BatteryIconRenderer
{
    private const float SegmentSweep = 360f / RingSegments.Count;

    public static Bitmap Draw(TrayIconModel model, Size size)
    {
        var bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var s = MathF.Min(size.Width, size.Height);
        var center = new PointF(size.Width / 2f, size.Height / 2f);

        var palette = IconPalette.For(model.Theme, model.Ink);
        if (palette.Disc is { } discColor)
        {
            using var disc = new SolidBrush(discColor);
            g.FillEllipse(disc, center.X - s / 2f, center.Y - s / 2f, s, s);
        }

        var ring = RingGeometry.For(s);
        DrawRing(g, center, ring, model, palette);
        DrawSilhouette(g, center, ring.SilhouetteRadius, model, palette);
        return bitmap;
    }

    /// <summary>
    /// Radii in whole pixels so ring edges land on pixel boundaries (16 px: ring 8..7, glyph radius 5).
    /// The ring is a thin continuous band on the disc edge, leaving as much room as possible for the glyph.
    /// </summary>
    internal readonly record struct RingGeometry(float Outer, float Inner, float SilhouetteRadius)
    {
        public static RingGeometry For(float s)
        {
            var outer = MathF.Floor(s / 2f);
            var inner = outer - MathF.Max(1f, MathF.Round(s / 16f));
            var glyph = inner - MathF.Max(2f, MathF.Round(s / 8f));
            return new RingGeometry(outer, inner, glyph);
        }
    }

    // Only the remaining charge is drawn; the depleted part has no track, so the level reads as line versus no line
    // rather than as one hue versus another.
    private static void DrawRing(Graphics g, PointF center, RingGeometry ring, TrayIconModel model, IconPalette palette)
    {
        if (model.LitSegments == 0)
            return;

        // GDI+ angles start at 3 o'clock and run clockwise, so 12 o'clock is -90.
        using var lit = new SolidBrush(palette.For(model.Color));
        using var level = AnnularSector(center, ring.Outer, ring.Inner, -90f, model.LitSegments * SegmentSweep);
        g.FillPath(lit, level);
    }

    private static GraphicsPath AnnularSector(PointF c, float outer, float inner, float start, float sweep)
    {
        var path = new GraphicsPath();
        path.AddArc(c.X - outer, c.Y - outer, 2 * outer, 2 * outer, start, sweep);
        path.AddArc(c.X - inner, c.Y - inner, 2 * inner, 2 * inner, start + sweep, -sweep);
        path.CloseFigure();
        return path;
    }

    // While charging the glyph is a lightning bolt instead of the device: the cyan ring alone would tell charging apart by
    // hue only, which a colorblind user cannot read. The bolt is a shape, drawn in the glyph's color, so it reads the same
    // in every theme. A small corner badge was tried first, but at 16 px it had to cut into the ring and the glyph and
    // blurred into a blob; the full-size bolt stays sharp and leaves the ring untouched.
    private static void DrawSilhouette(Graphics g, PointF center, float radius, TrayIconModel model, IconPalette palette)
    {
        var (body, cuts) = model.Color == LevelColor.Charging ? Silhouettes.Bolt() : Silhouettes.For(model.Kind);
        using (body)
        using (cuts)
        using (var toPixels = new Matrix())
        {
            // Shapes are authored in a unit box (-1..1) and scaled to fit inside the ring.
            toPixels.Translate(center.X, center.Y);
            toPixels.Scale(radius, radius);
            body.Transform(toPixels);
            cuts.Transform(toPixels);

            using var fill = new SolidBrush(model.LevelKnown ? palette.Silhouette : palette.SilhouetteUnknown);
            if (palette.Disc is { } discColor)
            {
                g.FillPath(fill, body);
                using var cut = new SolidBrush(discColor);
                g.FillPath(cut, cuts);
            }
            else
            {
                // No disc to paint the cuts with: leave them out of the body so the taskbar shows through.
                g.SetClip(cuts, CombineMode.Exclude);
                g.FillPath(fill, body);
                g.ResetClip();
            }
        }
    }

    /// <summary>Device glyphs in unit coordinates: a filled body plus cut lines painted in the disc color (holes without a disc).</summary>
    private static class Silhouettes
    {
        // Windows 11 ships Segoe Fluent Icons; Segoe MDL2 Assets (Windows 10) has the same code points.
        private static readonly string[] IconFonts = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

        public static (GraphicsPath Body, GraphicsPath Cuts) For(DeviceKind kind) => kind switch
        {
            DeviceKind.Mouse => Glyph('') ?? Mouse(),
            DeviceKind.Headset => Glyph('') ?? Headset(),
            DeviceKind.Keyboard => Keyboard(),
            _ => Gamepad(),
        };

        // An icon font glyph as a path, its ink box fitted to the unit box. Null when no icon font is installed.
        private static (GraphicsPath, GraphicsPath)? Glyph(char codePoint)
        {
            foreach (var name in IconFonts)
            {
                FontFamily family;
                try { family = new FontFamily(name); }
                catch (ArgumentException) { continue; }

                using (family)
                {
                    var body = new GraphicsPath();
                    body.AddString(codePoint.ToString(), family, (int)FontStyle.Regular, 100f, PointF.Empty, StringFormat.GenericTypographic);
                    var bounds = body.GetBounds();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        body.Dispose();
                        continue;
                    }

                    // Center the ink box on the origin, then scale its longest side to 2 (the -1..1 box).
                    var scale = 2f / MathF.Max(bounds.Width, bounds.Height);
                    using var toUnit = new Matrix();
                    toUnit.Scale(scale, scale);
                    toUnit.Translate(-(bounds.X + bounds.Width / 2f), -(bounds.Y + bounds.Height / 2f));
                    body.Transform(toUnit);
                    return (body, new GraphicsPath());
                }
            }
            return null;
        }

        // Lightning bolt, 1.5 wide by 2 tall: a zigzag whose middle is a solid bar, with no hairline tips that would
        // vanish at 16 px.
        public static (GraphicsPath Body, GraphicsPath Cuts) Bolt()
        {
            var body = new GraphicsPath();
            body.AddPolygon(new PointF[]
            {
                new(0.18f, -1f), new(-0.675f, 0.16f), new(-0.09f, 0.16f), new(-0.3f, 1f),
                new(0.675f, -0.2f), new(0.09f, -0.2f), new(0.42f, -1f),
            });
            return (body, new GraphicsPath());
        }

        // Top-down mouse: a capsule body with the left/right button split.
        private static (GraphicsPath, GraphicsPath) Mouse()
        {
            var body = new GraphicsPath();
            AddRoundedRect(body, new RectangleF(-0.62f, -0.95f, 1.24f, 1.9f), 0.62f);
            // 0.2-unit cuts are exactly one pixel wide at 16 px (glyph radius 5).
            var cuts = new GraphicsPath();
            cuts.AddRectangle(new RectangleF(-0.1f, -1f, 0.2f, 0.8f));
            cuts.AddRectangle(new RectangleF(-0.7f, -0.2f, 1.4f, 0.2f));
            return (body, cuts);
        }

        // Thin headband arc over two chunky ear cups that stick out past it on both sides.
        private static (GraphicsPath, GraphicsPath) Headset()
        {
            var body = new GraphicsPath();
            body.AddArc(-0.78f, -0.72f, 1.56f, 1.56f, 180, 180);
            body.AddArc(-0.53f, -0.47f, 1.06f, 1.06f, 0, -180);
            body.CloseFigure();
            AddRoundedRect(body, new RectangleF(-1.02f, 0.02f, 0.6f, 0.8f), 0.22f);
            AddRoundedRect(body, new RectangleF(0.42f, 0.02f, 0.6f, 0.8f), 0.22f);
            body.FillMode = FillMode.Winding;
            return (body, new GraphicsPath());
        }

        // Generic keyboard: a wide slab with a space bar cut.
        private static (GraphicsPath, GraphicsPath) Keyboard()
        {
            var body = new GraphicsPath();
            AddRoundedRect(body, new RectangleF(-0.92f, -0.5f, 1.84f, 1.0f), 0.18f);
            var cuts = new GraphicsPath();
            cuts.AddRectangle(new RectangleF(-0.45f, 0.12f, 0.9f, 0.18f));
            return (body, cuts);
        }

        // Generic gamepad: a bar with two grips.
        private static (GraphicsPath, GraphicsPath) Gamepad()
        {
            var body = new GraphicsPath();
            AddRoundedRect(body, new RectangleF(-0.9f, -0.45f, 1.8f, 0.7f), 0.3f);
            body.AddEllipse(-0.9f, -0.2f, 0.6f, 0.75f);
            body.AddEllipse(0.3f, -0.2f, 0.6f, 0.75f);
            body.FillMode = FillMode.Winding;
            return (body, new GraphicsPath());
        }

        private static void AddRoundedRect(GraphicsPath path, RectangleF r, float radius)
        {
            var d = MathF.Min(radius * 2, MathF.Min(r.Width, r.Height));
            path.StartFigure();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
        }
    }
}
