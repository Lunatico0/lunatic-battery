using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;

namespace BatteryDisplay.Devices.Redragon;

/// <summary>
/// Redragon keyboard whose 2.4 GHz receiver pushes the 0x13 battery frame, described by a catalog entry of family
/// "redragon-hid" (options interface and usage). The keyboard answers no query, so a read returns what
/// <see cref="RedragonListener"/> has heard so far. Only listens: the receiver also carries other firmware, where a
/// write would be an unknown command, so nothing is ever written.
/// </summary>
public sealed class RedragonHidProvider : IBatteryProvider, IDisposable
{
    private readonly CatalogEntry _entry;
    private readonly RedragonListener _listener;

    public RedragonHidProvider(CatalogEntry entry, Guid? containerId = null, IRedragonHid? hid = null,
        Action<string>? log = null, TimeSpan? retryDelay = null, TimeProvider? time = null)
    {
        _entry = entry;
        ContainerId = containerId;
        InterfaceNumber = checked((int)entry.RequireOption("interface"));
        Usage = checked((uint)entry.RequireOption("usage"));

        // Without a log, events are dropped (this library never writes files).
        var sink = log ?? (_ => { });
        _listener = new RedragonListener(
            hid ?? new HidSharpRedragonHid(), entry.VendorId, entry.ProductIds,
            collection => RedragonProtocol.IsBatteryCollection(collection, InterfaceNumber, Usage),
            message => sink($"{entry.Id}: {message}"), retryDelay, time: time);
    }

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public DeviceKind Kind => _entry.Kind;
    public string Source => "Dongle Redragon";
    public DeviceChannel Channel => DeviceChannel.VendorHid;
    public Guid? ContainerId { get; }
    public int VendorId => _entry.VendorId;
    public IReadOnlyList<int> ProductIds => _entry.ProductIds;
    public int InterfaceNumber { get; }
    public uint Usage { get; }

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => Task.FromResult(_listener.Read());

    public void Dispose() => _listener.Dispose();
}
