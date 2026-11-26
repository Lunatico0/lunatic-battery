using System.Globalization;
using System.Windows.Input;
using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Ui.Devices;
using BatteryDisplay.App.Ui.Mvvm;

namespace BatteryDisplay.App.Ui.Settings;

/// <summary>The "Iniciar con Windows" switch, behind an interface so the view model never touches the registry.</summary>
public interface IStartupSetting
{
    bool IsEnabled { get; }

    /// <summary>Throws when the setting could not be written.</summary>
    void SetEnabled(bool enabled);
}

public enum SettingsSection { Devices, Tray, General, About }

public sealed record SettingsSectionItem(SettingsSection Section, string Title, string Glyph);

public sealed record AppearanceOption(UiAppearance Appearance, string Title);

/// <summary>
/// One tray mode card. Checking it asks the owner to switch to its mode; unchecking is ignored, because a card is
/// only left by checking another one, and the owner then refreshes every card from the saved mode.
/// </summary>
public sealed class TrayModeOption : ObservableObject
{
    private readonly Action<TrayIconMode> _select;
    private bool _isSelected;

    internal TrayModeOption(TrayIconMode mode, string title, string description, Action<TrayIconMode> select)
    {
        Mode = mode;
        Title = title;
        Description = description;
        _select = select;
    }

    public TrayIconMode Mode { get; }
    public string Title { get; }
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value && !_isSelected)
                _select(Mode);
            else
                // Tells the radio button to read the value back when the change was ignored.
                OnPropertyChanged();
        }
    }

    /// <summary>Always notifies, so a radio button the user unchecked shows the saved state again.</summary>
    internal void Refresh(TrayIconMode current)
    {
        _isSelected = current == Mode;
        OnPropertyChanged(nameof(IsSelected));
    }
}

