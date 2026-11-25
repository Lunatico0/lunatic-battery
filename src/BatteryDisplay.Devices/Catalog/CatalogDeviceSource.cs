using System.Collections.Concurrent;
using BatteryDisplay.Core;
using HidSharp;

namespace BatteryDisplay.Devices.Catalog;

/// <summary>Detects the catalog models whose HID collections are present right now.</summary>
public sealed class CatalogDeviceSource(
    IReadOnlyList<CatalogEntry> catalog,
    IReadOnlyDictionary<string, Func<CatalogEntry, IBatteryProvider>> families,
    Func<IEnumerable<HidId>> enumerate) : IDeviceSource
{
    // Same instance per entry across calls, so provider state survives rediscovery.
    private readonly ConcurrentDictionary<string, IBatteryProvider> _providers = new();

    public CatalogDeviceSource() : this(DeviceCatalog.LoadEmbedded(), DeviceFamilies.Default, EnumerateHid) { }

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        IReadOnlyList<CatalogEntry> present;
        try
        {
            present = CatalogMatcher.Match(catalog, enumerate());
        }
        catch (Exception)
        {
            return [];
        }

        var providers = new List<IBatteryProvider>();
        foreach (var entry in present)
        {
            if (TryProvider(entry) is { } provider)
                providers.Add(provider);
        }
        return providers;
    }

    private IBatteryProvider? TryProvider(CatalogEntry entry)
    {
        if (!families.TryGetValue(entry.Family, out var create))
            return null;

        try
        {
            return _providers.GetOrAdd(entry.Id, _ => create(entry));
        }
        catch (Exception)
        {
            // Entry with bad options for its family: skip it, keep the others.
            return null;
        }
    }

    private static List<HidId> EnumerateHid() =>
        DeviceList.Local.GetHidDevices().Select(d => new HidId(d.VendorID, d.ProductID)).ToList();
}
