using System.Diagnostics;
using BatteryDisplay.App.Polling;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Polling;

public class BatteryPollerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(300);
    private static readonly BatteryStatus Ok = new(78, ChargeState.Discharging);

    [Fact]
    public async Task OneOk_OneThrows_OneHangs_OkStillReads_OthersAreNull()
    {
        var logs = new List<string>();
        var poller = new BatteryPoller(Timeout, m => { lock (logs) logs.Add(m); });
        IBatteryProvider[] providers =
        [
            FakeProvider.Returning("ok", Ok),
            FakeProvider.Throwing("boom"),
            FakeProvider.Hanging("stuck"),
            FakeProvider.FaultingAsync("late-boom"),
        ];

        var sw = Stopwatch.StartNew();
        var results = await poller.PollAsync(providers, CancellationToken.None);
        sw.Stop();

        Assert.Equal(new BatteryStatus?[] { Ok, null, null, null }, results);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"poll took {sw.Elapsed}");
        Assert.Contains(logs, l => l.Contains("boom"));
        Assert.Contains(logs, l => l.Contains("stuck"));
        Assert.Contains(logs, l => l.Contains("late-boom"));
    }

    [Fact]
    public async Task ProvidersAreReadInParallel()
    {
        var poller = new BatteryPoller(TimeSpan.FromSeconds(5));
        var providers = Enumerable.Range(0, 4)
            .Select(i => (IBatteryProvider)new FakeProvider($"slow{i}", async ct =>
            {
                await Task.Delay(400, ct);
                return Ok;
            }))
            .ToArray();

        var sw = Stopwatch.StartNew();
        var results = await poller.PollAsync(providers, CancellationToken.None);
        sw.Stop();

        Assert.All(results, r => Assert.Equal(Ok, r));
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(1400), $"poll took {sw.Elapsed}, expected parallel reads");
    }

    [Fact]
    public async Task SynchronousProviderWork_DoesNotBlockTheCaller()
    {
        var poller = new BatteryPoller(TimeSpan.FromSeconds(5));
        IBatteryProvider[] providers = [FakeProvider.BlockingSynchronously("sync", TimeSpan.FromMilliseconds(800), Ok)];

        var sw = Stopwatch.StartNew();
        var task = poller.PollAsync(providers, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(400), $"PollAsync blocked the caller for {sw.Elapsed}");
        Assert.Equal(new BatteryStatus?[] { Ok }, await task);
    }

    [Fact]
    public async Task ProviderReceivesACancelledTokenAfterTheTimeout()
    {
        var cancelled = new TaskCompletionSource();
        var poller = new BatteryPoller(TimeSpan.FromMilliseconds(100));
        IBatteryProvider[] providers =
        [
            new FakeProvider("cooperative", async ct =>
            {
                using var _ = ct.Register(() => cancelled.TrySetResult());
                await Task.Delay(System.Threading.Timeout.Infinite, ct);
                return Ok;
            }),
        ];

        var results = await poller.PollAsync(providers, CancellationToken.None);

        Assert.Equal(new BatteryStatus?[] { null }, results);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
