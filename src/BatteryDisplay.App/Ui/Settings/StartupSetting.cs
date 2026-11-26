using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Tray;

namespace BatteryDisplay.App.Ui.Settings;

/// <summary>The real "Iniciar con Windows" switch: the same Run key entry the tray menu toggles.</summary>
internal sealed class StartupSetting(string exePath) : IStartupSetting
{
    public bool IsEnabled
    {
        get
        {
            try
            {
                return StartupRegistration.IsEnabled(exePath);
            }
            catch (Exception ex)
            {
                AppLog.Write("startup", "Could not read startup setting", ex);
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled) => StartupRegistration.SetEnabled(enabled, exePath);
}
