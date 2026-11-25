using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Polling;

public interface ITrayIcon
{
    void Apply(TrayIconModel model);
}

/// <summary>
/// One polling cycle: read all providers and push the resulting model to each provider's icon.
/// Icons are updated on the caller's synchronization context (the UI thread when called from WinForms).
/// </summary>
public sealed class BatteryMonitor
{
    private readonly IReadOnlyList<IBatteryProvider> _providers;
    private readonly IReadOnlyList<ITrayIcon> _icons;
    private readonly BatteryPoller _poller;
    private readonly bool[] _everShown;
    private int _busy;

    public BatteryMonitor(IReadOnlyList<IBatteryProvider> providers, IReadOnlyList<ITrayIcon> icons, BatteryPoller poller)
    {
        if (providers.Count != icons.Count)
            throw new ArgumentException("Each provider needs exactly one icon.", nameof(icons));
        _providers = providers;
        _icons = icons;
        _poller = poller;
        _everShown = new bool[providers.Count];
    }

    /// <summary>Returns true when some icon became visible for the first time in this session. Overlapping calls are skipped.</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return false;
        try
        {
            var readings = await _poller.PollAsync(_providers, ct);
            ct.ThrowIfCancellationRequested();

            var newlyShown = false;
            for (var i = 0; i < _providers.Count; i++)
            {
                var model = TrayIconPresenter.Present(_providers[i].Name, _providers[i].Kind, readings[i]);
                _icons[i].Apply(model);
                if (model.Visible && !_everShown[i])
                {
                    _everShown[i] = true;
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
}
