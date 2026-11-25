using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Polling;

public interface ITrayIcon : IDisposable
{
    void Apply(TrayIconModel model);
}

/// <summary>
/// One polling cycle: rediscover the connected devices, keep one icon per provider Id, then read all providers
/// and push each model to its icon. Discovery runs on the thread pool; icons are created, updated and disposed
/// on the caller's synchronization context (the UI thread when called from WinForms).
/// </summary>
public sealed class BatteryMonitor(IDeviceSource source, Func<IBatteryProvider, ITrayIcon> createIcon, BatteryPoller poller) : IDisposable
{
    private sealed class Entry(IBatteryProvider provider, ITrayIcon icon)
    {
        public IBatteryProvider Provider { get; set; } = provider;
        public ITrayIcon Icon { get; } = icon;
        public bool EverShown { get; set; }
    }

    // Icon order is first appearance: Windows lays tray icons out in creation order.
    private readonly List<Entry> _entries = [];
    private int _busy;
    private volatile bool _disposed;

    public IReadOnlyList<string> DeviceIds => _entries.Select(e => e.Provider.Id).ToList();

    /// <summary>Returns true when some icon became visible for the first time since it was created. Overlapping calls are skipped.</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        if (_disposed || Interlocked.Exchange(ref _busy, 1) == 1)
            return false;
        try
        {
            var discovered = await Task.Run(() => source.Discover(), ct);
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                return false;
            Reconcile(discovered);

            var entries = _entries.ToArray();
            var readings = await poller.PollAsync(entries.Select(e => e.Provider).ToArray(), ct);
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                return false;

            var newlyShown = false;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var model = TrayIconPresenter.Present(entry.Provider.Name, entry.Provider.Kind, readings[i]);
                entry.Icon.Apply(model);
                if (model.Visible && !entry.EverShown)
                {
                    entry.EverShown = true;
                    newlyShown = true;
                }
            }
            return newlyShown;
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    // Vanished devices lose their icon; present ones keep it and read through the latest provider instance;
    // new ones get an icon at the end. A repeated Id within one discovery keeps the first provider.
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
            var entry = _entries[i];
            if (latest.Remove(entry.Provider.Id, out var current))
            {
                entry.Provider = current;
                continue;
            }
            _entries.RemoveAt(i);
            entry.Icon.Dispose();
        }

        foreach (var provider in order)
        {
            if (latest.ContainsKey(provider.Id))
                _entries.Add(new Entry(provider, createIcon(provider)));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var entry in _entries)
            entry.Icon.Dispose();
        _entries.Clear();
    }
}
