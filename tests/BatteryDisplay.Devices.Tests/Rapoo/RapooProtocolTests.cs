using BatteryDisplay.Core;
using BatteryDisplay.Devices.Rapoo;

namespace BatteryDisplay.Devices.Tests.Rapoo;

public class RapooProtocolTests
{
    // Real frame pushed by the 24AE:1211 dongle while the Rapoo software showed 35% and 700 DPI.
    private static readonly byte[] Captured = [0xBB, 0xB0, 0x41, 0xBC, 0x02, 0x01, 0x23];

    private static byte[] With(int index, byte value)
    {
        var frame = (byte[])Captured.Clone();
        frame[index] = value;
        return frame;
    }

    private const string Mi01 = @"\\?\hid#vid_24ae&pid_1211&mi_01&col05#8&2a3f&0&0004#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string Mi01Col06 = @"\\?\hid#vid_24ae&pid_1211&mi_01&col06#8&2a3f&0&0005#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string Mi00 = @"\\?\hid#vid_24ae&pid_1211&mi_00#8&1b2c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const uint VendorUsage = 0xFF000002;

    private static RapooCollection Collection(string path, byte reportId, int inputLength, uint usage = VendorUsage) =>
        new(path, [usage], [reportId], inputLength);

    [Fact]
    public void Captured_frame_reads_35_percent_with_unknown_charging()
    {
        var ok = RapooProtocol.TryParseBattery(Captured, out var status);

        Assert.True(ok);
        Assert.Equal(new BatteryStatus(35, ChargeState.Unknown), status);
    }

    [Fact]
    public void Frame_with_another_report_id_is_rejected()
    {
        Assert.False(RapooProtocol.TryParseBattery(With(0, 0xBC), out var status));
        Assert.Null(status);
    }

    [Fact]
    public void Frame_with_another_frame_type_is_rejected()
    {
        Assert.False(RapooProtocol.TryParseBattery(With(1, 0xB1), out var status));
        Assert.Null(status);
    }

    [Fact]
    public void Frame_shorter_than_7_bytes_is_rejected()
    {
        Assert.False(RapooProtocol.TryParseBattery(Captured.AsSpan(0, 6), out var status));
        Assert.Null(status);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(255)]
    public void Frame_with_battery_above_100_is_rejected(int battery)
    {
        Assert.False(RapooProtocol.TryParseBattery(With(6, (byte)battery), out var status));
        Assert.Null(status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Battery_limits_are_accepted(int battery)
    {
        Assert.True(RapooProtocol.TryParseBattery(With(6, (byte)battery), out var status));
        Assert.Equal(battery, status!.Percent);
    }

    [Fact]
    public void Battery_collection_is_the_0xBB_one_not_the_0xBC_one_with_the_same_usage()
    {
        var battery = Collection(Mi01, 0xBB, 7);
        var config = Collection(Mi01Col06, 0xBC, 10);

        Assert.True(RapooProtocol.IsBatteryCollection(battery, interfaceNumber: 1, VendorUsage));
        Assert.False(RapooProtocol.IsBatteryCollection(config, interfaceNumber: 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_the_7_byte_input_report()
    {
        Assert.False(RapooProtocol.IsBatteryCollection(Collection(Mi01, 0xBB, 10), 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_the_configured_interface()
    {
        Assert.False(RapooProtocol.IsBatteryCollection(Collection(Mi00, 0xBB, 7), 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_the_configured_usage()
    {
        Assert.False(RapooProtocol.IsBatteryCollection(Collection(Mi01, 0xBB, 7, usage: 0xFF000001), 1, VendorUsage));
    }

    private sealed class FakeInput(params byte[]?[] reports)
    {
        private readonly Queue<byte[]?> _reports = new(reports);
        public int Reads { get; private set; }

        public byte[]? Read()
        {
            Reads++;
            return _reports.Count > 0 ? _reports.Dequeue() : null;
        }
    }

    [Fact]
    public void Read_returns_the_first_battery_frame()
    {
        var input = new FakeInput(Captured);

        Assert.Equal(new BatteryStatus(35, ChargeState.Unknown), RapooProtocol.ReadStatus(input.Read));
    }

    [Fact]
    public void Read_skips_frames_that_are_not_battery_frames()
    {
        var input = new FakeInput(With(1, 0xB1), With(6, 0xFF), With(6, 0x50));

        Assert.Equal(new BatteryStatus(80, ChargeState.Unknown), RapooProtocol.ReadStatus(input.Read));
        Assert.Equal(3, input.Reads);
    }

    [Fact]
    public void Sleeping_mouse_read_timeout_yields_null()
    {
        var input = new FakeInput();

        Assert.Null(RapooProtocol.ReadStatus(input.Read));
        Assert.Equal(1, input.Reads);
    }

    [Fact]
    public void Read_gives_up_after_eight_foreign_frames()
    {
        var foreign = Enumerable.Range(0, 20).Select(_ => (byte[]?)With(0, 0xBC)).ToArray();
        var input = new FakeInput(foreign);

        Assert.Null(RapooProtocol.ReadStatus(input.Read));
        Assert.Equal(8, input.Reads);
    }
}
