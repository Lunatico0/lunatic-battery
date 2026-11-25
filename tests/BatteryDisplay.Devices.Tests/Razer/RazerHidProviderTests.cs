using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Razer;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.Razer;

public class RazerHidProviderTests(ITestOutputHelper output)
{
    private static CatalogEntry Cobra => DeviceCatalog.LoadEmbedded().Single(e => e.Id == "razer-cobra-hyperspeed");

    [Fact]
    public void Identifies_the_mouse_from_its_catalog_entry()
    {
        var provider = new RazerHidProvider(Cobra);

        Assert.Equal("razer-cobra-hyperspeed", provider.Id);
        Assert.Equal("Razer Cobra HyperSpeed", provider.Name);
        Assert.Equal(DeviceKind.Mouse, provider.Kind);
    }

    [Fact]
    public void Takes_vendor_product_ids_and_transaction_id_from_the_entry()
    {
        var entry = new CatalogEntry("razer-x", "razer-hid", "Razer X", DeviceKind.Keyboard, 0x1532, [0x0100],
            new Dictionary<string, long> { ["transactionId"] = 0x3F });

        var provider = new RazerHidProvider(entry);

        Assert.Equal(0x1532, provider.VendorId);
        Assert.Equal(new[] { 0x0100 }, provider.ProductIds);
        Assert.Equal(0x3F, provider.TransactionId);
        Assert.Equal(DeviceKind.Keyboard, provider.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0x100L)]
    [InlineData(-1L)]
    public void Rejects_an_entry_without_a_valid_transaction_id(long? transactionId)
    {
        var options = transactionId is { } t
            ? new Dictionary<string, long> { ["transactionId"] = t }
            : new Dictionary<string, long>();
        var entry = new CatalogEntry("razer-x", "razer-hid", "Razer X", DeviceKind.Mouse, 0x1532, [0x0100], options);

        Assert.Throws<FormatException>(() => new RazerHidProvider(entry));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public async Task Reads_the_real_mouse()
    {
        var provider = new RazerHidProvider(Cobra);

        var status = await provider.ReadAsync(CancellationToken.None);

        output.WriteLine($"Cobra HyperSpeed: {status}");
        Assert.NotNull(status);
        Assert.InRange(status.Percent!.Value, 0, 100);
    }
}
