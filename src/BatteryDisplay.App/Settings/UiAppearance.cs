namespace BatteryDisplay.App.Settings;

/// <summary>
/// How the flyout and the settings window look. A Windows high contrast theme wins over either: the windows then use
/// the system colors.
/// </summary>
public enum UiAppearance
{
    /// <summary>The WPF Fluent theme, following the Windows light or dark app mode.</summary>
    Fluent,

    /// <summary>Lunatic Glass: translucent panels over a teal glow, also in light and dark.</summary>
    Glass,
}
