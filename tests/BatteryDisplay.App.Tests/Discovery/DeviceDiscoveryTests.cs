using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Tests.Polling;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Discovery;

public class DeviceDiscoveryTests
{
    [Fact]
    public void Concatenates_InSourceOrder()
    {
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null), FakeProvider.Returning("b", null)),
            new FakeSource(FakeProvider.Returning("c", null)),
        ]);

        Assert.Equal(["a", "b", "c"], discovery.Discover().Select(p => p.Id));
    }

    [Fact]
    public void DuplicateId_FirstSourceWins()
    {
        var first = FakeProvider.Returning("same", null);
        var second = FakeProvider.Returning("same", null);
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(first),
            new FakeSource(second, FakeProvider.Returning("other", null)),
        ]);

        var found = discovery.Discover();

        Assert.Equal(["same", "other"], found.Select(p => p.Id));
        Assert.Same(first, found[0]);
    }

    [Fact]
    public void ThrowingSource_IsLoggedAndSkipped()
    {
        var logs = new List<string>();
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null)),
            new ThrowingSource(),
            new FakeSource(FakeProvider.Returning("b", null)),
        ], logs.Add);

        Assert.Equal(["a", "b"], discovery.Discover().Select(p => p.Id));
        Assert.Contains(logs, l => l.Contains(nameof(ThrowingSource)) && l.Contains("enumeration exploded"));
    }

    [Fact]
    public void FixedSource_ReturnsItsProviders()
    {
        IBatteryProvider[] providers = [FakeProvider.Returning("a", null), FakeProvider.Returning("b", null)];

        Assert.Equal(providers, new FixedDeviceSource(providers).Discover());
    }
}
