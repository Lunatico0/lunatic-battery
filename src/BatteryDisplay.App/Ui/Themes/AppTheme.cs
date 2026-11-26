using System.ComponentModel;
using System.Windows;
using BatteryDisplay.App.Settings;
using Microsoft.Win32;

namespace BatteryDisplay.App.Ui.Themes;

/// <summary>
/// Turns on the WPF Fluent theme, following the system light or dark setting, and keeps the Lunatic tone dictionary
/// in step with it and with the user's appearance (Fluent or Lunatic Glass, a pair of dictionaries each). Swapping a
/// tone dictionary never touches a view, since views read every theme value through DynamicResource; a new
/// appearance applies live, with the windows open. A Windows high contrast theme wins over everything: the tones
/// become system colors (Lunatic.HighContrast.xaml) and Fluent loads its own high contrast dictionary.
/// </summary>
internal static class AppTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly Uri DarkTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Dark.xaml", UriKind.Relative);
    private static readonly Uri LightTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Light.xaml", UriKind.Relative);
    private static readonly Uri HighContrastTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.HighContrast.xaml", UriKind.Relative);
    private static readonly Uri GlassDarkTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Glass.Dark.xaml", UriKind.Relative);
    private static readonly Uri GlassLightTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Glass.Light.xaml", UriKind.Relative);
    private static readonly Uri Styles = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Styles.xaml", UriKind.Relative);

    private static ToneSelector? _selector;
    private static ResourceDictionary? _tones;
    private static ISettingsStore? _settings;

    public static void Install(System.Windows.Application app, ISettingsStore settings)
    {
        // ThemeMode is still flagged experimental (WPF0001) in .NET 10; it is the supported way to get the
        // built-in Fluent theme, and System follows the Windows light or dark app mode on its own.
#pragma warning disable WPF0001
        app.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
        _settings = settings;
        _selector = new ToneSelector(() => SystemParameters.HighContrast, IsLight, () => settings.Current.Appearance);
        _tones = new ResourceDictionary { Source = SourceOf(_selector.Current) };
        app.Resources.MergedDictionaries.Add(_tones);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = Styles });
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        settings.Changed += OnSettingsChanged;
    }

    public static void Uninstall()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        if (_settings is not null)
            _settings.Changed -= OnSettingsChanged;
        _settings = null;
    }

    /// <summary>Any saved change rechecks: only a new appearance swaps anything, and a recheck that finds nothing new does nothing.</summary>
    private static void OnSettingsChanged(object? sender, AppSettings e) => PostApply();

    /// <summary>
    /// Any category rechecks, like TaskbarTheme: the app mode and high contrast switches arrive under different
    /// categories, and a recheck that finds nothing new does nothing.
    /// </summary>
    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => PostApply();

    /// <summary>
    /// WPF refreshes SystemParameters.HighContrast on its own WM_SETTINGCHANGE, which SystemEvents may beat; this is
    /// the signal that the value read in the recheck is current.
    /// </summary>
    private static void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            PostApply();
    }

    // SystemEvents may raise on its own thread; the dictionaries belong to the UI thread. Posting also lets WPF finish
    // its own theme handling for the same message first.
    private static void PostApply() => System.Windows.Application.Current?.Dispatcher.BeginInvoke(Apply);

    private static void Apply()
    {
        if (_selector is null || _tones is null || System.Windows.Application.Current is not { } app)
            return;
        var change = _selector.Recheck();
        if (change.HighContrast)
        {
            // WPF reloads its Fluent dictionary only when light or dark changes, not when high contrast does.
            // Setting ThemeMode again (even to the same value) reloads it, and picks Fluent.HC.xaml while it is on.
#pragma warning disable WPF0001
            app.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
        }
        if (!change.Tones)
            return;
        var index = app.Resources.MergedDictionaries.IndexOf(_tones);
        var tones = new ResourceDictionary { Source = SourceOf(_selector.Current) };
        if (index >= 0)
            app.Resources.MergedDictionaries[index] = tones;
        else
            app.Resources.MergedDictionaries.Add(tones);
        _tones = tones;
    }

    internal static Uri SourceOf(ThemeTones tones) => tones switch
    {
        ThemeTones.HighContrast => HighContrastTones,
        ThemeTones.Light => LightTones,
        ThemeTones.GlassDark => GlassDarkTones,
        ThemeTones.GlassLight => GlassLightTones,
        _ => DarkTones,
    };

    /// <summary>The Windows "app mode" the Fluent theme follows. Missing means light, like Windows.</summary>
    private static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }
}
