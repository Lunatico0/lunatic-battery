using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Tests.Discovery;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Polling;

public class BatteryMonitorTests
{
    private sealed class FakeIcon(string deviceId) : ITrayIcon
    {
        public string DeviceId => deviceId;
        public List<TrayIconModel> Applied { get; } = [];
        public TrayIconModel? Last => Applied.LastOrDefault();
        public bool Disposed { get; private set; }
        public void Apply(TrayIconModel model) => Applied.Add(model);
        public void Dispose() => Disposed = true;
    }

    private static readonly BatteryPoller Poller = new(TimeSpan.FromMilliseconds(300));
    private static readonly BatteryStatus Ok = new(60, ChargeState.Discharging);

    private readonly List<FakeIcon> _created = [];

    private BatteryMonitor Monitor(IDeviceSource source, BatteryPoller? poller = null) =>
        new(source, provider =>
        {
            var icon = new FakeIcon(provider.Id);
            _created.Add(icon);
            return icon;
        }, poller ?? Poller);

    private FakeIcon IconOf(string id) => _created.Last(i => i.DeviceId == id);

    [Fact]
    public async Task Refresh_UpdatesHealthyIcons_AndHidesFailingOnes()
    {
        var monitor = Monitor(new FakeSource(
            FakeProvider.Returning("ok", new BatteryStatus(78, ChargeState.Discharging)),
            FakeProvider.Throwing("boom"),
            FakeProvider.Hanging("stuck")));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(new TrayIconModel(true, DeviceKind.Mouse, 7, LevelColor.High, true, "ok: 78%"), IconOf("ok").Last);
        Assert.False(IconOf("boom").Last!.Visible);
        Assert.False(IconOf("stuck").Last!.Visible);
    }

    [Fact]
    public async Task Refresh_PassesEachProvidersKindToItsIcon()
    {
        var monitor = Monitor(new FakeSource(
            new FakeProvider("mouse", _ => Task.FromResult<BatteryStatus?>(new BatteryStatus(40, ChargeState.Discharging))),
            new FakeProvider("headset", _ => Task.FromResult<BatteryStatus?>(new BatteryStatus(40, ChargeState.Discharging)), DeviceKind.Headset)));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(DeviceKind.Mouse, IconOf("mouse").Last!.Kind);
        Assert.Equal(DeviceKind.Headset, IconOf("headset").Last!.Kind);
    }

    [Fact]
    public async Task HiddenIcon_ReappearsWhenTheDeviceReadsAgain()
    {
        BatteryStatus? current = null;
        var monitor = Monitor(new FakeSource(new FakeProvider("pad", _ => Task.FromResult(current))));

        await monitor.RefreshAsync(CancellationToken.None);
        Assert.False(IconOf("pad").Last!.Visible);

        current = new BatteryStatus(15, ChargeState.Discharging);
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.True(IconOf("pad").Last!.Visible);
        Assert.Equal(LevelColor.Low, IconOf("pad").Last!.Color);
    }

    [Fact]
    public async Task Refresh_ReportsWhenAnIconIsShownForTheFirstTime()
    {
        BatteryStatus? current = null;
        var monitor = Monitor(new FakeSource(new FakeProvider("pad", _ => Task.FromResult(current))));

        Assert.False(await monitor.RefreshAsync(CancellationToken.None));
        current = new BatteryStatus(50, ChargeState.Discharging);
        Assert.True(await monitor.RefreshAsync(CancellationToken.None));
        Assert.False(await monitor.RefreshAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OverlappingRefresh_IsSkipped()
    {
        var gate = new TaskCompletionSource<BatteryStatus?>();
        var provider = new FakeProvider("slow", _ => gate.Task);
        var source = new FakeSource(provider);
        var monitor = Monitor(source, new BatteryPoller(TimeSpan.FromSeconds(5)));

        var first = monitor.RefreshAsync(CancellationToken.None);
        Assert.False(await monitor.RefreshAsync(CancellationToken.None));
        gate.SetResult(new BatteryStatus(60, ChargeState.Discharging));
        await first;

        Assert.Equal(1, provider.Reads);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task EachDiscoveredDevice_GetsExactlyOneIcon()
    {
        var monitor = Monitor(new FakeSource(FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok)));

        await monitor.RefreshAsync(CancellationToken.None);
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["a", "b"], _created.Select(i => i.DeviceId));
    }

    [Fact]
    public async Task DuplicateIdsInOneDiscovery_ShareOneIcon_FirstProviderWins()
    {
        var first = FakeProvider.Returning("same", Ok);
        var second = FakeProvider.Returning("same", null);
        var monitor = Monitor(new FakeSource(first, second));

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Single(_created);
        Assert.Equal(1, first.Reads);
        Assert.Equal(0, second.Reads);
    }

    [Fact]
    public async Task NewDevice_GetsAnIcon_OnTheNextRefresh()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        source.Current = [FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok)];
        var newlyShown = await monitor.RefreshAsync(CancellationToken.None);

