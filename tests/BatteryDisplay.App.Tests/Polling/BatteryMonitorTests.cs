using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tests.Discovery;
using BatteryDisplay.App.Tests.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Polling;

public class BatteryMonitorTests
{
    private static readonly BatteryPoller Poller = new(TimeSpan.FromMilliseconds(300));
    private static readonly BatteryStatus Ok = new(60, ChargeState.Discharging);
    private static readonly DateTimeOffset T0 = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeSettingsStore _settings = new();
    private readonly ManualTimeProvider _clock = new(T0);
    private readonly List<IReadOnlyList<DeviceSnapshot>> _published = [];

    private BatteryMonitor Monitor(IDeviceSource source, BatteryPoller? poller = null, Action<string>? log = null)
    {
        var monitor = new BatteryMonitor(source, poller ?? Poller, _settings, _clock, log);
        monitor.DevicesChanged += (_, devices) => _published.Add(devices);
        return monitor;
    }

    private static IEnumerable<string> Ids(BatteryMonitor monitor) => monitor.Devices.Select(d => d.Id);

    [Fact]
    public async Task Refresh_PublishesOneSnapshotPerDevice_WithEveryField()
    {
        _settings.Save(AppSettings.Default.WithDevice("nova", DeviceSettings.Default with { DisplayName = "Auriculares" }));
        var monitor = Monitor(new FakeSource(
            new FakeProvider("nova", _ => Task.FromResult<BatteryStatus?>(new BatteryStatus(78, ChargeState.Charging)),
                DeviceKind.Headset, "Dongle SteelSeries", "Arctis Nova 5"),
            new FakeProvider("pad", _ => Task.FromResult<BatteryStatus?>(null), DeviceKind.Gamepad, "XInput", "Control 1")));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(
            [
                new DeviceSnapshot("nova", "Arctis Nova 5", "Auriculares", DeviceKind.Headset, "Dongle SteelSeries", new BatteryStatus(78, ChargeState.Charging), T0),
                new DeviceSnapshot("pad", "Control 1", "Control 1", DeviceKind.Gamepad, "XInput", null, null),
            ],
            monitor.Devices);
        Assert.Same(monitor.Devices, Assert.Single(_published));
    }

    [Fact]
    public async Task FailingOrHangingProviders_HaveNoReading()
    {
        var monitor = Monitor(new FakeSource(FakeProvider.Returning("ok", Ok), FakeProvider.Throwing("boom"), FakeProvider.Hanging("stuck")));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal<BatteryStatus?>([Ok, null, null], monitor.Devices.Select(d => d.Status));
    }

    [Fact]
    public async Task LastReadAt_KeepsTheTimeOfTheLastReading_WhenALaterReadFails()
    {
        BatteryStatus? current = Ok;
        var monitor = Monitor(new FakeSource(new FakeProvider("a", _ => Task.FromResult<BatteryStatus?>(current))));
        await monitor.RefreshAsync(CancellationToken.None);

        _clock.Now = T0.AddMinutes(1);
        current = null;
        await monitor.RefreshAsync(CancellationToken.None);

        var device = Assert.Single(monitor.Devices);
        Assert.Null(device.Status);
        Assert.Equal(T0, device.LastReadAt);

        _clock.Now = T0.AddMinutes(2);
        current = Ok;
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(T0.AddMinutes(2), Assert.Single(monitor.Devices).LastReadAt);
    }

    [Fact]
    public async Task Order_IsFirstAppearance_EvenWhenDiscoveryReorders()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        source.Current = [FakeProvider.Returning("c", Ok), FakeProvider.Returning("b", Ok), FakeProvider.Returning("a", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["a", "b", "c"], Ids(monitor));
    }

    [Fact]
    public async Task VanishedDevice_LeavesTheList_AndComesBackAtTheEnd_WithNoPastReading()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        source.Current = [FakeProvider.Returning("b", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);
        Assert.Equal(["b"], Ids(monitor));

        _clock.Now = T0.AddMinutes(5);
        source.Current = [FakeProvider.Returning("a", null), FakeProvider.Returning("b", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["b", "a"], Ids(monitor));
        Assert.Null(monitor.Devices[1].LastReadAt);
    }

    [Fact]
    public async Task DuplicateIdsInOneDiscovery_FirstProviderWins()
    {
        var first = FakeProvider.Returning("same", Ok);
        var second = FakeProvider.Returning("same", null);
        var monitor = Monitor(new FakeSource(first, second));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(Ok, Assert.Single(monitor.Devices).Status);
        Assert.Equal(1, first.Reads);
        Assert.Equal(0, second.Reads);
    }

    private static readonly Guid Mouse = new("c5cc23df-e5b5-11f1-beb0-f44efc8ff591");
    private static readonly string BtMouse = $"bt-{Mouse}";

    private static MergedProvider Merged(FakeProvider winner, params FakeProvider[] losers) => new(winner, losers);

    [Fact]
    public async Task MergedDevice_LearnsTheAlias_AndTheLosersSettingsFollowTheWinner()
    {
        _settings.Save(AppSettings.Default.WithDevice(BtMouse, DeviceSettings.Default with { DisplayName = "Mi mouse" }));
        var monitor = Monitor(new FakeSource(Merged(
            FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid, Ok),
            FakeProvider.Device(BtMouse, Mouse, DeviceChannel.Bluetooth))));

        await monitor.RefreshAsync(CancellationToken.None);

        var device = Assert.Single(monitor.Devices);
        Assert.Equal(("razer", "Mi mouse"), (device.Id, device.DisplayName));
        Assert.Equal("razer", _settings.Current.CanonicalIdOf(BtMouse));
    }

