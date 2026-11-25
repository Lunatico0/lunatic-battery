using System.Diagnostics;
using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;
using Timer = System.Windows.Forms.Timer;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Tray-only application. Devices are rediscovered and read on start, every minute, and shortly after Windows
/// reports a hardware change; the icons follow the device list and the icon mode in the settings.
/// UI seams: <see cref="Devices"/>, <see cref="Settings"/>, <see cref="RequestRefresh"/>, <see cref="TrayIconLeftClicked"/>
/// and <see cref="SettingsRequested"/>. Everything is raised and must be called on the UI thread.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    // Explorer may write the NotifyIconSettings entry a moment after the icon appears.
    private static readonly TimeSpan PromoteRetryDelay = TimeSpan.FromSeconds(5);
    // Plugging one device fires a burst of WM_DEVICECHANGE; wait for it to settle.
    private static readonly TimeSpan HotPlugSettleDelay = TimeSpan.FromSeconds(2);

    private readonly string _exePath = Environment.ProcessPath ?? Application.ExecutablePath;
    private readonly CancellationTokenSource _exit = new();
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly BatteryMonitor _monitor;
    private readonly TaskbarTheme _taskbarTheme;
    private readonly TrayIconCoordinator _icons;
    private readonly Timer _pollTimer;
    private readonly Timer _promoteRetryTimer;
    private readonly Debouncer _hotPlug;
    private readonly DeviceChangeWindow _deviceChanges;
    private bool _exiting;

    public TrayApplicationContext(IDeviceSource source, ISettingsStore settings)
    {
        Settings = settings;

        _startupItem = new ToolStripMenuItem("Iniciar con Windows", null, (_, _) => ToggleStartup());
        // Hidden until a settings window subscribes to SettingsRequested.
        _settingsItem = new ToolStripMenuItem("Configuración", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)) { Visible = false };
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Actualizar ahora", null, (_, _) => RefreshNow());
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, _) => Exit());
        _menu.Opening += (_, _) =>
        {
            _startupItem.Checked = IsStartupEnabled();
            _settingsItem.Visible = SettingsRequested is not null;
        };

        _pollTimer = new Timer { Interval = (int)PollInterval.TotalMilliseconds };
        _pollTimer.Tick += (_, _) => RefreshNow();
        _promoteRetryTimer = new Timer { Interval = (int)PromoteRetryDelay.TotalMilliseconds };
        _promoteRetryTimer.Tick += (_, _) =>
        {
            _promoteRetryTimer.Stop();
            NotifyIconPromoter.PromoteIconsOf(_exePath);
        };

        _monitor = new BatteryMonitor(source, new BatteryPoller(ReadTimeout), settings);
        // The menu is a control, so the WinForms context is installed by now; the fallback only guards that.
        _taskbarTheme = TaskbarTheme.FollowSystem(SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());
        _icons = new TrayIconCoordinator(_monitor, settings, _taskbarTheme,
            key => new NotifyIconTray(_menu, () => TrayIconLeftClicked?.Invoke(this, key)), PromoteNewIcons);

        _hotPlug = new Debouncer(HotPlugSettleDelay, RefreshNow, ScheduleOnUiThread);
        _deviceChanges = new DeviceChangeWindow(_hotPlug.Signal);

        _pollTimer.Start();
        RefreshNow();
    }

    /// <summary>The tracked devices, for the flyout. Changes arrive on the UI thread.</summary>
    public IDeviceSnapshots Devices => _monitor;

    /// <summary>The settings; saving through it re-plans the tray icons and republishes display names.</summary>
    public ISettingsStore Settings { get; }

    /// <summary>Left click on any tray icon; the argument is the icon key (a device Id or <see cref="TrayIconPlanner.CombinedKey"/>).</summary>
    public event EventHandler<string>? TrayIconLeftClicked;

    /// <summary>The "Configuración" menu item, shown only while something subscribes here.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Rediscovers and rereads now; ignored while a refresh is already running.</summary>
    public void RequestRefresh() => RefreshNow();

    private void PromoteNewIcons()
    {
        if (_exiting)
            return;
        NotifyIconPromoter.PromoteIconsOf(_exePath);
        _promoteRetryTimer.Stop();
        _promoteRetryTimer.Start();
    }

    private static IDisposable ScheduleOnUiThread(TimeSpan delay, Action callback)
    {
        var timer = new Timer { Interval = (int)delay.TotalMilliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            callback();
        };
        timer.Start();
        return timer;
    }

    private async void RefreshNow()
    {
        if (_exiting)
            return;
        try
        {
            // Icons follow through the coordinator, which promotes any icon shown for the first time.
            await _monitor.RefreshAsync(_exit.Token);
        }
        catch (OperationCanceledException) when (_exit.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Refresh failed: {ex}", "BatteryDisplay");
        }
    }

    private bool IsStartupEnabled()
    {
        try
        {
            return StartupRegistration.IsEnabled(_exePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read startup setting: {ex.Message}", "BatteryDisplay");
            return false;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            StartupRegistration.SetEnabled(!IsStartupEnabled(), _exePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not change startup setting: {ex.Message}", "BatteryDisplay");
        }
    }

    private void Exit()
    {
        Shutdown();
        ExitThread();
    }

    private void Shutdown()
    {
        if (_exiting)
            return;
        _exiting = true;
        _exit.Cancel();
        _pollTimer.Stop();
        _promoteRetryTimer.Stop();
        _deviceChanges.Dispose();
        _hotPlug.Dispose();
        _icons.Dispose();
        _taskbarTheme.Dispose();
        _monitor.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Shutdown();
            _pollTimer.Dispose();
            _promoteRetryTimer.Dispose();
            _menu.Dispose();
            _exit.Dispose();
        }
        base.Dispose(disposing);
    }
}
