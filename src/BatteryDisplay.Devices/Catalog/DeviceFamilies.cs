using BatteryDisplay.Core;
using BatteryDisplay.Devices.Rapoo;
using BatteryDisplay.Devices.Razer;
using BatteryDisplay.Devices.Redragon;
using BatteryDisplay.Devices.SteelSeries;

namespace BatteryDisplay.Devices.Catalog;

/// <summary>Maps a catalog family to the provider that speaks its protocol.</summary>
public static class DeviceFamilies
{
    /// <summary>Each builder takes the entry and the PnP container of the device it found, if known.</summary>
    public static IReadOnlyDictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>> Default { get; } = Create();

    /// <summary>The same families, with <paramref name="log"/> passed to the providers that report events.</summary>
    public static IReadOnlyDictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>> Create(Action<string>? log = null) =>
        new Dictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>>
        {
            ["razer-hid"] = (entry, container) => new RazerHidProvider(entry, container),
            ["steelseries-arctis-nova-5"] = (entry, container) => new SteelSeriesArctisNova5Provider(entry, container),
            ["rapoo-hid"] = (entry, container) => new RapooHidProvider(entry, container),
            ["redragon-hid"] = (entry, container) => new RedragonHidProvider(entry, container, log: log),
        };
}
