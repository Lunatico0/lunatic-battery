namespace BatteryDisplay.App.Presentation;

/// <summary>The tray icon shows the level as a ring of <see cref="Count"/> segments, clockwise from 12 o'clock.</summary>
public static class RingSegments
{
    public const int Count = 8;

    /// <summary>Segments lit for <paramref name="percent"/>: each one covers 12.5%, rounded up so any charge lights at least one.</summary>
    public static int Lit(int? percent) => percent is { } p ? Math.Clamp((p * Count + 99) / 100, 0, Count) : 0;
}
