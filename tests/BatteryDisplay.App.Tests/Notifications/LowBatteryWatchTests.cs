using BatteryDisplay.App.Notifications;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tests.Settings;
using BatteryDisplay.App.Tests.Ui;
using BatteryDisplay.Core;
using static BatteryDisplay.App.Tests.Ui.Snapshots;

namespace BatteryDisplay.App.Tests.Notifications;

/// <summary>Records every alert; <see cref="CanShow"/> false plays a tray with no visible icon.</summary>
internal sealed class FakeLowBatteryNotifier : ILowBatteryNotifier
{
    public bool CanShow { get; set; } = true;
    public List<LowBatteryAlert> Shown { get; } = [];

    public bool Notify(LowBatteryAlert alert)
    {
        if (!CanShow)
            return false;
        Shown.Add(alert);
        return true;
    }
}

public sealed class LowBatteryWatchTests : IDisposable
{
    private const int Threshold = DeviceSettings.DefaultLowBatteryThreshold;

    private readonly FakeDeviceSnapshots _devices = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeLowBatteryNotifier _notifier = new();
    private readonly LowBatteryWatch _watch;

    public LowBatteryWatchTests() => _watch = new LowBatteryWatch(_devices, _settings, _notifier);

    public void Dispose() => _watch.Dispose();

    private IEnumerable<(string, int)> Shown => _notifier.Shown.Select(a => (a.DeviceId, a.Percent));

    private void Read(params int[] levels)
    {
        foreach (var level in levels)
            _devices.Publish(Device("mouse", level));
    }

    private void SetThreshold(string id, int threshold) =>
        _settings.Save(_settings.Current.WithDevice(id, _settings.Current.For(id) with { LowBatteryThreshold = threshold }));

    // Rule 1: one notice when an armed device reaches its own threshold

    [Fact]
    public void CrossingTheThreshold_NotifiesOnce()
    {
        Read(40, 16, Threshold, Threshold - 1, 3);

        Assert.Equal([("mouse", Threshold)], Shown);
    }

    [Fact]
    public void UsesTheDevicesOwnThreshold()
    {
        SetThreshold("mouse", 30);

        Read(31, 30);

        Assert.Equal([("mouse", 30)], Shown);
    }

    [Fact]
    public void Alert_IsTitledBateriaBaja_WithTheDisplayNameAndLevel()
    {
        _devices.Publish(Device("mouse", 12, name: "Razer Cobra"));

        var alert = Assert.Single(_notifier.Shown);
        Assert.Equal("Batería baja", LowBatteryAlert.Title);
        Assert.Equal("Razer Cobra: 12%", alert.Text);
    }

    [Fact]
    public void HiddenDevice_IsNotNotified()
    {
        _settings.Save(_settings.Current.WithDevice("mouse", DeviceSettings.Default with { Hidden = true }));

        Read(10);

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void ChargingDevice_IsNotNotified()
    {
        _devices.Publish(Device("mouse", 10, ChargeState.Charging));

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void UnknownLevel_OrNoReading_IsNotNotified()
    {
        _devices.Publish(Device("mouse", null), Unread("pad"));

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void EachDevice_IsTrackedOnItsOwn()
    {
        _devices.Publish(Device("mouse", 10), Device("pad", 50));
        _devices.Publish(Device("mouse", 9), Device("pad", 14));

        Assert.Equal([("mouse", 10), ("pad", 14)], Shown);
    }

    // Rule 2: charging, or climbing to threshold + 5, rearms

    [Fact]
    public void Charging_Rearms()
    {
        Read(10);
        _devices.Publish(Device("mouse", 11, ChargeState.Charging));
        Read(11);

        Assert.Equal([("mouse", 10), ("mouse", 11)], Shown);
    }

    [Fact]
    public void ClimbingToThresholdPlusFive_Rearms()
    {
        Read(Threshold, Threshold + LowBatteryWatch.RearmMargin, Threshold);

        Assert.Equal([("mouse", Threshold), ("mouse", Threshold)], Shown);
    }

    [Fact]
    public void ALevelOscillatingAroundTheThreshold_DoesNotRepeatTheNotice()
    {
        SetThreshold("mouse", 20);

        Read(20, 21, 20, 24, 19);

        Assert.Equal([("mouse", 20)], Shown);
    }

    // Rule 3: the first reading after start already low counts as a crossing

    [Fact]
    public void FirstReadingAlreadyLow_Notifies()
    {
        Read(8);

        Assert.Equal([("mouse", 8)], Shown);
    }

    // Rule 4: the state lives per device Id for the session

    [Fact]
    public void DeviceThatLeavesAndComesBack_KeepsItsState()
    {
        Read(10);
        _devices.Publish();
        Read(9);

        Assert.Equal([("mouse", 10)], Shown);
    }

    // Rule 5: the global switch

    [Fact]
    public void SwitchedOff_NotifiesNothing()
    {
        _settings.Save(_settings.Current with { NotifyLowBattery = false });

        Read(10, 9);

        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void SwitchedBackOn_NotifiesOnlyAtTheNextReading()
    {
        _settings.Save(_settings.Current with { NotifyLowBattery = false });
        Read(10);

        _settings.Save(_settings.Current with { NotifyLowBattery = true });
        Assert.Empty(_notifier.Shown);

        Read(10);
        Assert.Equal([("mouse", 10)], Shown);
    }

    [Fact]
    public void ChargingWhileSwitchedOff_StillRearms()
    {
        Read(10);
        _settings.Save(_settings.Current with { NotifyLowBattery = false });
        _devices.Publish(Device("mouse", 60, ChargeState.Charging));
        Read(10);
        _settings.Save(_settings.Current with { NotifyLowBattery = true });

        Read(10);

        Assert.Equal([("mouse", 10), ("mouse", 10)], Shown);
    }

    // Rule 6: a notice that could not be shown leaves the device armed

    [Fact]
    public void NoVisibleIcon_KeepsTheDeviceArmed()
    {
        _notifier.CanShow = false;
        Read(10);
        Assert.Empty(_notifier.Shown);

        _notifier.CanShow = true;
        Read(10);

        Assert.Equal([("mouse", 10)], Shown);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        _watch.Dispose();

        Read(10);

        Assert.Empty(_notifier.Shown);
        Assert.Equal(0, _devices.Subscribers);
    }
}
