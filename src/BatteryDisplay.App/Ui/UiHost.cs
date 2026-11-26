using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.Integration;
using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Tray;
using BatteryDisplay.App.Ui.Diagnostics;
using BatteryDisplay.App.Ui.Flyout;
using BatteryDisplay.App.Ui.Settings;
using BatteryDisplay.App.Ui.Themes;
using BatteryDisplay.App.Updates;
using BatteryDisplay.Devices.Diagnostics;

namespace BatteryDisplay.App.Ui;

/// <summary>
/// Hosts the WPF windows inside the WinForms tray app. The WinForms message loop (Application.Run in Program) pumps
/// the WPF dispatcher too, since both live on the same STA thread; WPF only needs an Application object for its
/// resources and theme, with explicit shutdown so closing a window never ends anything. Windows open only in answer
/// to a user action: a left click on a tray icon (flyout) or "Configuración" (settings).
/// </summary>
internal sealed class UiHost : IDisposable
{
    private readonly TrayApplicationContext _tray;
    private readonly FlyoutViewModel _flyoutViewModel;
    private readonly FlyoutToggle _toggle = new(TimeProvider.System);
    private readonly string _exePath = Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private DiagnosticsWindow? _diagnosticsWindow;

    public UiHost(TrayApplicationContext tray)
    {
        _tray = tray;
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AppTheme.Install(app, tray.Settings);

        _flyoutViewModel = new FlyoutViewModel(tray.Devices, tray.Settings, TimeProvider.System);
        _flyoutViewModel.SettingsRequested += OnFlyoutSettingsRequested;
        _tray.TrayIconLeftClicked += OnTrayIconLeftClicked;
        // Subscribing also shows "Configuración" in the tray menu.
        _tray.SettingsRequested += OnSettingsRequested;
    }

    private void OnTrayIconLeftClicked(object? sender, string key)
    {
        var flyout = _flyout ??= CreateFlyout();
        switch (_toggle.Click(flyout.IsVisible))
        {
            case FlyoutClick.Open:
                flyout.ShowAt(System.Windows.Forms.Cursor.Position);
                break;
            case FlyoutClick.Close:
                flyout.Hide();
                break;
        }
    }

    private FlyoutWindow CreateFlyout()
    {
        var flyout = new FlyoutWindow(_flyoutViewModel);
        // WinForms owns the message loop, so WPF needs this to see Tab, arrows and Escape.
        ElementHost.EnableModelessKeyboardInterop(flyout);
        flyout.Dismissed += (_, _) => _toggle.Deactivated();
        return flyout;
    }

    private void OnFlyoutSettingsRequested(object? sender, EventArgs e)
    {
        _flyout?.Hide();
        ShowSettings();
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => ShowSettings();

    /// <summary>One settings window at a time: a second request brings the open one forward.</summary>
    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            var viewModel = new SettingsViewModel(_tray.Devices, _tray.Settings, new StartupSetting(_exePath), Version(), AppRepository.Url, OpenUrl);
            var window = new SettingsWindow(viewModel);
            ElementHost.EnableModelessKeyboardInterop(window);
            viewModel.DiagnosticsRequested += (_, _) => ShowDiagnostics(window);
            window.Closed += (_, _) =>
            {
                viewModel.Dispose();
                _settingsWindow = null;
            };
            _settingsWindow = window;
        }
        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized)
            _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    /// <summary>
    /// One diagnostic window at a time, owned by the settings window so it closes with it. Each opening builds a
    /// fresh report, so plugging the device in and opening it again shows the change.
    /// </summary>
    private void ShowDiagnostics(Window owner)
    {
        if (_diagnosticsWindow is null)
        {
            var saver = new ReportFileDialog();
            var diagnostics = new DeviceDiagnostics();
            var viewModel = new DiagnosticsViewModel(
                () => DiagnosticReport.Build(Version(), RuntimeInformation.OSDescription, diagnostics.Collect()),
                new WpfClipboard(), saver);
            var window = new DiagnosticsWindow(viewModel) { Owner = owner };
            saver.Owner = window;
            ElementHost.EnableModelessKeyboardInterop(window);
            window.Closed += (_, _) => _diagnosticsWindow = null;
            _diagnosticsWindow = window;
        }
        _diagnosticsWindow.Show();
        if (_diagnosticsWindow.WindowState == WindowState.Minimized)
            _diagnosticsWindow.WindowState = WindowState.Normal;
        _diagnosticsWindow.Activate();
    }

    internal static string Version()
    {
        var assembly = typeof(UiHost).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "";
        // Drops the "+commit" build metadata the SDK appends.
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Write("ui", $"Could not open {url}", ex);
        }
    }

    public void Dispose()
    {
        _tray.TrayIconLeftClicked -= OnTrayIconLeftClicked;
        _tray.SettingsRequested -= OnSettingsRequested;
        _flyoutViewModel.SettingsRequested -= OnFlyoutSettingsRequested;
        _diagnosticsWindow?.Close();
        _settingsWindow?.Close();
        _flyout?.CloseForGood();
        _flyoutViewModel.Dispose();
        AppTheme.Uninstall();
    }
}
