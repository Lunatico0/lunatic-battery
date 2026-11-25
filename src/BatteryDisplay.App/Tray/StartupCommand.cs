namespace BatteryDisplay.App.Tray;

/// <summary>Value stored under HKCU Run for this app.</summary>
public static class StartupCommand
{
    public static string For(string exePath) => "\"" + exePath + "\"";

    public static bool Matches(string? registryValue, string exePath) =>
        registryValue is not null && string.Equals(registryValue.Trim().Trim('"'), exePath, StringComparison.OrdinalIgnoreCase);
}
