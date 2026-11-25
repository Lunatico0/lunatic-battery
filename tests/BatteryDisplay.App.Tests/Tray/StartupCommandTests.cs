using BatteryDisplay.App.Tray;

namespace BatteryDisplay.App.Tests.Tray;

public class StartupCommandTests
{
    private const string Exe = @"C:\Program Files\Battery Display\BatteryDisplay.App.exe";

    [Fact]
    public void QuotesTheExePath()
    {
        Assert.Equal("\"" + Exe + "\"", StartupCommand.For(Exe));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Battery Display\\BatteryDisplay.App.exe\"", true)]
    [InlineData("\"c:\\program files\\battery display\\batterydisplay.app.exe\"", true)]
    [InlineData("\"C:\\Other\\BatteryDisplay.App.exe\"", false)]
    [InlineData(null, false)]
    public void Matches(string? value, bool expected)
    {
        Assert.Equal(expected, StartupCommand.Matches(value, Exe));
    }
}
