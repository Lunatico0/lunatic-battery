using BatteryDisplay.Core;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Tests.XInput;

public class XInputGamepadProviderTests
{
    private sealed class FakeApi(Dictionary<uint, XInputBatteryInformation?> pads) : IXInputApi
    {
        public List<uint> BatteryQueries { get; } = [];

        public bool IsConnected(uint userIndex) => pads.ContainsKey(userIndex);

        public XInputBatteryInformation? GetGamepadBattery(uint userIndex)
        {
            BatteryQueries.Add(userIndex);
            return pads[userIndex];
        }
    }

    [Fact]
    public async Task No_pad_connected_returns_null()
    {
        var provider = new XInputGamepadProvider(new FakeApi([]));
        Assert.Null(await provider.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Uses_first_connected_user_index()
    {
        var api = new FakeApi(new()
        {
            [2] = new XInputBatteryInformation(XInputBatteryType.Alkaline, XInputBatteryLevel.Medium),
            [3] = new XInputBatteryInformation(XInputBatteryType.NiMH, XInputBatteryLevel.Full),
        });

        var status = await new XInputGamepadProvider(api).ReadAsync(CancellationToken.None);

        Assert.Equal(new BatteryStatus(50, ChargeState.Discharging), status);
        Assert.Equal([2u], api.BatteryQueries);
    }

    [Fact]
    public async Task Battery_query_failure_returns_null()
    {
        var api = new FakeApi(new() { [0] = null });
        Assert.Null(await new XInputGamepadProvider(api).ReadAsync(CancellationToken.None));
    }
}
