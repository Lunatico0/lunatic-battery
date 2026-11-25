using BatteryDisplay.Core;
using BatteryDisplay.Devices.SteelSeries;

namespace BatteryDisplay.Devices.Tests.SteelSeries;

public class ArctisNova5ExchangeTests
{
    private static byte[] Payload(params byte[] head)
    {
        var payload = new byte[64];
        head.CopyTo(payload, 0);
        return payload;
    }

    private sealed class FakeChannel(params byte[]?[] replies)
    {
        private readonly Queue<byte[]?> _replies = new(replies);
        public List<byte[]> Written { get; } = [];
        public int Reads { get; private set; }

        public void Write(byte[] report) => Written.Add(report);

        public byte[]? Read()
        {
            Reads++;
            return _replies.Count > 0 ? _replies.Dequeue() : null;
        }
    }

    [Fact]
    public void Sends_only_the_status_request_once()
    {
        var channel = new FakeChannel(Payload(0xB0, 0x03, 0x03, 0x0C, 0x03));

        ArctisNova5Protocol.QueryStatus(channel.Write, channel.Read, outputReportLength: 65);

        var written = Assert.Single(channel.Written);
        Assert.Equal(ArctisNova5Protocol.BuildStatusRequest(65), written);
    }

    [Fact]
    public void Skips_reports_from_other_clients_until_the_status_report()
    {
        var channel = new FakeChannel(
            Payload(0x20, 0x01, 0x02),
            Payload(0xB0, 0x03, 0x03, 0x41, 0x01));

        var status = ArctisNova5Protocol.QueryStatus(channel.Write, channel.Read, 65);

        Assert.Equal(new BatteryStatus(65, ChargeState.Charging), status);
    }

    [Fact]
    public void Offline_headset_yields_null()
    {
        var channel = new FakeChannel(Payload(0xB0, 0x02));

        Assert.Null(ArctisNova5Protocol.QueryStatus(channel.Write, channel.Read, 65));
    }

    [Fact]
    public void Read_timeout_yields_null()
    {
        var channel = new FakeChannel();

        Assert.Null(ArctisNova5Protocol.QueryStatus(channel.Write, channel.Read, 65));
        Assert.Equal(1, channel.Reads);
    }

    [Fact]
    public void Gives_up_after_eight_foreign_reports()
    {
        var foreign = Enumerable.Range(0, 20).Select(_ => (byte[]?)Payload(0x20)).ToArray();
        var channel = new FakeChannel(foreign);

        Assert.Null(ArctisNova5Protocol.QueryStatus(channel.Write, channel.Read, 65));
        Assert.Equal(8, channel.Reads);
    }
}
