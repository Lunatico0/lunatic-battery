using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Tray;
using Microsoft.Win32;

namespace BatteryDisplay.App.Tests.Tray;

public class TaskbarThemeTests
{
    private static readonly TrayIconInk WhiteOnBlack = new(Text: unchecked((int)0xFFFFFFFF), GrayText: unchecked((int)0xFF3FF23F));
    private static readonly TrayIconInk YellowOnBlack = new(Text: unchecked((int)0xFFFFFF00), GrayText: unchecked((int)0xFFA6A6A6));

    private bool _light;
    private bool _highContrast;
    private TrayIconInk _ink = WhiteOnBlack;
    private readonly QueuedContext _ui = new();

    private TaskbarTheme Create() => new(() => _light, () => _highContrast, () => _ink, _ui);

    [Theory]
    [InlineData(false, TrayIconTheme.Dark)]
    [InlineData(true, TrayIconTheme.Light)]
    public void StartsFromTheTaskbarSetting(bool light, TrayIconTheme expected)
    {
        _light = light;

        using var theme = Create();

        Assert.Equal(expected, theme.Theme);
    }

    [Fact]
    public void PreferenceChange_RechecksOnTheUiContext_ThenRaisesChanged()
    {
        using var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;

        _light = true;
        theme.PreferenceChanged(UserPreferenceCategory.General);

        // Nothing happens on the SystemEvents thread: the recheck is posted to the UI context.
        Assert.Equal((TrayIconTheme.Dark, 0), (theme.Theme, raised));

        _ui.RunAll();

        Assert.Equal((TrayIconTheme.Light, 1), (theme.Theme, raised));
    }

    [Fact]
    public void PreferenceChange_WithTheSameTaskbarTheme_DoesNotRaise()
    {
        using var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;

        theme.PreferenceChanged(UserPreferenceCategory.General);
        _ui.RunAll();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SwitchingBackToDark_RaisesAgain()
    {
        _light = true;
        using var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;

        _light = false;
        theme.PreferenceChanged(UserPreferenceCategory.General);
        _ui.RunAll();

        Assert.Equal((TrayIconTheme.Dark, 1), (theme.Theme, raised));
    }

    [Fact]
    public void AfterDispose_APendingRecheck_DoesNotRaise()
    {
        var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;
        _light = true;
        theme.PreferenceChanged(UserPreferenceCategory.General);

        theme.Dispose();
        _ui.RunAll();

        Assert.Equal(0, raised);
    }

    // High contrast

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HighContrast_WinsOverTheTaskbarSetting_AndCarriesTheSystemInk(bool light)
    {
        _light = light;
        _highContrast = true;

        using var theme = Create();

        Assert.Equal((TrayIconTheme.HighContrast, WhiteOnBlack), (theme.Theme, theme.Ink));
    }

    [Fact]
    public void OutsideHighContrast_TheInkIsEmpty()
    {
        using var theme = Create();

        Assert.Equal(default, theme.Ink);
    }

    [Fact]
    public void TurningHighContrastOnAndOff_RaisesEachTime()
    {
        _light = true;
        using var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;

        _highContrast = true;
        theme.PreferenceChanged(UserPreferenceCategory.Accessibility);
        _ui.RunAll();

        Assert.Equal((TrayIconTheme.HighContrast, WhiteOnBlack, 1), (theme.Theme, theme.Ink, raised));

        _highContrast = false;
        theme.PreferenceChanged(UserPreferenceCategory.Accessibility);
        _ui.RunAll();

        Assert.Equal((TrayIconTheme.Light, default(TrayIconInk), 2), (theme.Theme, theme.Ink, raised));
    }

    [Fact]
    public void SwitchingBetweenHighContrastThemes_RaisesWithTheNewInk()
    {
        _highContrast = true;
        using var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;

        _ink = YellowOnBlack;
        theme.PreferenceChanged(UserPreferenceCategory.Color);
        _ui.RunAll();

        Assert.Equal((TrayIconTheme.HighContrast, YellowOnBlack, 1), (theme.Theme, theme.Ink, raised));
    }

    [Fact]
    public void SystemColorChange_OutsideHighContrast_DoesNotRaise()
    {
        using var theme = Create();
        var raised = 0;
        theme.Changed += (_, _) => raised++;

        _ink = YellowOnBlack;
        theme.PreferenceChanged(UserPreferenceCategory.Color);
        _ui.RunAll();

        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    [InlineData("1", false)]
    public void RegistryValue_OneMeansLight_AnythingElseIsDark(object? value, bool light)
    {
        Assert.Equal(light, TaskbarTheme.IsLightValue(value));
    }

    [Fact]
    public void ReadsTheTaskbarValue_NotTheAppsOne()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", TaskbarTheme.KeyPath);
        Assert.Equal("SystemUsesLightTheme", TaskbarTheme.ValueName);
    }

    /// <summary>Queues posted work until the test runs it, standing in for the UI thread's message loop.</summary>
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public void RunAll()
        {
            while (_posted.TryDequeue(out var work))
                work.Callback(work.State);
        }
    }
}
