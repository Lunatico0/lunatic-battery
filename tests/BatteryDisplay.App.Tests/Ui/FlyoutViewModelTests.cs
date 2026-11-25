using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tests.Settings;
using BatteryDisplay.App.Ui.Devices;
using BatteryDisplay.App.Ui.Flyout;
using BatteryDisplay.Core;
using static BatteryDisplay.App.Tests.Ui.Snapshots;

namespace BatteryDisplay.App.Tests.Ui;

public class FlyoutViewModelTests
{
    private readonly FakeDeviceSnapshots _devices = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly ManualTimeProvider _time = new(T0);

    private FlyoutViewModel Create() => new(_devices, _settings, _time);

    [Fact]
    public void Rows_FollowTheSnapshots_InOrder()
    {
        _devices.Publish(Device("xion", 10, kind: DeviceKind.Headset), Device("n80", 39, kind: DeviceKind.Keyboard), Unread("pad", DeviceKind.Gamepad));

        using var vm = Create();

        Assert.Equal(["xion", "n80", "pad"], vm.Rows.Select(r => r.Id));
        Assert.Equal([DeviceState.Low, DeviceState.Discharging, DeviceState.NoData], vm.Rows.Select(r => r.State));
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void NoDevices_IsTheEmptyState()
    {
        using var vm = Create();

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Rows);
        Assert.False(string.IsNullOrWhiteSpace(FlyoutViewModel.EmptyText));
    }

    [Fact]
    public void DevicesChanged_RebuildsTheRows_AndNotifies()
    {
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        _devices.Publish(Device("cobra", 82));

        Assert.Equal(["cobra"], vm.Rows.Select(r => r.Id));
        Assert.False(vm.IsEmpty);
        Assert.Contains(nameof(FlyoutViewModel.Rows), changes.Names);
        Assert.Contains(nameof(FlyoutViewModel.IsEmpty), changes.Names);
        Assert.Contains(nameof(FlyoutViewModel.Subtitle), changes.Names);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var vm = Create();

        vm.Dispose();

        Assert.Equal(0, _devices.Subscribers);
        Assert.Equal(0, _settings.Subscribers);
    }

    [Theory]
    [InlineData(1, 0, "1 dispositivo")]
    [InlineData(2, 0, "2 dispositivos")]
    [InlineData(5, 1, "5 dispositivos · 1 con batería baja")]
    [InlineData(5, 2, "5 dispositivos · 2 con batería baja")]
    [InlineData(1, 1, "1 dispositivo · 1 con batería baja")]
    [InlineData(0, 0, "0 dispositivos")]
    public void Subtitle_CountsDevicesAndLowOnes(int devices, int low, string expected)
    {
        Assert.Equal(expected, FlyoutViewModel.SubtitleFor(devices, low));
    }

    [Fact]
    public void LowCount_UsesEachDeviceThreshold()
    {
        _settings.Save(_settings.Current.WithDevice("a", DeviceSettings.Default with { LowBatteryThreshold = 30 }));
        _devices.Publish(Device("a", 25), Device("b", 25), Device("c", 15), Device("d", null));

        using var vm = Create();

        Assert.Equal("4 dispositivos · 2 con batería baja", vm.Subtitle);
    }

    [Fact]
    public void LowCount_SkipsChargingDevices_LikeTheirRowState()
    {
        _devices.Publish(Device("a", 5, ChargeState.Charging), Device("b", 10));

        using var vm = Create();

        Assert.Equal("2 dispositivos · 1 con batería baja", vm.Subtitle);
    }

    [Fact]
    public void ThresholdChange_RecomputesStateAndSubtitle_WithoutANewDeviceList()
    {
        _devices.Publish(Device("a", 25));
        using var vm = Create();
        Assert.Equal(DeviceState.Discharging, vm.Rows[0].State);

        _settings.Save(_settings.Current.WithDevice("a", DeviceSettings.Default with { LowBatteryThreshold = 25 }));

        Assert.Equal(DeviceState.Low, vm.Rows[0].State);
        Assert.Equal("1 dispositivo · 1 con batería baja", vm.Subtitle);
    }

    private void Hide(string id) => _settings.Save(_settings.Current.WithDevice(id, DeviceSettings.Default with { Hidden = true }));

    [Fact]
    public void HiddenDevice_IsNotListed_AndDoesNotCountInTheSubtitle()
    {
        Hide("xion");
        _devices.Publish(Device("cobra", 82), Device("xion", 10, kind: DeviceKind.Headset), Device("pad", 5));

        using var vm = Create();

        Assert.Equal(["cobra", "pad"], vm.Rows.Select(r => r.Id));
        Assert.Equal("2 dispositivos · 1 con batería baja", vm.Subtitle);
    }

    [Fact]
    public void HidingADevice_DropsItsRow_WithoutANewDeviceList()
    {
        _devices.Publish(Device("cobra", 82), Device("xion", 10));
        using var vm = Create();

        Hide("xion");

        Assert.Equal(["cobra"], vm.Rows.Select(r => r.Id));
        Assert.Equal("1 dispositivo", vm.Subtitle);
    }

    [Fact]
    public void EveryDeviceHidden_IsTheEmptyState()
    {
        Hide("xion");
        _devices.Publish(Device("xion", 10));

        using var vm = Create();

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasUpdatedText);
    }

    [Fact]
    public void UpdatedText_IgnoresAHiddenDevicesReading()
    {
        Hide("xion");
        _devices.Publish(Device("a", 50, readAt: T0.AddSeconds(-40)), Device("xion", 10, readAt: T0.AddSeconds(-2)));

        using var vm = Create();

        Assert.Equal("Actualizado hace 40 s", vm.UpdatedText);
    }

    [Theory]
    [InlineData(0, "Actualizado hace 0 s")]
    [InlineData(12, "Actualizado hace 12 s")]
    [InlineData(59, "Actualizado hace 59 s")]
    [InlineData(60, "Actualizado hace 1 min")]
    [InlineData(119, "Actualizado hace 1 min")]
    [InlineData(600, "Actualizado hace 10 min")]
    [InlineData(-3, "Actualizado hace 0 s")]
    public void Updated_IsSecondsThenMinutes(int seconds, string expected)
    {
        Assert.Equal(expected, FlyoutViewModel.UpdatedFor(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Updated_CountsFromTheNewestReading()
    {
        _devices.Publish(Device("a", 50, readAt: T0.AddSeconds(-40)), Device("b", 50, readAt: T0.AddSeconds(-12)), Unread("c"));

        using var vm = Create();

        Assert.Equal("Actualizado hace 12 s", vm.UpdatedText);
        Assert.True(vm.HasUpdatedText);
    }

    [Fact]
    public void Tick_MovesTheUpdatedText_WithTheClock()
    {
        _devices.Publish(Device("a", 50, readAt: T0));
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        _time.Now = T0.AddSeconds(75);
        vm.Tick();

        Assert.Equal("Actualizado hace 1 min", vm.UpdatedText);
        Assert.Contains(nameof(FlyoutViewModel.UpdatedText), changes.Names);
    }

    [Fact]
    public void NoReadingAtAll_HidesTheUpdatedText()
    {
        _devices.Publish(Unread("a"));

        using var vm = Create();

        Assert.False(vm.HasUpdatedText);
        Assert.Equal("", vm.UpdatedText);
    }

    [Fact]
    public void OpenSettings_RaisesSettingsRequested()
    {
        using var vm = Create();
        var raised = 0;
        vm.SettingsRequested += (_, _) => raised++;

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, raised);
    }
}
