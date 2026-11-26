using Microsoft.Win32;

namespace BatteryDisplay.App.Tray;

/// <summary>
/// "Start with Windows" through HKCU\Software\Microsoft\Windows\CurrentVersion\Run. Callers pass the running exe;
/// the entry points at <see cref="StartupCommand.LaunchPath"/>, so it keeps working after a Velopack update.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BatteryDisplay";

    public static bool IsEnabled(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return StartupCommand.Matches(key?.GetValue(ValueName) as string, LaunchPath(exePath));
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, StartupCommand.For(LaunchPath(exePath)), RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string LaunchPath(string exePath) => StartupCommand.LaunchPath(exePath, File.Exists);
}
