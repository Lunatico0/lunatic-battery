using BatteryDisplay.App.Presentation;
using Microsoft.Win32;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Follows the taskbar's light or dark setting. That is SystemUsesLightTheme, not AppsUseLightTheme: Windows lets the
/// taskbar stay dark while apps go light, and the tray icon sits on the taskbar. A missing value reads as dark.
/// SystemEvents raises on its own thread, so every recheck is posted to the UI context and Changed is raised there,
/// only when the theme actually switched.
/// </summary>
internal sealed class TaskbarTheme : ITaskbarTheme, IDisposable
{
    internal const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    internal const string ValueName = "SystemUsesLightTheme";

    private readonly Func<bool> _isLight;
    private readonly SynchronizationContext _ui;
    private Action? _unsubscribe;
    private bool _disposed;

    public TaskbarTheme(Func<bool> isLight, SynchronizationContext ui)
    {
        _isLight = isLight;
        _ui = ui;
        Theme = Read();
    }

    public TrayIconTheme Theme { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Follows the real taskbar setting, rechecking on every user preference change Windows reports.</summary>
    public static TaskbarTheme FollowSystem(SynchronizationContext ui)
    {
        var theme = new TaskbarTheme(ReadRegistry, ui);
        UserPreferenceChangedEventHandler handler = (_, e) => theme.PreferenceChanged(e.Category);
        SystemEvents.UserPreferenceChanged += handler;
        theme._unsubscribe = () => SystemEvents.UserPreferenceChanged -= handler;
        return theme;
    }

    /// <summary>
    /// Any category rechecks: the taskbar switch has been reported as General and as other categories across Windows
    /// builds, and the recheck is one registry read that raises nothing when the theme did not change.
    /// </summary>
    internal void PreferenceChanged(UserPreferenceCategory category) => _ui.Post(_ => Recheck(), null);

    internal static bool IsLightValue(object? value) => value is int v && v != 0;

    private void Recheck()
    {
        if (_disposed)
            return;
        var theme = Read();
        if (theme == Theme)
            return;
        Theme = theme;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private TrayIconTheme Read() => _isLight() ? TrayIconTheme.Light : TrayIconTheme.Dark;

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
