using BatteryDisplay.Devices.Bluetooth;

namespace BatteryDisplay.Devices.Tests.Bluetooth;

internal sealed class FakeBluetoothApi : IBluetoothDeviceApi
{
    public List<BluetoothNode> Nodes { get; } = [];
    public Dictionary<string, BluetoothEndpoint> Endpoints { get; } = [];
    public Exception? NodesFailure { get; set; }
    public Exception? EndpointFailure { get; set; }

    public IReadOnlyList<BluetoothNode> GetPresentNodes() =>
        NodesFailure is null ? Nodes.ToList() : throw NodesFailure;

    public BluetoothEndpoint? GetEndpoint(string endpointId) =>
        EndpointFailure is null ? Endpoints.GetValueOrDefault(endpointId) : throw EndpointFailure;

    /// <summary>Adds a classic device as Windows lays it out: a device node plus a profile node carrying the battery.</summary>
    public Guid AddClassic(string name, int? battery, bool connected, uint classOfDevice = 0x240408)
    {
        var container = Guid.NewGuid();
        var address = container.ToString("N")[..12].ToUpperInvariant();
        var endpoint = $"Bluetooth#Bluetooth-{address}";
        Nodes.Add(new BluetoothNode($@"BTHENUM\Dev_{address}\7&1&0&BluetoothDevice_{address}", container, name, null, endpoint));
        Nodes.Add(new BluetoothNode(
            $@"BTHENUM\{{0000111e-0000-1000-8000-00805f9b34fb}}_LOCALMFG&03e0\7&1&0&{address}_C00000000",
            container, $"{name} Hands-Free AG", battery, null));
        Endpoints[endpoint] = new BluetoothEndpoint(connected, classOfDevice, null);
        return container;
    }

    /// <summary>Adds an LE device: the device node itself carries the battery.</summary>
    public Guid AddLowEnergy(string name, int? battery, bool connected, ushort appearance = 0x03C1)
    {
        var container = Guid.NewGuid();
        var address = container.ToString("N")[..12];
        var endpoint = $"BluetoothLE#BluetoothLE-{address}";
        Nodes.Add(new BluetoothNode($@"BTHLE\Dev_{address}\7&2&0&{address}", container, name, battery, endpoint));
        Endpoints[endpoint] = new BluetoothEndpoint(connected, null, appearance);
        return container;
    }
}
