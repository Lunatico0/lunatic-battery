using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using HidSharp;

namespace BatteryDisplay.Devices.SteelSeries;

/// <summary>
/// SteelSeries base station that speaks the Arctis Nova 5 status protocol, described by a catalog entry
/// of family "steelseries-arctis-nova-5" (options interface and usage).
/// </summary>
public sealed class SteelSeriesArctisNova5Provider : IBatteryProvider
{
    private readonly CatalogEntry _entry;

    public SteelSeriesArctisNova5Provider(CatalogEntry entry)
    {
        _entry = entry;
        InterfaceNumber = checked((int)entry.RequireOption("interface"));
        Usage = checked((uint)entry.RequireOption("usage"));
    }

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public DeviceKind Kind => _entry.Kind;
    public int VendorId => _entry.VendorId;
    public IReadOnlyList<int> ProductIds => _entry.ProductIds;
    public int InterfaceNumber { get; }
    public uint Usage { get; }

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => Task.Run(() => Read(ct), ct);

    private BatteryStatus? Read(CancellationToken ct)
    {
        var device = FindStatusInterface();
        if (device is null)
            return null;

        try
        {
            using var stream = device.Open();
            stream.ReadTimeout = ArctisNova5Protocol.ReadTimeoutMs;
            stream.WriteTimeout = ArctisNova5Protocol.ReadTimeoutMs;
            var input = new byte[device.GetMaxInputReportLength()];

            byte[]? ReadPayload()
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    int read = stream.Read(input, 0, input.Length);
                    return ArctisNova5Protocol.PayloadOf(input, read).ToArray();
                }
                catch (TimeoutException)
                {
                    return null;
                }
            }

            return ArctisNova5Protocol.QueryStatus(stream.Write, ReadPayload, device.GetMaxOutputReportLength());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException)
        {
            // Dongle unplugged or busy: treat as not present.
            return null;
        }
    }

    private HidDevice? FindStatusInterface() =>
        DeviceList.Local.GetHidDevices(VendorId)
            .FirstOrDefault(d => ProductIds.Contains(d.ProductID)
                && ArctisNova5Protocol.IsStatusInterface(d.DevicePath, InterfaceNumber)
                && HasStatusUsage(d));

    private bool HasStatusUsage(HidDevice device)
    {
        try
        {
            return device.GetReportDescriptor().DeviceItems
                .Any(item => item.Usages.GetAllValues().Contains(Usage));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
