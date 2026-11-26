using BatteryDisplay.App.Logging;
using BatteryDisplay.App.Ui.Mvvm;

namespace BatteryDisplay.App.Ui.Diagnostics;

/// <summary>The clipboard, behind an interface so the view model never touches WPF. Throws when the clipboard is busy.</summary>
public interface IClipboardService
{
    void SetText(string text);
}

/// <summary>The save dialog and the file write, behind an interface so the view model can be tested without either.</summary>
public interface IReportFileSaver
{
    /// <summary>The path the user picked, or null when the dialog was cancelled.</summary>
    string? AskPath(string defaultFileName);

    /// <summary>Throws when the file could not be written.</summary>
    void Write(string path, string text);
}

/// <summary>
/// The "Mi dispositivo no aparece" window: builds the read-only diagnostic report off the UI thread, then lets the
/// user copy it or save it as a text file. Nothing leaves the machine unless the user pastes it somewhere.
/// </summary>
public sealed class DiagnosticsViewModel : ObservableObject
{
    public const string DefaultFileName = "lunatic-battery-diagnostico.txt";

    private readonly Func<string> _buildReport;
    private readonly IClipboardService _clipboard;
    private readonly IReportFileSaver _saver;
    private string _reportText = "Buscando dispositivos...";
    private bool _isReady;
    private string _statusText = "";

    public DiagnosticsViewModel(Func<string> buildReport, IClipboardService clipboard, IReportFileSaver saver)
    {
        _buildReport = buildReport;
        _clipboard = clipboard;
        _saver = saver;
        CopyCommand = new RelayCommand(Copy, () => IsReady);
        SaveCommand = new RelayCommand(Save, () => IsReady);
    }

    public string ReportText
    {
        get => _reportText;
        private set => SetProperty(ref _reportText, value);
    }

    /// <summary>The report is built; copying and saving are on.</summary>
    public bool IsReady
    {
        get => _isReady;
        private set
        {
            if (!SetProperty(ref _isReady, value))
                return;
            CopyCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>What the last action did, shown next to the buttons.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public RelayCommand CopyCommand { get; }

    public RelayCommand SaveCommand { get; }

    /// <summary>Enumerating devices can take a moment, so the report is built on the thread pool.</summary>
    public async Task LoadAsync()
    {
        try
        {
            ReportText = await Task.Run(_buildReport);
            IsReady = true;
        }
        catch (Exception ex)
        {
            // The type only: a message can hold a device path.
            ReportText = $"No se pudo armar el diagnóstico ({ex.GetType().Name}).";
        }
    }

    private void Copy()
    {
        try
        {
            _clipboard.SetText(ReportText);
            StatusText = "Copiado al portapapeles.";
        }
        catch (Exception ex)
        {
            AppLog.Write("diagnostics", "Could not copy the diagnostic report", ex);
            StatusText = "No se pudo copiar al portapapeles. Probá de nuevo.";
        }
    }

    private void Save()
    {
        if (_saver.AskPath(DefaultFileName) is not { } path)
            return;
        try
        {
            _saver.Write(path, ReportText);
            StatusText = $"Guardado como {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            AppLog.Write("diagnostics", "Could not save the diagnostic report", ex);
            StatusText = "No se pudo guardar el archivo.";
        }
    }
}
