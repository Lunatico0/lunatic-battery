using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Redragon;

/// <summary>
/// Keeps the receiver's battery collection open on a background thread and folds every pushed frame into
/// <see cref="RedragonBatteryState"/>, because the keyboard answers no query: a poll can only return what was pushed.
/// Started by the first <see cref="Read"/>, so building a provider during discovery touches no device. It stops when
/// disposed, or by itself when nobody read it for <see cref="DefaultLease"/> (a provider discovery replaced), keeping
/// the reading for the next <see cref="Read"/>, which restarts it. When the collection goes away (receiver unplugged)
/// the reading is dropped and the collection is looked for again every retry delay. Only reads, never writes.
/// </summary>
public sealed class RedragonListener : IDisposable
{
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);

    // Several poll periods (one minute in the app), so only a provider nobody polls any more lets it stop.
    public static readonly TimeSpan DefaultLease = TimeSpan.FromMinutes(5);

    private readonly IRedragonHid _hid;
    private readonly int _vendorId;
    private readonly IReadOnlyList<int> _productIds;
    private readonly Func<RedragonCollection, bool> _isBatteryCollection;
    private readonly Action<string> _log;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _lease;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();

    private RedragonBatteryState _state = RedragonBatteryState.Unknown;
    private bool _framed;
    private long _lastRead;
    private Thread? _thread;
    private bool _disposed;

    public RedragonListener(IRedragonHid hid, int vendorId, IReadOnlyList<int> productIds,
        Func<RedragonCollection, bool> isBatteryCollection, Action<string> log,
        TimeSpan? retryDelay = null, TimeSpan? lease = null, TimeProvider? time = null)
    {
        _hid = hid;
        _vendorId = vendorId;
        _productIds = productIds;
        _isBatteryCollection = isBatteryCollection;
        _log = log;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
        _lease = lease ?? DefaultLease;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The reading the frames pushed so far give; null before the first one. Never blocks on the device.</summary>
    public BatteryStatus? Read()
    {
        lock (_gate)
        {
            if (_disposed)
                return null;

            Interlocked.Exchange(ref _lastRead, _time.GetTimestamp());
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "Redragon listener" };
                _thread.Start();
            }
            return _state.Reading;
        }
    }

    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            thread = _thread;
        }

        _stop.Cancel();
        // One read timeout is the longest the thread can take to notice; then its input is closed.
        thread?.Join(TimeSpan.FromMilliseconds(RedragonProtocol.ReadTimeoutMs * 3));
    }

    private void Run()
    {
        try
        {
            Listen();
        }
        catch (Exception ex)
        {
            // A background thread must never take the app down; the next Read starts a new one.
            _log($"listener failed: {ex.GetType().Name}: {ex.Message}");
            lock (_gate)
                _thread = null;
        }
    }

    private void Listen()
    {
        var waiting = false;
        while (true)
        {
            lock (_gate)
            {
                if (_disposed || LeaseExpired())
                {
                    if (!_disposed)
                        _log($"stopped: not read for {_lease.TotalMinutes:0} min");
                    _thread = null;
                    return;
                }
            }

            var opened = Open();
            if (opened is not { } found)
            {
                if (!waiting)
                    _log("battery collection not found, waiting for it");
                waiting = true;
                Forget();
                _stop.Token.WaitHandle.WaitOne(_retryDelay);
                continue;
            }

            waiting = false;
            _log($"listening on {found.Path}");
            using (found.Input)
            {
                if (Pump(found.Input))
                    continue;
            }

            _log("receiver gone, waiting for it");
            waiting = true;
            Forget();
        }
    }

    private (IRedragonInput Input, string Path)? Open()
    {
        try
        {
            foreach (var collection in _hid.FindCollections(_vendorId, _productIds).Where(_isBatteryCollection))
            {
                if (_hid.OpenInput(collection.Path, RedragonProtocol.ReadTimeoutMs) is { } input)
                    return (input, collection.Path);
            }
        }
        catch (Exception)
        {
            // Receiver unplugged while it was being opened: same as not found.
        }
        return null;
    }

    /// <summary>Reads until stopped or the lease runs out (true) or until the collection goes away (false).</summary>
    private bool Pump(IRedragonInput input)
    {
        while (!_stop.IsCancellationRequested && !LeaseExpired())
        {
            byte[]? report;
            try
            {
                report = input.Read();
            }
            catch (Exception)
            {
                return false;
            }

            if (report is not null && RedragonProtocol.TryParse(report, out var frame))
                Apply(frame);
        }
        return true;
    }

    private void Apply(RedragonFrame frame)
    {
        bool first;
        BatteryStatus? reading;
        lock (_gate)
        {
            _state = _state.Apply(frame);
            reading = _state.Reading;
            first = !_framed;
            _framed = true;
        }

        if (first)
            _log($"first frame received, reading {Describe(reading)}");
        if (frame is RedragonLinkFrame link)
            _log(link.Up ? "link up" : "link lost (keyboard off or asleep), keeping the last reading");
    }

    private void Forget()
    {
        lock (_gate)
        {
            _state = RedragonBatteryState.Unknown;
            _framed = false;
        }
    }

    private bool LeaseExpired() => _time.GetElapsedTime(Interlocked.Read(ref _lastRead)) > _lease;

    private static string Describe(BatteryStatus? reading) =>
        reading is null ? "none yet" : $"{reading.Percent}% {reading.Charge}";
}
