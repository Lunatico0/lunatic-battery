using System.Collections.Concurrent;
using BatteryDisplay.Core;
using BatteryDisplay.Devices.Pnp;
using HidSharp;

namespace BatteryDisplay.Devices.Catalog;

/// <summary>
/// Detects the catalog models whose HID collections are present right now. With a container resolver, each
/// provider also names the physical device it found (PnP ContainerId), so discovery can merge it with the same
/// device seen by another source.
/// </summary>
public sealed class CatalogDeviceSource(
    IReadOnlyList<CatalogEntry> catalog,
    IReadOnlyDictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>> families,
    Func<IEnumerable<HidCollection>> enumerate,
    IHidContainerResolver? containers = null) : IDeviceSource
{
    // Same instance per entry across calls, so provider state survives rediscovery.
    private readonly ConcurrentDictionary<string, IBatteryProvider> _providers = new();

    /// <param name="log">Where providers that listen in the background report their events; dropped when null.</param>
    public CatalogDeviceSource(Action<string>? log = null)
        : this(DeviceCatalog.LoadEmbedded(), DeviceFamilies.Create(log), EnumerateHid, new CfgMgrHidContainerResolver()) { }

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        List<HidCollection> collections;
        IReadOnlyList<CatalogEntry> present;
        try
        {
            collections = enumerate().ToList();
            present = CatalogMatcher.Match(catalog, collections.Select(c => c.Id));
        }
        catch (Exception)
        {
            return [];
        }

        var providers = new List<IBatteryProvider>();
        foreach (var entry in present)
        {
            if (TryProvider(entry, ContainerOf(entry, collections)) is { } provider)
                providers.Add(provider);
        }
        return providers;
    }

    /// <summary>
    /// Container of the first collection, in catalog product order (the order reads try), that names a real device.
    /// A failed lookup only costs the identity: the device still shows, just never merged.
    /// </summary>
    private Guid? ContainerOf(CatalogEntry entry, List<HidCollection> collections)
    {
        if (containers is null)
            return null;

        foreach (var pid in entry.ProductIds)
        {
            var id = new HidId(entry.VendorId, pid);
            foreach (var collection in collections.Where(c => c.Id == id))
            {
                Guid? container;
                try
                {
                    container = containers.ContainerOf(collection.Path);
                }
                catch (Exception)
                {
                    container = null;
                }
                if (PhysicalIdentity.Identifies(container))
                    return container;
            }
        }
        return null;
    }

    private IBatteryProvider? TryProvider(CatalogEntry entry, Guid? container)
    {
        if (!families.TryGetValue(entry.Family, out var create))
            return null;

        try
        {
            // A new container means another physical device behind the entry (or the dongle on another port),
            // so the provider is rebuilt to carry it; otherwise the known instance stays.
            if (_providers.TryGetValue(entry.Id, out var known) && known.ContainerId == container)
                return known;
            return _providers[entry.Id] = create(entry, container);
        }
        catch (Exception)
        {
            // Entry with bad options for its family: skip it, keep the others.
            return null;
        }
    }

    private static List<HidCollection> EnumerateHid() =>
        DeviceList.Local.GetHidDevices()
            .Select(d => new HidCollection(new HidId(d.VendorID, d.ProductID), d.DevicePath))
            .ToList();
}
