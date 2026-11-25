using BatteryDisplay.App.Settings;

namespace BatteryDisplay.App.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "LunaticBatteryTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _logs = [];

    private string SettingsPath => Path.Combine(_dir, "nested", "settings.json");

    private SettingsStore Open() => new(SettingsPath, _logs.Add);

    private void WriteFile(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, json);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void MissingFile_LoadsDefaults()
    {
        var store = Open();

        Assert.Equal(TrayIconMode.PerDevice, store.Current.TrayIconMode);
        Assert.Empty(store.Current.Devices);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{\"devices\": {\"a\": {\"pinned\": \"yes\"}}}")]
    public void CorruptFile_LoadsDefaults_AndLogs(string json)
    {
        WriteFile(json);

        var store = Open();

        Assert.Equal(TrayIconMode.PerDevice, store.Current.TrayIconMode);
        Assert.Empty(store.Current.Devices);
        Assert.NotEmpty(_logs);
    }

    [Fact]
    public void UnknownFields_AreIgnored()
    {
        WriteFile("""
            {
              "schema": 7,
              "trayIconMode": "Lowest",
              "theme": { "accent": "violet" },
              "devices": {
                "a": { "displayName": "Mi mouse", "pinned": true, "futureFlag": [1, 2, 3] }
              }
            }
            """);

        var store = Open();

        Assert.Equal(TrayIconMode.Lowest, store.Current.TrayIconMode);
        Assert.Equal("Mi mouse", store.Current.For("a").DisplayName);
        Assert.True(store.Current.For("a").Pinned);
    }

    [Fact]
    public void UnknownModeValue_FallsBackToTheDefaultMode_KeepingTheRest()
    {
        WriteFile("""{ "trayIconMode": "Hologram", "devices": { "a": { "pinned": true } } }""");

        var store = Open();

        Assert.Equal(TrayIconMode.PerDevice, store.Current.TrayIconMode);
        Assert.True(store.Current.For("a").Pinned);
    }

    [Fact]
    public void MissingDeviceFields_TakeTheirDefaults()
    {
        WriteFile("""{ "devices": { "a": { "displayName": "Auriculares" }, "b": null } }""");

        var a = Open().Current.For("a");

        Assert.Equal(new DeviceSettings("Auriculares", Pinned: false, LowBatteryThreshold: 15, NotifyWhenCharged: false), a);
    }

    [Fact]
    public void UnknownDevice_GetsDefaults()
    {
        Assert.Equal(new DeviceSettings(null, false, 15, false), Open().Current.For("never-seen"));
    }

    [Fact]
    public void Save_RoundTripsEveryField()
    {
        var store = Open();
        var settings = store.Current
            .WithDevice("a", new DeviceSettings("Teclado", Pinned: true, LowBatteryThreshold: 30, NotifyWhenCharged: true, Hidden: true))
            with { TrayIconMode = TrayIconMode.LowestWithPinned };

        Assert.True(store.Save(settings));
        var reloaded = Open().Current;

        Assert.Equal(TrayIconMode.LowestWithPinned, reloaded.TrayIconMode);
        Assert.Equal(new DeviceSettings("Teclado", true, 30, true, true), reloaded.For("a"));
    }

    [Fact]
    public void Hidden_IsWrittenToTheFile()
    {
        var store = Open();

        store.Save(store.Current.WithDevice("xion", DeviceSettings.Default with { Hidden = true }));

        Assert.Contains("\"hidden\": true", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void FileWrittenBeforeHiddenExisted_LoadsTheDeviceAsShown()
    {
        WriteFile("""{ "devices": { "xion": { "displayName": "Home theater", "pinned": true, "lowBatteryThreshold": 20, "notifyWhenCharged": true } } }""");

        var xion = Open().Current.For("xion");

        Assert.Equal(new DeviceSettings("Home theater", true, 20, true, Hidden: false), xion);
    }

    [Fact]
    public void Save_ReplacesTheFile_AndLeavesNoTempFileBehind()
    {
        WriteFile("""{ "trayIconMode": "Lowest" }""");
        var store = Open();

        Assert.True(store.Save(store.Current with { TrayIconMode = TrayIconMode.LowestWithPinned }));

        Assert.Equal(TrayIconMode.LowestWithPinned, Open().Current.TrayIconMode);
        Assert.Equal(["settings.json"], Directory.GetFileSystemEntries(Path.GetDirectoryName(SettingsPath)!).Select(Path.GetFileName));
    }

    [Fact]
    public void FailedWrite_LeavesTheExistingFileIntact()
    {
        const string original = """{ "trayIconMode": "Lowest" }""";
        WriteFile(original);
        // A directory where the temp file goes makes the write fail before anything touches the real file.
        Directory.CreateDirectory(SettingsPath + ".tmp");
        var store = Open();

        var saved = store.Save(store.Current with { TrayIconMode = TrayIconMode.LowestWithPinned });

        Assert.False(saved);
        Assert.Equal(original, File.ReadAllText(SettingsPath));
        Assert.NotEmpty(_logs);
    }

    [Fact]
    public void FailedWrite_StillAppliesTheChangeForThisSession()
    {
        Directory.CreateDirectory(SettingsPath + ".tmp");
        var store = Open();
        AppSettings? notified = null;
        store.Changed += (_, s) => notified = s;

        store.Save(store.Current with { TrayIconMode = TrayIconMode.Lowest });

        Assert.Equal(TrayIconMode.Lowest, store.Current.TrayIconMode);
        Assert.Equal(TrayIconMode.Lowest, notified?.TrayIconMode);
    }

    [Fact]
    public void Save_UpdatesCurrent_ThenRaisesChangedWithTheNewSettings()
    {
        var store = Open();
        var seen = new List<(AppSettings Arg, AppSettings Current)>();
        store.Changed += (_, s) => seen.Add((s, store.Current));
        var next = store.Current.WithDevice("a", new DeviceSettings("X", false, 15, false));

        store.Save(next);

        var (arg, current) = Assert.Single(seen);
        Assert.Same(next, arg);
        Assert.Same(next, current);
    }

    [Fact]
    public void DefaultPath_IsUnderLocalAppData()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunaticBattery", "settings.json");

        Assert.Equal(expected, SettingsStore.DefaultPath);
    }
}
