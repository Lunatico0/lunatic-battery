using BatteryDisplay.Devices.XInput;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.XInput;

[Trait("Category", "Hardware")]
public class XInputHardwareTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Reads_real_gamepad()
    {
        var api = new XInputNativeApi();
        for (uint i = 0; i < 4; i++)
        {
            var connected = api.IsConnected(i);
            var raw = connected ? api.GetGamepadBattery(i) : null;
            output.WriteLine($"user {i}: connected={connected} raw={raw?.ToString() ?? "n/a"} rawType=0x{(byte?)raw?.Type:X2} rawLevel=0x{(byte?)raw?.Level:X2}");
        }

        foreach (var provider in new XInputDeviceSource().Discover())
        {
            var status = await provider.ReadAsync(CancellationToken.None);
            output.WriteLine($"{provider.Id} ({provider.Name}): {status?.ToString() ?? "null"}");
        }
    }
}
