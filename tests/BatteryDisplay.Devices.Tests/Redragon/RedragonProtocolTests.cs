using BatteryDisplay.Devices.Redragon;
using static BatteryDisplay.Devices.Tests.Redragon.RedragonFrames;

namespace BatteryDisplay.Devices.Tests.Redragon;

public class RedragonProtocolTests
{
    private const string Mi01 = @"\\?\hid#vid_3554&pid_fa09&mi_01&col03#8&1f2e&0&0002#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string Mi00 = @"\\?\hid#vid_3554&pid_fa09&mi_00#8&3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const uint VendorUsage = 0xFF020002;

    private static RedragonCollection Collection(string path, byte reportId = 0x13, int inputLength = 20, uint usage = VendorUsage) =>
        new(path, [usage], [reportId], inputLength);

    private static RedragonFrame? Parse(byte[] report) =>
        RedragonProtocol.TryParse(report, out var frame) ? frame : null;

    [Theory]
    [MemberData(nameof(CapturedVectors))]
    public void Captured_frames_parse_to_what_the_keyboard_was_doing(byte[] report, RedragonFrame expected)
    {
        Assert.Equal(expected, Parse(report));
    }

    public static TheoryData<byte[], RedragonFrame> CapturedVectors => new()
    {
        { CablePlugged75, new RedragonBatteryFrame(75, CableIn: true) },
        { Cable75, new RedragonBatteryFrame(75, CableIn: true) },
        { LinkLost, new RedragonLinkFrame(Up: false) },
        { LinkUp, new RedragonLinkFrame(Up: true) },
        { Cable100, new RedragonBatteryFrame(100, CableIn: true) },
        { OnBattery86, new RedragonBatteryFrame(86, CableIn: false) },
        { OnBattery85, new RedragonBatteryFrame(85, CableIn: false) },
        { OnBattery84, new RedragonBatteryFrame(84, CableIn: false) },
    };

    [Fact]
    public void Frame_with_a_bad_checksum_is_rejected()
    {
        Assert.Null(Parse(With(OnBattery86, 19, 0x7F, reseal: false)));
    }

    [Fact]
    public void Frame_whose_payload_changed_without_its_checksum_is_rejected()
    {
        Assert.Null(Parse(With(OnBattery86, 6, 0x50, reseal: false)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Frame_with_another_prefix_is_rejected(int index)
    {
        Assert.Null(Parse(With(OnBattery86, index, 0x07)));
    }

    [Fact]
    public void Frame_with_another_report_id_is_rejected()
    {
        Assert.Null(Parse(With(OnBattery86, 0, 0x14)));
    }

    [Fact]
    public void Frame_shorter_than_20_bytes_is_rejected()
    {
        Assert.Null(Parse(OnBattery86[..19]));
    }

    [Fact]
    public void Frame_of_an_unknown_type_is_rejected()
    {
        Assert.Null(Parse(Frame(type: 0x03, value: 0x01)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(255)]
    public void Battery_frame_with_a_level_outside_1_to_100_is_rejected(int percent)
    {
        Assert.Null(Parse(Frame(type: 0x05, value: (byte)percent, flags: 0x01)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Battery_limits_are_accepted(int percent)
    {
        Assert.Equal(new RedragonBatteryFrame(percent, CableIn: false), Parse(Frame(0x05, (byte)percent, flags: 0x01)));
    }

    [Fact]
    public void Link_frame_with_a_state_other_than_0_or_1_is_rejected()
    {
        Assert.Null(Parse(Frame(type: 0x02, value: 0x02)));
    }

    [Fact]
    public void Battery_collection_is_the_20_byte_0x13_report_on_the_vendor_usage_of_the_configured_interface()
    {
        Assert.True(RedragonProtocol.IsBatteryCollection(Collection(Mi01), interfaceNumber: 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_the_configured_interface()
    {
        Assert.False(RedragonProtocol.IsBatteryCollection(Collection(Mi00), 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_the_configured_usage()
    {
        Assert.False(RedragonProtocol.IsBatteryCollection(Collection(Mi01, usage: 0xFF020001), 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_report_0x13()
    {
        Assert.False(RedragonProtocol.IsBatteryCollection(Collection(Mi01, reportId: 0x14), 1, VendorUsage));
    }

    [Fact]
    public void Battery_collection_needs_the_20_byte_input_report()
    {
        Assert.False(RedragonProtocol.IsBatteryCollection(Collection(Mi01, inputLength: 64), 1, VendorUsage));
    }
}
