using System.Windows;

namespace BatteryDisplay.App.Ui.Diagnostics;

/// <summary>The "Mi dispositivo no aparece" window. It builds the report once, when it first shows.</summary>
public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // LoadAsync never throws; a failed build shows in the report box.
        Loaded += (_, _) => _ = viewModel.LoadAsync();
    }
}
