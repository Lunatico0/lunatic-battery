namespace BatteryDisplay.App.Ui.Flyout;

public enum FlyoutClick { Open, Close, Ignore }

/// <summary>
/// Decides what a tray click does to the flyout. Pressing the tray icon while the flyout is open deactivates it
/// (the taskbar takes the mouse down) before the click arrives, so a click right after a deactivation is the
/// user closing it, not asking to reopen it.
/// </summary>
public sealed class FlyoutToggle(TimeProvider time)
{
    public static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(500);

    private DateTimeOffset? _deactivatedAt;

    /// <summary>The flyout closed because it lost activation.</summary>
    public void Deactivated() => _deactivatedAt = time.GetUtcNow();

    public FlyoutClick Click(bool isOpen)
    {
        var deactivatedAt = _deactivatedAt;
        _deactivatedAt = null;
        if (isOpen)
            return FlyoutClick.Close;
        return deactivatedAt is { } at && time.GetUtcNow() - at <= ReopenGuard ? FlyoutClick.Ignore : FlyoutClick.Open;
    }
}
