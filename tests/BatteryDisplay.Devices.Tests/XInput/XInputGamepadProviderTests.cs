using BatteryDisplay.Core;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Tests.XInput;

public class XInputGamepadProviderTests
{
    [Fact]
    public async Task No_pad_connected_returns_null()
    {
        var provider = new XInputGamepadProvider(new FakeXInputApi([]), slot: 0);
        Assert.Null(await provider.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Reads_only_its_own_slot()
    {
        var api = new FakeXInputApi(new()
        {
            [2] = new XInputBatteryInformation(XInputBatteryType.Alkaline, XInputBatteryLevel.Medium),
            [3] = new XInputBatteryInformation(XInputBatteryType.NiMH, XInputBatteryLevel.Full),
        });

        var status = await new XInputGamepadProvider(api, slot: 3).ReadAsync(CancellationToken.None);

        Assert.Equal(new BatteryStatus(100, ChargeState.Discharging), status);
        Assert.Equal([3u], api.BatteryQueries);
    }

    [Fact]
    public async Task Pad_disconnected_from_its_slot_returns_null_even_if_another_slot_has_one()
    {
        var api = new FakeXInputApi(new()
        {
            [0] = new XInputBatteryInformation(XInputBatteryType.Alkaline, XInputBatteryLevel.Full),
        });

        Assert.Null(await new XInputGamepadProvider(api, slot: 1).ReadAsync(CancellationToken.None));
        Assert.Empty(api.BatteryQueries);
    }

    [Fact]
    public async Task Battery_query_failure_returns_null()
    {
        var api = new FakeXInputApi(new() { [0] = null });
        Assert.Null(await new XInputGamepadProvider(api, slot: 0).ReadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(0u, "xinput-0", "Control 1")]
    [InlineData(3u, "xinput-3", "Control 4")]
    public void Identity_follows_the_slot(uint slot, string id, string name)
    {
        var provider = new XInputGamepadProvider(new FakeXInputApi([]), slot);

        Assert.Equal(id, provider.Id);
        Assert.Equal(name, provider.Name);
        Assert.Equal(DeviceKind.Gamepad, provider.Kind);
    }

    [Theory]
    [InlineData(4u)]
    [InlineData(uint.MaxValue)]
    public void Rejects_a_slot_outside_0_to_3(uint slot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XInputGamepadProvider(new FakeXInputApi([]), slot));
    }
}
