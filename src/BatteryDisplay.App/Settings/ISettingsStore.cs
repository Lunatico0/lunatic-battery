namespace BatteryDisplay.App.Settings;

/// <summary>
/// Holds the current settings and persists changes. Changed is raised on the thread that called Save,
/// after Current already holds the new settings.
/// </summary>
public interface ISettingsStore
{
    AppSettings Current { get; }

    event EventHandler<AppSettings>? Changed;

    /// <summary>Makes <paramref name="settings"/> current, notifies, and persists. False when the write failed; the change still applies for this session.</summary>
    bool Save(AppSettings settings);
}
