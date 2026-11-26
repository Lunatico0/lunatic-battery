using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Pnp;
using BatteryDisplay.Devices.Rapoo;
using BatteryDisplay.Devices.Razer;
using BatteryDisplay.Devices.Redragon;
using BatteryDisplay.Devices.SteelSeries;
using Xunit.Abstractions;
using static BatteryDisplay.Devices.Tests.Catalog.CatalogMatcherTests;

namespace BatteryDisplay.Devices.Tests.Catalog;

public class CatalogDeviceSourceTests(ITestOutputHelper output)
{
    private sealed class FakeProvider(CatalogEntry entry, Guid? container) : IBatteryProvider
    {
        public string Id => entry.Id;
        public string Name => entry.Name;
        public DeviceKind Kind => entry.Kind;
        public Guid? ContainerId => container;
        public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => Task.FromResult<BatteryStatus?>(null);
    }

    /// <summary>Answers from a path to container map; a path it does not know has no container.</summary>
    private sealed class FakeContainers : IHidContainerResolver
    {
        public Dictionary<string, Guid?> ByPath { get; } = [];
        public Func<string, Guid?>? Override { get; set; }

        public Guid? ContainerOf(string devicePath) =>
            Override is { } answer ? answer(devicePath) : ByPath.GetValueOrDefault(devicePath);
    }

    private static readonly CatalogEntry Mouse = Entry("mouse", 0x1532, 0x00DA, 0x00DB);
    private static readonly CatalogEntry Headset = Entry("headset", 0x1038, 0x2232) with { Family = "other" };

    private static readonly Guid Dongle = new("c5cc23df-e5b5-11f1-beb0-f44efc8ff591");
    private static readonly Guid OtherPort = new("d2a1c7e0-0b5e-11f2-beb2-f44efc8ff591");

    private static readonly Dictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>> FakeFamilies = new()
    {
        ["fam"] = (e, c) => new FakeProvider(e, c),
        ["other"] = (e, c) => new FakeProvider(e, c),
    };

    private static string PathOf(HidId id) => $@"\\?\hid#vid_{id.VendorId:x4}&pid_{id.ProductId:x4}";

    private static CatalogDeviceSource Source(
        IReadOnlyList<CatalogEntry> catalog,
        IReadOnlyDictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>> families,
        params HidId[] present) =>
        Source(catalog, families, null, present);

    private static CatalogDeviceSource Source(
        IReadOnlyList<CatalogEntry> catalog,
        IReadOnlyDictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>> families,
        IHidContainerResolver? containers,
        params HidId[] present) =>
        new(catalog, families, () => present.Select(id => new HidCollection(id, PathOf(id))), containers);

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
        var families = new Dictionary<string, Func<CatalogEntry, Guid?, IBatteryProvider>>(FakeFamilies)
        {
            ["fam"] = (_, _) => throw new FormatException("missing option"),
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
    public void Provider_carries_the_container_of_its_collection()
    {
        var containers = new FakeContainers { ByPath = { [PathOf(new HidId(0x1532, 0x00DB))] = Dongle } };
        var source = Source([Mouse], FakeFamilies, containers, new HidId(0x1532, 0x00DB));

        Assert.Equal(Dongle, Assert.Single(source.Discover()).ContainerId);
    }

    [Fact]
    public void Container_comes_from_the_first_collection_that_identifies_a_device_in_catalog_order()
    {
        var containers = new FakeContainers
        {
            ByPath =
            {
                [PathOf(new HidId(0x1532, 0x00DA))] = PhysicalIdentity.LocalMachine,
                [PathOf(new HidId(0x1532, 0x00DB))] = Dongle,
            },
        };
        var source = Source([Mouse], FakeFamilies, containers, new HidId(0x1532, 0x00DB), new HidId(0x1532, 0x00DA));

        Assert.Equal(Dongle, Assert.Single(source.Discover()).ContainerId);
    }

    [Fact]
    public void Container_follows_catalog_product_order_not_enumeration_order()
    {
        var containers = new FakeContainers
        {
            ByPath =
            {
                [PathOf(new HidId(0x1532, 0x00DA))] = OtherPort,
                [PathOf(new HidId(0x1532, 0x00DB))] = Dongle,
            },
        };
        var source = Source([Mouse], FakeFamilies, containers, new HidId(0x1532, 0x00DB), new HidId(0x1532, 0x00DA));

        Assert.Equal(OtherPort, Assert.Single(source.Discover()).ContainerId);
    }

    [Fact]
    public void Container_lookup_failure_still_shows_the_device_without_identity()
    {
        var containers = new FakeContainers { Override = _ => throw new InvalidOperationException("property store gone") };
        var source = Source([Mouse, Headset], FakeFamilies, containers, new HidId(0x1532, 0x00DB), new HidId(0x1038, 0x2232));

        var providers = source.Discover();

        Assert.Equal(new[] { "mouse", "headset" }, providers.Select(p => p.Id));
        Assert.All(providers, p => Assert.Null(p.ContainerId));
    }

    [Fact]
    public void Rediscovery_with_the_same_container_returns_the_same_provider_instance()
    {
        var containers = new FakeContainers { Override = _ => Dongle };
        var source = Source([Mouse], FakeFamilies, containers, new HidId(0x1532, 0x00DB));

        Assert.Same(Assert.Single(source.Discover()), Assert.Single(source.Discover()));
    }

    [Fact]
    public void Device_that_moved_to_another_container_gets_a_provider_with_the_new_identity()
    {
        var containers = new FakeContainers { Override = _ => Dongle };
        var source = Source([Mouse], FakeFamilies, containers, new HidId(0x1532, 0x00DB));
        var before = Assert.Single(source.Discover());

        containers.Override = _ => OtherPort;
        var after = Assert.Single(source.Discover());

        Assert.NotSame(before, after);
        Assert.Equal("mouse", after.Id);
        Assert.Equal(OtherPort, after.ContainerId);
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
    public void Default_families_build_the_rapoo_provider_for_its_dongle()
    {
        var source = Source(DeviceCatalog.LoadEmbedded(), DeviceFamilies.Default, new HidId(0x24AE, 0x1211));

        var provider = Assert.IsType<RapooHidProvider>(Assert.Single(source.Discover()));
        Assert.Equal("rapoo-gaming-mouse", provider.Id);
    }

    [Fact]
    public void Default_families_build_the_redragon_provider_for_its_receiver()
    {
        var source = Source(DeviceCatalog.LoadEmbedded(), DeviceFamilies.Default, new HidId(0x3554, 0xFA09));

        var provider = Assert.IsType<RedragonHidProvider>(Assert.Single(source.Discover()));
        Assert.Equal("redragon-k673", provider.Id);
        provider.Dispose();
    }

    [Fact]
    public void Default_families_give_vendor_hid_providers_that_carry_the_container()
    {
        var containers = new FakeContainers { Override = _ => Dongle };
        var source = Source(DeviceCatalog.LoadEmbedded(), DeviceFamilies.Default, containers,
            new HidId(0x1532, 0x00DB), new HidId(0x1038, 0x2232));

        var providers = source.Discover();

        Assert.Equal(2, providers.Count);
        Assert.All(providers, p => Assert.Equal(DeviceChannel.VendorHid, p.Channel));
        Assert.All(providers, p => Assert.Equal(Dongle, p.ContainerId));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void Discovers_real_devices_without_throwing()
    {
        var providers = new CatalogDeviceSource().Discover();

        foreach (var provider in providers)
            output.WriteLine($"{provider.Id}: {provider.Name} ({provider.Kind}) container {provider.ContainerId}");
    }
}
