using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using BatteryDisplay.App.Tray;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace BatteryDisplay.App.Ui.Flyout;

/// <summary>
/// The flyout window. Code-behind only places it next to the notification area and hides it; what it shows is
/// <see cref="FlyoutViewModel"/>. The window is created once and shown and hidden on each tray click.
/// </summary>
public partial class FlyoutWindow : Window
{
    private const double MarginDips = 12;

    private readonly FlyoutViewModel _viewModel;
    private readonly DispatcherTimer _clock;
    private System.Drawing.Point _anchor;
    private bool _closing;

    public FlyoutWindow(FlyoutViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        // Moves "Actualizado hace" while the flyout is open.
        _clock = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => _viewModel.Tick();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                _clock.Start();
            else
                _clock.Stop();
        };
        // A new row, or a move to a monitor with another scale, changes the size: keep the corner in place.
        SizeChanged += (_, _) => Reposition();
    }

    /// <summary>Raised when the flyout hides itself because it lost activation (a click elsewhere, the tray icon included).</summary>
    public event EventHandler? Dismissed;

    /// <summary>Shows the flyout in the work area of the monitor that holds <paramref name="anchor"/> (the tray click, in physical screen pixels).</summary>
    public void ShowAt(System.Drawing.Point anchor)
    {
        _anchor = anchor;
        _viewModel.Tick();
        Show();
        Reposition();
        // The user just clicked our tray icon, so taking the foreground here is the expected response.
        Activate();
    }

    /// <summary>Closes for real; until then a close request (Alt+F4) only hides the flyout.</summary>
    public void CloseForGood()
    {
        _closing = true;
        _clock.Stop();
        Close();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!IsVisible)
            return;
        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.Escape)
            return;
        e.Handled = true;
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closing)
            return;
        e.Cancel = true;
        Hide();
    }

    private void Reposition()
    {
        if (!IsVisible)
            return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var rect))
            return;
        // Measured at the scale of the monitor under the click, not the one the window was last on: moving there
        // resizes it (WM_DPICHANGED), and the SizeChanged that follows places it again with its real size.
        if (FlyoutPlacement.MonitorAt(FlyoutMonitors.All(), _anchor) is not { } monitor)
            return;
        var size = new System.Drawing.Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        var corner = FlyoutPlacement.Place(monitor, size, FlyoutMonitors.ScaleOf(handle), MarginDips);
        if (corner.X == rect.Left && corner.Y == rect.Top)
            return;
        NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopmost, corner.X, corner.Y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }
}
