using BatteryDisplay.Core;
using BatteryDisplay.Devices.Pnp;
using HidSharp;
using Xunit.Abstractions;

namespace BatteryDisplay.Devices.Tests.Pnp;

public class CfgMgrHidContainerResolverTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(@"\\?\hid#vid_ffff&pid_ffff#0&0000&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    [InlineData("")]
    public void Path_of_no_present_interface_has_no_container(string path)
    {
        Assert.Null(new CfgMgrHidContainerResolver().ContainerOf(path));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void Present_hid_collections_resolve_to_their_device_container()
    {
        var resolver = new CfgMgrHidContainerResolver();
        foreach (var device in DeviceList.Local.GetHidDevices().DistinctBy(d => (d.VendorID, d.ProductID)))
        {
            var container = resolver.ContainerOf(device.DevicePath);
            output.WriteLine($"{device.VendorID:X4}:{device.ProductID:X4} {container} identifies {PhysicalIdentity.Identifies(container)}");
            Assert.NotNull(container);
        }
    }
}
