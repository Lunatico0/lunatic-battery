namespace BatteryDisplay.App.Notifications;

/// <summary>The icon a notice carries: a warning (low battery) or plain information (an update check).</summary>
public enum NoticeKind { Warning, Info }

/// <summary>A tray icon that can carry a notice. Key is a device Id or the combined icon's key.</summary>
public interface INoticeIcon
{
    string Key { get; }
    bool IsShown { get; }
    void ShowNotice(string title, string text, NoticeKind kind);
}

/// <summary>
/// Shows the notice on the device's own tray icon when it has a visible one in the current mode, otherwise on the
/// first visible icon. With no visible icon there is nowhere to show it, and it reports false.
/// </summary>
public sealed class TrayIconNotifier(Func<IReadOnlyList<INoticeIcon>> icons) : ILowBatteryNotifier
{
    public bool Notify(LowBatteryAlert alert)
    {
        var shown = icons().Where(i => i.IsShown).ToArray();
        var icon = shown.FirstOrDefault(i => i.Key == alert.DeviceId) ?? shown.FirstOrDefault();
        if (icon is null)
            return false;
        icon.ShowNotice(LowBatteryAlert.Title, alert.Text, NoticeKind.Warning);
        return true;
    }
}
