using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Razer;
using BatteryDisplay.Devices.SteelSeries;
using Xunit.Abstractions;
using static BatteryDisplay.Devices.Tests.Catalog.CatalogMatcherTests;

namespace BatteryDisplay.Devices.Tests.Catalog;

public class CatalogDeviceSourceTests(ITestOutputHelper output)
{
    private sealed class FakeProvider(CatalogEntry entry) : IBatteryProvider
    {
        public string Id => entry.Id;
        public string Name => entry.Name;
        public DeviceKind Kind => entry.Kind;
        public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => Task.FromResult<BatteryStatus?>(null);
    }

    private static readonly CatalogEntry Mouse = Entry("mouse", 0x1532, 0x00DA, 0x00DB);
    private static readonly CatalogEntry Headset = Entry("headset", 0x1038, 0x2232) with { Family = "other" };

    private static readonly Dictionary<string, Func<CatalogEntry, IBatteryProvider>> FakeFamilies = new()
    {
        ["fam"] = e => new FakeProvider(e),
        ["other"] = e => new FakeProvider(e),
    };

    private static CatalogDeviceSource Source(
        IReadOnlyList<CatalogEntry> catalog,
        IReadOnlyDictionary<string, Func<CatalogEntry, IBatteryProvider>> families,
        params HidId[] present) =>
        new(catalog, families, () => present);

    [Fact]
    public void Returns_one_provider_per_present_entry_named_by_the_entry_id()
    {
        var source = Source([Mouse, Headset], FakeFamilies,
            new HidId(0x1532, 0x00DA), new HidId(0x1532, 0x00DB), new HidId(0x1038, 0x2232));

        var providers = source.Discover();

        Assert.Equal(new[] { "mouse", "headset" }, providers.Select(p => p.Id));
    }

    [Fact]
    public void Absent_devices_give_no_provider()
    {
        Assert.Empty(Source([Mouse, Headset], FakeFamilies).Discover());
    }

    [Fact]
    public void Entry_without_a_registered_family_is_skipped()
    {
        var orphan = Entry("orphan", 0x1532, 0x00DA) with { Family = "unknown" };
        var source = Source([orphan, Headset], FakeFamilies, new HidId(0x1532, 0x00DA), new HidId(0x1038, 0x2232));

        var providers = source.Discover();

        Assert.Equal(new[] { "headset" }, providers.Select(p => p.Id));
    }

    [Fact]
    public void Entry_whose_family_rejects_it_is_skipped()
    {
        var families = new Dictionary<string, Func<CatalogEntry, IBatteryProvider>>(FakeFamilies)
        {
            ["fam"] = _ => throw new FormatException("missing option"),
        };
        var source = Source([Mouse, Headset], families, new HidId(0x1532, 0x00DA), new HidId(0x1038, 0x2232));

        Assert.Equal(new[] { "headset" }, source.Discover().Select(p => p.Id));
    }

    [Fact]
    public void Enumeration_failure_gives_an_empty_list()
    {
        var source = new CatalogDeviceSource([Mouse], FakeFamilies, () => throw new IOException("hid stack gone"));

        Assert.Empty(source.Discover());
    }

    [Fact]
    public void Rediscovery_returns_the_same_provider_instance()
    {
        var source = Source([Mouse], FakeFamilies, new HidId(0x1532, 0x00DB));

        var first = Assert.Single(source.Discover());
        var second = Assert.Single(source.Discover());

        Assert.Same(first, second);
    }

    [Fact]
    public void Default_families_build_the_razer_and_steelseries_providers_from_the_embedded_catalog()
    {
        var source = Source(DeviceCatalog.LoadEmbedded(), DeviceFamilies.Default,
            new HidId(0x1532, 0x00DB), new HidId(0x1038, 0x2232));

        var providers = source.Discover();

        Assert.Collection(providers,
            p => Assert.IsType<RazerHidProvider>(p),
            p => Assert.IsType<SteelSeriesArctisNova5Provider>(p));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void Discovers_real_devices_without_throwing()
    {
        var providers = new CatalogDeviceSource().Discover();

        foreach (var provider in providers)
            output.WriteLine($"{provider.Id}: {provider.Name} ({provider.Kind})");
    }
}
