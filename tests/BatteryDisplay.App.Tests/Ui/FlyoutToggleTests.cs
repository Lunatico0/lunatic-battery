using BatteryDisplay.App.Tests.Settings;
using BatteryDisplay.App.Ui.Flyout;
using static BatteryDisplay.App.Tests.Ui.Snapshots;

namespace BatteryDisplay.App.Tests.Ui;

/// <summary>
/// Clicking the tray icon while the flyout is open first deactivates it (the taskbar takes the mouse down),
/// so the click that follows must not reopen it.
/// </summary>
public class FlyoutToggleTests
{
    private readonly ManualTimeProvider _time = new(T0);

    [Fact]
    public void ClickWhileClosed_Opens()
    {
        var toggle = new FlyoutToggle(_time);

        Assert.Equal(FlyoutClick.Open, toggle.Click(isOpen: false));
    }

    [Fact]
    public void ClickWhileOpen_Closes()
    {
        var toggle = new FlyoutToggle(_time);

        Assert.Equal(FlyoutClick.Close, toggle.Click(isOpen: true));
    }

    [Fact]
    public void ClickRightAfterDeactivation_StaysClosed()
    {
        var toggle = new FlyoutToggle(_time);
        toggle.Deactivated();

        _time.Now = T0.AddMilliseconds(200);

        Assert.Equal(FlyoutClick.Ignore, toggle.Click(isOpen: false));
    }

    [Fact]
    public void TheGuardIsSpentByOneClick()
    {
        var toggle = new FlyoutToggle(_time);
        toggle.Deactivated();
        toggle.Click(isOpen: false);

        Assert.Equal(FlyoutClick.Open, toggle.Click(isOpen: false));
    }

    [Fact]
    public void ClickLongAfterDeactivation_Opens()
    {
        var toggle = new FlyoutToggle(_time);
        toggle.Deactivated();

        _time.Now = T0 + FlyoutToggle.ReopenGuard + TimeSpan.FromMilliseconds(1);

        Assert.Equal(FlyoutClick.Open, toggle.Click(isOpen: false));
    }
}
