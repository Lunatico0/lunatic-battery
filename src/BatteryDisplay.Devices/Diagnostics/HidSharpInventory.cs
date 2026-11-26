using HidSharp;

namespace BatteryDisplay.Devices.Diagnostics;

/// <summary>
/// The HID collections HidSharp lists. Reads only what a report needs; the device path and the serial number are
/// never read. A field Windows refuses to give stays null instead of dropping the collection.
/// </summary>
public sealed class HidSharpInventory : IHidInventory
{
    public IReadOnlyList<HidInterfaceInfo> GetInterfaces() =>
        DeviceList.Local.GetHidDevices()
            .Select(d => new HidInterfaceInfo(d.VendorID, d.ProductID, UsageOf(d), Try(d.GetManufacturer), Try(d.GetProductName)))
            .ToList();

    // Each Windows HID device is one top-level collection; its first usage is the collection's usage.
    private static HidUsage? UsageOf(HidDevice device)
    {
        try
        {
            var usages = device.GetReportDescriptor().DeviceItems.FirstOrDefault()?.Usages.GetAllValues();
            return usages?.FirstOrDefault() is { } value and not 0
                ? new HidUsage((int)(value >> 16), (int)(value & 0xFFFF))
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? Try(Func<string> read)
    {
        try
        {
            var text = read()?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
