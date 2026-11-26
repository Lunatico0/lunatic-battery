using BatteryDisplay.App.Notifications;

namespace BatteryDisplay.App.Tests.Notifications;

internal sealed class FakeNoticeIcon(string key, bool isShown = true) : INoticeIcon
{
    public string Key => key;
    public bool IsShown { get; set; } = isShown;
    public List<(string Title, string Text)> Notices { get; } = [];
    public List<NoticeKind> Kinds { get; } = [];

    public void ShowNotice(string title, string text, NoticeKind kind)
    {
        Notices.Add((title, text));
        Kinds.Add(kind);
    }
}

public class TrayIconNotifierTests
{
    private static readonly LowBatteryAlert Alert = new("mouse", "Razer Cobra", 12);

    private static bool Notify(params FakeNoticeIcon[] icons) => new TrayIconNotifier(() => icons).Notify(Alert);

    [Fact]
    public void ShowsTheNotice_OnTheDevicesOwnIcon()
    {
        var combined = new FakeNoticeIcon("*combined");
        var mouse = new FakeNoticeIcon("mouse");

        Assert.True(Notify(combined, mouse));

        Assert.Equal([(LowBatteryAlert.Title, "Razer Cobra: 12%")], mouse.Notices);
        Assert.Equal([NoticeKind.Warning], mouse.Kinds);
        Assert.Empty(combined.Notices);
    }

    [Fact]
    public void WithoutAnIconOfItsOwn_UsesTheFirstVisibleOne()
    {
        var hidden = new FakeNoticeIcon("pad", isShown: false);
        var combined = new FakeNoticeIcon("*combined");
        var mouse = new FakeNoticeIcon("mouse", isShown: false);

        Assert.True(Notify(hidden, combined, mouse));

        Assert.Single(combined.Notices);
        Assert.Empty(hidden.Notices);
        Assert.Empty(mouse.Notices);
    }

    [Fact]
    public void NoVisibleIcon_ShowsNothing_AndReportsIt()
    {
        var mouse = new FakeNoticeIcon("mouse", isShown: false);

        Assert.False(Notify(mouse));
        Assert.False(Notify());

        Assert.Empty(mouse.Notices);
    }
}
