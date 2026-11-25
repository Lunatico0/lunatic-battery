using System.Diagnostics;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Discovery;

/// <summary>
/// Composes every detection mechanism into one list: sources in order, the first provider wins on a repeated Id,
/// and a source that throws anyway is logged and skipped so the others still show.
/// </summary>
public sealed class DeviceDiscovery(IReadOnlyList<IDeviceSource> sources, Action<string>? log = null) : IDeviceSource
{
    private readonly Action<string> _log = log ?? (message => Debug.WriteLine(message, "BatteryDisplay"));

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        var found = new List<IBatteryProvider>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            try
            {
                foreach (var provider in source.Discover())
                {
                    if (seen.Add(provider.Id))
                        found.Add(provider);
                }
            }
            catch (Exception ex)
            {
                _log($"{source.GetType().Name}: discovery failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return found;
    }
}
