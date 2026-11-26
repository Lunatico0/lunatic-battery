using System.Text;
using System.Windows;

namespace BatteryDisplay.App.Ui.Diagnostics;

/// <summary>The WPF clipboard. Clipboard.SetText throws when another app holds the clipboard.</summary>
internal sealed class WpfClipboard : IClipboardService
{
    public void SetText(string text) => System.Windows.Clipboard.SetText(text);
}

/// <summary>The Windows save dialog, owned by the diagnostic window, and a UTF-8 write Notepad opens as is.</summary>
internal sealed class ReportFileDialog : IReportFileSaver
{
    public Window? Owner { get; set; }

    public string? AskPath(string defaultFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar diagnóstico",
            FileName = defaultFileName,
            DefaultExt = ".txt",
            Filter = "Texto (*.txt)|*.txt",
            AddExtension = true,
            OverwritePrompt = true,
        };
        var picked = Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return picked == true ? dialog.FileName : null;
    }

    public void Write(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));
}
