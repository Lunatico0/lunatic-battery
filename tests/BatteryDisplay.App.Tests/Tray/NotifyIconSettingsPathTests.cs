using BatteryDisplay.App.Tray;

namespace BatteryDisplay.App.Tests.Tray;

public class NotifyIconSettingsPathTests
{
    private static readonly Guid ProgramFiles = new("6D809377-6AF0-444B-8957-A3773F02200E");
    private const string Exe = @"C:\Program Files\Battery Display\BatteryDisplay.App.exe";

    private static string? Resolve(Guid id) => id == ProgramFiles ? @"C:\Program Files" : null;

    [Theory]
    [InlineData(@"C:\Program Files\Battery Display\BatteryDisplay.App.exe", true)]
    [InlineData(@"c:\program files\battery display\batterydisplay.app.exe", true)]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Battery Display\BatteryDisplay.App.exe", true)]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Steam\steam.exe", false)]
    [InlineData(@"{00000000-0000-0000-0000-000000000000}\Battery Display\BatteryDisplay.App.exe", false)]
    [InlineData(@"{not-a-guid}\Battery Display\BatteryDisplay.App.exe", false)]
    [InlineData("", false)]
    public void Matches(string registryPath, bool expected)
    {
        Assert.Equal(expected, NotifyIconSettingsPath.Matches(registryPath, Exe, Resolve));
    }
}
