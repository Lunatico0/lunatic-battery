using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Diagnostics;

/// <summary>Usage page and usage of a HID top-level collection.</summary>
public readonly record struct HidUsage(int Page, int Id);

/// <summary>
/// One HID top-level collection as Windows lists it. It never carries the device path or the serial number,
/// so nothing that identifies the user's unit can reach a report.
/// </summary>
public sealed record HidInterfaceInfo(int VendorId, int ProductId, HidUsage? Usage, string? Manufacturer, string? Product);

/// <summary>Read-only seam over the HID device list.</summary>
public interface IHidInventory
{
    IReadOnlyList<HidInterfaceInfo> GetInterfaces();
}

/// <summary>Every collection of one VID/PID, folded into one device. CatalogEntry is null when the catalog does not know it.</summary>
public sealed record HidDeviceDiagnostic(
    int VendorId, int ProductId, int InterfaceCount, IReadOnlyList<HidUsage> Usages,
    string? Manufacturer, string? Product, CatalogEntry? CatalogEntry);

/// <summary>
/// One paired Bluetooth device. IsConnected is null when Windows gives no endpoint state. RawBattery is the battery
/// property as Windows stores it (null when the device does not expose it), even when it is outside 0..100.
/// </summary>
public sealed record BluetoothDeviceDiagnostic(string Name, bool? IsConnected, int? RawBattery);

/// <summary>A connected XInput slot (0..3). Battery is null when the slot does not answer the battery query.</summary>
public sealed record XInputSlotDiagnostic(uint Slot, XInputBatteryInformation? Battery);

/// <summary>What one enumeration found. Error is the exception type name when it failed; the message is left out, it can hold paths.</summary>
public sealed record DiagnosticSection<T>(IReadOnlyList<T> Items, string? Error = null);

public sealed record DiagnosticSnapshot(
    DiagnosticSection<HidDeviceDiagnostic> Hid,
    DiagnosticSection<BluetoothDeviceDiagnostic> Bluetooth,
    DiagnosticSection<XInputSlotDiagnostic> XInput);
