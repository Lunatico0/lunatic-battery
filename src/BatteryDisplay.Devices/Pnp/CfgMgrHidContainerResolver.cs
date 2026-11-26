using System.Runtime.InteropServices;
using System.Text;

namespace BatteryDisplay.Devices.Pnp;

/// <summary>
/// Reads the ContainerId of a HID collection through cfgmgr32.dll: interface path to device instance id
/// (DEVPKEY_Device_InstanceId), then that node's DEVPKEY_Device_ContainerId. Only reads the property store.
/// A collection inherits the container of its USB device, so a dongle's collections share the dongle's one.
/// </summary>
public sealed class CfgMgrHidContainerResolver : IHidContainerResolver
{
    private const int CrSuccess = 0;
    private const int CrBufferSmall = 0x1A;
    private const uint DevPropTypeGuid = 0x0D;
    private const uint DevPropTypeString = 0x12;

    private static readonly DevPropKey InstanceIdKey = new(new("78c34fc8-104a-4aca-9ea4-524d52996e57"), 256);
    private static readonly DevPropKey ContainerIdKey = new(new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct DevPropKey(Guid FmtId, uint Pid);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_PropertyW(
        string deviceInterface, in DevPropKey key, out uint type, [Out] byte[]? buffer, ref uint size, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_DevNode_PropertyW(
        uint devInst, in DevPropKey key, out uint type, [Out] byte[]? buffer, ref uint size, uint flags);

    public Guid? ContainerOf(string devicePath)
    {
        if (string.IsNullOrEmpty(devicePath))
            return null;
        if (InterfaceProperty(devicePath, InstanceIdKey) is not { Type: DevPropTypeString } instance)
            return null;

        var instanceId = Encoding.Unicode.GetString(instance.Data).TrimEnd('\0');
        if (CM_Locate_DevNodeW(out var devInst, instanceId, 0) != CrSuccess)
            return null;
        return NodeProperty(devInst, ContainerIdKey) is { Type: DevPropTypeGuid, Data.Length: 16 } container
            ? new Guid(container.Data)
            : null;
    }

    private readonly record struct RawProperty(uint Type, byte[] Data);

    private static RawProperty? InterfaceProperty(string path, DevPropKey key)
    {
        uint size = 0;
        if (CM_Get_Device_Interface_PropertyW(path, key, out _, null, ref size, 0) != CrBufferSmall || size == 0)
            return null;
        var data = new byte[size];
        return CM_Get_Device_Interface_PropertyW(path, key, out var type, data, ref size, 0) == CrSuccess
            ? new RawProperty(type, data)
            : null;
    }

    private static RawProperty? NodeProperty(uint devInst, DevPropKey key)
    {
        uint size = 0;
        if (CM_Get_DevNode_PropertyW(devInst, key, out _, null, ref size, 0) != CrBufferSmall || size == 0)
            return null;
        var data = new byte[size];
        return CM_Get_DevNode_PropertyW(devInst, key, out var type, data, ref size, 0) == CrSuccess
            ? new RawProperty(type, data)
            : null;
    }
}
