using System.Diagnostics;
using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Notifications;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Updates;
using BatteryDisplay.Core;
using Timer = System.Windows.Forms.Timer;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Tray-only application. Devices are rediscovered and read on start, every minute, and shortly after Windows
/// reports a hardware change; the icons follow the device list and the icon mode in the settings.
/// Updates are checked in the background (<see cref="UpdateService"/>); "Salir" applies one already downloaded.
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
    private readonly ToolStripMenuItem _updateItem;
    private readonly UpdateService _updates;
    private readonly BatteryMonitor _monitor;
    private readonly TaskbarTheme _taskbarTheme;
    private readonly TrayIconCoordinator _icons;
    // Every NotifyIcon the coordinator created, in tray order; disposed ones are dropped when the list is read.
    private readonly List<NotifyIconTray> _trayIcons = [];
    private readonly LowBatteryWatch _lowBattery;
    private readonly Timer _pollTimer;
    private readonly Timer _promoteRetryTimer;
    private readonly Debouncer _hotPlug;
    private readonly DeviceChangeWindow _deviceChanges;
    private bool _exiting;

    public TrayApplicationContext(IDeviceSource source, ISettingsStore settings, IUpdateClient updates)
    {
        Settings = settings;
        _updates = new UpdateService(updates, ScheduleOnUiThread);

        _startupItem = new ToolStripMenuItem("Iniciar con Windows", null, (_, _) => ToggleStartup());
        // Hidden until a settings window subscribes to SettingsRequested.
        _settingsItem = new ToolStripMenuItem("Configuración", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)) { Visible = false };
        // Hidden outside a Velopack install (a build folder) or while no repository is configured.
        _updateItem = new ToolStripMenuItem(UpdateMessages.MenuText(null), null, (_, _) => OnUpdateItemClicked()) { Visible = false };
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Actualizar ahora", null, (_, _) => RefreshNow());
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(_updateItem);
        _menu.Items.Add("Abrir registro", null, (_, _) => OpenLog());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, _) => Exit());
        _menu.Opening += (_, _) =>
        {
            _startupItem.Checked = IsStartupEnabled();
            _settingsItem.Visible = SettingsRequested is not null;
            _updateItem.Visible = _updates.CanUpdate;
            _updateItem.Text = UpdateMessages.MenuText(_updates.PendingVersion);
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
            CreateTrayIcon, PromoteNewIcons);
        // Subscribed after the coordinator, so a notice finds the icons of the same device list already applied.
        _lowBattery = new LowBatteryWatch(_monitor, settings, new TrayIconNotifier(LiveTrayIcons));

        _hotPlug = new Debouncer(HotPlugSettleDelay, RefreshNow, ScheduleOnUiThread);
        _deviceChanges = new DeviceChangeWindow(_hotPlug.Signal);

        _pollTimer.Start();
        RefreshNow();
        _updates.Start();
    }

    /// <summary>The tracked devices, for the flyout. Changes arrive on the UI thread.</summary>
    public IDeviceSnapshots Devices => _monitor;

    /// <summary>The settings; saving through it re-plans the tray icons and republishes display names.</summary>
    public ISettingsStore Settings { get; }

    /// <summary>Left click on any tray icon; the argument is the icon key (a device Id or <see cref="TrayIconPlanner.CombinedKey"/>).</summary>
    public event EventHandler<string>? TrayIconLeftClicked;

    /// <summary>The "Configuración" menu item, shown only while something subscribes here.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Rediscovers and rereads now; while a refresh is already running, it runs once more right after it.</summary>
    public void RequestRefresh() => RefreshNow();

    private NotifyIconTray CreateTrayIcon(string key)
    {
        var icon = new NotifyIconTray(key, _menu, () => TrayIconLeftClicked?.Invoke(this, key));
        _trayIcons.Add(icon);
        return icon;
    }

    private IReadOnlyList<INoticeIcon> LiveTrayIcons()
    {
        _trayIcons.RemoveAll(icon => icon.IsDisposed);
        return _trayIcons.ToArray();
    }

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
            AppLog.Write("tray", "Refresh failed", ex);
        }
    }

    // Explorer with the log file selected; just its folder while nothing has been written yet.
    private static void OpenLog()
    {
        var log = AppLog.Installed ?? new AppLog(AppLog.DefaultDirectory);
        try
        {
            Directory.CreateDirectory(log.Folder);
            var arguments = File.Exists(log.FilePath) ? $"/select,\"{log.FilePath}\"" : $"\"{log.Folder}\"";
            Process.Start("explorer.exe", arguments)?.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Write("tray", "Could not open the log", ex);
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
            AppLog.Write("tray", "Could not read startup setting", ex);
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
            AppLog.Write("tray", "Could not change startup setting", ex);
        }
    }

    private void OnUpdateItemClicked()
    {
        if (_updates.PendingVersion is null)
            CheckForUpdatesNow();
        else
            RestartToUpdate();
    }

    /// <summary>The user asked, so the answer goes out as a notice, whatever it is.</summary>
    private async void CheckForUpdatesNow()
    {
        var result = await _updates.CheckNowAsync(_exit.Token);
        if (_exiting || UpdateMessages.For(result) is not { } text)
            return;
        LiveTrayIcons().FirstOrDefault(icon => icon.IsShown)?.ShowNotice(UpdateMessages.Title, text, NoticeKind.Info);
    }

    /// <summary>Velopack ends the process to apply the update and starts the app again; the icons go first.</summary>
    private void RestartToUpdate()
    {
        Shutdown();
        _updates.RestartToUpdate();
        // Only reached when the updater could not start; the update then applies on the next start.
        ExitThread();
    }

    private void Exit()
    {
        Shutdown();
        // Applied by the updater once this process is gone, without starting the app again.
        _updates.ApplyOnExit();
        ExitThread();
    }

    private void Shutdown()
    {
        if (_exiting)
            return;
        _exiting = true;
        _exit.Cancel();
        _updates.Dispose();
        _pollTimer.Stop();
        _promoteRetryTimer.Stop();
        _deviceChanges.Dispose();
        _hotPlug.Dispose();
        _lowBattery.Dispose();
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
