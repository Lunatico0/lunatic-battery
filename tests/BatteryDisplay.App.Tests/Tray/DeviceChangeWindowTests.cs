using BatteryDisplay.App.Tray;

namespace BatteryDisplay.App.Tests.Tray;

public class DeviceChangeWindowTests
{
    private const int WmDeviceChange = 0x0219;
    private const int DbtDevNodesChanged = 0x0007;
    private const int DbtDeviceArrival = 0x8000;
    private const int WmPaint = 0x000F;

    [Theory]
    [InlineData(WmDeviceChange, DbtDevNodesChanged, true)]
    [InlineData(WmDeviceChange, DbtDeviceArrival, false)]
    [InlineData(WmPaint, DbtDevNodesChanged, false)]
    public void OnlyDevNodesChanged_CountsAsAHardwareChange(int msg, int wParam, bool expected)
    {
        Assert.Equal(expected, DeviceChangeWindow.IsDevNodesChanged(msg, wParam));
    }
}
