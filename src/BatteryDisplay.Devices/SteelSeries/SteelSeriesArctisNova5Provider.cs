using BatteryDisplay.Core;
using HidSharp;

namespace BatteryDisplay.Devices.SteelSeries;

public sealed class SteelSeriesArctisNova5Provider : IBatteryProvider
{
    public string Id => "steelseries-arctis-nova-5";
    public string Name => "Arctis Nova 5";
    public DeviceKind Kind => DeviceKind.Headset;

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => Task.Run(() => Read(ct), ct);

    private static BatteryStatus? Read(CancellationToken ct)
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

    private static HidDevice? FindStatusInterface() =>
        DeviceList.Local.GetHidDevices(ArctisNova5Protocol.VendorId)
            .FirstOrDefault(d => ArctisNova5Protocol.ProductIds.Contains(d.ProductID)
                && ArctisNova5Protocol.IsStatusInterface(d.DevicePath)
                && HasStatusUsage(d));

    private static bool HasStatusUsage(HidDevice device)
    {
        try
        {
            return device.GetReportDescriptor().DeviceItems
                .Any(item => item.Usages.GetAllValues().Contains(ArctisNova5Protocol.Usage));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
