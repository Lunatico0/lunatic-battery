using System.Collections.Immutable;
using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Polling;

/// <summary>
/// One polling cycle: rediscover the connected devices, keep one entry per provider Id, read them all and publish
/// an immutable snapshot list. Discovery runs on the thread pool; the list is published on the caller's
/// synchronization context (the UI thread when called from WinForms). A settings change republishes the display
/// names without reading again. A device keeps one Id whichever source shows it: a merged device teaches the store
/// that the other sources' Ids are aliases of its own (see <see cref="AppSettings.DeviceAliases"/>). The first cycle
/// logs each phase (discovery, every reading, the publish), so a tray that never fills can be told apart on another PC.
/// </summary>
public sealed class BatteryMonitor : IDeviceSnapshots, IDisposable
{
    private sealed class Entry(string id, IBatteryProvider provider)
    {
        public string Id { get; } = id;
        public IBatteryProvider Provider { get; set; } = provider;
        public BatteryStatus? Status { get; set; }
        public DateTimeOffset? LastReadAt { get; set; }
    }

    private readonly IDeviceSource _source;
    private readonly BatteryPoller _poller;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;

    // List order is first appearance: Windows lays tray icons out in creation order.
    private readonly List<Entry> _entries = [];
    private int _requests;
    private bool _firstCycleDone;
    private volatile bool _disposed;

    public BatteryMonitor(IDeviceSource source, BatteryPoller poller, ISettingsStore settings, TimeProvider? time = null,
        Action<string>? log = null)
    {
        _source = source;
        _poller = poller;
        _settings = settings;
        _time = time ?? TimeProvider.System;
        _log = log ?? AppLog.For("monitor");
        _settings.Changed += OnSettingsChanged;
    }

    public IReadOnlyList<DeviceSnapshot> Devices { get; private set; } = ImmutableArray<DeviceSnapshot>.Empty;

    public event EventHandler<IReadOnlyList<DeviceSnapshot>>? DevicesChanged;

    /// <summary>
    /// Calls never overlap: one made while a refresh runs returns at once, and the running refresh does one more
    /// cycle when it ends, so a hot-plug that arrives mid-discovery is not lost. Any number of such calls during
    /// one cycle add a single extra cycle. <see cref="DevicesChanged"/> is raised only when the list differs, except
    /// after the first cycle, which always raises it (even with nothing connected) so the tray stops "searching".
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct)
    {
        // _requests counts calls since the running cycle started; only the call that moves it from 0 runs.
        if (_disposed || Interlocked.Increment(ref _requests) != 1)
            return;
        try
        {
            while (true)
            {
                await RunCycleAsync(ct);
                if (_disposed || Interlocked.CompareExchange(ref _requests, 0, 1) == 1)
                    return;
                // Requests arrived during the cycle: fold them all into one more.
                Interlocked.Exchange(ref _requests, 1);
            }
        }
        catch
        {
            Interlocked.Exchange(ref _requests, 0);
            throw;
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var first = !_firstCycleDone;
        var started = _time.GetTimestamp();
        var discovered = await Task.Run(() => _source.Discover(), ct);
        ct.ThrowIfCancellationRequested();
        if (_disposed)
            return;
        if (first)
            _log($"first cycle: discovered {discovered.Count} device(s) in {_time.GetElapsedTime(started).TotalMilliseconds:0} ms");
        Reconcile(discovered);

        var entries = _entries.ToArray();
        var readings = await _poller.PollAsync(entries.Select(e => e.Provider).ToArray(), ct);
        ct.ThrowIfCancellationRequested();
        if (_disposed)
            return;

        var now = _time.GetUtcNow();
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i].Status = readings[i];
            if (readings[i] is not null)
                entries[i].LastReadAt = now;
        }
        if (first)
        {
            foreach (var entry in entries)
                _log($"first cycle: {entry.Id} \"{entry.Provider.Name}\" via {entry.Provider.Source}: {Describe(entry.Status)}");
        }
        _firstCycleDone = true;
        Publish(always: first);
        if (first)
            _log($"first cycle: published {Devices.Count} device(s) after {_time.GetElapsedTime(started).TotalMilliseconds:0} ms");
    }

    private static string Describe(BatteryStatus? status) => status switch
    {
        null => "no reading",
        { Percent: { } percent, Charge: var charge } => $"{percent}% {charge}",
        { Charge: var charge } => $"no level, {charge}",
    };

    // Vanished devices leave the list (and forget their last reading); present ones read through the latest
    // provider instance; new ones go at the end. A repeated Id within one discovery keeps the first provider.
    private void Reconcile(IReadOnlyList<IBatteryProvider> discovered)
    {
        LearnAliases(discovered);
        var settings = _settings.Current;
        var discoveredIds = discovered.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

        var latest = new Dictionary<string, IBatteryProvider>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var provider in discovered)
        {
            var id = StableIdOf(provider.Id, settings, discoveredIds);
            if (latest.TryAdd(id, provider))
                order.Add(id);
        }

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (latest.Remove(_entries[i].Id, out var current))
                _entries[i].Provider = current;
            else
                _entries.RemoveAt(i);
        }

        foreach (var id in order)
        {
            if (latest.TryGetValue(id, out var provider))
                _entries.Add(new Entry(id, provider));
        }
    }

    // A source that once merged into another device shows under that device's Id when it comes alone (the most
    // direct source gone), so the user's settings follow the physical device. Never while the device it points to
    // shows on its own, or the two would fold into one entry and one reading would hide the other.
    private static string StableIdOf(string id, AppSettings settings, HashSet<string> discoveredIds) =>
        settings.CanonicalIdOf(id) is var canonical && canonical != id && !discoveredIds.Contains(canonical) ? canonical : id;

    // Saved before the entries change and only when something new was learned, so a stable merge writes nothing.
    private void LearnAliases(IReadOnlyList<IBatteryProvider> discovered)
    {
        var current = _settings.Current;
        var updated = current;
        foreach (var merged in discovered.OfType<MergedProvider>())
        {
            foreach (var alias in merged.MergedIds)
                updated = updated.WithAlias(alias, merged.Id);
        }
        if (!ReferenceEquals(updated, current))
            _settings.Save(updated);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (!_disposed)
            Publish();
    }

    private void Publish(bool always = false)
    {
        var settings = _settings.Current;
        // Boxed once, so Devices and the event argument are the same instance.
        IReadOnlyList<DeviceSnapshot> devices = _entries
            .Select(e => new DeviceSnapshot(e.Id, e.Provider.Name, settings.DisplayNameOf(e.Id, e.Provider.Name),
                e.Provider.Kind, e.Provider.Source, e.Status, e.LastReadAt))
            .ToImmutableArray();
        if (!always && devices.SequenceEqual(Devices))
            return;
        Devices = devices;
        DevicesChanged?.Invoke(this, devices);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        _entries.Clear();
    }
}
