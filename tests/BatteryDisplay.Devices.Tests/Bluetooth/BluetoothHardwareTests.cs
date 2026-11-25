using BatteryDisplay.Devices.Bluetooth;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.Bluetooth;

[Trait("Category", "Hardware")]
public class BluetoothHardwareTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Reads_real_bluetooth_devices()
    {
        var api = new CfgMgrBluetoothApi();
        foreach (var node in api.GetPresentNodes().Where(n => n.Battery is not null || n.EndpointId is not null))
        {
            var endpoint = node.EndpointId is { } id ? api.GetEndpoint(id) : null;
            output.WriteLine($"{node.InstanceId} container={node.ContainerId} name='{node.Name}' battery={node.Battery} endpoint={endpoint}");
        }

        foreach (var provider in new BluetoothDeviceSource(api, output.WriteLine).Discover())
        {
            var status = await provider.ReadAsync(CancellationToken.None);
            output.WriteLine($"provider {provider.Id} '{provider.Name}' {provider.Kind}: {status?.ToString() ?? "null"}");
        }
    }
}
