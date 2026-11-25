using BatteryDisplay.Core;
using BatteryDisplay.Devices.SteelSeries;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.SteelSeries;

public class SteelSeriesArctisNova5ProviderTests(ITestOutputHelper output)
{
    [Fact]
    public void Identifies_itself_as_the_nova5_headset()
    {
        IBatteryProvider provider = new SteelSeriesArctisNova5Provider();

        Assert.Equal("steelseries-arctis-nova-5", provider.Id);
        Assert.Equal("Arctis Nova 5", provider.Name);
        Assert.Equal(DeviceKind.Headset, provider.Kind);
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public async Task Reads_the_real_headset()
    {
        var status = await new SteelSeriesArctisNova5Provider().ReadAsync(CancellationToken.None);

        output.WriteLine(status is null ? "no dongle or headset off" : $"{status.Percent}% {status.Charge}");
        Assert.NotNull(status);
        Assert.InRange(status.Percent ?? 0, 0, 100);
    }
}
