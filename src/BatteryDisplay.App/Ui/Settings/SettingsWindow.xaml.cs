using System.Windows;

namespace BatteryDisplay.App.Ui.Settings;

/// <summary>The settings window. Everything it shows and saves lives in <see cref="SettingsViewModel"/>.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
