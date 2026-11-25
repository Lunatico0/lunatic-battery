using System.Windows;
using Microsoft.Win32;

namespace BatteryDisplay.App.Ui.Themes;

/// <summary>
/// Turns on the WPF Fluent theme, following the system light or dark setting, and keeps the Lunatic tone dictionary
/// in step with it. The tones are the only app colors; swapping their dictionary (and later adding Lunatic Glass)
/// never touches a view, since views use DynamicResource only.
/// </summary>
internal static class AppTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly Uri DarkTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Dark.xaml", UriKind.Relative);
    private static readonly Uri LightTones = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Light.xaml", UriKind.Relative);
    private static readonly Uri Styles = new("/BatteryDisplay.App;component/Ui/Themes/Lunatic.Styles.xaml", UriKind.Relative);

    private static ResourceDictionary? _tones;

    public static void Install(System.Windows.Application app)
    {
        // ThemeMode is still flagged experimental (WPF0001) in .NET 10; it is the supported way to get the
        // built-in Fluent theme, and System follows the Windows light or dark app mode on its own.
#pragma warning disable WPF0001
        app.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
        _tones = new ResourceDictionary { Source = IsLight() ? LightTones : DarkTones };
        app.Resources.MergedDictionaries.Add(_tones);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = Styles });
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Uninstall() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;
        // SystemEvents may raise on its own thread; the dictionaries belong to the UI thread.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(ApplyTones);
    }

    private static void ApplyTones()
    {
        var source = IsLight() ? LightTones : DarkTones;
        if (_tones is null || _tones.Source == source || System.Windows.Application.Current is not { } app)
            return;
        var index = app.Resources.MergedDictionaries.IndexOf(_tones);
        var tones = new ResourceDictionary { Source = source };
        if (index >= 0)
            app.Resources.MergedDictionaries[index] = tones;
        else
            app.Resources.MergedDictionaries.Add(tones);
        _tones = tones;
    }

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
