using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

namespace BatteryDisplay.App.Settings;

/// <summary>
/// Settings in a JSON file. A missing or unreadable file loads the defaults and never throws; unknown fields are ignored
/// so an older build still reads a newer file. Saving writes a temp file next to the target and then replaces it,
/// so a crash mid-write never leaves a half-written settings file.
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly Action<string> _log;

    public SettingsStore(string path, Action<string>? log = null)
    {
        _path = path;
        _log = log ?? (message => Debug.WriteLine(message, "BatteryDisplay"));
        Current = Load();
    }

    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunaticBattery", "settings.json");

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public bool Save(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, settings);
        return Write(settings);
    }

    private AppSettings Load()
    {
        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return AppSettings.Default;
        }
        catch (Exception ex)
        {
            _log($"settings: could not read {_path}, using defaults: {ex.GetType().Name}: {ex.Message}");
            return AppSettings.Default;
        }

        try
        {
            if (JsonSerializer.Deserialize<SettingsDocument>(text, Json) is { } document)
                return document.ToSettings(_log);
            _log($"settings: {_path} holds no settings, using defaults");
        }
        catch (JsonException ex)
        {
            _log($"settings: {_path} is corrupt, using defaults: {ex.Message}");
        }
        return AppSettings.Default;
    }

    private bool Write(AppSettings settings)
    {
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, SettingsDocument.From(settings), Json);
                stream.Flush(flushToDisk: true);
            }
            // Same directory, so the move is a rename that replaces the old file in one step.
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _log($"settings: could not save {_path}: {ex.GetType().Name}: {ex.Message}");
            TryDeleteFile(temp);
            return false;
        }
    }

    private static void TryDeleteFile(string file)
    {
        try
        {
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception)
        {
            // Best effort: a stale temp file is overwritten by the next save.
        }
    }

    // On-disk shape. Every field is optional so a partial or older file still loads; the mode is a string so an
    // unknown future value falls back to the default instead of failing the whole file.
    private sealed class SettingsDocument
    {
        public string? TrayIconMode { get; set; }
        public Dictionary<string, DeviceDocument?>? Devices { get; set; }

        public static SettingsDocument From(AppSettings settings) => new()
        {
            TrayIconMode = settings.TrayIconMode.ToString(),
            Devices = settings.Devices.ToDictionary(p => p.Key, p => (DeviceDocument?)DeviceDocument.From(p.Value), StringComparer.Ordinal),
        };

        public AppSettings ToSettings(Action<string> log)
        {
            var mode = Settings.TrayIconMode.PerDevice;
            if (TrayIconMode is { } text && !(Enum.TryParse(text, ignoreCase: true, out mode) && Enum.IsDefined(mode)))
            {
                log($"settings: unknown trayIconMode '{text}', using {Settings.TrayIconMode.PerDevice}");
                mode = Settings.TrayIconMode.PerDevice;
            }

            var devices = ImmutableDictionary.CreateBuilder<string, DeviceSettings>(StringComparer.Ordinal);
            foreach (var (id, device) in Devices ?? [])
            {
                if (device is not null)
                    devices[id] = device.ToSettings();
            }
            return new AppSettings(mode, devices.ToImmutable());
        }
    }

    private sealed class DeviceDocument
    {
        public string? DisplayName { get; set; }
        public bool? Pinned { get; set; }
        public int? LowBatteryThreshold { get; set; }
        public bool? NotifyWhenCharged { get; set; }
        public bool? Hidden { get; set; }

        public static DeviceDocument From(DeviceSettings s) => new()
        {
            DisplayName = s.DisplayName,
            Pinned = s.Pinned,
            LowBatteryThreshold = s.LowBatteryThreshold,
            NotifyWhenCharged = s.NotifyWhenCharged,
            Hidden = s.Hidden,
        };

        public DeviceSettings ToSettings() => new(
            DisplayName,
            Pinned ?? false,
            LowBatteryThreshold ?? DeviceSettings.DefaultLowBatteryThreshold,
            NotifyWhenCharged ?? false,
            Hidden ?? false);
    }
}
