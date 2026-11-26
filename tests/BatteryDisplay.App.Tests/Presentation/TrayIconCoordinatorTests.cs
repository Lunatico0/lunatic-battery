using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tests.Discovery;
using BatteryDisplay.App.Tests.Polling;
using BatteryDisplay.App.Tests.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

/// <summary>Monitor, settings and icons together: what the tray actually shows.</summary>
public sealed class TrayIconCoordinatorTests : IDisposable
{
    private static readonly BatteryStatus Ok = new(60, ChargeState.Discharging);

    private readonly FakeSettingsStore _settings = new();
    private readonly FakeSource _source = new(FakeProvider.Returning("mouse", new BatteryStatus(80, ChargeState.Discharging)),
        FakeProvider.Returning("pad", new BatteryStatus(20, ChargeState.Discharging), DeviceKind.Gamepad));
    private readonly List<FakeIcon> _created = [];
    private readonly BatteryMonitor _monitor;
    private int _firstShown;

    public TrayIconCoordinatorTests()
    {
        _monitor = new BatteryMonitor(_source, new BatteryPoller(TimeSpan.FromMilliseconds(300)), _settings);
    }

    public void Dispose() => _monitor.Dispose();

    private readonly FakeTaskbarTheme _taskbar = new();

    private TrayIconCoordinator Coordinator() => new(_monitor, _settings, _taskbar, key =>
    {
        var icon = new FakeIcon(key);
        _created.Add(icon);
        return icon;
    }, () => _firstShown++);

    private IEnumerable<string> LiveKeys => _created.Where(i => !i.Disposed).Select(i => i.Key);
    // The "searching" icon shown before the first list is created and disposed on the way, so tests that follow the
    // device icons look at the live ones.
    private List<FakeIcon> LiveIcons => _created.Where(i => !i.Disposed).ToList();
    private FakeIcon IconOf(string key) => _created.Last(i => i.Key == key);

    private void SetMode(TrayIconMode mode) => _settings.Save(_settings.Current with { TrayIconMode = mode });

    private void Pin(string id) => _settings.Save(_settings.Current.WithDevice(id, _settings.Current.For(id) with { Pinned = true }));

