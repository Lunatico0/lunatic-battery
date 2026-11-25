using System.Globalization;
using System.Windows.Input;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Ui.Devices;
using BatteryDisplay.App.Ui.Mvvm;

namespace BatteryDisplay.App.Ui.Flyout;

/// <summary>
/// The tray flyout: one row per device in snapshot order, a header that counts devices and low ones, and how long ago
/// the newest reading came in. Rebuilds on every device list and every settings change (a threshold changes the
/// state without a new list). <see cref="Tick"/> moves the "Actualizado hace" text while the flyout is open.
/// Runs on the thread that raises the events (the UI thread in the app).
/// </summary>
public sealed class FlyoutViewModel : ObservableObject, IDisposable
{
    public const string Title = "Lunatic Battery";
    public const string EmptyText = "Conectá un mouse, un teclado, unos auriculares o un control para ver su batería acá.";

    private readonly IDeviceSnapshots _devices;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private IReadOnlyList<DeviceRowViewModel> _rows = [];
    private DateTimeOffset? _newestReadAt;
    private string _subtitle = "";
    private string _updatedText = "";
    private bool _disposed;

    public FlyoutViewModel(IDeviceSnapshots devices, ISettingsStore settings, TimeProvider time)
    {
        _devices = devices;
        _settings = settings;
        _time = time;
        OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _devices.DevicesChanged += OnDevicesChanged;
        _settings.Changed += OnSettingsChanged;
        Rebuild();
    }

    public IReadOnlyList<DeviceRowViewModel> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>"N dispositivos · M con batería baja"; the second part only when some device is low.</summary>
    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    /// <summary>"Actualizado hace X s/min" from the newest reading; empty when no device has been read.</summary>
    public string UpdatedText
    {
        get => _updatedText;
        private set
        {
            if (SetProperty(ref _updatedText, value))
                OnPropertyChanged(nameof(HasUpdatedText));
        }
    }

    public bool HasUpdatedText => UpdatedText.Length > 0;

    /// <summary>The header button and the footer link; the host closes the flyout and opens the settings window.</summary>
    public ICommand OpenSettingsCommand { get; }

    public event EventHandler? SettingsRequested;

    public static string SubtitleFor(int devices, int low)
    {
        var count = string.Create(CultureInfo.InvariantCulture, $"{devices} {(devices == 1 ? "dispositivo" : "dispositivos")}");
        return low == 0 ? count : string.Create(CultureInfo.InvariantCulture, $"{count} · {low} con batería baja");
    }

    public static string UpdatedFor(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;
        return age < TimeSpan.FromMinutes(1)
            ? string.Create(CultureInfo.InvariantCulture, $"Actualizado hace {(int)age.TotalSeconds} s")
            : string.Create(CultureInfo.InvariantCulture, $"Actualizado hace {(int)age.TotalMinutes} min");
    }

    /// <summary>Recomputes the age text against the clock; the flyout calls it every second while it is open.</summary>
    public void Tick() => UpdatedText = _newestReadAt is { } at ? UpdatedFor(_time.GetUtcNow() - at) : "";

    private void OnDevicesChanged(object? sender, IReadOnlyList<DeviceSnapshot> devices) => Rebuild();

    private void OnSettingsChanged(object? sender, AppSettings settings) => Rebuild();

    private void Rebuild()
    {
        if (_disposed)
            return;
        var settings = _settings.Current;
        // Hidden devices are only listed in the settings window: no row, no count, no "Actualizado" time.
        var devices = _devices.Devices.Where(d => !settings.For(d.Id).Hidden).ToArray();
        var wasEmpty = IsEmpty;
        Rows = devices.Select(d => DeviceRowViewModel.From(d, settings.For(d.Id))).ToArray();
        if (wasEmpty != IsEmpty)
            OnPropertyChanged(nameof(IsEmpty));
        // Counted like the row state, so a charging device below its threshold is not reported as low.
        Subtitle = SubtitleFor(Rows.Count, Rows.Count(r => r.State == DeviceState.Low));
        _newestReadAt = devices.Max(d => d.LastReadAt);
        Tick();
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
