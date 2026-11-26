namespace BatteryDisplay.App.Updates;

/// <summary>
/// Thin seam over the Velopack UpdateManager, so the decisions in <see cref="UpdateService"/> can be tested.
/// Any member may throw (network, not installed, another update in progress); the service absorbs it.
/// </summary>
public interface IUpdateClient
{
    /// <summary>False when the app runs from a build folder instead of a Velopack install, or no feed is configured.</summary>
    bool CanUpdate { get; }

    /// <summary>The version already downloaded and waiting to be applied, or null.</summary>
    string? PendingVersion { get; }

    /// <summary>Looks for a newer release; returns its version, or null when this one is the newest.</summary>
    Task<string?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the release the last <see cref="CheckAsync"/> found, which then becomes the pending one.</summary>
    Task DownloadAsync(CancellationToken cancellationToken);

    /// <summary>Starts the updater, which applies the pending release once this process exits and does not restart it.</summary>
    void ApplyAfterExit();

    /// <summary>Applies the pending release now and starts the app again; this process exits.</summary>
    void ApplyAndRestart();
}

/// <summary>Development runs and builds without a configured feed: nothing to check, nothing to apply.</summary>
public sealed class UnavailableUpdateClient : IUpdateClient
{
    public static readonly UnavailableUpdateClient Instance = new();

    private UnavailableUpdateClient()
    {
    }

    public bool CanUpdate => false;
    public string? PendingVersion => null;
    public Task<string?> CheckAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    public Task DownloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void ApplyAfterExit()
    {
    }

    public void ApplyAndRestart()
    {
    }
}
