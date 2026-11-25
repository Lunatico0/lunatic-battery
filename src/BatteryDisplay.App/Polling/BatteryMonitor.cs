using System.Collections.Immutable;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Polling;

/// <summary>
/// One polling cycle: rediscover the connected devices, keep one entry per provider Id, read them all and publish
/// an immutable snapshot list. Discovery runs on the thread pool; the list is published on the caller's
/// synchronization context (the UI thread when called from WinForms). A settings change republishes the display
/// names without reading again.
/// </summary>
public sealed class BatteryMonitor : IDeviceSnapshots, IDisposable
{
    private sealed class Entry(IBatteryProvider provider)
    {
        public IBatteryProvider Provider { get; set; } = provider;
        public BatteryStatus? Status { get; set; }
        public DateTimeOffset? LastReadAt { get; set; }
    }

    private readonly IDeviceSource _source;
    private readonly BatteryPoller _poller;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;

    // List order is first appearance: Windows lays tray icons out in creation order.
    private readonly List<Entry> _entries = [];
    private int _busy;
    private volatile bool _disposed;

    public BatteryMonitor(IDeviceSource source, BatteryPoller poller, ISettingsStore settings, TimeProvider? time = null)
    {
        _source = source;
        _poller = poller;
        _settings = settings;
        _time = time ?? TimeProvider.System;
        _settings.Changed += OnSettingsChanged;
    }

    public IReadOnlyList<DeviceSnapshot> Devices { get; private set; } = ImmutableArray<DeviceSnapshot>.Empty;

    public event EventHandler<IReadOnlyList<DeviceSnapshot>>? DevicesChanged;

    /// <summary>Overlapping calls are skipped. <see cref="DevicesChanged"/> is raised only when the list differs.</summary>
    public async Task RefreshAsync(CancellationToken ct)
    {
        if (_disposed || Interlocked.Exchange(ref _busy, 1) == 1)
            return;
        try
        {
            var discovered = await Task.Run(() => _source.Discover(), ct);
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                return;
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
            Publish();
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    // Vanished devices leave the list (and forget their last reading); present ones read through the latest
    // provider instance; new ones go at the end. A repeated Id within one discovery keeps the first provider.
    private void Reconcile(IReadOnlyList<IBatteryProvider> discovered)
    {
        var latest = new Dictionary<string, IBatteryProvider>(StringComparer.Ordinal);
        var order = new List<IBatteryProvider>();
        foreach (var provider in discovered)
        {
            if (latest.TryAdd(provider.Id, provider))
                order.Add(provider);
        }

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (latest.Remove(_entries[i].Provider.Id, out var current))
                _entries[i].Provider = current;
            else
                _entries.RemoveAt(i);
        }

        foreach (var provider in order)
        {
            if (latest.ContainsKey(provider.Id))
                _entries.Add(new Entry(provider));
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (!_disposed)
            Publish();
    }

    private void Publish()
    {
        var settings = _settings.Current;
        // Boxed once, so Devices and the event argument are the same instance.
        IReadOnlyList<DeviceSnapshot> devices = _entries
            .Select(e => new DeviceSnapshot(e.Provider.Id, e.Provider.Name, settings.DisplayNameOf(e.Provider.Id, e.Provider.Name),
                e.Provider.Kind, e.Provider.Source, e.Status, e.LastReadAt))
            .ToImmutableArray();
        if (devices.SequenceEqual(Devices))
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
