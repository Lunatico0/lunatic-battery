namespace BatteryDisplay.Devices.Catalog;

/// <summary>Vendor and product id of one HID collection present on the system.</summary>
public readonly record struct HidId(int VendorId, int ProductId);

/// <summary>One present HID collection: its ids and the interface path Windows knows it by.</summary>
public readonly record struct HidCollection(HidId Id, string Path);

public static class CatalogMatcher
{
    /// <summary>
    /// Catalog entries with at least one present collection, in catalog order. A model seen through
    /// several product ids or collections (cable and dongle) appears once.
    /// </summary>
    public static IReadOnlyList<CatalogEntry> Match(IEnumerable<CatalogEntry> catalog, IEnumerable<HidId> present)
    {
        var ids = present.ToHashSet();
        return catalog.Where(e => e.ProductIds.Any(pid => ids.Contains(new HidId(e.VendorId, pid)))).ToList();
    }
}
