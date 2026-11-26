using BatteryDisplay.App.Logging;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Polling;

/// <summary>Reads every provider in parallel on the thread pool, each bounded by a timeout. A failure or timeout yields null.</summary>
public sealed class BatteryPoller(TimeSpan timeout, Action<string>? log = null)
{
    private readonly Action<string> _log = log ?? AppLog.For("poller");

    public Task<BatteryStatus?[]> PollAsync(IReadOnlyList<IBatteryProvider> providers, CancellationToken ct) =>
        Task.WhenAll(providers.Select(p => ReadOneAsync(p, ct)));

    private async Task<BatteryStatus?> ReadOneAsync(IBatteryProvider provider, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Task.Run keeps synchronous work inside a provider off the caller (UI) thread;
            // WaitAsync stops waiting even when a provider ignores its token.
            return await Task.Run(() => provider.ReadAsync(cts.Token), CancellationToken.None)
                .WaitAsync(timeout, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            _log($"{provider.Id}: no answer within {timeout.TotalSeconds:0.#} s");
            CancelQuietly(cts);
            return null;
        }
        catch (Exception ex)
        {
            _log($"{provider.Id}: read failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private void CancelQuietly(CancellationTokenSource cts)
    {
        try
        {
            cts.Cancel();
        }
        catch (AggregateException ex)
        {
            _log($"provider cancellation callback failed: {ex.InnerException?.Message}");
        }
    }
}
