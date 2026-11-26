namespace BatteryDisplay.App.Tray;

/// <summary>Value stored under HKCU Run for this app.</summary>
public static class StartupCommand
{
    private const string VelopackAppFolder = "current";
    private const string VelopackUpdater = "Update.exe";

    public static string For(string exePath) => "\"" + exePath + "\"";

    public static bool Matches(string? registryValue, string exePath) =>
        registryValue is not null && string.Equals(registryValue.Trim().Trim('"'), exePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The exe Windows should start at sign-in. A Velopack install runs from &lt;root&gt;\current\, which the updater
    /// replaces on every update, and leaves a stub with the same exe name at &lt;root&gt; next to Update.exe; the stub
    /// is the path that survives updates. Anywhere else (a build folder, a copied exe) it is the running exe.
    /// </summary>
    public static string LaunchPath(string processPath, Func<string, bool> fileExists)
    {
        var appFolder = Path.GetDirectoryName(processPath);
        if (appFolder is null || !string.Equals(Path.GetFileName(appFolder), VelopackAppFolder, StringComparison.OrdinalIgnoreCase))
            return processPath;
        var root = Path.GetDirectoryName(appFolder);
        if (root is null)
            return processPath;
        var stub = Path.Combine(root, Path.GetFileName(processPath));
        return fileExists(Path.Combine(root, VelopackUpdater)) && fileExists(stub) ? stub : processPath;
    }
}
