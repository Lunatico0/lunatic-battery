using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Tests.Settings;
using BatteryDisplay.App.Ui.Settings;
using BatteryDisplay.Core;
using static BatteryDisplay.App.Tests.Ui.Snapshots;

namespace BatteryDisplay.App.Tests.Ui;

public class SettingsViewModelTests
{
    private readonly FakeDeviceSnapshots _devices = new(
        Device("cobra", 82, kind: DeviceKind.Mouse, name: "Razer Cobra HyperSpeed", source: "Dongle Razer"),
        Device("nova", 45, ChargeState.Charging, DeviceKind.Headset, name: "Arctis Nova 5", source: "Dongle SteelSeries"),
        Unread("pad", DeviceKind.Gamepad, source: ""));
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeStartupSetting _startup = new();
    private readonly List<string> _opened = [];
    private int _saves;

    public SettingsViewModelTests() => _settings.Changed += (_, _) => _saves++;

    private SettingsViewModel Create() => new(_devices, _settings, _startup, "1.2.3", _opened.Add);

    private DeviceSettings Saved(string id) => _settings.Current.For(id);

    private void SetMode(TrayIconMode mode) => _settings.Save(_settings.Current with { TrayIconMode = mode });

    [Fact]
    public void ListsTheDevices_AndSelectsTheFirst()
    {
        using var vm = Create();

        Assert.Equal(["cobra", "nova", "pad"], vm.Devices.Select(d => d.Id));
        Assert.Equal("cobra", vm.SelectedDevice?.Id);
        Assert.True(vm.HasSelection);
        Assert.Equal("Razer Cobra HyperSpeed", vm.DetailTitle);
        Assert.Equal("82%, descargando · Dongle Razer", vm.DetailSubtitle);
    }

    [Fact]
    public void Detail_WithoutPercent_ShowsTheState_AndOmitsAnEmptySource()
    {
        using var vm = Create();

        vm.SelectedDevice = vm.Devices[2];

        Assert.Equal("Sin datos", vm.DetailSubtitle);
    }

    [Fact]
    public void Selecting_ShowsThatDevicesOptions()
    {
        _settings.Save(_settings.Current.WithDevice("nova", new DeviceSettings("Nova", true, 25, true)));
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        vm.SelectedDevice = vm.Devices[1];

        Assert.Equal(("Nova", true, 25, true), (vm.DisplayName, vm.Pinned, vm.LowBatteryThreshold, vm.NotifyWhenCharged));
        Assert.Equal("45%, cargando · Dongle SteelSeries", vm.DetailSubtitle);
        Assert.Contains(nameof(SettingsViewModel.Pinned), changes.Names);
        Assert.Contains(nameof(SettingsViewModel.DisplayName), changes.Names);
    }

