namespace BatteryDisplay.App.Tray;

/// <summary>
/// ExecutablePath values under HKCU\Control Panel\NotifyIconSettings may start with a known folder GUID
/// (for example "{6D809377-...}\App\app.exe" for Program Files) instead of a drive path.
/// </summary>
public static class NotifyIconSettingsPath
{
    public static bool Matches(string registryPath, string exePath, Func<Guid, string?> resolveKnownFolder) =>
        Expand(registryPath, resolveKnownFolder) is { } path
        && string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase);

    private static string? Expand(string registryPath, Func<Guid, string?> resolveKnownFolder)
    {
        if (!registryPath.StartsWith('{'))
            return registryPath.Length > 0 ? registryPath : null;

        var close = registryPath.IndexOf('}');
        if (close < 0 || !Guid.TryParse(registryPath.AsSpan(0, close + 1), out var folderId))
            return null;

        var folder = resolveKnownFolder(folderId);
        return folder is null ? null : folder.TrimEnd('\\') + registryPath[(close + 1)..];
    }
}
