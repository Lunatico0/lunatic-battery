using BatteryDisplay.Devices.Bluetooth;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Diagnostics;

/// <summary>
/// Gathers what the "Mi dispositivo no aparece" report shows: every HID device, every paired Bluetooth device and
/// every connected XInput slot. Only reads, like discovery; one failing enumeration never hides the others.
/// </summary>
public sealed class DeviceDiagnostics(
    IHidInventory hid, IBluetoothDeviceApi bluetooth, IXInputApi xinput, IReadOnlyList<CatalogEntry> catalog)
{
    public DeviceDiagnostics()
        : this(new HidSharpInventory(), new CfgMgrBluetoothApi(), new XInputNativeApi(), DeviceCatalog.LoadEmbedded()) { }

    public DiagnosticSnapshot Collect() => new(Section(CollectHid), Section(CollectBluetooth), Section(CollectXInput));

    private static DiagnosticSection<T> Section<T>(Func<IReadOnlyList<T>> collect)
    {
        try
        {
            return new(collect());
        }
        catch (Exception ex)
        {
            return new([], ex.GetType().Name);
        }
    }

    // A device exposes one collection per interface and usage; the report shows it once.
    private List<HidDeviceDiagnostic> CollectHid() =>
        hid.GetInterfaces()
            .GroupBy(i => (i.VendorId, i.ProductId))
            .OrderBy(g => g.Key.VendorId).ThenBy(g => g.Key.ProductId)
            .Select(g => new HidDeviceDiagnostic(
                g.Key.VendorId, g.Key.ProductId, g.Count(),
                g.Where(i => i.Usage is not null).Select(i => i.Usage!.Value).Distinct().ToList(),
                g.Select(i => i.Manufacturer).FirstOrDefault(m => m is not null),
                g.Select(i => i.Product).FirstOrDefault(p => p is not null),
                CatalogMatcher.Match(catalog, [new HidId(g.Key.VendorId, g.Key.ProductId)]).FirstOrDefault()))
            .ToList();

    private List<BluetoothDeviceDiagnostic> CollectBluetooth() =>
        BluetoothContainer.Group(bluetooth.GetPresentNodes())
            .Select(nodes =>
            {
                var device = BluetoothContainer.From(nodes);
                var endpoint = device.EndpointId is { } id ? bluetooth.GetEndpoint(id) : null;
                var raw = device.Battery ?? nodes.Select(n => n.Battery).FirstOrDefault(b => b is not null);
                return new BluetoothDeviceDiagnostic(device.Name, endpoint?.IsConnected, raw);
            })
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private List<XInputSlotDiagnostic> CollectXInput()
    {
        var slots = new List<XInputSlotDiagnostic>();
        for (uint slot = 0; slot < XInputGamepadProvider.MaxSlots; slot++)
        {
            if (xinput.IsConnected(slot))
                slots.Add(new XInputSlotDiagnostic(slot, xinput.GetGamepadBattery(slot)));
        }
        return slots;
    }
}
