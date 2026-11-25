using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Polling;

internal sealed class FakeProvider(string id, Func<CancellationToken, Task<BatteryStatus?>> read, DeviceKind kind = DeviceKind.Mouse) : IBatteryProvider
{
    public int Reads;

    public string Id => id;
    public string Name => id;
    public DeviceKind Kind => kind;

    public Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Reads);
        return read(ct);
    }

    public static FakeProvider Returning(string id, BatteryStatus? status) => new(id, _ => Task.FromResult(status));

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
