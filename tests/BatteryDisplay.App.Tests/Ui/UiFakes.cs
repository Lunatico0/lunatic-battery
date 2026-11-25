using System.Collections.Immutable;
using System.ComponentModel;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Ui.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Ui;

/// <summary>A device list the test replaces by hand, raising DevicesChanged like the monitor does.</summary>
internal sealed class FakeDeviceSnapshots(params DeviceSnapshot[] devices) : IDeviceSnapshots
{
    public IReadOnlyList<DeviceSnapshot> Devices { get; private set; } = devices.ToImmutableArray();

    public event EventHandler<IReadOnlyList<DeviceSnapshot>>? DevicesChanged;

    public void Publish(params DeviceSnapshot[] devices)
    {
        Devices = devices.ToImmutableArray();
        DevicesChanged?.Invoke(this, Devices);
    }

    public int Subscribers => DevicesChanged?.GetInvocationList().Length ?? 0;
}

internal sealed class FakeStartupSetting(bool enabled = false) : IStartupSetting
{
    public bool IsEnabled { get; set; } = enabled;
    public bool Throws { get; set; }
    public List<bool> Calls { get; } = [];

    public void SetEnabled(bool enabled)
    {
        Calls.Add(enabled);
        if (Throws)
            throw new UnauthorizedAccessException("denied");
        IsEnabled = enabled;
    }
}

internal static class Snapshots
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 24, 21, 43, 0, TimeSpan.Zero);

    public static DeviceSnapshot Device(string id, int? percent, ChargeState charge = ChargeState.Discharging,
        DeviceKind kind = DeviceKind.Mouse, string? name = null, string source = "Bluetooth", DateTimeOffset? readAt = null) =>
        new(id, name ?? id, name ?? id, kind, source, new BatteryStatus(percent, charge), readAt ?? T0);

    public static DeviceSnapshot Unread(string id, DeviceKind kind = DeviceKind.Mouse, string source = "Bluetooth") =>
        new(id, id, id, kind, source, null, null);
}

/// <summary>Records the PropertyChanged names an object raises.</summary>
internal sealed class PropertyChanges
{
    public PropertyChanges(INotifyPropertyChanged source) => source.PropertyChanged += (_, e) => Names.Add(e.PropertyName ?? "");

    public List<string> Names { get; } = [];
}
