using BatteryDisplay.Core;

namespace BatteryDisplay.App.Discovery;

/// <summary>
/// One physical device seen by several sources (same PnP container). It is the most direct of them in every
/// respect, reading included, so an empty reading from a lesser source never hides a good one. MergedIds are the
/// Ids the other sources gave it, so the user's settings can follow the device whichever source shows it.
/// </summary>
public sealed class MergedProvider(IBatteryProvider primary, IReadOnlyList<IBatteryProvider> merged) : IBatteryProvider
{
    public IBatteryProvider Primary => primary;
    public IReadOnlyList<string> MergedIds { get; } = merged.Select(p => p.Id).ToList();

    public string Id => primary.Id;
    public string Name => primary.Name;
    public DeviceKind Kind => primary.Kind;
    public string Source => primary.Source;
    public DeviceChannel Channel => primary.Channel;
    public Guid? ContainerId => primary.ContainerId;

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct) => primary.ReadAsync(ct);
}
