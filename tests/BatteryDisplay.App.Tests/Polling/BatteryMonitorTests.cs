using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Polling;

public class BatteryMonitorTests
{
    private sealed class FakeIcon : ITrayIcon
    {
        public List<TrayIconModel> Applied { get; } = [];
        public TrayIconModel? Last => Applied.LastOrDefault();
        public void Apply(TrayIconModel model) => Applied.Add(model);
    }

    private static readonly BatteryPoller Poller = new(TimeSpan.FromMilliseconds(300));

    [Fact]
    public async Task Refresh_UpdatesHealthyIcons_AndHidesFailingOnes()
    {
        IBatteryProvider[] providers =
        [
            FakeProvider.Returning("ok", new BatteryStatus(78, ChargeState.Discharging)),
            FakeProvider.Throwing("boom"),
            FakeProvider.Hanging("stuck"),
        ];
        FakeIcon[] icons = [new(), new(), new()];
        var monitor = new BatteryMonitor(providers, icons, Poller);

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(new TrayIconModel(true, DeviceKind.Mouse, 7, LevelColor.High, true, "ok: 78%"), icons[0].Last);
        Assert.False(icons[1].Last!.Visible);
        Assert.False(icons[2].Last!.Visible);
    }

    [Fact]
    public async Task Refresh_PassesEachProvidersKindToItsIcon()
    {
        IBatteryProvider[] providers =
        [
            new FakeProvider("mouse", _ => Task.FromResult<BatteryStatus?>(new BatteryStatus(40, ChargeState.Discharging))),
            new FakeProvider("headset", _ => Task.FromResult<BatteryStatus?>(new BatteryStatus(40, ChargeState.Discharging)), DeviceKind.Headset),
        ];
        FakeIcon[] icons = [new(), new()];
        var monitor = new BatteryMonitor(providers, icons, Poller);

        await monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(DeviceKind.Mouse, icons[0].Last!.Kind);
        Assert.Equal(DeviceKind.Headset, icons[1].Last!.Kind);
    }

    [Fact]
    public async Task HiddenIcon_ReappearsWhenTheDeviceReadsAgain()
    {
        BatteryStatus? current = null;
        IBatteryProvider[] providers = [new FakeProvider("pad", _ => Task.FromResult(current))];
        FakeIcon[] icons = [new()];
        var monitor = new BatteryMonitor(providers, icons, Poller);

        await monitor.RefreshAsync(CancellationToken.None);
        Assert.False(icons[0].Last!.Visible);

        current = new BatteryStatus(15, ChargeState.Discharging);
        await monitor.RefreshAsync(CancellationToken.None);

        Assert.True(icons[0].Last!.Visible);
        Assert.Equal(LevelColor.Low, icons[0].Last!.Color);
    }

    [Fact]
    public async Task Refresh_ReportsWhenAnIconIsShownForTheFirstTime()
    {
        BatteryStatus? current = null;
        IBatteryProvider[] providers = [new FakeProvider("pad", _ => Task.FromResult(current))];
        var monitor = new BatteryMonitor(providers, [new FakeIcon()], Poller);

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
        var monitor = new BatteryMonitor([provider], [new FakeIcon()], new BatteryPoller(TimeSpan.FromSeconds(5)));

        var first = monitor.RefreshAsync(CancellationToken.None);
        await monitor.RefreshAsync(CancellationToken.None);
        gate.SetResult(new BatteryStatus(60, ChargeState.Discharging));
        await first;

        Assert.Equal(1, provider.Reads);
    }

    [Fact]
    public void ProvidersAndIcons_MustMatch()
    {
        Assert.Throws<ArgumentException>(() =>
            new BatteryMonitor([FakeProvider.Returning("a", null)], [], Poller));
    }
}
