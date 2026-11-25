using BatteryDisplay.App.Ui.Mvvm;

namespace BatteryDisplay.App.Tests.Ui;

public class MvvmTests
{
    private sealed class Sample : ObservableObject
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [Fact]
    public void SetProperty_NotifiesOnlyOnChange()
    {
        var sample = new Sample();
        var changes = new PropertyChanges(sample);

        sample.Value = 1;
        sample.Value = 1;

        Assert.Equal([nameof(Sample.Value)], changes.Names);
    }

    [Fact]
    public void RelayCommand_RunsTheAction_WhenItCan()
    {
        var runs = 0;
        var can = false;
        var command = new RelayCommand(() => runs++, () => can);

        Assert.False(command.CanExecute(null));
        command.Execute(null);
        can = true;
        command.Execute(null);

        Assert.True(command.CanExecute(null));
        Assert.Equal(1, runs);
    }

    [Fact]
    public void RelayCommand_RaiseCanExecuteChanged_Notifies()
    {
        var command = new RelayCommand(() => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }
}
