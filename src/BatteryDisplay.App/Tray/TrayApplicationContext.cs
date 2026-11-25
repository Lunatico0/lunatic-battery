using System.Diagnostics;
using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Polling;
using BatteryDisplay.Core;
using Timer = System.Windows.Forms.Timer;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Tray-only application: one icon per discovered device. Devices are rediscovered and read on start,
/// every minute, and shortly after Windows reports a hardware change.
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
    private readonly BatteryMonitor _monitor;
    private readonly Timer _pollTimer;
    private readonly Timer _promoteRetryTimer;
    private readonly Debouncer _hotPlug;
    private readonly DeviceChangeWindow _deviceChanges;
    private bool _exiting;

    public TrayApplicationContext(IDeviceSource source)
    {
        _startupItem = new ToolStripMenuItem("Iniciar con Windows", null, (_, _) => ToggleStartup());
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Actualizar ahora", null, (_, _) => RefreshNow());
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, _) => Exit());
        _menu.Opening += (_, _) => _startupItem.Checked = IsStartupEnabled();

        _monitor = new BatteryMonitor(source, _ => new NotifyIconTray(_menu), new BatteryPoller(ReadTimeout));

        _pollTimer = new Timer { Interval = (int)PollInterval.TotalMilliseconds };
        _pollTimer.Tick += (_, _) => RefreshNow();
        _promoteRetryTimer = new Timer { Interval = (int)PromoteRetryDelay.TotalMilliseconds };
        _promoteRetryTimer.Tick += (_, _) =>
        {
            _promoteRetryTimer.Stop();
            NotifyIconPromoter.PromoteIconsOf(_exePath);
        };
        _hotPlug = new Debouncer(HotPlugSettleDelay, RefreshNow, ScheduleOnUiThread);
        _deviceChanges = new DeviceChangeWindow(_hotPlug.Signal);

        _pollTimer.Start();
        RefreshNow();
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
            var newlyShown = await _monitor.RefreshAsync(_exit.Token);
            if (newlyShown && !_exiting)
            {
                NotifyIconPromoter.PromoteIconsOf(_exePath);
                _promoteRetryTimer.Stop();
                _promoteRetryTimer.Start();
            }
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
