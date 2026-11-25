using System.Diagnostics;
using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Bluetooth;

/// <summary>
/// Finds Bluetooth Classic and LE peripherals that are connected right now and publish a battery level
/// through the PnP property Windows fills in (HFP for Classic, GATT Battery Service for LE).
/// One provider per device container; a paired device that is off keeps its last level, so the
/// association endpoint's live connection state decides what is shown.
/// </summary>
public sealed class BluetoothDeviceSource(IBluetoothDeviceApi api, Action<string>? log = null) : IDeviceSource
{
    private readonly Action<string> _log = log ?? (message => Debug.WriteLine(message, "BatteryDisplay"));

    public BluetoothDeviceSource() : this(new CfgMgrBluetoothApi()) { }

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        try
        {
            var providers = new List<IBatteryProvider>();
            foreach (var group in BluetoothContainer.Group(api.GetPresentNodes()))
            {
                var device = BluetoothContainer.From(group);
                if (device.Battery is null)
                    continue;

                var endpoint = device.EndpointId is { } id ? api.GetEndpoint(id) : null;
                if (endpoint is not { IsConnected: true })
                    continue;

                var kind = BluetoothKindClassifier.Classify(endpoint.ClassOfDevice, endpoint.Appearance);
                providers.Add(new BluetoothBatteryProvider(api, device.ContainerId, device.Name, kind));
            }
            return providers;
        }
        catch (Exception ex)
        {
            _log($"bluetooth: discovery failed: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }
}

public sealed class BluetoothBatteryProvider(IBluetoothDeviceApi api, Guid containerId, string name, DeviceKind kind)
    : IBatteryProvider
{
    public string Id => BluetoothContainer.IdOf(containerId);
    public string Name => name;
    public DeviceKind Kind => kind;

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var nodes = api.GetPresentNodes().Where(n => n.ContainerId == containerId).ToList();
        if (nodes.Count == 0)
            return Task.FromResult<BatteryStatus?>(null);

        var device = BluetoothContainer.From(nodes);
        var endpoint = device.EndpointId is { } id ? api.GetEndpoint(id) : null;
        if (endpoint is not { IsConnected: true })
            return Task.FromResult<BatteryStatus?>(null);

        return Task.FromResult<BatteryStatus?>(new BatteryStatus(device.Battery, ChargeState.Unknown));
    }
}

/// <summary>Pure view of the nodes Windows creates for one physical device (same ContainerId).</summary>
internal sealed record BluetoothContainer(Guid ContainerId, string Name, int? Battery, string? EndpointId)
{
    private const string FallbackName = "Bluetooth device";

    public static string IdOf(Guid containerId) => $"bt-{containerId}";

    public static IEnumerable<IReadOnlyList<BluetoothNode>> Group(IEnumerable<BluetoothNode> nodes) =>
        nodes.Where(n => n.ContainerId != Guid.Empty)
            .GroupBy(n => n.ContainerId)
            .Select(g => (IReadOnlyList<BluetoothNode>)g.ToList());

    /// <summary>The device node (BTHENUM\Dev_ or BTHLE\Dev_) is preferred over profile and service nodes.</summary>
    public static BluetoothContainer From(IReadOnlyList<BluetoothNode> nodes)
    {
        var ordered = nodes.OrderBy(n => IsDeviceNode(n.InstanceId) ? 0 : 1)
            .ThenBy(n => n.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var name = ordered.Select(n => n.Name?.Trim()).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? FallbackName;
        var battery = ordered.Select(n => n.Battery).FirstOrDefault(b => b is >= 0 and <= 100);
        var endpoint = ordered.Select(n => n.EndpointId).FirstOrDefault(e => !string.IsNullOrEmpty(e));
        return new BluetoothContainer(ordered[0].ContainerId, name, battery, endpoint);
    }

    private static bool IsDeviceNode(string instanceId) =>
        instanceId.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase)
        || instanceId.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase);
}
