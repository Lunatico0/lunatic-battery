using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Forms.Integration;
using BatteryDisplay.App.Tray;
using BatteryDisplay.App.Ui.Flyout;
using BatteryDisplay.App.Ui.Settings;
using BatteryDisplay.App.Ui.Themes;

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

    public UiHost(TrayApplicationContext tray)
    {
        _tray = tray;
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AppTheme.Install(app);

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
            var viewModel = new SettingsViewModel(_tray.Devices, _tray.Settings, new StartupSetting(_exePath), Version(), OpenUrl);
            var window = new SettingsWindow(viewModel);
            ElementHost.EnableModelessKeyboardInterop(window);
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

    private static string Version()
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
            Debug.WriteLine($"Could not open {url}: {ex.Message}", "BatteryDisplay");
        }
    }

    public void Dispose()
    {
        _tray.TrayIconLeftClicked -= OnTrayIconLeftClicked;
        _tray.SettingsRequested -= OnSettingsRequested;
        _flyoutViewModel.SettingsRequested -= OnFlyoutSettingsRequested;
        _settingsWindow?.Close();
        _flyout?.CloseForGood();
        _flyoutViewModel.Dispose();
        AppTheme.Uninstall();
    }
}
