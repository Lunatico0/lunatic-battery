using System.Collections.Concurrent;
using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Redragon;
using Xunit.Abstractions;
using static BatteryDisplay.Devices.Tests.Redragon.RedragonFrames;

namespace BatteryDisplay.Devices.Tests.Redragon;

public class RedragonHidProviderTests(ITestOutputHelper output)
{
    private static CatalogEntry Keyboard => DeviceCatalog.LoadEmbedded().Single(e => e.Id == "redragon-k673");

    private const string BatteryPath = @"\\?\hid#vid_3554&pid_fa09&mi_01&col03#x";
    private const string KeysPath = @"\\?\hid#vid_3554&pid_fa09&mi_00#x";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <summary>The receiver's battery collection: frames pushed by the test arrive on the open input, if any.</summary>
    private sealed class FakeHid : IRedragonHid
    {
        private readonly Lock _gate = new();
        private bool _present = true;
        private FakeInput? _open;

        public int Opens { get; private set; }
        public List<FakeInput> Inputs { get; } = [];
        public (int VendorId, IReadOnlyList<int> ProductIds)? Searched { get; private set; }

        public IReadOnlyList<RedragonCollection> FindCollections(int vendorId, IReadOnlyList<int> productIds)
        {
            lock (_gate)
            {
                Searched = (vendorId, productIds);
                return _present
                    ?
                    [
                        new RedragonCollection(KeysPath, [0x00010006], [0x01], 9),
                        new RedragonCollection(BatteryPath, [0xFF020002], [0x13], 20),
                    ]
                    : [];
            }
        }

        public IRedragonInput? OpenInput(string path, int readTimeoutMs)
        {
            lock (_gate)
            {
                Assert.Equal(BatteryPath, path);
                if (!_present)
                    return null;
                Opens++;
                _open = new FakeInput();
                Inputs.Add(_open);
                return _open;
            }
        }

        public void Push(byte[] frame)
        {
            lock (_gate)
                _open!.Frames.Add(frame);
        }

        public void Unplug()
        {
            lock (_gate)
            {
                _present = false;
                _open?.Remove();
            }
        }

        public void Replug()
        {
            lock (_gate)
                _present = true;
        }
    }

    private sealed class FakeInput : IRedragonInput
    {
        private volatile bool _removed;
        private int _disposals;

        public BlockingCollection<byte[]> Frames { get; } = [];
        public int Disposals => Volatile.Read(ref _disposals);

        public byte[]? Read()
        {
            if (_removed)
                throw new IOException("device removed");
            return Frames.TryTake(out var frame, 20) ? frame : null;
        }

        public void Remove() => _removed = true;

        public void Dispose() => Interlocked.Increment(ref _disposals);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private static RedragonHidProvider Provider(FakeHid hid, List<string>? log = null, TimeProvider? time = null) =>
        new(Keyboard, hid: hid, log: log is null ? null : m => { lock (log) log.Add(m); },
            retryDelay: TimeSpan.FromMilliseconds(10), time: time);

    private static async Task<BatteryStatus?> ReadUntil(IBatteryProvider provider, Func<BatteryStatus?, bool> done)
    {
        var deadline = DateTime.UtcNow + Wait;
        BatteryStatus? status;
        do
        {
            status = await provider.ReadAsync(CancellationToken.None);
            if (done(status))
                return status;
            await Task.Delay(10);
        } while (DateTime.UtcNow < deadline);
        return status;
    }

    private static void WaitFor(Func<bool> condition) => Assert.True(SpinWait.SpinUntil(condition, Wait));

    [Fact]
    public void Identifies_the_keyboard_from_its_catalog_entry()
    {
        using var provider = new RedragonHidProvider(Keyboard);

        Assert.Equal("redragon-k673", provider.Id);
        Assert.Equal("Redragon K673", provider.Name);
        Assert.Equal(DeviceKind.Keyboard, provider.Kind);
        Assert.Equal("Dongle Redragon", provider.Source);
        Assert.Equal(DeviceChannel.VendorHid, provider.Channel);
        Assert.Equal(1, provider.InterfaceNumber);
        Assert.Equal(0xFF020002u, provider.Usage);
    }

    [Theory]
    [InlineData("interface")]
    [InlineData("usage")]
    public void Rejects_an_entry_without_a_required_option(string missing)
    {
        var options = new Dictionary<string, long> { ["interface"] = 1, ["usage"] = 0xFF020002 };
        options.Remove(missing);
        var entry = Keyboard with { Options = options };

        Assert.Throws<FormatException>(() => new RedragonHidProvider(entry));
    }

    [Fact]
    public async Task No_reading_before_the_first_frame()
    {
        var hid = new FakeHid();
        using var provider = Provider(hid);

        Assert.Null(await provider.ReadAsync(CancellationToken.None));
        WaitFor(() => hid.Opens == 1);
        Assert.Null(await provider.ReadAsync(CancellationToken.None));
        Assert.Equal(0x3554, hid.Searched!.Value.VendorId);
        Assert.Equal(new[] { 0xFA09 }, hid.Searched.Value.ProductIds);
    }

