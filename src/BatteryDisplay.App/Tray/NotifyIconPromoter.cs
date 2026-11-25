using System.Diagnostics;
using Microsoft.Win32;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// Best effort, undocumented: Windows 11 keeps one subkey per tray icon under HKCU\Control Panel\NotifyIconSettings,
/// and IsPromoted = 1 shows the icon on the taskbar instead of the overflow flyout.
/// </summary>
internal static class NotifyIconPromoter
{
    private const string SettingsKey = @"Control Panel\NotifyIconSettings";

    public static void PromoteIconsOf(string exePath)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(SettingsKey);
            if (root is null)
                return;

            foreach (var name in root.GetSubKeyNames())
            {
                using var entry = root.OpenSubKey(name, writable: true);
                if (entry?.GetValue("ExecutablePath") is not string path
                    || !NotifyIconSettingsPath.Matches(path, exePath, NativeMethods.GetKnownFolderPath))
                    continue;

                if (entry.GetValue("IsPromoted") is not int promoted || promoted != 1)
                    entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not promote tray icons: {ex.Message}", "BatteryDisplay");
        }
    }
}
