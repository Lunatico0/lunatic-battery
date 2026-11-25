namespace BatteryDisplay.App.Tray;

/// <summary>
/// Hidden top-level window that reports hardware changes. WM_DEVICECHANGE is broadcast only to top-level
/// windows, so this cannot be a message-only window. DBT_DEVNODES_CHANGED needs no registration.
/// </summary>
internal sealed class DeviceChangeWindow : NativeWindow, IDisposable
{
    private const int WmDeviceChange = 0x0219;
    private const int DbtDevNodesChanged = 0x0007;

    private readonly Action _onChange;

    public DeviceChangeWindow(Action onChange)
    {
        _onChange = onChange;
        CreateHandle(new CreateParams());
    }

    internal static bool IsDevNodesChanged(int msg, nint wParam) => msg == WmDeviceChange && wParam == DbtDevNodesChanged;

    protected override void WndProc(ref Message m)
    {
        if (IsDevNodesChanged(m.Msg, m.WParam))
            _onChange();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}
