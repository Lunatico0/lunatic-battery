using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Settings;

namespace BatteryDisplay.App.Presentation;

/// <summary>
/// Keeps the tray icons in line with the device list and the settings: re-plans on every published device list
/// and every settings change (mode, pinning, names), so a mode change applies without restarting or rereading.
/// A taskbar theme or high contrast ink switch redraws the same icons in the matching variant, without rediscovering devices.
/// Plans once right away, so <c>onIconFirstShown</c> also covers icons shown before the first refresh.
/// The app is never invisible: until the first device list arrives it shows the "searching" icon, and afterwards a plan
/// with no visible icon gets the "no devices" one (<see cref="TrayIconPlanner.EnsureOneVisible"/>).
/// Runs on the thread that raises the events (the UI thread in the app).
/// </summary>
public sealed class TrayIconCoordinator : IDisposable
{
    private readonly IDeviceSnapshots _devices;
    private readonly ISettingsStore _settings;
    private readonly ITaskbarTheme _taskbar;
    private readonly Action _onIconFirstShown;
    private readonly TrayIconSet _icons;
    // True until the monitor publishes its first list; a failed first cycle leaves it set, so the icon stays.
    private bool _searching;
    private bool _disposed;

    public TrayIconCoordinator(IDeviceSnapshots devices, ISettingsStore settings, ITaskbarTheme taskbar, Func<string, ITrayIcon> createIcon, Action onIconFirstShown)
    {
        _devices = devices;
        _settings = settings;
        _taskbar = taskbar;
        _onIconFirstShown = onIconFirstShown;
        _icons = new TrayIconSet(createIcon);
        _searching = devices.Devices.Count == 0;
        _devices.DevicesChanged += OnDevicesChanged;
        _settings.Changed += OnSettingsChanged;
        _taskbar.Changed += OnTaskbarChanged;
        Replan();
    }

    private void OnDevicesChanged(object? sender, IReadOnlyList<DeviceSnapshot> devices)
    {
        _searching = false;
        Replan();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => Replan();

    private void OnTaskbarChanged(object? sender, EventArgs e) => Replan();

    private void Replan()
    {
        if (_disposed)
            return;
        var settings = _settings.Current;
        var (theme, ink) = (_taskbar.Theme, _taskbar.Ink);
        var planned = _searching
            ? TrayIconPlanner.Searching()
            : TrayIconPlanner.EnsureOneVisible(TrayIconPlanner.Plan(settings.TrayIconMode, _devices.Devices, settings.Devices));
        var slots = planned
            .Select(slot => slot with { Model = slot.Model with { Theme = theme, Ink = ink } })
            .ToArray();
        if (_icons.Apply(slots))
            _onIconFirstShown();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _devices.DevicesChanged -= OnDevicesChanged;
        _settings.Changed -= OnSettingsChanged;
        _taskbar.Changed -= OnTaskbarChanged;
        _icons.Dispose();
    }
}
