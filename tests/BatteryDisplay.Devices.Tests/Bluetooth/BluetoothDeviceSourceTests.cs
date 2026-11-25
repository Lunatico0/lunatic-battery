using BatteryDisplay.Core;
using BatteryDisplay.Devices.Bluetooth;

namespace BatteryDisplay.Devices.Tests.Bluetooth;

public class BluetoothDeviceSourceTests
{
    private readonly FakeBluetoothApi _api = new();

    private IReadOnlyList<IBatteryProvider> Discover() => new BluetoothDeviceSource(_api).Discover();

    [Fact]
    public void Nodes_of_one_container_give_one_provider()
    {
        var container = _api.AddClassic("XION XI-HT325BT", battery: 10, connected: true);

        var provider = Assert.Single(Discover());

        Assert.Equal($"bt-{container}", provider.Id);
        Assert.Equal("XION XI-HT325BT", provider.Name);
        Assert.Equal(DeviceKind.Headset, provider.Kind);
    }

    [Fact]
    public void Each_container_gives_its_own_provider()
    {
        var headset = _api.AddClassic("XION", battery: 10, connected: true);
        var keyboard = _api.AddLowEnergy("RK N80", battery: 39, connected: true);

        var ids = Discover().Select(p => p.Id).Order().ToArray();

        Assert.Equal(new[] { $"bt-{headset}", $"bt-{keyboard}" }.Order().ToArray(), ids);
    }

    [Fact]
    public void Id_is_stable_across_discoveries()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);

        Assert.Equal(Assert.Single(Discover()).Id, Assert.Single(Discover()).Id);
    }

    [Fact]
    public void Paired_but_disconnected_device_is_skipped()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: false);
        _api.AddClassic("XION", battery: 10, connected: false);

        Assert.Empty(Discover());
    }

    [Fact]
    public void Device_without_endpoint_is_skipped()
    {
        var container = Guid.NewGuid();
        _api.Nodes.Add(new BluetoothNode(@"BTHLE\Dev_aabbccddeeff\1", container, "Ghost", 50, null));

        Assert.Empty(Discover());
    }

    [Fact]
    public void Device_without_battery_is_skipped()
    {
        _api.AddClassic("Phone", battery: null, connected: true);

        Assert.Empty(Discover());
    }

    [Fact]
    public void Node_without_container_is_ignored()
    {
        _api.Nodes.Add(new BluetoothNode(@"BTHLE\Dev_aabbccddeeff\1", Guid.Empty, "Orphan", 50, "aep"));
        _api.Endpoints["aep"] = new BluetoothEndpoint(true, null, 0x03C1);

        Assert.Empty(Discover());
    }

    [Theory]
    [InlineData(101)]
    [InlineData(255)]
    [InlineData(-1)]
    public void Out_of_range_battery_is_discarded(int value)
    {
        _api.AddLowEnergy("RK N80", battery: value, connected: true);

        Assert.Empty(Discover());
    }

    [Fact]
    public async Task Valid_battery_wins_over_out_of_range_node()
    {
        var container = _api.AddClassic("XION", battery: 150, connected: true);
        _api.Nodes.Add(new BluetoothNode(@"BTHENUM\{0000110b-0000-1000-8000-00805f9b34fb}_X\1", container, "XION A2DP", 80, null));

        var provider = Assert.Single(Discover());

        Assert.Equal(new BatteryStatus(80, ChargeState.Unknown), await provider.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Device_node_battery_is_preferred_over_profile_node()
    {
        var container = _api.AddLowEnergy("Pad", battery: 70, connected: true);
        _api.Nodes.Insert(0, new BluetoothNode(@"BTHLEDevice\{0000180f-0000-1000-8000-00805f9b34fb}_Dev\1", container, null, 20, null));

        var provider = Assert.Single(Discover());

        Assert.Equal(new BatteryStatus(70, ChargeState.Unknown), await provider.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public void Name_is_trimmed()
    {
        _api.AddLowEnergy("RK N80 ", battery: 39, connected: true);

        Assert.Equal("RK N80", Assert.Single(Discover()).Name);
    }

    [Fact]
    public void Name_falls_back_to_profile_node_then_to_generic()
    {
        var named = Guid.NewGuid();
        _api.Nodes.Add(new BluetoothNode(@"BTHENUM\Dev_111111111111\1", named, null, null, "a"));
        _api.Nodes.Add(new BluetoothNode(@"BTHENUM\{0000111e-0000-1000-8000-00805f9b34fb}_X\1", named, "Buds Hands-Free AG", 40, null));
        _api.Endpoints["a"] = new BluetoothEndpoint(true, 0x240404, null);

        var anonymous = Guid.NewGuid();
        _api.Nodes.Add(new BluetoothNode(@"BTHLE\Dev_222222222222\1", anonymous, "  ", 40, "b"));
        _api.Endpoints["b"] = new BluetoothEndpoint(true, null, null);

        var names = Discover().ToDictionary(p => p.Id, p => p.Name);

        Assert.Equal("Buds Hands-Free AG", names[$"bt-{named}"]);
        Assert.Equal("Bluetooth device", names[$"bt-{anonymous}"]);
    }

    [Fact]
    public void Kind_comes_from_appearance_for_low_energy()
    {
        _api.AddLowEnergy("Mouse", battery: 60, connected: true, appearance: 0x03C2);

        Assert.Equal(DeviceKind.Mouse, Assert.Single(Discover()).Kind);
    }

    [Fact]
    public void Unclassifiable_device_is_other()
    {
        _api.AddClassic("Speaker", battery: 60, connected: true, classOfDevice: 0x240414);

        Assert.Equal(DeviceKind.Other, Assert.Single(Discover()).Kind);
    }

    [Fact]
    public void Discover_returns_empty_when_enumeration_fails()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        _api.NodesFailure = new InvalidOperationException("CM_Get_Device_ID_List failed");

        Assert.Empty(Discover());
    }

    [Fact]
    public void Discover_returns_empty_when_endpoint_query_fails()
    {
        _api.AddLowEnergy("RK N80", battery: 39, connected: true);
        _api.EndpointFailure = new System.ComponentModel.Win32Exception(5);

        Assert.Empty(Discover());
    }
}
