using BatteryDisplay.App.Settings;

namespace BatteryDisplay.App.Ui.Themes;

/// <summary>The Lunatic tone dictionary in use.</summary>
public enum ThemeTones { Dark, Light, HighContrast, GlassDark, GlassLight }

/// <summary>What a recheck found: the tone dictionary must be swapped, and the high contrast setting flipped.</summary>
internal readonly record struct ToneChange(bool Tones, bool HighContrast);

/// <summary>
/// Picks the tones from the system and the user's appearance: a Windows high contrast theme wins over both, since its
/// colors are the user's; otherwise the appearance (Fluent or Glass) follows the light or dark app mode. Recheck
/// reads all three again and says what changed; it never touches WPF.
/// </summary>
internal sealed class ToneSelector(Func<bool> highContrast, Func<bool> appsLight, Func<UiAppearance> appearance)
{
    public ThemeTones Current { get; private set; } = Pick(highContrast(), appsLight(), appearance());

    public static ThemeTones Pick(bool highContrast, bool appsLight, UiAppearance appearance) =>
        highContrast ? ThemeTones.HighContrast
        : appearance == UiAppearance.Glass ? (appsLight ? ThemeTones.GlassLight : ThemeTones.GlassDark)
        : appsLight ? ThemeTones.Light : ThemeTones.Dark;

    public ToneChange Recheck()
    {
        var before = Current;
        Current = Pick(highContrast(), appsLight(), appearance());
        return new ToneChange(Current != before, (Current == ThemeTones.HighContrast) != (before == ThemeTones.HighContrast));
    }
}