        Assert.True(newlyShown);
        Assert.Equal(["a", "b"], _created.Select(i => i.DeviceId));
        Assert.True(IconOf("b").Last!.Visible);
    }

    [Fact]
    public async Task VanishedDevice_ItsIconIsDisposed_AndNoLongerUpdated()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);
        var gone = IconOf("b");
        var appliedBefore = gone.Applied.Count;

        source.Current = [FakeProvider.Returning("a", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.True(gone.Disposed);
        Assert.Equal(appliedBefore, gone.Applied.Count);
        Assert.False(IconOf("a").Disposed);
        Assert.Equal(["a"], monitor.DeviceIds);
    }

    [Fact]
    public async Task ReturningDevice_GetsAFreshIcon_AndIsReportedAsNewlyShown()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok));
        var monitor = Monitor(source);
        Assert.True(await monitor.RefreshAsync(CancellationToken.None));
        var original = IconOf("a");

        source.Current = [];
        Assert.False(await monitor.RefreshAsync(CancellationToken.None));

        source.Current = [FakeProvider.Returning("a", Ok)];
        Assert.True(await monitor.RefreshAsync(CancellationToken.None));

        Assert.True(original.Disposed);
        Assert.NotSame(original, IconOf("a"));
        Assert.False(IconOf("a").Disposed);
        Assert.True(IconOf("a").Last!.Visible);
    }

    [Fact]
    public async Task StillPresentDevice_KeepsItsIcon_AndReadsTheLatestProviderInstance()
    {
        var oldInstance = FakeProvider.Returning("a", new BatteryStatus(80, ChargeState.Discharging));
        var source = new FakeSource(oldInstance);
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        var newInstance = FakeProvider.Returning("a", new BatteryStatus(20, ChargeState.Discharging));
        source.Current = [newInstance];
        var newlyShown = await monitor.RefreshAsync(CancellationToken.None);

        Assert.False(newlyShown);
        Assert.Single(_created);
        Assert.False(_created[0].Disposed);
        Assert.Equal(1, oldInstance.Reads);
        Assert.Equal(1, newInstance.Reads);
        Assert.Equal("a: 20%", _created[0].Last!.Tooltip);
    }

    [Fact]
    public async Task IconOrder_IsFirstAppearance_EvenWhenDiscoveryReorders()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        source.Current = [FakeProvider.Returning("c", Ok), FakeProvider.Returning("b", Ok), FakeProvider.Returning("a", Ok)];
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["a", "b", "c"], monitor.DeviceIds);
        Assert.Equal(["a", "b", "c"], _created.Select(i => i.DeviceId));
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
    public async Task Dispose_DisposesEveryIcon_AndLaterRefreshesCreateNone()
    {
        var source = new FakeSource(FakeProvider.Returning("a", Ok));
        var monitor = Monitor(source);
        await monitor.RefreshAsync(CancellationToken.None);

        monitor.Dispose();
        source.Current = [FakeProvider.Returning("a", Ok), FakeProvider.Returning("b", Ok)];
        Assert.False(await monitor.RefreshAsync(CancellationToken.None));

        Assert.Single(_created);
        Assert.True(_created[0].Disposed);
    }

    [Fact]
    public async Task DisposeDuringARefresh_CreatesNoIconsAfterwards()
    {
        var gate = new TaskCompletionSource();
        var source = new FakeSource(FakeProvider.Returning("a", Ok)) { OnDiscover = () => gate.Task.Wait() };
        var monitor = Monitor(source);

        var refresh = monitor.RefreshAsync(CancellationToken.None);
        monitor.Dispose();
        gate.SetResult();

        Assert.False(await refresh);
        Assert.Empty(_created);
    }
}
