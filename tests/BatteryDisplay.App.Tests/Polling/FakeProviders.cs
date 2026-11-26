using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Polling;

internal sealed class FakeProvider(
    string id, Func<CancellationToken, Task<BatteryStatus?>> read, DeviceKind kind = DeviceKind.Mouse, string source = "", string? name = null)
    : IBatteryProvider
{
    public int Reads;

    public string Id => id;
    public string Name => name ?? id;
    public DeviceKind Kind => kind;
    public string Source => source;
    public DeviceChannel Channel { get; init; } = DeviceChannel.Other;
    public Guid? ContainerId { get; init; }

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Reads);
        return read(ct);
    }

    public static FakeProvider Returning(string id, BatteryStatus? status, DeviceKind kind = DeviceKind.Mouse) =>
        new(id, _ => Task.FromResult(status), kind);

    /// <summary>A provider that knows which physical device it reaches, and how.</summary>
    public static FakeProvider Device(string id, Guid? container, DeviceChannel channel, BatteryStatus? status = null) =>
        new(id, _ => Task.FromResult(status)) { ContainerId = container, Channel = channel };

    public static FakeProvider Throwing(string id) => new(id, _ => throw new InvalidOperationException("device exploded"));

    public static FakeProvider FaultingAsync(string id) => new(id, async _ =>
    {
        await Task.Yield();
        throw new InvalidOperationException("device exploded later");
    });

    /// <summary>Never completes and ignores cancellation, like a stuck HID read.</summary>
    public static FakeProvider Hanging(string id) => new(id, _ => new TaskCompletionSource<BatteryStatus?>().Task);

    /// <summary>Blocks the calling thread before returning, like a synchronous driver call.</summary>
    public static FakeProvider BlockingSynchronously(string id, TimeSpan block, BatteryStatus? status) => new(id, _ =>
    {
        Thread.Sleep(block);
        return Task.FromResult(status);
    });
}
