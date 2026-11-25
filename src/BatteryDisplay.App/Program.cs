using BatteryDisplay.App.Tray;
using BatteryDisplay.Core;
using BatteryDisplay.Devices.Razer;
using BatteryDisplay.Devices.SteelSeries;

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

        // Composition root: one entry per device, in tray order.
        IBatteryProvider[] providers =
        [
            new RazerCobraHyperSpeedProvider(),
            new SteelSeriesArctisNova5Provider(),
        ];

        using var context = new TrayApplicationContext(providers);
        Application.Run(context);
    }
}
