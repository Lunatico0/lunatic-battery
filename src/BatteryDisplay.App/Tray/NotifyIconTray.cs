using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;

namespace BatteryDisplay.App.Tray;

/// <summary>One NotifyIcon bound to one provider. Owns the HICON it draws and destroys it when replaced.</summary>
internal sealed class NotifyIconTray(ContextMenuStrip menu) : ITrayIcon, IDisposable
{
    private readonly NotifyIcon _notifyIcon = new() { ContextMenuStrip = menu, Visible = false };
    private Icon? _icon;
    private IntPtr _hIcon;
    private (TrayIconModel Model, Size Size)? _drawn;
    private bool _disposed;

    public void Apply(TrayIconModel model)
    {
        if (_disposed)
            return;
        if (!model.Visible)
        {
            _notifyIcon.Visible = false;
            return;
        }

        var size = SystemInformation.SmallIconSize;
        if (_drawn != (model, size))
        {
            ReplaceIcon(model, size);
            _drawn = (model, size);
        }
        _notifyIcon.Text = model.Tooltip;
        _notifyIcon.Visible = true;
    }

    private void ReplaceIcon(TrayIconModel model, Size size)
    {
        using var bitmap = BatteryIconRenderer.Draw(model, size);
        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        _notifyIcon.Icon = icon;
        ReleaseIcon();
        _icon = icon;
        _hIcon = hIcon;
    }

    private void ReleaseIcon()
    {
        _icon?.Dispose();
        _icon = null;
        if (_hIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ReleaseIcon();
    }
}
