using BatteryDisplay.Core;
using BatteryDisplay.Devices.SteelSeries;

namespace BatteryDisplay.Devices.Tests.SteelSeries;

public class ArctisNova5ProtocolTests
{
    private static byte[] Payload(params byte[] head)
    {
        var payload = new byte[64];
        head.CopyTo(payload, 0);
        return payload;
    }

    [Fact]
    public void Status_request_is_report_id_zero_then_0xB0_padded_to_report_length()
    {
        var request = ArctisNova5Protocol.BuildStatusRequest(65);

        Assert.Equal(65, request.Length);
        Assert.Equal(0x00, request[0]);
        Assert.Equal(0xB0, request[1]);
        Assert.All(request.Skip(2), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Status_request_needs_room_for_the_command()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArctisNova5Protocol.BuildStatusRequest(1));
    }

    [Fact]
    public void Online_discharging_reads_percent_from_byte_3()
    {
        // Real reply captured from the dongle (report ID already stripped).
        var ok = ArctisNova5Protocol.TryParseStatus(Payload(0xB0, 0x03, 0x03, 0x0C, 0x03), out var status);

        Assert.True(ok);
        Assert.Equal(new BatteryStatus(12, ChargeState.Discharging), status);
    }

    [Fact]
    public void Byte_4_equal_to_1_means_charging()
    {
        var ok = ArctisNova5Protocol.TryParseStatus(Payload(0xB0, 0x03, 0x03, 0x50, 0x01), out var status);

        Assert.True(ok);
        Assert.Equal(new BatteryStatus(80, ChargeState.Charging), status);
    }

    [Fact]
    public void Byte_1_equal_to_2_means_headset_offline()
    {
        var ok = ArctisNova5Protocol.TryParseStatus(Payload(0xB0, 0x02, 0x00, 0x00, 0x00), out var status);

        Assert.True(ok);
        Assert.Null(status);
    }

    [Fact]
    public void Level_above_100_is_reported_as_unknown_percent()
    {
        var ok = ArctisNova5Protocol.TryParseStatus(Payload(0xB0, 0x03, 0x03, 0xFF, 0x03), out var status);

        Assert.True(ok);
        Assert.Equal(new BatteryStatus(null, ChargeState.Discharging), status);
    }

    [Fact]
    public void Report_with_other_header_is_not_a_status_report()
    {
        var ok = ArctisNova5Protocol.TryParseStatus(Payload(0x20, 0x03, 0x03, 0x50, 0x01), out var status);

        Assert.False(ok);
        Assert.Null(status);
    }

    [Fact]
    public void Report_shorter_than_16_bytes_is_not_a_status_report()
    {
        var ok = ArctisNova5Protocol.TryParseStatus(new byte[] { 0xB0, 0x03, 0x03, 0x50, 0x01 }, out var status);

        Assert.False(ok);
        Assert.Null(status);
    }

    [Theory]
    [InlineData(@"\\?\hid#vid_1038&pid_2232&mi_03#7&162212a7&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 3, true)]
    [InlineData(@"\\?\HID#VID_1038&PID_2253&MI_03#x", 3, true)]
    [InlineData(@"\\?\hid#vid_1038&pid_2232&mi_05#7&e7811c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 3, false)]
    [InlineData(@"\\?\hid#vid_1038&pid_2232&mi_04#x", 3, false)]
    [InlineData(@"\\?\hid#vid_1038&pid_2232&mi_04#x", 4, true)]
    public void Only_the_configured_interface_carries_the_status_report(string devicePath, int interfaceNumber, bool expected)
    {
        Assert.Equal(expected, ArctisNova5Protocol.IsStatusInterface(devicePath, interfaceNumber));
    }

    [Fact]
    public void Input_report_payload_drops_the_report_id_byte()
    {
        var report = new byte[] { 0x00, 0xB0, 0x03, 0x03, 0x0C, 0x03, 0x00 };

        var payload = ArctisNova5Protocol.PayloadOf(report, bytesRead: 6);

        Assert.Equal(new byte[] { 0xB0, 0x03, 0x03, 0x0C, 0x03 }, payload.ToArray());
    }
}
