namespace BatteryDisplay.App.Presentation;

public interface ITrayIcon : IDisposable
{
    void Apply(TrayIconModel model);
}

/// <summary>One tray icon to show: a stable key (a device Id or <see cref="TrayIconPlanner.CombinedKey"/>) and its model.</summary>
public sealed record TrayIconSlot(string Key, TrayIconModel Model);

/// <summary>
/// Keeps one icon per slot key. Icons are created, updated and disposed on the caller's thread (the UI thread).
/// Windows lays tray icons out in creation order, so surviving icons keep their place and new ones go at the end.
/// </summary>
public sealed class TrayIconSet(Func<string, ITrayIcon> createIcon) : IDisposable
{
    private sealed class Entry(string key, ITrayIcon icon)
    {
        public string Key { get; } = key;
        public ITrayIcon Icon { get; } = icon;
        public bool EverShown { get; set; }
    }

    private readonly List<Entry> _entries = [];
    private bool _disposed;

    public IReadOnlyList<string> Keys => _entries.Select(e => e.Key).ToArray();

    /// <summary>
    /// Disposes icons whose key is gone, creates icons for new keys and applies every model. A repeated key keeps
    /// its first slot. Returns true when some icon became visible for the first time since it was created.
    /// </summary>
    public bool Apply(IReadOnlyList<TrayIconSlot> slots)
    {
        if (_disposed)
            return false;

        var wanted = new Dictionary<string, TrayIconModel>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var slot in slots)
        {
            if (wanted.TryAdd(slot.Key, slot.Model))
                order.Add(slot.Key);
        }

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (wanted.ContainsKey(_entries[i].Key))
                continue;
            _entries[i].Icon.Dispose();
            _entries.RemoveAt(i);
        }

        var known = _entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in order)
        {
            if (!known.Contains(key))
                _entries.Add(new Entry(key, createIcon(key)));
        }

        var newlyShown = false;
        foreach (var entry in _entries)
        {
            var model = wanted[entry.Key];
            entry.Icon.Apply(model);
            if (model.Visible && !entry.EverShown)
            {
                entry.EverShown = true;
                newlyShown = true;
            }
        }
        return newlyShown;
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
