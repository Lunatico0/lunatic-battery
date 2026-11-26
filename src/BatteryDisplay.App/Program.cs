using System.Runtime.InteropServices;
using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tray;
using BatteryDisplay.App.Ui;
using BatteryDisplay.App.Updates;
using BatteryDisplay.Core;
using BatteryDisplay.Devices.Bluetooth;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.XInput;
using Microsoft.Win32;
using Velopack;
using Velopack.Locators;

namespace BatteryDisplay.App;

static class Program
{
    private const string SingleInstanceMutex = @"Local\BatteryDisplay.SingleInstance";

    [STAThread]
    static void Main()
    {
        // The local diagnostic log (never sent anywhere), and every unhandled error goes to it: in a Release build
        // nothing else would show why the app misbehaves on another PC. Before any control exists, as WinForms requires.
        AppLog.Install(new AppLog(AppLog.DefaultDirectory));
        CatchUnhandledErrors();

        // First thing: Velopack runs its install, update and uninstall hooks here and exits the process when it
        // was launched for one. It also applies an update downloaded earlier and not applied yet (a session that
        // ended without "Salir"). Outside a Velopack install it does nothing.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => RemoveStartupEntry())
            .Run();

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutex, out var isFirstInstance);
        if (!isFirstInstance)
        {
            AppLog.Write("startup", "Another instance is running; exiting");
            return;
        }

        LogEnvironment();
        ApplicationConfiguration.Initialize();

        // Composition root: device sources, in tray order. Each refresh rediscovers what is connected.
        IDeviceSource[] sources =
        [
            new CatalogDeviceSource(AppLog.For("catalog")),
            new BluetoothDeviceSource(AppLog.For("bluetooth")),
            new XInputDeviceSource(),
        ];

        var settings = new SettingsStore(SettingsStore.DefaultPath);
        var updates = VelopackUpdateClient.Create(AppRepository.Url);
        using var context = new TrayApplicationContext(new DeviceDiscovery(sources), settings, updates);
        // The flyout and the settings window (WPF), on this same thread and message loop.
        using var ui = new UiHost(context);
        AppLog.Write("startup", "Tray ready, message loop starting");
        Application.Run(context);
        AppLog.Write("startup", "Exiting");
    }

    private static void CatchUnhandledErrors()
    {
        // An error on the UI thread is logged and the app keeps running instead of closing without a word.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Write("crash", "Unhandled error on the UI thread (the app keeps running)", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Write("crash", $"Unhandled error (terminating: {e.IsTerminating})", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Write("crash", "Unobserved task error", e.Exception);
    }

    // What a log from another PC needs first: which build, on which Windows, installed how.
    private static void LogEnvironment()
    {
        AppLog.Write("startup", $"Lunatic Battery {UiHost.Version()}, {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}");
        AppLog.Write("startup", $"Windows {Environment.OSVersion.Version} {WindowsRelease()} ({RuntimeInformation.OSDescription})");
        AppLog.Write("startup", $"{InstallKind()}, {Environment.ProcessPath}");
    }

    // The release name and the patch level (UBR) Environment.OSVersion leaves out, e.g. "24H2 build 26100.4349".
    private static string WindowsRelease()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return $"{key?.GetValue("DisplayVersion")} build {key?.GetValue("CurrentBuild")}.{key?.GetValue("UBR")}";
        }
        catch (Exception ex)
        {
            return $"(release unknown: {ex.Message})";
        }
    }

    private static string InstallKind()
    {
        try
        {
            var locator = VelopackLocator.Current;
            if (locator.CurrentlyInstalledVersion is null)
                return "Not a Velopack install (build folder)";
            return $"Velopack {(locator.IsPortable ? "portable" : "install")} {locator.CurrentlyInstalledVersion}";
        }
        catch (Exception ex)
        {
            return $"Install kind unknown ({ex.Message})";
        }
    }

    /// <summary>An uninstalled app must not stay in the Run key pointing at a deleted exe.</summary>
    private static void RemoveStartupEntry()
    {
        try
        {
            StartupRegistration.SetEnabled(false, Environment.ProcessPath ?? Application.ExecutablePath);
        }
        catch (Exception ex)
        {
            AppLog.Write("startup", "Could not remove the startup entry", ex);
        }
    }
}
