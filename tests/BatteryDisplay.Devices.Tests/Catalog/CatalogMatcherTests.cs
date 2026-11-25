using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;

namespace BatteryDisplay.Devices.Tests.Catalog;

public class CatalogMatcherTests
{
    internal static CatalogEntry Entry(string id, int vendorId, params int[] productIds) =>
        new(id, "fam", id, DeviceKind.Other, vendorId, productIds, new Dictionary<string, long>());

    private static readonly CatalogEntry Mouse = Entry("mouse", 0x1532, 0x00DA, 0x00DB);
    private static readonly CatalogEntry Headset = Entry("headset", 0x1038, 0x2232, 0x2253);
    private static readonly CatalogEntry[] Catalog = [Mouse, Headset];

    [Fact]
    public void Nothing_connected_matches_nothing()
    {
        Assert.Empty(CatalogMatcher.Match(Catalog, []));
    }

    [Fact]
    public void Matches_an_entry_by_vendor_and_product_id()
    {
        var present = new[] { new HidId(0x1038, 0x2253) };

        Assert.Equal(new[] { Headset }, CatalogMatcher.Match(Catalog, present));
    }

    [Fact]
    public void Same_product_id_under_another_vendor_does_not_match()
    {
        var present = new[] { new HidId(0x046D, 0x00DA) };

        Assert.Empty(CatalogMatcher.Match(Catalog, present));
    }

    [Fact]
    public void Cable_and_dongle_with_many_collections_give_one_entry()
    {
        var present = new[]
        {
            new HidId(0x1532, 0x00DA), new HidId(0x1532, 0x00DA), new HidId(0x1532, 0x00DA),
            new HidId(0x1532, 0x00DB), new HidId(0x1532, 0x00DB),
        };

        Assert.Equal(new[] { Mouse }, CatalogMatcher.Match(Catalog, present));
    }

    [Fact]
    public void Keeps_catalog_order()
    {
        var present = new[] { new HidId(0x1038, 0x2232), new HidId(0x1532, 0x00DB) };

        Assert.Equal(new[] { Mouse, Headset }, CatalogMatcher.Match(Catalog, present));
    }
}
