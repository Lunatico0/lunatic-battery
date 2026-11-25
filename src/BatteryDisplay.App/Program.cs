using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Tray;
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

        using var context = new TrayApplicationContext(new DeviceDiscovery(sources));
        Application.Run(context);
    }
}
