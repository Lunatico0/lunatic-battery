using BatteryDisplay.Core;
using BatteryDisplay.Devices.Razer;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.Razer;

public class RazerCobraHyperSpeedProviderTests(ITestOutputHelper output)
{
    [Fact]
    public void Identifies_the_mouse()
    {
        var provider = new RazerCobraHyperSpeedProvider();

        Assert.Equal("razer-cobra-hyperspeed", provider.Id);
        Assert.Equal("Razer Cobra HyperSpeed", provider.Name);
        Assert.Equal(DeviceKind.Mouse, provider.Kind);
    }

    [Fact]
    public void Supports_dongle_and_wired_product_ids()
    {
        Assert.Equal(new[] { 0x00DA, 0x00DB }, RazerCobraHyperSpeedProvider.ProductIds.Order());
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public async Task Reads_the_real_mouse()
    {
        var provider = new RazerCobraHyperSpeedProvider();

        var status = await provider.ReadAsync(CancellationToken.None);

        output.WriteLine($"Cobra HyperSpeed: {status}");
        Assert.NotNull(status);
        Assert.InRange(status.Percent!.Value, 0, 100);
    }
}
