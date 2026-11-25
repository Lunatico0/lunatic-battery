using System.Runtime.InteropServices;
using System.Text;

namespace BatteryDisplay.Devices.Bluetooth;

/// <summary>
/// P/Invoke wrapper over cfgmgr32.dll. Only reads the PnP property store; never talks to a device.
/// Lists every present node and filters by prefix: CM_GETIDLIST_FILTER_ENUMERATOR("BTHENUM") returned
/// 2 of 28 present BTHENUM nodes and none of the BTHLE ones on Windows 11 26300.
/// </summary>
public sealed class CfgMgrBluetoothApi : IBluetoothDeviceApi
{
    private const int CrSuccess = 0;
    private const int CrBufferSmall = 0x1A;
    private const uint CmGetIdListFilterPresent = 0x100;
    private const int DevObjectTypeAep = 5;
    private const uint DevPropTypeByte = 0x03;
    private const uint DevPropTypeUInt16 = 0x05;
    private const uint DevPropTypeUInt32 = 0x07;
    private const uint DevPropTypeGuid = 0x0D;
    private const uint DevPropTypeBoolean = 0x11;
    private const uint DevPropTypeString = 0x12;

    private static readonly string[] Prefixes = [@"BTHENUM\", @"BTHLE\"];

    private static readonly DevPropKey ContainerIdKey = new(new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);
    private static readonly DevPropKey FriendlyNameKey = new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    private static readonly DevPropKey BatteryKey = new(new("104ea319-6ee2-4701-bd47-8ddbf425bbe5"), 2);
    private static readonly DevPropKey AepIdKey = new(new("3b2ce006-5e61-4fde-bab8-9b8aac9b26df"), 8);
    private static readonly DevPropKey AepIsConnectedKey = new(new("a35996ab-11cf-4935-8b61-a6761081ecdf"), 7);
    private static readonly DevPropKey ClassOfDeviceKey = new(new("2bd67d8b-8beb-48d5-87e0-6cda3428040a"), 10);
    private static readonly DevPropKey LeAppearanceKey = new(new("995ef0b0-7eb3-4a8b-b9ce-068bb3f4af69"), 1);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct DevPropKey(Guid FmtId, uint Pid);

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropCompKey
    {
        public DevPropKey Key;
        public int Store;
        public IntPtr LocaleName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevProperty
    {
        public DevPropCompKey CompKey;
        public uint Type;
        public uint BufferSize;
        public IntPtr Buffer;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_DevNode_PropertyW(
        uint devInst, in DevPropKey key, out uint type, [Out] byte[]? buffer, ref uint size, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int DevGetObjectProperties(
        int objectType, string objectId, uint queryFlags, uint requestedCount,
        [In] DevPropCompKey[] requested, out uint propertyCount, out IntPtr properties);

    [DllImport("cfgmgr32.dll")]
    private static extern void DevFreeObjectProperties(uint propertyCount, IntPtr properties);

    public IReadOnlyList<BluetoothNode> GetPresentNodes()
    {
        var nodes = new List<BluetoothNode>();
        foreach (var id in PresentDeviceIds())
        {
            if (!Prefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (CM_Locate_DevNodeW(out var inst, id, 0) != CrSuccess)
                continue;
            if (GetNodeProperty(inst, ContainerIdKey) is not { Type: DevPropTypeGuid, Data.Length: 16 } container)
                continue;

            var battery = GetNodeProperty(inst, BatteryKey) is { Type: DevPropTypeByte, Data.Length: > 0 } b
                ? b.Data[0]
                : (int?)null;
            nodes.Add(new BluetoothNode(
                id,
                new Guid(container.Data),
                AsString(GetNodeProperty(inst, FriendlyNameKey)),
                battery,
                AsString(GetNodeProperty(inst, AepIdKey))));
        }
        return nodes;
    }

    public BluetoothEndpoint? GetEndpoint(string endpointId)
    {
        DevPropCompKey[] requested =
        [
            new() { Key = AepIsConnectedKey },
            new() { Key = ClassOfDeviceKey },
            new() { Key = LeAppearanceKey },
        ];
        int hr = DevGetObjectProperties(DevObjectTypeAep, endpointId, 0, (uint)requested.Length, requested,
            out var count, out var properties);
        if (hr != 0)
            return null;

        try
        {
            bool? connected = null;
            uint? cod = null;
            ushort? appearance = null;
            int size = Marshal.SizeOf<DevProperty>();
            for (int i = 0; i < count; i++)
            {
                var p = Marshal.PtrToStructure<DevProperty>(properties + i * size);
                if (p.Buffer == IntPtr.Zero)
                    continue;
                var key = p.CompKey.Key;
                if (key == AepIsConnectedKey && p is { Type: DevPropTypeBoolean, BufferSize: >= 1 })
                    connected = Marshal.ReadByte(p.Buffer) != 0;
                else if (key == ClassOfDeviceKey && p is { Type: DevPropTypeUInt32, BufferSize: >= 4 })
                    cod = (uint)Marshal.ReadInt32(p.Buffer);
                else if (key == LeAppearanceKey && p is { Type: DevPropTypeUInt16, BufferSize: >= 2 })
                    appearance = (ushort)Marshal.ReadInt16(p.Buffer);
            }
            return connected is { } c ? new BluetoothEndpoint(c, cod, appearance) : null;
        }
        finally
        {
            DevFreeObjectProperties(count, properties);
        }
    }

    private static string[] PresentDeviceIds()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (CM_Get_Device_ID_List_SizeW(out var length, null, CmGetIdListFilterPresent) != CrSuccess || length == 0)
                return [];
            var buffer = new char[length];
            int cr = CM_Get_Device_ID_ListW(null, buffer, length, CmGetIdListFilterPresent);
            if (cr == CrBufferSmall)
                continue; // A device arrived between the two calls.
            if (cr != CrSuccess)
                return [];
            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        return [];
    }

    private readonly record struct RawProperty(uint Type, byte[] Data);

    private static RawProperty? GetNodeProperty(uint devInst, DevPropKey key)
    {
        uint size = 0;
        int cr = CM_Get_DevNode_PropertyW(devInst, key, out var type, null, ref size, 0);
        if (cr != CrBufferSmall || size == 0)
            return null;
        var data = new byte[size];
        return CM_Get_DevNode_PropertyW(devInst, key, out type, data, ref size, 0) == CrSuccess
            ? new RawProperty(type, data)
            : null;
    }

    private static string? AsString(RawProperty? property) =>
        property is { Type: DevPropTypeString } p ? Encoding.Unicode.GetString(p.Data).TrimEnd('\0') : null;
}
