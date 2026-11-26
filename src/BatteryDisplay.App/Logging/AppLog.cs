using System.Globalization;
using System.Text;

namespace BatteryDisplay.App.Logging;

/// <summary>
/// The app's diagnostic log: one line per entry, "timestamp [category] message", appended to
/// <see cref="FileName"/> in a folder the user can open from the tray menu ("Abrir registro"). Past
/// <see cref="DefaultMaxBytes"/> the file moves to <see cref="BackupFileName"/> (one backup only) and a fresh one starts.
/// Thread-safe, and it never throws: a log that cannot be written is silently skipped. Local only, it is never sent.
/// Program installs the app's instance (<see cref="Install"/>); until then, and in tests, <see cref="Write"/> goes nowhere.
/// </summary>
public sealed class AppLog
{
    public const long DefaultMaxBytes = 1024 * 1024;
    public const string FileName = "lunatic-battery.log";
    public const string BackupFileName = "lunatic-battery.1.log";

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static volatile AppLog? _installed;

    private readonly long _maxBytes;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    public AppLog(string directory, long maxBytes = DefaultMaxBytes, TimeProvider? time = null)
    {
        Folder = directory;
        FilePath = Path.Combine(directory, FileName);
        BackupPath = Path.Combine(directory, BackupFileName);
        _maxBytes = maxBytes;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>%LOCALAPPDATA%\LunaticBattery\logs, next to the settings file.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunaticBattery", "logs");

    /// <summary>The app's log once Program installed it; null before that and in tests.</summary>
    public static AppLog? Installed => _installed;

    public string Folder { get; }

    public string FilePath { get; }

    public string BackupPath { get; }

    public static void Install(AppLog log) => _installed = log;

    /// <summary>Writes to the installed log, if any.</summary>
    public static void Write(string category, string message, Exception? error = null) =>
        _installed?.Append(category, message, error);

    /// <summary>A sink for classes that take an <c>Action&lt;string&gt;</c> log; resolved at each call, not when created.</summary>
    public static Action<string> For(string category) => message => Write(category, message);

    /// <summary>Appends one entry; with <paramref name="error"/>, the whole exception (type, message, stack, inner) follows the message.</summary>
    public void Append(string category, string message, Exception? error = null)
    {
        try
        {
            var stamp = _time.GetLocalNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
            var text = error is null ? message : $"{message}: {error}";
            var bytes = Utf8.GetBytes($"{stamp} [{category}] {text}{Environment.NewLine}");
            lock (_gate)
            {
                Directory.CreateDirectory(Folder);
                RotateIfFull(bytes.Length);
                using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.Write(bytes);
            }
        }
        catch (Exception)
        {
            // Logging must never take the app down; an entry that cannot be written is lost.
        }
    }

    private void RotateIfFull(int incoming)
    {
        var file = new FileInfo(FilePath);
        if (file.Exists && file.Length > 0 && file.Length + incoming > _maxBytes)
            File.Move(FilePath, BackupPath, overwrite: true);
    }
}