/// <summary>
/// The settings window. Every change is saved through <see cref="ISettingsStore"/> as soon as it is made; there is no
/// Apply. Device options act on the selected device; the selection follows the device by Id across list changes.
/// Changes saved anywhere else (the tray menu, another view model) come back through the store's Changed event.
/// Runs on the thread that raises the events (the UI thread in the app).
/// </summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    public const string AppName = "Lunatic Battery";
    public const string LicenseText = "Software libre bajo licencia MIT.";

    private const string PinnedEnabledHint = "Si lo apagás, entra en el ícono combinado";
    private const string PinnedDisabledHint = "Disponible en el modo \"Combinado, con fijados\" de Bandeja y avisos";

    private readonly IDeviceSnapshots _devices;
    private readonly ISettingsStore _settings;
    private readonly IStartupSetting _startup;
    private IReadOnlyList<DeviceRowViewModel> _deviceRows = [];
    private string? _selectedId;
    private SettingsSection _selectedSection = SettingsSection.Devices;
    private bool _startWithWindows;
    private bool _disposed;

    public SettingsViewModel(IDeviceSnapshots devices, ISettingsStore settings, IStartupSetting startup, string version, string? repositoryUrl, Action<string> openUrl)
    {
        _devices = devices;
        _settings = settings;
        _startup = startup;
        VersionText = $"Versión {version}";
        HasRepository = repositoryUrl is not null;
        OpenRepositoryCommand = new RelayCommand(() =>
        {
            if (repositoryUrl is not null)
                openUrl(repositoryUrl);
        });
        ShowDiagnosticsCommand = new RelayCommand(() => DiagnosticsRequested?.Invoke(this, EventArgs.Empty));
        Sections =
        [
            new(SettingsSection.Devices, "Dispositivos", "\uE772"),
            new(SettingsSection.Tray, "Bandeja y avisos", "\uEA8F"),
            new(SettingsSection.General, "General", "\uE713"),
            new(SettingsSection.About, "Acerca de", "\uE946"),
        ];
        TrayModes =
        [
            new(TrayIconMode.PerDevice, "Un ícono por dispositivo",
                "Cada dispositivo con su anillo y su silueta, se lee de un vistazo.", SelectMode),
            new(TrayIconMode.Lowest, "Un solo ícono, el más bajo",
                "Muestra solo el nivel más bajo. El resto lo ves en el panel.", SelectMode),
            new(TrayIconMode.LowestWithPinned, "Combinado, con fijados",
                "Un ícono combinado más los que vos fijes (por ejemplo, el mouse). Se elige por dispositivo en Configuración.", SelectMode),
        ];
        foreach (var mode in TrayModes)
            mode.Refresh(_settings.Current.TrayIconMode);
        _startWithWindows = _startup.IsEnabled;
        RebuildDevices();
        _devices.DevicesChanged += OnDevicesChanged;
        _settings.Changed += OnSettingsChanged;
    }

    public string AppTitle => AppName;

    public IReadOnlyList<SettingsSectionItem> Sections { get; }

    public SettingsSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (!SetProperty(ref _selectedSection, value))
                return;
            OnPropertyChanged(nameof(SectionTitle));
            if (value == SettingsSection.General)
            {
                // The tray menu can change it while the window is open.
                _startWithWindows = _startup.IsEnabled;
                OnPropertyChanged(nameof(StartWithWindows));
            }
        }
    }

    public string SectionTitle => Sections.First(s => s.Section == SelectedSection).Title;

    /// <summary>Connected devices only, in the monitor's order.</summary>
    public IReadOnlyList<DeviceRowViewModel> Devices
    {
        get => _deviceRows;
        private set => SetProperty(ref _deviceRows, value);
    }

    /// <summary>
    /// The device the options act on. A null from the list (it clears its selection when the list is replaced) keeps
    /// the current device while there is one.
    /// </summary>
    public DeviceRowViewModel? SelectedDevice
    {
        get => Devices.FirstOrDefault(d => d.Id == _selectedId);
        set
        {
            if (value is null && Devices.Count > 0)
            {
                OnPropertyChanged();
                return;
            }
            if (value?.Id == _selectedId)
                return;
            _selectedId = value?.Id;
            RaiseSelection();
        }
    }

    public bool HasSelection => SelectedDevice is not null;

    /// <summary>No device is connected: the section shows an invitation instead of the list and the options.</summary>
    public bool IsEmpty => Devices.Count == 0;

    public string DetailTitle => SelectedDevice?.Name ?? "";

    /// <summary>"82%, descargando · Dongle Razer"; just the state when there is no level, and no source when it is empty.</summary>
    public string DetailSubtitle
    {
        get
        {
            if (SelectedDevice is not { } device)
                return "";
            var level = device.LevelKnown
                ? $"{device.ValueText}, {device.StateText.ToLower(CultureInfo.GetCultureInfo("es"))}"
                : device.StateText;
            return string.IsNullOrEmpty(device.Source) ? level : $"{level} · {device.Source}";
        }
    }

    /// <summary>"Mostrar este dispositivo": off hides it from the tray and the flyout. Saved as soon as it changes.</summary>
    public bool ShowDevice
    {
        get => !SelectedOptions.Hidden;
        set => UpdateSelected(s => s with { Hidden = !value });
    }

    /// <summary>The other device options only matter for a device that is shown; the window disables them while hidden.</summary>
    public bool OptionsEnabled => !SelectedOptions.Hidden;

    public bool Pinned
    {
        get => SelectedOptions.Pinned;
        set
        {
            if (CanPin)
                UpdateSelected(s => s with { Pinned = value });
            else
                OnPropertyChanged();
        }
    }

    /// <summary>Pinning only does something in <see cref="TrayIconMode.LowestWithPinned"/>.</summary>
    public bool CanPin => _settings.Current.TrayIconMode == TrayIconMode.LowestWithPinned;

    public string PinnedHint => CanPin ? PinnedEnabledHint : PinnedDisabledHint;

    public IReadOnlyList<int> ThresholdOptions { get; } = [5, 10, 15, 20, 25, 30];

    public int LowBatteryThreshold
    {
        get => SelectedOptions.LowBatteryThreshold;
        set => UpdateSelected(s => s with { LowBatteryThreshold = value });
    }

    public bool NotifyWhenCharged
    {
        get => SelectedOptions.NotifyWhenCharged;
        set => UpdateSelected(s => s with { NotifyWhenCharged = value });
    }

    /// <summary>The user's name for the device, empty when it uses the provider name. Blank text clears it.</summary>
    public string DisplayName
    {
        get => SelectedOptions.DisplayName ?? "";
        set
        {
            var name = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            UpdateSelected(s => s with { DisplayName = name });
            // The text box shows the trimmed value even when nothing was saved.
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<AppearanceOption> Appearances { get; } =
    [
        new(UiAppearance.Fluent, "Fluent, como Windows"),
        new(UiAppearance.Glass, "Lunatic Glass"),
    ];

    /// <summary>How the flyout and this window look. Saved as soon as it changes; the theme follows the save live.</summary>
    public UiAppearance Appearance
    {
        get => _settings.Current.Appearance;
        set
        {
            if (value != _settings.Current.Appearance)
                _settings.Save(_settings.Current with { Appearance = value });
        }
    }

    public IReadOnlyList<TrayModeOption> TrayModes { get; }

    public TrayIconMode TrayIconMode
    {
        get => _settings.Current.TrayIconMode;
        set
        {
            if (value != _settings.Current.TrayIconMode)
                _settings.Save(_settings.Current with { TrayIconMode = value });
        }
    }

    /// <summary>"Avisar cuando la batería esté baja", for every device. Saved as soon as it changes.</summary>
    public bool NotifyLowBattery
    {
        get => _settings.Current.NotifyLowBattery;
        set
        {
            if (value != _settings.Current.NotifyLowBattery)
                _settings.Save(_settings.Current with { NotifyLowBattery = value });
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value == _startWithWindows)
                return;
            try
            {
                _startup.SetEnabled(value);
            }
            catch (Exception ex)
            {
                AppLog.Write("startup", "Could not change startup setting", ex);
            }
            _startWithWindows = _startup.IsEnabled;
            OnPropertyChanged();
        }
    }

    public string VersionText { get; }

    public string RepositoryText => "Repositorio en GitHub";

    /// <summary>False while RepositoryUrl in Directory.Build.props is empty (Updates/AppRepository); the link hides.</summary>
    public bool HasRepository { get; }

    public ICommand OpenRepositoryCommand { get; }

    /// <summary>"Mi dispositivo no aparece": the owner opens the diagnostic window.</summary>
    public ICommand ShowDiagnosticsCommand { get; }

    public event EventHandler? DiagnosticsRequested;

    private DeviceSettings SelectedOptions => _selectedId is { } id ? _settings.Current.For(id) : DeviceSettings.Default;

    private void SelectMode(TrayIconMode mode) => TrayIconMode = mode;

    private void UpdateSelected(Func<DeviceSettings, DeviceSettings> change)
    {
        if (_selectedId is not { } id)
            return;
        var current = _settings.Current.For(id);
        var updated = change(current);
        if (updated != current)
            _settings.Save(_settings.Current.WithDevice(id, updated));
    }

    private void OnDevicesChanged(object? sender, IReadOnlyList<DeviceSnapshot> devices) => RebuildDevices();

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_disposed)
            return;
        // The threshold changes the rows' state text.
        RebuildDevices();
        foreach (var mode in TrayModes)
            mode.Refresh(settings.TrayIconMode);
        OnPropertyChanged(nameof(TrayIconMode));
        OnPropertyChanged(nameof(NotifyLowBattery));
        OnPropertyChanged(nameof(Appearance));
        OnPropertyChanged(nameof(CanPin));
        OnPropertyChanged(nameof(PinnedHint));
        RaiseOptions();
    }

    private void RebuildDevices()
    {
        if (_disposed)
            return;
        var settings = _settings.Current;
        Devices = _devices.Devices.Select(d => DeviceRowViewModel.From(d, settings.For(d.Id))).ToArray();
        var selectedId = _selectedId;
        if (Devices.All(d => d.Id != selectedId))
            _selectedId = Devices.FirstOrDefault()?.Id;
        // Raised even when the Id did not change: the list just replaced its items and the row itself is new.
        RaiseSelection();
    }

    private void RaiseSelection()
    {
        OnPropertyChanged(nameof(SelectedDevice));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(DetailTitle));
        OnPropertyChanged(nameof(DetailSubtitle));
        RaiseOptions();
    }

    private void RaiseOptions()
    {
        OnPropertyChanged(nameof(ShowDevice));
        OnPropertyChanged(nameof(OptionsEnabled));
        OnPropertyChanged(nameof(Pinned));
        OnPropertyChanged(nameof(LowBatteryThreshold));
        OnPropertyChanged(nameof(NotifyWhenCharged));
        OnPropertyChanged(nameof(DisplayName));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _devices.DevicesChanged -= OnDevicesChanged;
        _settings.Changed -= OnSettingsChanged;
    }
}
