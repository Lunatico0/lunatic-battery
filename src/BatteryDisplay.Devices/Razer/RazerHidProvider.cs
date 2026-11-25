using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using HidSharp;

namespace BatteryDisplay.Devices.Razer;

/// <summary>
/// Razer device that answers the 90-byte feature report battery commands, described by a catalog entry
/// of family "razer-hid" (option transactionId). Reads over any of its product ids (cable or dongle).
/// </summary>
public sealed class RazerHidProvider : IBatteryProvider
{
    // One request/response pair at a time; overlapping exchanges would read each other's answers.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly CatalogEntry _entry;

    public RazerHidProvider(CatalogEntry entry)
    {
        var transactionId = entry.RequireOption("transactionId");
        if (transactionId is < byte.MinValue or > byte.MaxValue)
            throw new FormatException($"Catalog entry '{entry.Id}' has transactionId {transactionId}, outside a byte.");

        _entry = entry;
        TransactionId = (byte)transactionId;
    }

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public DeviceKind Kind => _entry.Kind;
    public string Source => "Dongle Razer";
    public int VendorId => _entry.VendorId;

    /// <summary>Tried in catalog order.</summary>
    public IReadOnlyList<int> ProductIds => _entry.ProductIds;

    public byte TransactionId { get; }

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

                var reader = new RazerBatteryReader(transport, TransactionId, RazerTiming.Default);
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
    private List<string> FindControlCollections()
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
