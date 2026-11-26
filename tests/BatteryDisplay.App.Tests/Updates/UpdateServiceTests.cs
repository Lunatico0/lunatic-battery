using BatteryDisplay.App.Updates;

namespace BatteryDisplay.App.Tests.Updates;

/// <summary>Scripted updater: Available is what the feed offers, Download makes it pending.</summary>
internal sealed class FakeUpdateClient : IUpdateClient
{
    public bool Installed { get; set; } = true;
    public string? Available { get; set; }
    public string? Pending { get; set; }
    public Exception? CheckError { get; set; }
    public Exception? DownloadError { get; set; }
    public Exception? ApplyError { get; set; }
    /// <summary>When set, CheckAsync waits on it instead of answering at once.</summary>
    public TaskCompletionSource<string?>? CheckGate { get; set; }

    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public int AppliedAfterExit { get; private set; }
    public int Restarts { get; private set; }

    public bool CanUpdate => Installed;
    public string? PendingVersion => Pending;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        Checks++;
        if (CheckError is not null)
            throw CheckError;
        return CheckGate is null ? Available : await CheckGate.Task;
    }

    public Task DownloadAsync(CancellationToken cancellationToken)
    {
        Downloads++;
        if (DownloadError is not null)
            throw DownloadError;
        Pending = Available;
        return Task.CompletedTask;
    }

    public void ApplyAfterExit()
    {
        if (ApplyError is not null)
            throw ApplyError;
        AppliedAfterExit++;
    }

    public void ApplyAndRestart()
    {
        if (ApplyError is not null)
            throw ApplyError;
        Restarts++;
    }
}

/// <summary>Manual clock: callbacks run only when Advance moves past their due time.</summary>
internal sealed class ManualScheduler
{
    private readonly List<(TimeSpan Due, Action Callback, Handle Handle)> _pending = [];
    private TimeSpan _now;

    public int Scheduled => _pending.Count(p => !p.Handle.Cancelled);

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        var handle = new Handle();
        _pending.Add((_now + delay, callback, handle));
        return handle;
    }

    public void Advance(TimeSpan by)
    {
        var target = _now + by;
        while (true)
        {
            var next = _pending.Where(p => p.Due <= target).OrderBy(p => p.Due).FirstOrDefault();
            if (next.Callback is null)
                break;
            _pending.Remove(next);
            _now = next.Due;
            if (!next.Handle.Cancelled)
                next.Callback();
        }
        _now = target;
    }

    public sealed class Handle : IDisposable
    {
        public bool Cancelled { get; private set; }
        public void Dispose() => Cancelled = true;
    }
}

public sealed class UpdateServiceTests : IDisposable
{
    private readonly FakeUpdateClient _client = new();
    private readonly ManualScheduler _scheduler = new();
    private readonly UpdateService _service;

