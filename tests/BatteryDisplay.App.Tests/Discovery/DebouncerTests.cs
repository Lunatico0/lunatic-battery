using BatteryDisplay.App.Discovery;

namespace BatteryDisplay.App.Tests.Discovery;

public class DebouncerTests
{
    /// <summary>Manual clock: callbacks run only when Advance moves past their due time.</summary>
    private sealed class FakeScheduler
    {
        private readonly List<(TimeSpan Due, Action Callback, Handle Handle)> _pending = [];
        private TimeSpan _now;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            var handle = new Handle();
            _pending.Add((_now + delay, callback, handle));
            return handle;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var item in _pending.Where(p => p.Due <= _now).ToList())
            {
                _pending.Remove(item);
                if (!item.Handle.Cancelled)
                    item.Callback();
            }
        }

        public sealed class Handle : IDisposable
        {
            public bool Cancelled { get; private set; }
            public void Dispose() => Cancelled = true;
        }
    }

    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2);

    private readonly FakeScheduler _scheduler = new();
    private int _fired;

    private Debouncer Create() => new(Delay, () => _fired++, _scheduler.Schedule);

    [Fact]
    public void SingleSignal_FiresOnceAfterTheDelay()
    {
        var debouncer = Create();

        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Equal(0, _fired);

        _scheduler.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, _fired);

        _scheduler.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, _fired);
    }

    [Fact]
    public void BurstOfSignals_FiresOnce_TheDelayAfterTheLastSignal()
    {
        var debouncer = Create();

        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromSeconds(1.5));
        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromSeconds(1.5));
        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromSeconds(1.9));
        Assert.Equal(0, _fired);

        _scheduler.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, _fired);
    }

    [Fact]
    public void SignalsFurtherApartThanTheDelay_EachFire()
    {
        var debouncer = Create();

        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromSeconds(3));
        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(2, _fired);
    }

    [Fact]
    public void Dispose_CancelsThePendingSignal_AndIgnoresLaterOnes()
    {
        var debouncer = Create();

        debouncer.Signal();
        debouncer.Dispose();
        debouncer.Signal();
        _scheduler.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(0, _fired);
    }
}
