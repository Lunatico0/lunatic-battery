namespace BatteryDisplay.Devices.Bluetooth;

/// <summary>
/// A present PnP node created by BTHENUM or BTHLE. Battery is the raw DEVPKEY value
/// {104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2; EndpointId is the association endpoint (System.Devices.AepId).
/// </summary>
public sealed record BluetoothNode(string InstanceId, Guid ContainerId, string? Name, int? Battery, string? EndpointId);

/// <summary>Association endpoint of a paired Bluetooth device: live connection state and what it says it is.</summary>
public sealed record BluetoothEndpoint(bool IsConnected, uint? ClassOfDevice, ushort? Appearance);

/// <summary>Seam over CfgMgr32 so the discovery logic can be tested without hardware. Read only.</summary>
public interface IBluetoothDeviceApi
{
    IReadOnlyList<BluetoothNode> GetPresentNodes();
    BluetoothEndpoint? GetEndpoint(string endpointId);
}
