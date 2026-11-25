using System.Windows.Input;

namespace BatteryDisplay.App.Ui.Mvvm;

/// <summary>ICommand over a delegate. CanExecuteChanged is raised only by <see cref="RaiseCanExecuteChanged"/>.</summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
