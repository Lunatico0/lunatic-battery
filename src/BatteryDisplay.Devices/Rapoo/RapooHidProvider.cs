using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;

namespace BatteryDisplay.Devices.Rapoo;

/// <summary>
/// Rapoo mouse whose dongle pushes the 0xBB battery frame, described by a catalog entry of family "rapoo-hid"
/// (options interface and usage). Only listens: the other vendor collections of the mouse are configuration and
/// firmware channels, so nothing is ever written.
/// </summary>
public sealed class RapooHidProvider : IBatteryProvider
{
    private readonly CatalogEntry _entry;
    private readonly IRapooHid _hid;

    public RapooHidProvider(CatalogEntry entry, Guid? containerId = null, IRapooHid? hid = null)
    {
        _entry = entry;
        _hid = hid ?? new HidSharpRapooHid();
        ContainerId = containerId;
        InterfaceNumber = checked((int)entry.RequireOption("interface"));
        Usage = checked((uint)entry.RequireOption("usage"));
    }

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public DeviceKind Kind => _entry.Kind;
    public string Source => "Dongle Rapoo";
    public DeviceChannel Channel => DeviceChannel.VendorHid;
    public Guid? ContainerId { get; }
    public int VendorId => _entry.VendorId;
    public IReadOnlyList<int> ProductIds => _entry.ProductIds;
    public int InterfaceNumber { get; }
    public uint Usage { get; }

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => Task.Run(() => Read(ct), ct);

    private BatteryStatus? Read(CancellationToken ct)
    {
        try
        {
            var collections = _hid.FindCollections(VendorId, ProductIds)
                .Where(c => RapooProtocol.IsBatteryCollection(c, InterfaceNumber, Usage));

            foreach (var collection in collections)
            {
                using var input = _hid.OpenInput(collection.Path, RapooProtocol.ReadTimeoutMs);
                if (input is null)
                    continue;

                var status = RapooProtocol.ReadStatus(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    return input.Read();
                });
                if (status is not null)
                    return status;
            }
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException)
        {
            // Dongle unplugged or busy: treat as not present.
            return null;
        }
    }
}
