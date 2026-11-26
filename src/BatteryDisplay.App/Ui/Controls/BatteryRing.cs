using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BatteryDisplay.App.Presentation;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace BatteryDisplay.App.Ui.Controls;

/// <summary>
/// The battery ring of the tray icon at any size: <see cref="RingSegments.Count"/> segments clockwise from twelve
/// o'clock, the first <see cref="LitSegments"/> in <see cref="LitBrush"/> and nothing for the rest, with the device
/// glyph in the middle. The depleted part has no track so the level reads as line versus no line, not by hue.
/// Purely visual: the caller picks the brushes (from the theme) and the count.
/// </summary>
public sealed class BatteryRing : FrameworkElement
{
    private const double GapDegrees = 6;
    private const double InnerRatio = 0.72;
    private const double GlyphRatio = 0.42;

    public static readonly DependencyProperty LitSegmentsProperty = DependencyProperty.Register(
        nameof(LitSegments), typeof(int), typeof(BatteryRing),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LitBrushProperty = DependencyProperty.Register(
        nameof(LitBrush), typeof(Brush), typeof(BatteryRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(BatteryRing),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlyphBrushProperty = DependencyProperty.Register(
        nameof(GlyphBrush), typeof(Brush), typeof(BatteryRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlyphFontFamilyProperty = DependencyProperty.Register(
        nameof(GlyphFontFamily), typeof(FontFamily), typeof(BatteryRing),
        new FrameworkPropertyMetadata(new FontFamily("Segoe Fluent Icons"), FrameworkPropertyMetadataOptions.AffectsRender));

    public int LitSegments
    {
        get => (int)GetValue(LitSegmentsProperty);
        set => SetValue(LitSegmentsProperty, value);
    }

    public Brush? LitBrush
    {
        get => (Brush?)GetValue(LitBrushProperty);
        set => SetValue(LitBrushProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Brush? GlyphBrush
    {
        get => (Brush?)GetValue(GlyphBrushProperty);
        set => SetValue(GlyphBrushProperty, value);
    }

    public FontFamily GlyphFontFamily
    {
        get => (FontFamily)GetValue(GlyphFontFamilyProperty);
        set => SetValue(GlyphFontFamilyProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsNaN(Width) ? 0 : Width,
        double.IsNaN(Height) ? 0 : Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
            return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var outer = size / 2;
        var inner = outer * InnerRatio;
        var lit = Math.Clamp(LitSegments, 0, RingSegments.Count);
        var sweep = 360.0 / RingSegments.Count;

        for (var i = 0; LitBrush is not null && i < lit; i++)
        {
            var start = i * sweep + GapDegrees / 2;
            drawingContext.DrawGeometry(LitBrush, null, Sector(center, inner, outer, start, start + sweep - GapDegrees));
        }

        if (string.IsNullOrEmpty(Glyph) || GlyphBrush is null)
            return;
        var text = new FormattedText(
            Glyph,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(GlyphFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size * GlyphRatio,
            GlyphBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    /// <summary>An annular sector between two angles, in degrees clockwise from twelve o'clock.</summary>
    private static StreamGeometry Sector(Point center, double inner, double outer, double from, double to)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var large = to - from > 180;
            context.BeginFigure(At(center, outer, from), isFilled: true, isClosed: true);
            context.ArcTo(At(center, outer, to), new Size(outer, outer), 0, large, SweepDirection.Clockwise, true, false);
            context.LineTo(At(center, inner, to), true, false);
            context.ArcTo(At(center, inner, from), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point At(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Sin(radians), center.Y - radius * Math.Cos(radians));
    }
}
