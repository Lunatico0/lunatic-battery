using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Rapoo;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.Rapoo;

public class RapooHidProviderTests(ITestOutputHelper output)
{
    private static CatalogEntry Mouse => DeviceCatalog.LoadEmbedded().Single(e => e.Id == "rapoo-gaming-mouse");

    private const string BatteryPath = @"\\?\hid#vid_24ae&pid_1211&mi_01&col05#x";
    private const string ConfigPath = @"\\?\hid#vid_24ae&pid_1211&mi_01&col06#x";

    private static readonly byte[] Captured = [0xBB, 0xB0, 0x41, 0xBC, 0x02, 0x01, 0x23];

    private sealed class FakeInput(Queue<byte[]?> reports) : IRapooInput
    {
        public bool Disposed { get; private set; }
        public byte[]? Read() => reports.Count > 0 ? reports.Dequeue() : null;
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeHid : IRapooHid
    {
        public List<RapooCollection> Collections { get; } = [];
        public Dictionary<string, Queue<byte[]?>> Reports { get; } = [];
        public List<string> Opened { get; } = [];
        public List<FakeInput> Inputs { get; } = [];
        public (int VendorId, IReadOnlyList<int> ProductIds)? Searched { get; private set; }

        public IReadOnlyList<RapooCollection> FindCollections(int vendorId, IReadOnlyList<int> productIds)
        {
            Searched = (vendorId, productIds);
            return Collections;
        }

        public IRapooInput? OpenInput(string path, int readTimeoutMs)
        {
            Opened.Add(path);
            if (!Reports.TryGetValue(path, out var reports))
                return null;
            var input = new FakeInput(reports);
            Inputs.Add(input);
            return input;
        }
    }

    private static FakeHid DongleWith(params byte[]?[] batteryReports)
    {
        var hid = new FakeHid();
        hid.Collections.Add(new RapooCollection(ConfigPath, [0xFF000002], [0xBC], 10));
        hid.Collections.Add(new RapooCollection(BatteryPath, [0xFF000002], [0xBB], 7));
        hid.Reports[ConfigPath] = new Queue<byte[]?>([[0xBC, 0, 0, 0, 0, 0, 0, 0, 0, 0]]);
        hid.Reports[BatteryPath] = new Queue<byte[]?>(batteryReports);
        return hid;
    }

    [Fact]
    public void Identifies_the_mouse_from_its_catalog_entry()
    {
        IBatteryProvider provider = new RapooHidProvider(Mouse);

        Assert.Equal("rapoo-gaming-mouse", provider.Id);
        Assert.Equal("Rapoo Gaming Mouse", provider.Name);
        Assert.Equal(DeviceKind.Mouse, provider.Kind);
        Assert.Equal("Dongle Rapoo", provider.Source);
        Assert.Equal(DeviceChannel.VendorHid, provider.Channel);
    }

    [Fact]
    public void Takes_ids_interface_and_usage_from_the_entry()
    {
        var entry = new CatalogEntry("rapoo-x", "rapoo-hid", "Rapoo X", DeviceKind.Mouse, 0x24AE, [0x1870],
            new Dictionary<string, long> { ["interface"] = 2, ["usage"] = 0xFF000003 });

        var provider = new RapooHidProvider(entry);

        Assert.Equal(0x24AE, provider.VendorId);
        Assert.Equal(new[] { 0x1870 }, provider.ProductIds);
        Assert.Equal(2, provider.InterfaceNumber);
        Assert.Equal(0xFF000003u, provider.Usage);
    }

    [Theory]
    [InlineData("interface")]
    [InlineData("usage")]
    public void Rejects_an_entry_without_a_required_option(string missing)
    {
        var options = new Dictionary<string, long> { ["interface"] = 1, ["usage"] = 0xFF000002 };
        options.Remove(missing);
        var entry = new CatalogEntry("rapoo-x", "rapoo-hid", "Rapoo X", DeviceKind.Mouse, 0x24AE, [0x1211], options);

        Assert.Throws<FormatException>(() => new RapooHidProvider(entry));
    }

    [Fact]
    public async Task Reads_the_pushed_frame_from_the_0xBB_collection_only()
    {
        var hid = DongleWith(Captured);

        var status = await new RapooHidProvider(Mouse, hid: hid).ReadAsync(CancellationToken.None);

        Assert.Equal(new BatteryStatus(35, ChargeState.Unknown), status);
        Assert.Equal(new[] { BatteryPath }, hid.Opened);
        Assert.Equal(0x24AE, hid.Searched!.Value.VendorId);
        Assert.Equal(new[] { 0x1211 }, hid.Searched.Value.ProductIds);
    }

    [Fact]
    public async Task Sleeping_mouse_times_out_into_no_reading()
    {
        var hid = DongleWith();

        var status = await new RapooHidProvider(Mouse, hid: hid).ReadAsync(CancellationToken.None);

        Assert.Null(status);
        Assert.All(hid.Inputs, i => Assert.True(i.Disposed));
    }

    [Fact]
    public async Task Dongle_absent_gives_no_reading()
    {
        var status = await new RapooHidProvider(Mouse, hid: new FakeHid()).ReadAsync(CancellationToken.None);

        Assert.Null(status);
    }

    [Fact]
    public async Task Collection_that_cannot_be_opened_gives_no_reading()
    {
        var hid = DongleWith(Captured);
        hid.Reports.Remove(BatteryPath);

        var status = await new RapooHidProvider(Mouse, hid: hid).ReadAsync(CancellationToken.None);

        Assert.Null(status);
    }

    [Fact]
    public async Task Dongle_unplugged_mid_read_gives_no_reading()
    {
        var hid = new ThrowingHid();

        var status = await new RapooHidProvider(Mouse, hid: hid).ReadAsync(CancellationToken.None);

        Assert.Null(status);
    }

    private sealed class ThrowingHid : IRapooHid
    {
        public IReadOnlyList<RapooCollection> FindCollections(int vendorId, IReadOnlyList<int> productIds) =>
            [new RapooCollection(BatteryPath, [0xFF000002], [0xBB], 7)];

        public IRapooInput? OpenInput(string path, int readTimeoutMs) => throw new IOException("device removed");
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public async Task Reads_the_real_mouse()
    {
        var status = await new RapooHidProvider(Mouse).ReadAsync(CancellationToken.None);

        output.WriteLine(status is null ? "no dongle or mouse asleep" : $"{status.Percent}% {status.Charge}");
        Assert.NotNull(status);
        Assert.InRange(status.Percent ?? 0, 0, 100);
    }
}
