using BatteryDisplay.Devices.Razer;

namespace BatteryDisplay.Devices.Tests.Razer;

public class RazerReportTests
{
    // Battery response captured from a Cobra HyperSpeed through its dongle (PID 00DB), report id stripped.
    internal const string RealBatteryResponseHex =
        "021F0000000207800040000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000C500";

    [Fact]
    public void Battery_request_has_the_exact_bytes()
    {
        var request = RazerReport.BuildRequest(RazerReport.CobraTransactionId, RazerReport.BatteryLevel);

        var expected = new byte[90];
        expected[1] = 0x1F;
        expected[5] = 0x02;
        expected[6] = 0x07;
        expected[7] = 0x80;
        expected[88] = 0x85;
        Assert.Equal(expected, request);
    }

    [Fact]
    public void Charging_request_has_the_exact_bytes()
    {
        var request = RazerReport.BuildRequest(RazerReport.CobraTransactionId, RazerReport.ChargingStatus);

        var expected = new byte[90];
        expected[1] = 0x1F;
        expected[5] = 0x02;
        expected[6] = 0x07;
        expected[7] = 0x84;
        expected[88] = 0x81;
        Assert.Equal(expected, request);
    }

    [Fact]
    public void Checksum_xors_bytes_2_to_87_and_ignores_status_and_transaction_id()
    {
        var report = new byte[90];
        report[0] = 0xAA;
        report[1] = 0xBB;
        report[2] = 0x01;
        report[50] = 0x10;
        report[87] = 0x04;
        report[88] = 0xFF;
        report[89] = 0xFF;

        Assert.Equal(0x15, RazerReport.Checksum(report));
    }

    [Fact]
    public void Parses_a_real_battery_response()
    {
        var bytes = Convert.FromHexString(RealBatteryResponseHex);

        Assert.True(RazerReport.TryParse(bytes, out var response));
        Assert.Equal(RazerStatus.Successful, response.Status);
        Assert.Equal(0x07, response.CommandClass);
        Assert.Equal(0x80, response.CommandId);
        Assert.Equal(0x40, response.Value);
    }

    [Fact]
    public void Parses_a_charging_response()
    {
        var bytes = Response(RazerStatus.Successful, RazerReport.ChargingStatus, 0x01);

        Assert.True(RazerReport.TryParse(bytes, out var response));
        Assert.Equal(0x84, response.CommandId);
        Assert.Equal(0x01, response.Value);
    }

    [Theory]
    [InlineData(RazerStatus.Busy)]
    [InlineData(RazerStatus.Failure)]
    [InlineData(RazerStatus.Timeout)]
    [InlineData(RazerStatus.NotSupported)]
    public void Parses_non_successful_statuses(RazerStatus status)
    {
        var bytes = Response(status, RazerReport.BatteryLevel, 0x00);

        Assert.True(RazerReport.TryParse(bytes, out var response));
        Assert.Equal(status, response.Status);
    }

    [Fact]
    public void Rejects_a_bad_checksum()
    {
        var bytes = Convert.FromHexString(RealBatteryResponseHex);
        bytes[88] ^= 0xFF;

        Assert.False(RazerReport.TryParse(bytes, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(89)]
    [InlineData(91)]
    public void Rejects_a_wrong_length(int length)
    {
        Assert.False(RazerReport.TryParse(new byte[length], out _));
    }

    [Fact]
    public void Rejects_an_unknown_status()
    {
        var bytes = Response(RazerStatus.Successful, RazerReport.BatteryLevel, 0x40);
        bytes[0] = 0x09;

        Assert.False(RazerReport.TryParse(bytes, out _));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(64, 25)]
    [InlineData(127, 50)]
    [InlineData(128, 50)]
    [InlineData(254, 100)]
    [InlineData(255, 100)]
    public void Scales_raw_level_to_percent_with_rounding(int raw, int expected)
    {
        Assert.Equal(expected, RazerReport.ToPercent((byte)raw));
    }

    internal static byte[] Response(RazerStatus status, RazerCommand command, byte value)
    {
        var bytes = RazerReport.BuildRequest(RazerReport.CobraTransactionId, command);
        bytes[0] = (byte)status;
        bytes[9] = value;
        bytes[88] = RazerReport.Checksum(bytes);
        return bytes;
    }
}