    [Fact]
    public async Task Reading_is_the_cached_state_of_the_pushed_frames()
    {
        var hid = new FakeHid();
        using var provider = Provider(hid);
        await provider.ReadAsync(CancellationToken.None);
        WaitFor(() => hid.Opens == 1);

        hid.Push(OnBattery86);
        Assert.Equal(new BatteryStatus(86, ChargeState.Discharging),
            await ReadUntil(provider, s => s is not null));

        hid.Push(LinkLost);
        hid.Push(Cable100);
        Assert.Equal(new BatteryStatus(86, ChargeState.Charging),
            await ReadUntil(provider, s => s?.Charge == ChargeState.Charging));
        Assert.Equal(new BatteryStatus(86, ChargeState.Charging), await provider.ReadAsync(CancellationToken.None));
        Assert.Equal(1, hid.Opens);
    }

    [Fact]
    public async Task Frames_with_a_bad_checksum_are_ignored()
    {
        var hid = new FakeHid();
        using var provider = Provider(hid);
        await provider.ReadAsync(CancellationToken.None);
        WaitFor(() => hid.Opens == 1);

        hid.Push(With(OnBattery86, 19, 0x00, reseal: false));
        hid.Push(OnBattery85);

        Assert.Equal(new BatteryStatus(85, ChargeState.Discharging), await ReadUntil(provider, s => s is not null));
    }

    [Fact]
    public async Task Receiver_unplugged_gives_no_reading_and_the_listener_restarts_when_it_comes_back()
    {
        var hid = new FakeHid();
        var log = new List<string>();
        using var provider = Provider(hid, log);
        await provider.ReadAsync(CancellationToken.None);
        WaitFor(() => hid.Opens == 1);
        hid.Push(OnBattery84);
        await ReadUntil(provider, s => s is not null);

        hid.Unplug();
        Assert.Null(await ReadUntil(provider, s => s is null));
        WaitFor(() => hid.Inputs[0].Disposals == 1);

        hid.Replug();
        WaitFor(() => hid.Opens == 2);
        Assert.Null(await provider.ReadAsync(CancellationToken.None));
        hid.Push(OnBattery85);
        Assert.Equal(new BatteryStatus(85, ChargeState.Discharging), await ReadUntil(provider, s => s is not null));

        lock (log)
        {
            Assert.Contains(log, m => m.Contains("gone"));
            Assert.Equal(2, log.Count(m => m.Contains("listening")));
        }
    }

    [Fact]
    public async Task Logs_the_first_frame_and_link_changes_but_not_every_percent()
    {
        var hid = new FakeHid();
        var log = new List<string>();
        using var provider = Provider(hid, log);
        await provider.ReadAsync(CancellationToken.None);
        WaitFor(() => hid.Opens == 1);

        foreach (var frame in Captured)
            hid.Push(frame);
        await ReadUntil(provider, s => s?.Percent == 84);

        lock (log)
        {
            Assert.Single(log, m => m.Contains("first frame"));
            Assert.Equal(2, log.Count(m => m.Contains("link lost")));
            Assert.Equal(2, log.Count(m => m.Contains("link up")));
            Assert.DoesNotContain(log, m => m.Contains("85"));
            Assert.All(log, m => Assert.StartsWith("redragon-k673: ", m));
        }
    }

    [Fact]
    public async Task Dispose_stops_the_reader_once()
    {
        var hid = new FakeHid();
        var provider = Provider(hid);
        await provider.ReadAsync(CancellationToken.None);
        WaitFor(() => hid.Opens == 1);
        hid.Push(OnBattery86);
        await ReadUntil(provider, s => s is not null);

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(1, hid.Inputs[0].Disposals);
        Assert.Null(await provider.ReadAsync(CancellationToken.None));
        await Task.Delay(50);
        Assert.Equal(1, hid.Opens);
    }

    [Fact]
    public async Task Listener_not_read_for_the_lease_stops_and_the_next_read_restarts_it_with_the_reading_kept()
    {
        var hid = new FakeHid();
        var time = new ManualTime();
        using var provider = Provider(hid, time: time);
        await provider.ReadAsync(CancellationToken.None);
        WaitFor(() => hid.Opens == 1);
        hid.Push(OnBattery86);
        await ReadUntil(provider, s => s is not null);

        time.Advance(RedragonListener.DefaultLease + TimeSpan.FromSeconds(1));
        WaitFor(() => hid.Inputs[0].Disposals == 1);

        Assert.Equal(new BatteryStatus(86, ChargeState.Discharging), await provider.ReadAsync(CancellationToken.None));
        WaitFor(() => hid.Opens == 2);
    }

    [Fact]
    public void Building_the_provider_does_not_touch_the_device()
    {
        var hid = new FakeHid();

        using var provider = Provider(hid);

        Assert.Null(hid.Searched);
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public async Task Reads_the_real_keyboard()
    {
        // Read only. The keyboard pushes nothing at start: plug or unplug its cable, or wake it, within 30 s.
        using var provider = new RedragonHidProvider(Keyboard, log: output.WriteLine);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        BatteryStatus? status = null;
        while (status is null && DateTime.UtcNow < deadline)
        {
            status = await provider.ReadAsync(CancellationToken.None);
            await Task.Delay(250);
        }

        output.WriteLine(status is null ? "no receiver or no event within 30 s" : $"{status.Percent}% {status.Charge}");
        Assert.NotNull(status);
        Assert.InRange(status.Percent ?? 0, 1, 100);
    }
}
