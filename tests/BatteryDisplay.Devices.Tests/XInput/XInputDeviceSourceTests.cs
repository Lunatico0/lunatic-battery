using BatteryDisplay.Core;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.Devices.Tests.XInput;

public class XInputDeviceSourceTests
{
    private static readonly XInputBatteryInformation Full = new(XInputBatteryType.NiMH, XInputBatteryLevel.Full);

    [Fact]
    public void No_pad_connected_gives_no_provider()
    {
        Assert.Empty(new XInputDeviceSource(new FakeXInputApi([])).Discover());
    }

    [Fact]
    public void Gives_one_gamepad_provider_per_connected_slot()
    {
        var source = new XInputDeviceSource(new FakeXInputApi(new() { [0] = Full, [2] = Full }));

        var providers = source.Discover();

        Assert.Equal(new[] { "xinput-0", "xinput-2" }, providers.Select(p => p.Id));
        Assert.Equal(new[] { "Control 1", "Control 3" }, providers.Select(p => p.Name));
        Assert.All(providers, p => Assert.Equal(DeviceKind.Gamepad, p.Kind));
    }

    [Fact]
    public void Discover_only_checks_connection_and_never_queries_the_battery()
    {
        var api = new FakeXInputApi(new() { [1] = Full });

        new XInputDeviceSource(api).Discover();

        Assert.Empty(api.BatteryQueries);
    }

    [Fact]
    public void Gamepad_is_reached_over_xinput_and_has_no_physical_identity()
    {
        var provider = Assert.Single(new XInputDeviceSource(new FakeXInputApi(new() { [0] = Full })).Discover());

        Assert.Equal(DeviceChannel.XInput, provider.Channel);
        Assert.Null(provider.ContainerId);
    }

    [Fact]
    public void Rediscovery_returns_the_same_provider_instance()
    {
        var source = new XInputDeviceSource(new FakeXInputApi(new() { [1] = Full }));

        Assert.Same(Assert.Single(source.Discover()), Assert.Single(source.Discover()));
    }

    [Fact]
    public void Missing_xinput_gives_an_empty_list()
    {
        var api = new FakeXInputApi([]) { Failure = new DllNotFoundException("xinput1_4.dll") };

        Assert.Empty(new XInputDeviceSource(api).Discover());
    }
}
