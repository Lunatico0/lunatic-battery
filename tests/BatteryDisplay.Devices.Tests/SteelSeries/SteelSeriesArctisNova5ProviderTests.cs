using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.SteelSeries;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.SteelSeries;

public class SteelSeriesArctisNova5ProviderTests(ITestOutputHelper output)
{
    private static CatalogEntry Nova5 => DeviceCatalog.LoadEmbedded().Single(e => e.Id == "steelseries-arctis-nova-5");

    [Fact]
    public void Identifies_itself_as_the_nova5_headset()
    {
        IBatteryProvider provider = new SteelSeriesArctisNova5Provider(Nova5);

        Assert.Equal("steelseries-arctis-nova-5", provider.Id);
        Assert.Equal("Arctis Nova 5", provider.Name);
        Assert.Equal(DeviceKind.Headset, provider.Kind);
    }

    [Fact]
    public void Source_is_the_steelseries_dongle()
    {
        IBatteryProvider provider = new SteelSeriesArctisNova5Provider(Nova5);

        Assert.Equal("Dongle SteelSeries", provider.Source);
    }

    [Fact]
    public void Takes_ids_interface_and_usage_from_the_entry()
    {
        var entry = new CatalogEntry("nova-x", "steelseries-arctis-nova-5", "Nova X", DeviceKind.Headset, 0x1038, [0x2299],
            new Dictionary<string, long> { ["interface"] = 4, ["usage"] = 0xFFC00002 });

        var provider = new SteelSeriesArctisNova5Provider(entry);

        Assert.Equal(0x1038, provider.VendorId);
        Assert.Equal(new[] { 0x2299 }, provider.ProductIds);
        Assert.Equal(4, provider.InterfaceNumber);
        Assert.Equal(0xFFC00002u, provider.Usage);
    }

    [Theory]
    [InlineData("interface")]
    [InlineData("usage")]
    public void Rejects_an_entry_without_a_required_option(string missing)
    {
        var options = new Dictionary<string, long> { ["interface"] = 3, ["usage"] = 0xFFC00001 };
        options.Remove(missing);
        var entry = new CatalogEntry("nova-x", "steelseries-arctis-nova-5", "Nova X", DeviceKind.Headset, 0x1038, [0x2299], options);

        Assert.Throws<FormatException>(() => new SteelSeriesArctisNova5Provider(entry));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public async Task Reads_the_real_headset()
    {
        var status = await new SteelSeriesArctisNova5Provider(Nova5).ReadAsync(CancellationToken.None);

        output.WriteLine(status is null ? "no dongle or headset off" : $"{status.Percent}% {status.Charge}");
        Assert.NotNull(status);
        Assert.InRange(status.Percent ?? 0, 0, 100);
    }
}
