using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tray;
using BatteryDisplay.App.Ui;
using BatteryDisplay.Core;
using BatteryDisplay.Devices.Bluetooth;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.App;

static class Program
{
    private const string SingleInstanceMutex = @"Local\BatteryDisplay.SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutex, out var isFirstInstance);
        if (!isFirstInstance)
            return;

        ApplicationConfiguration.Initialize();

        // Composition root: device sources, in tray order. Each refresh rediscovers what is connected.
        IDeviceSource[] sources =
        [
            new CatalogDeviceSource(),
            new BluetoothDeviceSource(),
            new XInputDeviceSource(),
        ];

        var settings = new SettingsStore(SettingsStore.DefaultPath);
        using var context = new TrayApplicationContext(new DeviceDiscovery(sources), settings);
        // The flyout and the settings window (WPF), on this same thread and message loop.
        using var ui = new UiHost(context);
        Application.Run(context);
    }
}