    [Fact]
    public async Task KnownAlias_IsSavedOnlyOnce()
    {
        var saves = 0;
        _settings.Changed += (_, _) => saves++;
        var monitor = Monitor(new FakeSource(Merged(
            FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid, Ok),
            FakeProvider.Device(BtMouse, Mouse, DeviceChannel.Bluetooth))));

        await monitor.RefreshAsync(CancellationToken.None);
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, saves);
    }

    [Fact]
    public async Task WinnerGone_TheLoserAlone_KeepsTheDeviceIdAndItsSettings()
    {
        var bluetooth = FakeProvider.Device(BtMouse, Mouse, DeviceChannel.Bluetooth, new BatteryStatus(40, ChargeState.Discharging));
        var source = new FakeSource(Merged(FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid, Ok), bluetooth));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);
        _settings.Save(_settings.Current.WithDevice("razer", DeviceSettings.Default with { DisplayName = "Cobra" }));

        source.Current = [bluetooth];
        await monitor.RefreshAsync(CancellationToken.None);

        var device = Assert.Single(monitor.Devices);
        Assert.Equal(("razer", "Cobra", 40), (device.Id, device.DisplayName, device.Status!.Percent));
    }

    [Fact]
    public async Task AfterARestart_TheLoserAlone_ShowsUnderTheWinnersId()
    {
        _settings.Save(AppSettings.Default
            .WithAlias(BtMouse, "razer")
            .WithDevice("razer", DeviceSettings.Default with { DisplayName = "Cobra" }));
        var monitor = Monitor(new FakeSource(FakeProvider.Device(BtMouse, Mouse, DeviceChannel.Bluetooth, Ok)));

        await monitor.RefreshAsync(CancellationToken.None);

        var device = Assert.Single(monitor.Devices);
        Assert.Equal(("razer", "Cobra"), (device.Id, device.DisplayName));
    }

    [Fact]
    public async Task Alias_IsNotApplied_WhileTheCanonicalDeviceShowsOnItsOwn()
    {
        _settings.Save(AppSettings.Default.WithAlias(BtMouse, "razer"));
        var monitor = Monitor(new FakeSource(
            FakeProvider.Returning("razer", null),
            FakeProvider.Device(BtMouse, Mouse, DeviceChannel.Bluetooth, Ok)));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["razer", BtMouse], Ids(monitor));
        Assert.Equal<BatteryStatus?>([null, Ok], monitor.Devices.Select(d => d.Status));
    }

    [Fact]
    public async Task StillPresentDevice_ReadsTheLatestProviderInstance()
    {
        var oldInstance = FakeProvider.Returning("a", new BatteryStatus(80, ChargeState.Discharging));
        var source = new FakeSource(oldInstance);
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        var newInstance = FakeProvider.Returning("a", new BatteryStatus(20, ChargeState.Discharging));
        source.Current = [newInstance];
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal((1, 1), (oldInstance.Reads, newInstance.Reads));
        Assert.Equal(20, Assert.Single(monitor.Devices).Status!.Percent);
    }

    [Fact]
    public async Task DevicesChanged_IsRaisedOnlyWhenTheListChanges()
    {
        var source = new FakeSource(FakeProvider.Returning("a", null));
        var monitor = Monitor(source);

        await monitor.RefreshAsync(CancellationToken.None);
        await monitor.RefreshAsync(CancellationToken.None);
        Assert.Single(_published);

        source.Current = [FakeProvider.Returning("a", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);
        Assert.Equal(2, _published.Count);
    }

    [Fact]
    public async Task FirstCycle_PublishesEvenWhenNothingIsConnected_SoTheTrayStopsSearching()
    {
        var monitor = Monitor(new FakeSource());

        await monitor.RefreshAsync(CancellationToken.None);
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Empty(Assert.Single(_published));
    }

    [Fact]
    public async Task FirstCycle_LogsTheDiscovery_EachReading_AndThePublish()
    {
        var logs = new List<string>();
        var monitor = Monitor(new FakeSource(FakeProvider.Returning("ok", Ok), FakeProvider.Throwing("boom")), log: logs.Add);

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Contains(logs, l => l.StartsWith("first cycle: discovered 2 device(s) in ", StringComparison.Ordinal));
        Assert.Contains(logs, l => l.StartsWith("first cycle: ok ", StringComparison.Ordinal) && l.EndsWith(": 60% Discharging", StringComparison.Ordinal));
        Assert.Contains(logs, l => l.StartsWith("first cycle: boom ", StringComparison.Ordinal) && l.EndsWith(": no reading", StringComparison.Ordinal));
        Assert.Contains(logs, l => l.StartsWith("first cycle: published 2 device(s) after ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LaterCycles_LogNothing()
    {
        var logs = new List<string>();
        var monitor = Monitor(new FakeSource(FakeProvider.Returning("ok", Ok)), log: logs.Add);
        await monitor.RefreshAsync(CancellationToken.None);
        logs.Clear();

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Empty(logs);
    }

    [Fact]
    public void SettingsChangeBeforeTheFirstCycle_PublishesNothing()
    {
        Monitor(new FakeSource(FakeProvider.Returning("a", Ok)));

        _settings.Save(_settings.Current with { TrayIconMode = TrayIconMode.Lowest });

        Assert.Empty(_published);
    }

    [Fact]
    public async Task Rename_RepublishesWithTheNewDisplayName_WithoutRereading()
    {
        var provider = FakeProvider.Returning("a", Ok);
        var monitor = Monitor(new FakeSource(provider));
        await monitor.RefreshAsync(CancellationToken.None);

        _settings.Save(_settings.Current.WithDevice("a", DeviceSettings.Default with { DisplayName = "Mi mouse" }));

        Assert.Equal("Mi mouse", Assert.Single(monitor.Devices).DisplayName);
        Assert.Equal("Mi mouse", _published[^1].Single().DisplayName);
        Assert.Equal(1, provider.Reads);
    }

    [Fact]
    public async Task SettingsChangeThatKeepsEveryName_PublishesNothing()
    {
        var monitor = Monitor(new FakeSource(FakeProvider.Returning("a", Ok)));
        await monitor.RefreshAsync(CancellationToken.None);

        _settings.Save(_settings.Current with { TrayIconMode = TrayIconMode.Lowest });

        Assert.Single(_published);
    }

    [Fact]
    public async Task PublishedList_IsImmutable()
    {
        var monitor = Monitor(new FakeSource(FakeProvider.Returning("a", Ok)));
        await monitor.RefreshAsync(CancellationToken.None);

        var list = Assert.IsAssignableFrom<IList<DeviceSnapshot>>(monitor.Devices);
        Assert.Throws<NotSupportedException>(() => list[0] = list[0] with { Name = "hacked" });
    }

    [Fact]
    public async Task OverlappingRefresh_DoesNotRunAlongside_ButRunsOnceMoreAfterwards()
    {
        var gate = new TaskCompletionSource<BatteryStatus?>();
        var provider = new FakeProvider("slow", _ => gate.Task);
        var source = new FakeSource(provider);
        var monitor = Monitor(source, new BatteryPoller(TimeSpan.FromSeconds(5)));

        var first = monitor.RefreshAsync(CancellationToken.None);
        await monitor.RefreshAsync(CancellationToken.None);
        gate.SetResult(Ok);
        await first;

        Assert.Equal(2, provider.Reads);
        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task HotPlugDuringADiscovery_IsPickedUpRightAfter_AndBurstsCoalesceIntoOneMoreDiscovery()
    {
        var gate = new ManualResetEventSlim();
        var source = new FakeSource(FakeProvider.Returning("a", Ok));
        source.OnDiscover = () =>
        {
            if (source.Calls == 1)
                gate.Wait(TimeSpan.FromSeconds(5));
        };
        var monitor = Monitor(source);

        var first = monitor.RefreshAsync(CancellationToken.None);
        source.Current = [FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok)];
        Task[] overlapping =
        [
            monitor.RefreshAsync(CancellationToken.None),
            monitor.RefreshAsync(CancellationToken.None),
            monitor.RefreshAsync(CancellationToken.None),
        ];
        Assert.All(overlapping, t => Assert.True(t.IsCompleted));
        gate.Set();
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, source.Calls);
        Assert.Equal(["a", "b"], Ids(monitor));
    }

    [Fact]
    public async Task Discover_RunsOnTheThreadPool()
    {
        bool? onPool = null;
        var source = new FakeSource(FakeProvider.Returning("a", Ok))
        {
            OnDiscover = () => onPool = Thread.CurrentThread.IsThreadPoolThread,
        };

        await Monitor(source).RefreshAsync(CancellationToken.None);

        Assert.True(onPool);
    }

    [Fact]
    public async Task Dispose_StopsPublishing_AndUnsubscribesFromSettings()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        monitor.Dispose();
        source.Current = [FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Single(_published);
        Assert.Equal(0, _settings.Subscribers);
    }

    [Fact]
    public async Task DisposeDuringARefresh_PublishesNothingAfterwards()
    {
        var gate = new TaskCompletionSource();
        var source = new FakeSource(FakeProvider.Returning("a", Ok)) { OnDiscover = () => gate.Task.Wait() };
        var monitor = Monitor(source);

        var refresh = monitor.RefreshAsync(CancellationToken.None);
        monitor.Dispose();
        gate.SetResult();
        await refresh;

        Assert.Empty(_published);
        Assert.Empty(monitor.Devices);
    }
}