    [Fact]
    public void NoDevices_HasNoSelection()
    {
        _devices.Publish();

        using var vm = Create();

        Assert.Null(vm.SelectedDevice);
        Assert.False(vm.HasSelection);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void DevicesArriving_LeaveTheEmptyState()
    {
        _devices.Publish();
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        _devices.Publish(Device("cobra", 80));

        Assert.False(vm.IsEmpty);
        Assert.Contains(nameof(SettingsViewModel.IsEmpty), changes.Names);
    }

    [Fact]
    public void DevicesChanged_KeepsTheSelectionById()
    {
        using var vm = Create();
        vm.SelectedDevice = vm.Devices[1];

        _devices.Publish(Device("new", 50), Device("nova", 50, ChargeState.Charging, DeviceKind.Headset), Device("cobra", 80));

        Assert.Equal("nova", vm.SelectedDevice?.Id);
        Assert.Equal("50%, cargando · Bluetooth", vm.DetailSubtitle);
    }

    [Fact]
    public void SelectedDeviceGone_SelectsTheFirst()
    {
        using var vm = Create();
        vm.SelectedDevice = vm.Devices[1];

        _devices.Publish(Device("cobra", 80));

        Assert.Equal("cobra", vm.SelectedDevice?.Id);
    }

    [Fact]
    public void NullSelectionFromTheList_KeepsTheCurrentDevice()
    {
        using var vm = Create();
        vm.SelectedDevice = vm.Devices[1];

        vm.SelectedDevice = null;

        Assert.Equal("nova", vm.SelectedDevice?.Id);
    }

    // Hidden devices

    private void Hide(string id) => _settings.Save(_settings.Current.WithDevice(id, DeviceSettings.Default with { Hidden = true }));

    [Fact]
    public void HiddenDevice_IsStillListed_MarkedHidden()
    {
        Hide("nova");

        using var vm = Create();

        Assert.Equal(["cobra", "nova", "pad"], vm.Devices.Select(d => d.Id));
        Assert.Equal([false, true, false], vm.Devices.Select(d => d.Hidden));
    }

    [Fact]
    public void ShowDevice_IsOnForAShownDevice_WithTheOptionsEnabled()
    {
        using var vm = Create();

        Assert.True(vm.ShowDevice);
        Assert.True(vm.OptionsEnabled);
    }

    [Fact]
    public void SelectingAHiddenDevice_TurnsShowDeviceOff_AndDisablesTheOtherOptions()
    {
        Hide("nova");
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        vm.SelectedDevice = vm.Devices[1];

        Assert.False(vm.ShowDevice);
        Assert.False(vm.OptionsEnabled);
        Assert.Contains(nameof(SettingsViewModel.ShowDevice), changes.Names);
        Assert.Contains(nameof(SettingsViewModel.OptionsEnabled), changes.Names);
    }

    [Fact]
    public void ShowDeviceOff_SavesHidden_AndUpdatesTheRowAndTheOptions()
    {
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        vm.ShowDevice = false;

        Assert.True(Saved("cobra").Hidden);
        Assert.True(vm.SelectedDevice?.Hidden);
        Assert.Equal("cobra", vm.SelectedDevice?.Id);
        Assert.False(vm.OptionsEnabled);
        Assert.Contains(nameof(SettingsViewModel.OptionsEnabled), changes.Names);
    }

    [Fact]
    public void ShowDeviceOn_ClearsHidden_KeepingTheOtherOptions()
    {
        _settings.Save(_settings.Current.WithDevice("cobra", new DeviceSettings("Mi mouse", false, 25, true, Hidden: true)));
        using var vm = Create();

        vm.ShowDevice = true;

        Assert.Equal(new DeviceSettings("Mi mouse", false, 25, true, Hidden: false), Saved("cobra"));
        Assert.True(vm.OptionsEnabled);
    }

    [Fact]
    public void ShowDevice_SameValue_DoesNotSave()
    {
        using var vm = Create();
        var before = _saves;

        vm.ShowDevice = true;

        Assert.Equal(before, _saves);
    }

    [Fact]
    public void Pinned_Saves()
    {
        SetMode(TrayIconMode.LowestWithPinned);
        using var vm = Create();

        vm.Pinned = true;

        Assert.True(Saved("cobra").Pinned);
    }

    [Fact]
    public void LowBatteryThreshold_Saves()
    {
        using var vm = Create();

        vm.LowBatteryThreshold = 25;

        Assert.Equal(25, Saved("cobra").LowBatteryThreshold);
        Assert.Equal([5, 10, 15, 20, 25, 30], vm.ThresholdOptions);
    }

    [Fact]
    public void NotifyWhenCharged_Saves()
    {
        using var vm = Create();

        vm.NotifyWhenCharged = true;

        Assert.True(Saved("cobra").NotifyWhenCharged);
    }

    [Fact]
    public void DisplayName_Saves_Trimmed()
    {
        using var vm = Create();

        vm.DisplayName = "  Cobra  ";

        Assert.Equal("Cobra", Saved("cobra").DisplayName);
        Assert.Equal("Cobra", vm.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DisplayName_Blank_MeansTheProviderName(string? blank)
    {
        _settings.Save(_settings.Current.WithDevice("cobra", DeviceSettings.Default with { DisplayName = "Cobra" }));
        using var vm = Create();

        vm.DisplayName = blank!;

        Assert.Null(Saved("cobra").DisplayName);
        Assert.Equal("", vm.DisplayName);
    }

    [Fact]
    public void SavesOnlyTheSelectedDevice_KeepingItsOtherOptions()
    {
        _settings.Save(_settings.Current.WithDevice("nova", new DeviceSettings("Nova", true, 25, true)));
        using var vm = Create();
        vm.SelectedDevice = vm.Devices[1];

        vm.LowBatteryThreshold = 10;

        Assert.Equal(new DeviceSettings("Nova", true, 10, true), Saved("nova"));
        Assert.Equal(DeviceSettings.Default, Saved("cobra"));
    }

    [Fact]
    public void SettingTheSameValue_DoesNotSave()
    {
        using var vm = Create();
        var before = _saves;

        vm.LowBatteryThreshold = DeviceSettings.DefaultLowBatteryThreshold;
        vm.Pinned = false;
        vm.NotifyWhenCharged = false;
        vm.DisplayName = "";
        vm.TrayIconMode = TrayIconMode.PerDevice;

        Assert.Equal(before, _saves);
    }

    [Theory]
    [InlineData(TrayIconMode.PerDevice, false)]
    [InlineData(TrayIconMode.Lowest, false)]
    [InlineData(TrayIconMode.LowestWithPinned, true)]
    public void Pinned_IsEnabledOnlyInLowestWithPinned(TrayIconMode mode, bool enabled)
    {
        SetMode(mode);

        using var vm = Create();

        Assert.Equal(enabled, vm.CanPin);
    }

    [Fact]
    public void PinnedHint_NamesTheModeThatEnablesIt()
    {
        using var vm = Create();
        var disabledHint = vm.PinnedHint;

        SetMode(TrayIconMode.LowestWithPinned);

        Assert.Contains("Combinado, con fijados", disabledHint);
        Assert.NotEqual(disabledHint, vm.PinnedHint);
    }

    [Fact]
    public void PinnedWhileDisabled_DoesNotSave()
    {
        using var vm = Create();

        vm.Pinned = true;

        Assert.False(Saved("cobra").Pinned);
    }

    [Fact]
    public void TrayModes_AreTheThreeCards_WithTheCurrentOneSelected()
    {
        SetMode(TrayIconMode.Lowest);

        using var vm = Create();

        Assert.Equal(
            [(TrayIconMode.PerDevice, "Un ícono por dispositivo"), (TrayIconMode.Lowest, "Un solo ícono, el más bajo"), (TrayIconMode.LowestWithPinned, "Combinado, con fijados")],
            vm.TrayModes.Select(m => (m.Mode, m.Title)));
        Assert.Equal([false, true, false], vm.TrayModes.Select(m => m.IsSelected));
        Assert.All(vm.TrayModes, m => Assert.False(string.IsNullOrWhiteSpace(m.Description)));
    }

    [Fact]
    public void SelectingAModeCard_SavesTheMode_AndEnablesPinning()
    {
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        vm.TrayModes[2].IsSelected = true;

        Assert.Equal(TrayIconMode.LowestWithPinned, _settings.Current.TrayIconMode);
        Assert.Equal([false, false, true], vm.TrayModes.Select(m => m.IsSelected));
        Assert.True(vm.CanPin);
        Assert.Contains(nameof(SettingsViewModel.CanPin), changes.Names);
    }

    [Fact]
    public void UncheckingAModeCard_DoesNothing()
    {
        using var vm = Create();
        var before = _saves;

        vm.TrayModes[0].IsSelected = false;

        Assert.Equal(before, _saves);
        Assert.True(vm.TrayModes[0].IsSelected);
    }

    [Fact]
    public void ExternalSettingsChange_RefreshesTheViewModel()
    {
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        _settings.Save(new AppSettings(TrayIconMode.LowestWithPinned, _settings.Current.Devices)
            .WithDevice("cobra", new DeviceSettings("Cobra", true, 30, true)));

        Assert.Equal(TrayIconMode.LowestWithPinned, vm.TrayIconMode);
        Assert.Equal(("Cobra", true, 30, true, true), (vm.DisplayName, vm.Pinned, vm.LowBatteryThreshold, vm.NotifyWhenCharged, vm.CanPin));
        Assert.True(vm.TrayModes[2].IsSelected);
        Assert.Contains(nameof(SettingsViewModel.LowBatteryThreshold), changes.Names);
        Assert.Contains(nameof(SettingsViewModel.TrayIconMode), changes.Names);
    }

    [Fact]
    public void StartWithWindows_ReadsAndWritesTheStartupSetting()
    {
        _startup.IsEnabled = true;
        using var vm = Create();
        Assert.True(vm.StartWithWindows);

        vm.StartWithWindows = false;

        Assert.Equal([false], _startup.Calls);
        Assert.False(_startup.IsEnabled);
    }

    [Fact]
    public void StartWithWindows_FailedWrite_ShowsTheRealState()
    {
        _startup.Throws = true;
        using var vm = Create();
        var changes = new PropertyChanges(vm);

        vm.StartWithWindows = true;

        Assert.False(vm.StartWithWindows);
        Assert.Contains(nameof(SettingsViewModel.StartWithWindows), changes.Names);
    }

    [Fact]
    public void ShowingGeneral_RereadsStartup_ChangedFromTheTrayMenu()
    {
        using var vm = Create();
        _startup.IsEnabled = true;
        var changes = new PropertyChanges(vm);

        vm.SelectedSection = SettingsSection.General;

        Assert.True(vm.StartWithWindows);
        Assert.Contains(nameof(SettingsViewModel.StartWithWindows), changes.Names);
    }

    [Fact]
    public void Sections_AreTheFourOfThisMilestone_StartingOnDevices()
    {
        using var vm = Create();

        Assert.Equal(["Dispositivos", "Bandeja y avisos", "General", "Acerca de"], vm.Sections.Select(s => s.Title));
        Assert.Equal(SettingsSection.Devices, vm.SelectedSection);
        Assert.Equal("Dispositivos", vm.SectionTitle);
    }

    [Fact]
    public void SelectingASection_ChangesTheTitle()
    {
        using var vm = Create();

        vm.SelectedSection = SettingsSection.About;

        Assert.Equal("Acerca de", vm.SectionTitle);
    }

    [Fact]
    public void About_ShowsVersion_AndOpensTheRepository()
    {
        using var vm = Create();

        vm.OpenRepositoryCommand.Execute(null);

        Assert.Equal("Versión 1.2.3", vm.VersionText);
        Assert.Equal("Repositorio en GitHub", vm.RepositoryText);
        Assert.Equal([SettingsViewModel.RepositoryUrl], _opened);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var vm = Create();

        vm.Dispose();

        Assert.Equal(0, _devices.Subscribers);
        Assert.Equal(1, _settings.Subscribers);
    }
}