    [Fact]
    public async Task PerDevice_OneIconPerDevice_AfterARefresh()
    {
        using var coordinator = Coordinator();

        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["mouse", "pad"], LiveKeys);
        Assert.Equal("pad: 20%", IconOf("pad").Last!.Tooltip);
        // The searching icon, then the device icons.
        Assert.Equal(2, _firstShown);
    }

    [Fact]
    public async Task ModeChangeAtRuntime_ReconcilesByKey_WithoutRediscovering()
    {
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);
        var perDevice = _created.ToList();

        SetMode(TrayIconMode.Lowest);

        Assert.All(perDevice, icon => Assert.True(icon.Disposed));
        Assert.Equal([TrayIconPlanner.CombinedKey], LiveKeys);
        Assert.Equal((DeviceKind.Gamepad, "mouse 80%\npad 20%"), (IconOf(TrayIconPlanner.CombinedKey).Last!.Kind, IconOf(TrayIconPlanner.CombinedKey).Last!.Tooltip));

        SetMode(TrayIconMode.PerDevice);

        Assert.All(_created.Where(i => i.Key == TrayIconPlanner.CombinedKey), icon => Assert.True(icon.Disposed));
        Assert.Equal(["mouse", "pad"], LiveKeys);
        Assert.Equal(1, _source.Calls);
    }

    [Fact]
    public async Task ModeChange_KeepsIconsWhoseKeySurvives()
    {
        using var coordinator = Coordinator();
        Pin("pad");
        await _monitor.RefreshAsync(CancellationToken.None);
        var pad = IconOf("pad");

        SetMode(TrayIconMode.LowestWithPinned);

        Assert.Same(pad, IconOf("pad"));
        Assert.False(pad.Disposed);
        Assert.True(IconOf("mouse").Disposed);
        Assert.Equal(["pad", TrayIconPlanner.CombinedKey], LiveKeys);
    }

    [Fact]
    public async Task PinningInLowestWithPinned_GivesTheDeviceItsOwnIcon()
    {
        SetMode(TrayIconMode.LowestWithPinned);
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);
        Assert.Equal([TrayIconPlanner.CombinedKey], LiveKeys);

        Pin("pad");

        Assert.Equal([TrayIconPlanner.CombinedKey, "pad"], LiveKeys);
        Assert.Equal("mouse 80%", IconOf(TrayIconPlanner.CombinedKey).Last!.Tooltip);
    }

    [Fact]
    public async Task Rename_UpdatesTheTooltip()
    {
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);

        _settings.Save(_settings.Current.WithDevice("pad", DeviceSettings.Default with { DisplayName = "Control de la Xbox" }));

        Assert.Equal("Control de la Xbox: 20%", IconOf("pad").Last!.Tooltip);
    }

    [Fact]
    public void LowestMode_ShowsTheCombinedIconRightAway_AndSignalsIt()
    {
        SetMode(TrayIconMode.Lowest);

        using var coordinator = Coordinator();

        Assert.Equal([TrayIconPlanner.CombinedKey], LiveKeys);
        Assert.True(IconOf(TrayIconPlanner.CombinedKey).Last!.Visible);
        Assert.Equal(1, _firstShown);
    }

    [Fact]
    public async Task NewIconFromAModeChange_IsSignalledAsFirstShown()
    {
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);
        // The searching icon, then the device icons.
        Assert.Equal(2, _firstShown);

        SetMode(TrayIconMode.Lowest);

        Assert.Equal(3, _firstShown);
    }

    // Never invisible

    [Theory]
    [InlineData(TrayIconMode.PerDevice)]
    [InlineData(TrayIconMode.Lowest)]
    [InlineData(TrayIconMode.LowestWithPinned)]
    public void BeforeTheFirstList_ShowsTheSearchingIcon_InEveryMode(TrayIconMode mode)
    {
        SetMode(mode);

        using var coordinator = Coordinator();

        Assert.Equal([TrayIconPlanner.CombinedKey], LiveKeys);
        Assert.Equal((true, TrayIconPlanner.SearchingTooltip), (IconOf(TrayIconPlanner.CombinedKey).Last!.Visible, IconOf(TrayIconPlanner.CombinedKey).Last!.Tooltip));
        Assert.Equal(1, _firstShown);
    }

    [Fact]
    public void SearchingIcon_FollowsTheTaskbarTheme()
    {
        _taskbar.Theme = TrayIconTheme.Light;

        using var coordinator = Coordinator();

        Assert.Equal(TrayIconTheme.Light, IconOf(TrayIconPlanner.CombinedKey).Last!.Theme);
    }

    [Fact]
    public void SettingsChangeBeforeTheFirstList_KeepsSearching()
    {
        using var coordinator = Coordinator();

        SetMode(TrayIconMode.Lowest);

        Assert.Equal(TrayIconPlanner.SearchingTooltip, IconOf(TrayIconPlanner.CombinedKey).Last!.Tooltip);
    }

    [Fact]
    public async Task FirstList_ReplacesTheSearchingIcon_WithTheDeviceIcons()
    {
        using var coordinator = Coordinator();
        var searching = IconOf(TrayIconPlanner.CombinedKey);

        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.True(searching.Disposed);
        Assert.Equal(["mouse", "pad"], LiveKeys);
    }

    [Fact]
    public async Task FirstListEmpty_PerDevice_ShowsTheNoDevicesIcon_InsteadOfNothing()
    {
        _source.Current = [];
        using var coordinator = Coordinator();

        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal([TrayIconPlanner.CombinedKey], LiveKeys);
        Assert.Equal((true, TrayIconPlanner.NoDevicesTooltip), (IconOf(TrayIconPlanner.CombinedKey).Last!.Visible, IconOf(TrayIconPlanner.CombinedKey).Last!.Tooltip));
    }

    [Fact]
    public async Task NoDeviceWithAReading_PerDevice_KeepsTheNoDevicesIconVisible_UntilOneReads()
    {
        _source.Current = [FakeProvider.Returning("mouse", null)];
        using var coordinator = Coordinator();

        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal([TrayIconPlanner.CombinedKey, "mouse"], LiveKeys);
        Assert.True(IconOf(TrayIconPlanner.CombinedKey).Last!.Visible);
        Assert.False(IconOf("mouse").Last!.Visible);

        _source.Current = [FakeProvider.Returning("mouse", Ok)];
        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.Equal(["mouse"], LiveKeys);
        Assert.True(IconOf("mouse").Last!.Visible);
    }

    [Fact]
    public async Task FirstCycleThatThrows_LeavesTheSearchingIconVisible()
    {
        _source.OnDiscover = () => throw new InvalidOperationException("enumeration exploded");
        using var coordinator = Coordinator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _monitor.RefreshAsync(CancellationToken.None));

        Assert.Equal([TrayIconPlanner.CombinedKey], LiveKeys);
        Assert.Equal((true, TrayIconPlanner.SearchingTooltip), (IconOf(TrayIconPlanner.CombinedKey).Last!.Visible, IconOf(TrayIconPlanner.CombinedKey).Last!.Tooltip));
    }

    // Taskbar theme

    [Fact]
    public async Task DarkTaskbar_DrawsEveryIconWithTheDarkVariant()
    {
        using var coordinator = Coordinator();

        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.All(_created, icon => Assert.Equal(TrayIconTheme.Dark, icon.Last!.Theme));
    }

    [Fact]
    public async Task LightTaskbar_DrawsEveryIconWithTheLightVariant_InEveryMode()
    {
        _taskbar.Theme = TrayIconTheme.Light;
        Pin("pad");
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);

        SetMode(TrayIconMode.LowestWithPinned);

        Assert.Equal([TrayIconPlanner.CombinedKey, "pad"], LiveKeys.Order());
        Assert.All(_created.Where(i => !i.Disposed), icon => Assert.Equal(TrayIconTheme.Light, icon.Last!.Theme));
    }

    [Fact]
    public async Task TaskbarTurningLight_RedrawsTheSameIcons_WithoutRediscovering()
    {
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);
        var icons = LiveIcons;
        var created = _created.Count;

        _taskbar.Switch(TrayIconTheme.Light);

        Assert.Equal(icons, LiveIcons);
        Assert.Equal(created, _created.Count);
        Assert.All(icons, icon => Assert.Equal(TrayIconTheme.Light, icon.Last!.Theme));
        Assert.Equal("pad: 20%", IconOf("pad").Last!.Tooltip);
        Assert.Equal(1, _source.Calls);

        _taskbar.Switch(TrayIconTheme.Dark);

        Assert.All(icons, icon => Assert.Equal(TrayIconTheme.Dark, icon.Last!.Theme));
    }

    [Fact]
    public async Task HighContrastInkChange_RedrawsTheSameIcons_WithTheNewInk()
    {
        var white = new TrayIconInk(unchecked((int)0xFFFFFFFF), unchecked((int)0xFF3FF23F));
        var yellow = new TrayIconInk(unchecked((int)0xFFFFFF00), unchecked((int)0xFFA6A6A6));
        _taskbar.Theme = TrayIconTheme.HighContrast;
        _taskbar.Ink = white;
        using var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);
        var icons = LiveIcons;
        var created = _created.Count;

        Assert.All(icons, icon => Assert.Equal((TrayIconTheme.HighContrast, white), (icon.Last!.Theme, icon.Last!.Ink)));

        _taskbar.Switch(TrayIconTheme.HighContrast, yellow);

        Assert.Equal(icons, LiveIcons);
        Assert.Equal(created, _created.Count);
        Assert.All(icons, icon => Assert.Equal((TrayIconTheme.HighContrast, yellow), (icon.Last!.Theme, icon.Last!.Ink)));
    }

    [Fact]
    public void Dispose_StopsListeningToTheTaskbarTheme()
    {
        var coordinator = Coordinator();

        coordinator.Dispose();

        Assert.Equal(0, _taskbar.Subscribers);
    }

    [Fact]
    public async Task Dispose_DisposesEveryIcon_AndIgnoresLaterChanges()
    {
        var coordinator = Coordinator();
        await _monitor.RefreshAsync(CancellationToken.None);

        coordinator.Dispose();
        SetMode(TrayIconMode.Lowest);
        _source.Current = [FakeProvider.Returning("other", Ok)];
        await _monitor.RefreshAsync(CancellationToken.None);

        Assert.All(_created, icon => Assert.True(icon.Disposed));
        // The searching icon, mouse and pad: nothing created after Dispose.
        Assert.Equal(3, _created.Count);
    }
}
