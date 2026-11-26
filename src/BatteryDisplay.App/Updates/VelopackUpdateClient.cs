using BatteryDisplay.App.Logging;
using Velopack;
using Velopack.Sources;

namespace BatteryDisplay.App.Updates;

/// <summary>
/// <see cref="IUpdateClient"/> over the Velopack UpdateManager and the GitHub releases of <see cref="AppRepository"/>.
/// No logic of its own; <see cref="UpdateService"/> decides when to call it and absorbs what it throws.
/// </summary>
internal sealed class VelopackUpdateClient : IUpdateClient
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _found;

    private VelopackUpdateClient(string repositoryUrl) =>
        _manager = new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));

    /// <summary>The client for this build: unavailable when no repository is set or Velopack cannot start.</summary>
    public static IUpdateClient Create(string? repositoryUrl)
    {
        if (repositoryUrl is null)
            return UnavailableUpdateClient.Instance;
        try
        {
            return new VelopackUpdateClient(repositoryUrl);
        }
        catch (Exception ex)
        {
            AppLog.Write("updates", "Updates disabled", ex);
            return UnavailableUpdateClient.Instance;
        }
    }

    public bool CanUpdate => _manager.IsInstalled;

    public string? PendingVersion => _manager.UpdatePendingRestart?.Version?.ToString();

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        _found = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
        return _found?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(CancellationToken cancellationToken) =>
        _found is null ? Task.CompletedTask : _manager.DownloadUpdatesAsync(_found, cancelToken: cancellationToken);

    public void ApplyAfterExit()
    {
        if (_manager.UpdatePendingRestart is { } pending)
            _manager.WaitExitThenApplyUpdates(pending, silent: true, restart: false);
    }

    public void ApplyAndRestart()
    {
        if (_manager.UpdatePendingRestart is { } pending)
            _manager.ApplyUpdatesAndRestart(pending);
    }
}
