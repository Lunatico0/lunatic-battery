using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Tests.Settings;

namespace BatteryDisplay.App.Tests.Logging;

public sealed class AppLogTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 13, 5, 9, 123, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lunatic-battery-tests", Guid.NewGuid().ToString("N"));
    private readonly ManualTimeProvider _clock = new(T0);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private AppLog Log(long maxBytes = AppLog.DefaultMaxBytes) => new(_directory, maxBytes, _clock);

    private string[] Lines(string file) => File.ReadAllLines(Path.Combine(_directory, file));

    [Fact]
    public void FilePath_IsTheLogFileInsideTheDirectory()
    {
        Assert.Equal(Path.Combine(_directory, "lunatic-battery.log"), Log().FilePath);
    }

    [Fact]
    public void DefaultDirectory_IsUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, "LunaticBattery", "logs"), AppLog.DefaultDirectory);
    }

    [Fact]
    public void Append_CreatesTheDirectory_AndWritesOneLine_WithAnIsoTimestampAndTheCategory()
    {
        Log().Append("monitor", "first cycle published 2 devices");

        var stamp = TimeZoneInfo.ConvertTime(T0, TimeZoneInfo.Local).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz");
        Assert.Equal([$"{stamp} [monitor] first cycle published 2 devices"], Lines("lunatic-battery.log"));
    }

    [Fact]
    public void Append_AddsToTheEndOfTheFile()
    {
        var log = Log();

        log.Append("a", "one");
        log.Append("b", "two");

        Assert.Equal(["[a] one", "[b] two"], Lines("lunatic-battery.log").Select(l => l[(l.IndexOf(' ') + 1)..]));
    }

    [Fact]
    public void Append_WithAnException_WritesTheWholeException()
    {
        var error = new InvalidOperationException("enumeration exploded");

        Log().Append("discovery", "failed", error);

        var text = File.ReadAllText(Log().FilePath);
        Assert.Contains("[discovery] failed: System.InvalidOperationException: enumeration exploded", text);
    }

    [Fact]
    public void PassingTheLimit_RotatesToTheBackupFile_AndStartsAFreshOne()
    {
        var log = Log(maxBytes: 200);
        for (var i = 0; i < 4; i++)
            log.Append("x", $"line {i} {new string('.', 40)}");

        // About 83 bytes a line: two fit, the third rotates them out.
        Assert.True(new FileInfo(log.FilePath).Length <= 200);
        Assert.Equal(["line 0", "line 1"], Lines("lunatic-battery.1.log").Select(l => l.Split("] ")[1][..6]));
        Assert.Equal(["line 2", "line 3"], Lines("lunatic-battery.log").Select(l => l.Split("] ")[1][..6]));
    }

    [Fact]
    public void Rotation_KeepsASingleBackup()
    {
        var log = Log(maxBytes: 120);
        for (var i = 0; i < 10; i++)
            log.Append("x", $"line {i} {new string('.', 40)}");

        Assert.Equal(["lunatic-battery.1.log", "lunatic-battery.log"], Directory.GetFiles(_directory).Select(Path.GetFileName).Order());
        Assert.EndsWith($"line 9 {new string('.', 40)}", Lines("lunatic-battery.log")[^1]);
    }

    [Fact]
    public void ConcurrentWriters_NeverInterleaveOrLoseLines()
    {
        var log = Log();

        Parallel.For(0, 200, i => log.Append("thread", $"message {i}"));

        var lines = Lines("lunatic-battery.log");
        Assert.Equal(200, lines.Length);
        Assert.Equal(Enumerable.Range(0, 200).Select(i => $"message {i}").Order(),
            lines.Select(l => l[(l.IndexOf("] ", StringComparison.Ordinal) + 2)..]).Order());
    }

    [Fact]
    public void AnInvalidDirectory_NeverThrows()
    {
        Directory.CreateDirectory(_directory);
        // A file where the log directory should be: creating the directory and opening the file both fail.
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "");

        var exception = Record.Exception(() =>
        {
            new AppLog(blocker).Append("x", "lost");
            new AppLog(blocker).Append("x", "lost", new InvalidOperationException());
            new AppLog("bad\0path").Append("x", "lost");
        });

        Assert.Null(exception);
    }
}
