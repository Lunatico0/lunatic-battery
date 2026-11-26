using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Notifications;

/// <summary>One low battery notice: "Batería baja", "Razer Cobra: 12%".</summary>
public sealed record LowBatteryAlert(string DeviceId, string DisplayName, int Percent)
{
    public const string Title = "Batería baja";

    public string Text => $"{DisplayName}: {Percent}%";
}

/// <summary>Shows a notice. False when it could not be shown (no tray icon is visible), so the device stays armed.</summary>
public interface ILowBatteryNotifier
{
    bool Notify(LowBatteryAlert alert);
}

/// <summary>
/// One low battery notice per discharge. Every device starts armed; a reading at or below its own low threshold
/// (shown, known level, not charging) notifies once and disarms it, the first reading after start included.
/// Charging, or climbing to threshold + <see cref="RearmMargin"/>, arms it again, so a level wavering around the
/// threshold does not repeat the notice. The state is kept per device Id for the session, also while the device is gone.
/// Runs on every published device list, never on a settings change: switching the notices on waits for the next reading.
/// Runs on the thread that raises the events (the UI thread in the app).
/// </summary>
public sealed class LowBatteryWatch : IDisposable
{
    public const int RearmMargin = 5;

    private readonly IDeviceSnapshots _devices;
    private readonly ISettingsStore _settings;
    private readonly ILowBatteryNotifier _notifier;
    private readonly HashSet<string> _disarmed = new(StringComparer.Ordinal);
    private bool _disposed;

    public LowBatteryWatch(IDeviceSnapshots devices, ISettingsStore settings, ILowBatteryNotifier notifier)
    {
        _devices = devices;
        _settings = settings;
        _notifier = notifier;
        _devices.DevicesChanged += OnDevicesChanged;
    }

    private void OnDevicesChanged(object? sender, IReadOnlyList<DeviceSnapshot> devices)
    {
        if (_disposed)
            return;
        var settings = _settings.Current;
        foreach (var device in devices)
        {
            if (device.Status is not { Percent: { } percent } status)
                continue;
            var options = settings.For(device.Id);

            // Rearming does not depend on the switch, so a device that charged while it was off is ready again.
            if (status.Charge == ChargeState.Charging || percent >= options.LowBatteryThreshold + RearmMargin)
            {
                _disarmed.Remove(device.Id);
                continue;
            }

            if (!settings.NotifyLowBattery || options.Hidden || percent > options.LowBatteryThreshold || _disarmed.Contains(device.Id))
                continue;
            if (_notifier.Notify(new LowBatteryAlert(device.Id, device.DisplayName, percent)))
                _disarmed.Add(device.Id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _devices.DevicesChanged -= OnDevicesChanged;
    }
}
