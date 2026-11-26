using BatteryDisplay.App.Ui.Diagnostics;

namespace BatteryDisplay.App.Tests.Ui.Diagnostics;

public class DiagnosticsViewModelTests
{
    private sealed class FakeClipboard : IClipboardService
    {
        public List<string> Texts { get; } = [];
        public bool Throws { get; set; }

        public void SetText(string text)
        {
            if (Throws)
                throw new System.Runtime.InteropServices.ExternalException("busy");
            Texts.Add(text);
        }
    }

    private sealed class FakeSaver : IReportFileSaver
    {
        public string? Answer { get; set; } = @"C:\Users\someone\Desktop\reporte.txt";
        public bool Throws { get; set; }
        public List<string> Asked { get; } = [];
        public List<(string Path, string Text)> Written { get; } = [];

        public string? AskPath(string defaultFileName)
        {
            Asked.Add(defaultFileName);
            return Answer;
        }

        public void Write(string path, string text)
        {
            if (Throws)
                throw new UnauthorizedAccessException("denied");
            Written.Add((path, text));
        }
    }

    private const string Report = "## Diagnóstico de Lunatic Battery";

    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSaver _saver = new();
    private Func<string> _build = () => Report;

    private DiagnosticsViewModel Create() => new(() => _build(), _clipboard, _saver);

    private static async Task<DiagnosticsViewModel> Loaded(DiagnosticsViewModel vm)
    {
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public void BeforeLoading_SaysItIsSearching_AndTheActionsAreOff()
    {
        var vm = Create();

        Assert.Equal("Buscando dispositivos...", vm.ReportText);
        Assert.False(vm.IsReady);
        Assert.False(vm.CopyCommand.CanExecute(null));
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Loading_ShowsTheReport_AndTurnsTheActionsOn()
    {
        var vm = Create();
        var changes = new PropertyChanges(vm);
        var raised = 0;
        vm.CopyCommand.CanExecuteChanged += (_, _) => raised++;
        vm.SaveCommand.CanExecuteChanged += (_, _) => raised++;

        await vm.LoadAsync();

        Assert.Equal(Report, vm.ReportText);
        Assert.True(vm.IsReady);
        Assert.True(vm.CopyCommand.CanExecute(null));
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(2, raised);
        Assert.Contains(nameof(DiagnosticsViewModel.ReportText), changes.Names);
        Assert.Contains(nameof(DiagnosticsViewModel.IsReady), changes.Names);
    }

    [Fact]
    public async Task Loading_BuildsTheReportOffTheCallingThread()
    {
        var caller = Environment.CurrentManagedThreadId;
        var builder = caller;
        _build = () =>
        {
            builder = Environment.CurrentManagedThreadId;
            return Report;
        };

        await Create().LoadAsync();

        Assert.NotEqual(caller, builder);
    }

    [Fact]
    public async Task AFailingBuild_SaysSo_AndKeepsTheActionsOff()
    {
        _build = () => throw new InvalidOperationException(@"C:\secret\path");

        var vm = await Loaded(Create());

        Assert.Equal("No se pudo armar el diagnóstico (InvalidOperationException).", vm.ReportText);
        Assert.False(vm.IsReady);
        Assert.False(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Copy_PutsTheReportOnTheClipboard()
    {
        var vm = await Loaded(Create());

        vm.CopyCommand.Execute(null);

        Assert.Equal([Report], _clipboard.Texts);
        Assert.Equal("Copiado al portapapeles.", vm.StatusText);
    }

    [Fact]
    public async Task Copy_WhenTheClipboardIsBusy_SaysSo()
    {
        _clipboard.Throws = true;
        var vm = await Loaded(Create());

        vm.CopyCommand.Execute(null);

        Assert.Equal("No se pudo copiar al portapapeles. Probá de nuevo.", vm.StatusText);
    }

    [Fact]
    public async Task Save_AsksWithTheDefaultName_AndWritesTheReport()
    {
        var vm = await Loaded(Create());

        vm.SaveCommand.Execute(null);

        Assert.Equal(["lunatic-battery-diagnostico.txt"], _saver.Asked);
        Assert.Equal([(@"C:\Users\someone\Desktop\reporte.txt", Report)], _saver.Written);
        Assert.Equal("Guardado como reporte.txt.", vm.StatusText);
    }

    [Fact]
    public async Task Save_Cancelled_WritesNothing_AndKeepsTheLastStatus()
    {
        _saver.Answer = null;
        var vm = await Loaded(Create());
        vm.CopyCommand.Execute(null);

        vm.SaveCommand.Execute(null);

        Assert.Single(_saver.Asked);
        Assert.Empty(_saver.Written);
        Assert.Equal("Copiado al portapapeles.", vm.StatusText);
    }

    [Fact]
    public async Task Save_WhenTheWriteFails_SaysSo()
    {
        _saver.Throws = true;
        var vm = await Loaded(Create());

        vm.SaveCommand.Execute(null);

        Assert.Equal("No se pudo guardar el archivo.", vm.StatusText);
    }
}
