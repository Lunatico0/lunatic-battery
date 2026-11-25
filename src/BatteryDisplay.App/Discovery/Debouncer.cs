namespace BatteryDisplay.App.Discovery;

/// <summary>
/// Runs the action once, the delay after the last of a burst of signals.
/// Schedule runs a callback after a delay and returns a handle that cancels it; the tray schedules on the UI thread.
/// </summary>
public sealed class Debouncer(TimeSpan delay, Action action, Func<TimeSpan, Action, IDisposable> schedule) : IDisposable
{
    private IDisposable? _pending;
    private int _generation;
    private bool _disposed;

    public void Signal()
    {
        if (_disposed)
            return;
        _pending?.Dispose();
        // A superseded callback that still fires is ignored by its stale generation.
        var generation = ++_generation;
        _pending = schedule(delay, () =>
        {
            if (_disposed || generation != _generation)
                return;
            _pending = null;
            action();
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pending?.Dispose();
        _pending = null;
    }
}
