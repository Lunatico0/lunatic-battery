using BatteryDisplay.Core;
using BatteryDisplay.Devices.Razer;
using BatteryDisplay.Devices.SteelSeries;

namespace BatteryDisplay.Devices.Catalog;

/// <summary>Maps a catalog family to the provider that speaks its protocol.</summary>
public static class DeviceFamilies
{
    public static IReadOnlyDictionary<string, Func<CatalogEntry, IBatteryProvider>> Default { get; } =
        new Dictionary<string, Func<CatalogEntry, IBatteryProvider>>
        {
            ["razer-hid"] = entry => new RazerHidProvider(entry),
            ["steelseries-arctis-nova-5"] = entry => new SteelSeriesArctisNova5Provider(entry),
        };
}
