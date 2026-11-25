using BatteryDisplay.Core;
using BatteryDisplay.Devices.Razer;

namespace BatteryDisplay.Devices.Tests.Razer;

public class RazerBatteryReaderTests
{
    private static readonly RazerTiming NoWait = new(TimeSpan.Zero, TimeSpan.Zero, MaxTries: 5);

    [Fact]
    public async Task Reads_level_and_discharging()
    {
        var mouse = new FakeMouse { Battery = 0x40, Charging = 0x00 };

        var status = await Read(mouse);

        Assert.Equal(new BatteryStatus(25, ChargeState.Discharging), status);
    }

    [Fact]
    public async Task Reports_charging_below_full()
    {
        var mouse = new FakeMouse { Battery = 128, Charging = 0x01 };

        Assert.Equal(new BatteryStatus(50, ChargeState.Charging), await Read(mouse));
    }

    [Fact]
    public async Task Reports_full_when_charging_at_100()
    {
        var mouse = new FakeMouse { Battery = 255, Charging = 0x01 };

        Assert.Equal(new BatteryStatus(100, ChargeState.Full), await Read(mouse));
    }

    [Fact]
    public async Task Not_charging_at_100_is_discharging()
    {
        var mouse = new FakeMouse { Battery = 255, Charging = 0x00 };

        Assert.Equal(new BatteryStatus(100, ChargeState.Discharging), await Read(mouse));
    }

    [Fact]
    public async Task Retries_while_busy_and_then_reads()
    {
        var mouse = new FakeMouse { Battery = 0x40 };
        mouse.Statuses.Enqueue(RazerStatus.Busy);
        mouse.Statuses.Enqueue(RazerStatus.Timeout);

        Assert.Equal(new BatteryStatus(25, ChargeState.Discharging), await Read(mouse));
    }

    [Theory]
    [InlineData(RazerStatus.Busy)]
    [InlineData(RazerStatus.Timeout)]
    public async Task Returns_null_when_the_mouse_never_answers(RazerStatus status)
    {
        var mouse = new FakeMouse();
        for (var i = 0; i < 20; i++)
            mouse.Statuses.Enqueue(status);

        Assert.Null(await Read(mouse));
        Assert.Equal(NoWait.MaxTries, mouse.Requests.Count);
    }

    [Theory]
    [InlineData(RazerStatus.NotSupported)]
    [InlineData(RazerStatus.Failure)]
    public async Task Unsupported_level_gives_null_percent(RazerStatus status)
    {
        var mouse = new FakeMouse { Charging = 0x00 };
        mouse.Statuses.Enqueue(status);

        Assert.Equal(new BatteryStatus(null, ChargeState.Discharging), await Read(mouse));
    }

    [Fact]
    public async Task Unsupported_charging_gives_unknown_charge()
    {
        var mouse = new FakeMouse { Battery = 0x40, ChargingStatus = RazerStatus.NotSupported };

        Assert.Equal(new BatteryStatus(25, ChargeState.Unknown), await Read(mouse));
    }

    [Fact]
    public async Task Retries_on_a_response_to_another_command()
    {
        var mouse = new FakeMouse { Battery = 0x40, Mismatches = 2 };

        Assert.Equal(new BatteryStatus(25, ChargeState.Discharging), await Read(mouse));
    }

    [Fact]
    public async Task Retries_on_a_bad_checksum()
    {
        var mouse = new FakeMouse { Battery = 0x40, BadChecksums = 1 };

        Assert.Equal(new BatteryStatus(25, ChargeState.Discharging), await Read(mouse));
    }

    [Fact]
    public async Task Returns_null_when_the_transport_fails()
    {
        var mouse = new FakeMouse { Broken = true };

        Assert.Null(await Read(mouse));
    }

    [Fact]
    public async Task Sends_only_the_two_documented_get_commands()
    {
        var mouse = new FakeMouse { Battery = 0x40 };

        await Read(mouse);

        Assert.All(mouse.Requests, request =>
        {
            Assert.Equal(0x1F, request[1]);
            Assert.Equal(0x02, request[5]);
            Assert.Equal(0x07, request[6]);
            Assert.Contains(request[7], new byte[] { 0x80, 0x84 });
        });
        Assert.Equal(new byte[] { 0x80, 0x84 }, mouse.Requests.Select(r => r[7]));
    }

    private static Task<BatteryStatus?> Read(FakeMouse mouse) =>
        new RazerBatteryReader(mouse, RazerReportTests.CobraTransactionId, NoWait).ReadAsync(CancellationToken.None);

    private sealed class FakeMouse : IRazerTransport
    {
        public byte Battery { get; init; }
        public byte Charging { get; init; }
        public RazerStatus ChargingStatus { get; init; } = RazerStatus.Successful;
        public bool Broken { get; init; }
        public int Mismatches { get; set; }
        public int BadChecksums { get; set; }
        public Queue<RazerStatus> Statuses { get; } = new();
        public List<byte[]> Requests { get; } = [];

        private byte[]? _pending;

        public bool TrySend(byte[] report)
        {
            if (Broken)
                return false;

            Requests.Add(report.ToArray());
            var command = new RazerCommand(report[6], report[7], report[5]);
            var isCharging = command == RazerReport.ChargingStatus;
            var status = Statuses.Count > 0
                ? Statuses.Dequeue()
                : isCharging ? ChargingStatus : RazerStatus.Successful;
            var response = RazerReportTests.Response(status, command, isCharging ? Charging : Battery);

            if (Mismatches > 0)
            {
                Mismatches--;
                response[7] ^= 0x01;
                response[88] = RazerReport.Checksum(response);
            }
            if (BadChecksums > 0)
            {
                BadChecksums--;
                response[88] ^= 0xFF;
            }

            _pending = response;
            return true;
        }

        public byte[]? TryReceive()
        {
            var response = _pending;
            _pending = null;
            return response;
        }
    }
}
