using BatteryDisplay.Core;
using BatteryDisplay.Devices.Redragon;
using static BatteryDisplay.Devices.Tests.Redragon.RedragonFrames;

namespace BatteryDisplay.Devices.Tests.Redragon;

public class RedragonBatteryStateTests
{
    private static RedragonFrame Parsed(byte[] report) =>
        RedragonProtocol.TryParse(report, out var frame) ? frame : throw new InvalidOperationException("test vector does not parse");

    private static BatteryStatus? After(params byte[][] reports) =>
        reports.Aggregate(RedragonBatteryState.Unknown, (state, report) => state.Apply(Parsed(report))).Reading;

    [Fact]
    public void Nothing_received_yet_is_no_reading()
    {
        Assert.Null(RedragonBatteryState.Unknown.Reading);
    }

    [Fact]
    public void Captured_sequence_gives_the_right_reading_after_each_frame()
    {
        var expected = new BatteryStatus?[]
        {
            new(75, ChargeState.Charging),  // cable plugged, nothing on battery seen yet: the reported level
            new(75, ChargeState.Charging),
            new(75, ChargeState.Charging),  // link lost: kept
            new(75, ChargeState.Charging),  // link up: kept
            new(100, ChargeState.Charging), // cable in, still no on battery level to fall back on
            new(100, ChargeState.Charging),
            new(100, ChargeState.Charging),
            new(86, ChargeState.Discharging),
            new(85, ChargeState.Discharging),
            new(84, ChargeState.Discharging),
        };

        var state = RedragonBatteryState.Unknown;
        for (var i = 0; i < Captured.Length; i++)
        {
            state = state.Apply(Parsed(Captured[i]));
            Assert.True(expected[i] == state.Reading, $"after frame {i}: expected {expected[i]}, got {state.Reading}");
        }
    }

    [Fact]
    public void Cable_plugged_after_running_on_battery_keeps_the_last_on_battery_level()
    {
        var reading = After([.. Captured, Cable100]);

        Assert.Equal(new BatteryStatus(84, ChargeState.Charging), reading);
    }

    [Fact]
    public void Plug_flag_0x11_counts_as_cable_in()
    {
        Assert.Equal(new BatteryStatus(84, ChargeState.Charging), After(OnBattery84, CablePlugged75));
    }

    [Fact]
    public void Link_lost_while_on_battery_keeps_the_reading()
    {
        Assert.Equal(new BatteryStatus(85, ChargeState.Discharging), After(OnBattery85, LinkLost));
    }

    [Fact]
    public void Link_frames_alone_give_no_reading()
    {
        Assert.Null(After(LinkLost, LinkUp));
    }

    [Fact]
    public void Unplugging_the_cable_reads_the_level_on_battery_again()
    {
        Assert.Equal(new BatteryStatus(83, ChargeState.Discharging),
            After(OnBattery84, Cable100, Frame(0x05, 83, flags: 0x01)));
    }
}
