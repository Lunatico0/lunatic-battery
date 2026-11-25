using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Catalog;

/// <summary>One model of the device catalog. Options are family specific (Razer transaction id, HID interface, ...).</summary>
public sealed record CatalogEntry(
    string Id, string Family, string Name, DeviceKind Kind, int VendorId,
    IReadOnlyList<int> ProductIds, IReadOnlyDictionary<string, long> Options)
{
    public long RequireOption(string key) =>
        Options.TryGetValue(key, out var value)
            ? value
            : throw new FormatException($"Catalog entry '{Id}' needs option '{key}' for family '{Family}'.");
}
