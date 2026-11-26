using BatteryDisplay.App.Presentation;
using Microsoft.Win32;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Follows the taskbar's light or dark setting. That is SystemUsesLightTheme, not AppsUseLightTheme: Windows lets the
/// taskbar stay dark while apps go light, and the tray icon sits on the taskbar. A missing value reads as dark.
/// A Windows high contrast theme wins over both, like in the flyout: SystemUsesLightTheme can stay 1 under a dark high
/// contrast theme, so the icon takes the theme's text colors (<see cref="Ink"/>) instead.
/// SystemEvents raises on its own thread, so every recheck is posted to the UI context and Changed is raised there,
/// only when the theme or the ink actually switched.
/// </summary>
internal sealed class TaskbarTheme : ITaskbarTheme, IDisposable
{
    internal const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    internal const string ValueName = "SystemUsesLightTheme";

    private readonly Func<bool> _isLight;
    private readonly Func<bool> _isHighContrast;
    private readonly Func<TrayIconInk> _highContrastInk;
    private readonly SynchronizationContext _ui;
    private Action? _unsubscribe;
    private bool _disposed;

    public TaskbarTheme(Func<bool> isLight, Func<bool> isHighContrast, Func<TrayIconInk> highContrastInk, SynchronizationContext ui)
    {
        _isLight = isLight;
        _isHighContrast = isHighContrast;
        _highContrastInk = highContrastInk;
        _ui = ui;
        (Theme, Ink) = Read();
    }

    public TrayIconTheme Theme { get; private set; }

    public TrayIconInk Ink { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Follows the real taskbar setting, rechecking on every user preference change Windows reports.</summary>
    public static TaskbarTheme FollowSystem(SynchronizationContext ui)
    {
        var theme = new TaskbarTheme(ReadRegistry, NativeMethods.IsHighContrastOn, ReadSystemInk, ui);
        UserPreferenceChangedEventHandler handler = (_, e) => theme.PreferenceChanged(e.Category);
        SystemEvents.UserPreferenceChanged += handler;
        theme._unsubscribe = () => SystemEvents.UserPreferenceChanged -= handler;
        return theme;
    }

    /// <summary>
    /// Any category rechecks: the taskbar switch has been reported as General and as other categories across Windows
    /// builds, high contrast arrives as Accessibility and a switch between high contrast themes as Color. The recheck is
    /// a few cheap reads that raise nothing when neither the theme nor the ink changed.
    /// </summary>
    internal void PreferenceChanged(UserPreferenceCategory category) => _ui.Post(_ => Recheck(), null);

    internal static bool IsLightValue(object? value) => value is int v && v != 0;

    private void Recheck()
    {
        if (_disposed)
            return;
        var read = Read();
        if (read == (Theme, Ink))
            return;
        (Theme, Ink) = read;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // The ink is read only in high contrast, so a system color change outside it raises nothing.
    private (TrayIconTheme, TrayIconInk) Read() => _isHighContrast()
        ? (TrayIconTheme.HighContrast, _highContrastInk())
        : (_isLight() ? TrayIconTheme.Light : TrayIconTheme.Dark, default);

    private static TrayIconInk ReadSystemInk() =>
        new(NativeMethods.SysColorArgb(NativeMethods.ColorWindowText), NativeMethods.SysColorArgb(NativeMethods.ColorGrayText));

    private static bool ReadRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return IsLightValue(key?.GetValue(ValueName));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _unsubscribe?.Invoke();
        _unsubscribe = null;
    }
}
