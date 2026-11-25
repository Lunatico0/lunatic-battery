using BatteryDisplay.Core;
using HidSharp;

namespace BatteryDisplay.Devices.Razer;

/// <summary>Razer Cobra HyperSpeed, over its 2.4 GHz dongle or the cable.</summary>
public sealed class RazerCobraHyperSpeedProvider : IBatteryProvider
{
    private const int VendorId = 0x1532;

    /// <summary>Wired (00DA) first, then the dongle (00DB), per OpenRazer razermouse_driver.h.</summary>
    public static IReadOnlyList<int> ProductIds { get; } = [0x00DA, 0x00DB];

    // One request/response pair at a time; overlapping exchanges would read each other's answers.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public string Id => "razer-cobra-hyperspeed";
    public string Name => "Razer Cobra HyperSpeed";
    public DeviceKind Kind => DeviceKind.Mouse;

    public async Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            foreach (var path in FindControlCollections())
            {
                using var transport = WindowsHidFeatureTransport.TryOpen(path);
                if (transport is null)
                    continue;

                var reader = new RazerBatteryReader(transport, RazerReport.CobraTransactionId, RazerTiming.Default);
                var status = await reader.ReadAsync(ct);
                if (status is not null)
                    return status;
            }
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    // The control collection is the only one whose feature report is the 90-byte Razer report plus report id (interface 0).
    private static List<string> FindControlCollections()
    {
        try
        {
            return ProductIds
                .SelectMany(pid => DeviceList.Local.GetHidDevices(VendorId, pid))
                .Where(device => FeatureLength(device) == RazerReport.Length + 1)
                .Select(device => device.DevicePath)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static int FeatureLength(HidDevice device)
    {
        try
        {
            return device.GetMaxFeatureReportLength();
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
