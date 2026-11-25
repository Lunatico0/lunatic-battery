using BatteryDisplay.Core;
using BatteryDisplay.Devices.Bluetooth;

namespace BatteryDisplay.Devices.Tests.Bluetooth;

public class BluetoothBatteryProviderTests
{
    private readonly FakeBluetoothApi _api = new();

    private IBatteryProvider DiscoverSingle() => Assert.Single(new BluetoothDeviceSource(_api).Discover());

    private static Task<BatteryStatus?> Read(IBatteryProvider provider) => provider.ReadAsync(CancellationToken.None);

    [Fact]
    public async Task Reads_percent_with_unknown_charge()
    {
        _api.AddClassic("XION", battery: 10, connected: true);

        Assert.Equal(new BatteryStatus(10, ChargeState.Unknown), await Read(DiscoverSingle()));
    }

    [Fact]
    public async Task Rereads_the_property_on_every_call()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        var provider = DiscoverSingle();
        _api.Nodes[0] = _api.Nodes[0] with { Battery = 38 };

        Assert.Equal(new BatteryStatus(38, ChargeState.Unknown), await Read(provider));
    }

    [Fact]
    public async Task Returns_null_once_disconnected()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        var provider = DiscoverSingle();
        var endpoint = _api.Endpoints.Keys.Single();
        _api.Endpoints[endpoint] = _api.Endpoints[endpoint] with { IsConnected = false };

        Assert.Null(await Read(provider));
    }

    [Fact]
    public async Task Returns_null_once_unpaired()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        var provider = DiscoverSingle();
        _api.Nodes.Clear();

        Assert.Null(await Read(provider));
    }

    [Fact]
    public async Task Connected_without_valid_level_returns_unknown_percent()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        var provider = DiscoverSingle();
        _api.Nodes[0] = _api.Nodes[0] with { Battery = 200 };

        Assert.Equal(new BatteryStatus(null, ChargeState.Unknown), await Read(provider));
    }

    [Fact]
    public async Task Ignores_other_containers()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        var provider = DiscoverSingle();
        _api.AddClassic("XION", battery: 10, connected: true);

        Assert.Equal(new BatteryStatus(39, ChargeState.Unknown), await Read(provider));
    }
}
