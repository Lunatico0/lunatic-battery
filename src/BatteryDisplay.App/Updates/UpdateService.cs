using BatteryDisplay.App.Logging;

namespace BatteryDisplay.App.Updates;

public enum UpdateCheckStatus
{
    /// <summary>Not a Velopack install (a build folder) or no feed configured.</summary>
    Unavailable,
    UpToDate,
    /// <summary>A newer version is downloaded and waits to be applied.</summary>
    Ready,
    /// <summary>Network or updater error; the next scheduled check tries again.</summary>
    Failed,
}

public sealed record UpdateCheck(UpdateCheckStatus Status, string? Version = null)
{
    public static readonly UpdateCheck Unavailable = new(UpdateCheckStatus.Unavailable);
    public static readonly UpdateCheck UpToDate = new(UpdateCheckStatus.UpToDate);
    public static readonly UpdateCheck Failed = new(UpdateCheckStatus.Failed);
    public static UpdateCheck Ready(string version) => new(UpdateCheckStatus.Ready, version);
}

/// <summary>
/// When to look for updates and what to do with them. Checks <see cref="FirstCheckDelay"/> after <see cref="Start"/>
/// and then every <see cref="CheckInterval"/>, downloading a newer release silently. A downloaded release is applied
/// when the app exits through its menu (<see cref="ApplyOnExit"/>), when the user asks for it
/// (<see cref="RestartToUpdate"/>), or by Velopack itself on the next start. Nothing here throws: a build folder,
/// a missing feed or a network error only mean no update this time.
/// Schedule runs a callback after a delay and returns a handle that cancels it; the tray schedules on the UI thread.
/// </summary>
public sealed class UpdateService(IUpdateClient client, Func<TimeSpan, Action, IDisposable> schedule) : IDisposable
{
    // Lets the tray, the devices and the first refresh settle before going to the network.
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);
    // Four checks a day stay far below GitHub's 60 unauthenticated API calls per hour and address.
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private Task<UpdateCheck>? _inFlight;
    private IDisposable? _nextCheck;
    private bool _handedToUpdater;
    private bool _disposed;

    public bool CanUpdate => Safe(() => client.CanUpdate, false);

    public string? PendingVersion => CanUpdate ? Safe(() => client.PendingVersion, null) : null;

    /// <summary>Starts the background checks; does nothing outside a Velopack install.</summary>
    public void Start()
    {
        if (_disposed || !CanUpdate)
            return;
        ScheduleCheck(FirstCheckDelay);
    }

    /// <summary>Checks now and downloads what it finds; a check already running is shared, not repeated.</summary>
    public Task<UpdateCheck> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !CanUpdate)
            return Task.FromResult(UpdateCheck.Unavailable);
        if (_inFlight is { IsCompleted: false })
            return _inFlight;
        var check = RunCheckAsync(cancellationToken);
        _inFlight = check;
        return check;
    }

    /// <summary>On exit: hands a downloaded release to the updater, which applies it once the process is gone.</summary>
    public void ApplyOnExit()
    {
        if (_handedToUpdater || PendingVersion is null)
            return;
        _handedToUpdater = true;
        Try(client.ApplyAfterExit, "apply the update on exit");
    }

    /// <summary>Applies a downloaded release now and starts the app again.</summary>
    public void RestartToUpdate()
    {
        if (PendingVersion is null)
            return;
        Try(client.ApplyAndRestart, "restart to update");
    }

    private void ScheduleCheck(TimeSpan delay)
    {
        _nextCheck?.Dispose();
        _nextCheck = schedule(delay, () =>
        {
            if (_disposed)
                return;
            // The next check is booked before this one starts, so a hung or failed check never stops the cycle.
            ScheduleCheck(CheckInterval);
            _ = CheckNowAsync();
        });
    }

    private async Task<UpdateCheck> RunCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pending = client.PendingVersion;
            var newest = await client.CheckAsync(cancellationToken);
            if (newest is null)
                return pending is null ? UpdateCheck.UpToDate : UpdateCheck.Ready(pending);
            if (!string.Equals(newest, pending, StringComparison.OrdinalIgnoreCase))
                await client.DownloadAsync(cancellationToken);
            return UpdateCheck.Ready(newest);
        }
        catch (Exception ex)
        {
            AppLog.Write("updates", "Update check failed", ex);
            return UpdateCheck.Failed;
        }
    }

    private static T Safe<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            AppLog.Write("updates", "Updater unavailable", ex);
            return fallback;
        }
    }

    private static void Try(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppLog.Write("updates", $"Could not {what}", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _nextCheck?.Dispose();
        _nextCheck = null;
    }
}
