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

    // Velopack layout: %LocalAppData%\<packId>\Update.exe, the stub <root>\App.exe and the real exe in current\.
    private const string Root = @"C:\Users\me\AppData\Local\LunaticBattery";
    private const string Installed = Root + @"\current\BatteryDisplay.App.exe";
    private const string Stub = Root + @"\BatteryDisplay.App.exe";
    private const string Updater = Root + @"\Update.exe";

    private static Func<string, bool> Existing(params string[] files) =>
        path => files.Contains(path, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void LaunchPath_InVelopackInstall_IsTheStubAtTheInstallRoot()
    {
        Assert.Equal(Stub, StartupCommand.LaunchPath(Installed, Existing(Updater, Stub)));
    }

    [Fact]
    public void LaunchPath_CurrentFolderNameIgnoresCase()
    {
        var installed = Root + @"\Current\BatteryDisplay.App.exe";

        Assert.Equal(Stub, StartupCommand.LaunchPath(installed, Existing(Updater, Stub)));
    }

    [Fact]
    public void LaunchPath_OutsideAnInstall_IsTheProcessPath()
    {
        const string bin = @"E:\src\BatteryDisplay.App\bin\Debug\net10.0-windows\BatteryDisplay.App.exe";

        Assert.Equal(bin, StartupCommand.LaunchPath(bin, Existing(Updater, Stub)));
    }

    [Fact]
    public void LaunchPath_CurrentFolderWithoutUpdater_IsTheProcessPath()
    {
        Assert.Equal(Installed, StartupCommand.LaunchPath(Installed, Existing(Stub)));
    }

    [Fact]
    public void LaunchPath_InstallWithoutStub_IsTheProcessPath()
    {
        Assert.Equal(Installed, StartupCommand.LaunchPath(Installed, Existing(Updater)));
    }
}