    public UpdateServiceTests() => _service = new UpdateService(_client, _scheduler.Schedule);

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task NewerRelease_IsDownloaded_AndReportedReady()
    {
        _client.Available = "1.1.0";

        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.Ready("1.1.0"), result);
        Assert.Equal(1, _client.Downloads);
        Assert.Equal("1.1.0", _service.PendingVersion);
    }

    [Fact]
    public async Task NoNewerRelease_IsUpToDate_AndDownloadsNothing()
    {
        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.UpToDate, result);
        Assert.Equal(0, _client.Downloads);
    }

    [Fact]
    public async Task ReleaseAlreadyDownloaded_IsReady_WithoutDownloadingItAgain()
    {
        _client.Available = "1.1.0";
        _client.Pending = "1.1.0";

        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.Ready("1.1.0"), result);
        Assert.Equal(0, _client.Downloads);
    }

    [Fact]
    public async Task NetworkErrorWhileChecking_IsFailed_AndDoesNotThrow()
    {
        _client.CheckError = new HttpRequestException("offline");

        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.Failed, result);
    }

    [Fact]
    public async Task ErrorWhileDownloading_IsFailed_AndLeavesNothingPending()
    {
        _client.Available = "1.1.0";
        _client.DownloadError = new IOException("disk full");

        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.Failed, result);
        Assert.Null(_service.PendingVersion);
    }

    [Fact]
    public async Task NotInstalled_IsUnavailable_AndNeverTouchesTheFeed()
    {
        _client.Installed = false;
        _client.Available = "1.1.0";

        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.Unavailable, result);
        Assert.Equal(0, _client.Checks);
        Assert.False(_service.CanUpdate);
    }

    [Fact]
    public async Task UpdaterThatCannotTellIfInstalled_IsUnavailable()
    {
        var service = new UpdateService(new ThrowingInstallClient(), _scheduler.Schedule);

        Assert.False(service.CanUpdate);
        Assert.Equal(UpdateCheck.Unavailable, await service.CheckNowAsync());
    }

    [Fact]
    public async Task CheckWhileAnotherRuns_SharesIt()
    {
        _client.CheckGate = new TaskCompletionSource<string?>();

        var first = _service.CheckNowAsync();
        var second = _service.CheckNowAsync();
        _client.CheckGate.SetResult(null);

        Assert.Equal(UpdateCheck.UpToDate, await first);
        Assert.Equal(UpdateCheck.UpToDate, await second);
        Assert.Equal(1, _client.Checks);
    }

    [Fact]
    public async Task CheckAfterAFinishedOne_RunsAgain()
    {
        await _service.CheckNowAsync();
        _client.Available = "1.1.0";

        var result = await _service.CheckNowAsync();

        Assert.Equal(UpdateCheck.Ready("1.1.0"), result);
        Assert.Equal(2, _client.Checks);
    }

    [Fact]
    public void Start_ChecksAfterTheFirstDelay_ThenEveryInterval()
    {
        _service.Start();

        _scheduler.Advance(UpdateService.FirstCheckDelay - TimeSpan.FromSeconds(1));
        Assert.Equal(0, _client.Checks);

        _scheduler.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _client.Checks);

        _scheduler.Advance(UpdateService.CheckInterval);
        Assert.Equal(2, _client.Checks);

        _scheduler.Advance(UpdateService.CheckInterval);
        Assert.Equal(3, _client.Checks);
    }

    [Fact]
    public void Start_KeepsChecking_AfterAFailedCheck()
    {
        _client.CheckError = new HttpRequestException("offline");
        _service.Start();

        _scheduler.Advance(UpdateService.FirstCheckDelay);
        _scheduler.Advance(UpdateService.CheckInterval);

        Assert.Equal(2, _client.Checks);
    }

    [Fact]
    public void Start_WhenNotInstalled_SchedulesNothing()
    {
        _client.Installed = false;

        _service.Start();

        Assert.Equal(0, _scheduler.Scheduled);
    }

    [Fact]
    public void Dispose_StopsTheScheduledChecks()
    {
        _service.Start();

        _service.Dispose();
        _scheduler.Advance(UpdateService.FirstCheckDelay + UpdateService.CheckInterval);

        Assert.Equal(0, _client.Checks);
    }

    [Fact]
    public void ApplyOnExit_WithAPendingRelease_HandsItToTheUpdaterOnce()
    {
        _client.Pending = "1.1.0";

        _service.ApplyOnExit();
        _service.ApplyOnExit();

        Assert.Equal(1, _client.AppliedAfterExit);
        Assert.Equal(0, _client.Restarts);
    }

    [Fact]
    public void ApplyOnExit_WithNothingPending_DoesNothing()
    {
        _service.ApplyOnExit();

        Assert.Equal(0, _client.AppliedAfterExit);
    }

    [Fact]
    public void ApplyOnExit_UpdaterError_DoesNotThrow()
    {
        _client.Pending = "1.1.0";
        _client.ApplyError = new InvalidOperationException("Update.exe missing");

        var error = Record.Exception(_service.ApplyOnExit);

        Assert.Null(error);
    }

    [Fact]
    public void RestartToUpdate_WithAPendingRelease_AppliesAndRestarts()
    {
        _client.Pending = "1.1.0";

        _service.RestartToUpdate();

        Assert.Equal(1, _client.Restarts);
    }

    [Fact]
    public void RestartToUpdate_WithNothingPending_DoesNothing()
    {
        _service.RestartToUpdate();

        Assert.Equal(0, _client.Restarts);
    }

    [Fact]
    public void RestartToUpdate_UpdaterError_DoesNotThrow()
    {
        _client.Pending = "1.1.0";
        _client.ApplyError = new InvalidOperationException("locked");

        var error = Record.Exception(_service.RestartToUpdate);

        Assert.Null(error);
    }

    private sealed class ThrowingInstallClient : IUpdateClient
    {
        public bool CanUpdate => throw new InvalidOperationException("no locator");
        public string? PendingVersion => throw new InvalidOperationException("no locator");
        public Task<string?> CheckAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task DownloadAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
        public void ApplyAfterExit() => throw new InvalidOperationException();
        public void ApplyAndRestart() => throw new InvalidOperationException();
    }
}
