using System.Runtime.CompilerServices;
using System.Xml.Linq;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Ui.Themes;

namespace BatteryDisplay.App.Tests.Ui;

public class ThemeTonesTests
{
    private bool _highContrast;
    private bool _light;
    private UiAppearance _appearance = UiAppearance.Fluent;

    private ToneSelector Create() => new(() => _highContrast, () => _light, () => _appearance);

    [Theory]
    [InlineData(false, false, ThemeTones.Dark)]
    [InlineData(false, true, ThemeTones.Light)]
    [InlineData(true, false, ThemeTones.HighContrast)]
    [InlineData(true, true, ThemeTones.HighContrast)]
    public void HighContrast_WinsOverTheAppMode(bool highContrast, bool light, ThemeTones expected)
    {
        _highContrast = highContrast;
        _light = light;

        Assert.Equal(expected, Create().Current);
    }

    [Theory]
    [InlineData(false, false, ThemeTones.GlassDark)]
    [InlineData(false, true, ThemeTones.GlassLight)]
    [InlineData(true, false, ThemeTones.HighContrast)]
    [InlineData(true, true, ThemeTones.HighContrast)]
    public void Glass_FollowsTheAppMode_AndHighContrastStillWins(bool highContrast, bool light, ThemeTones expected)
    {
        _highContrast = highContrast;
        _light = light;
        _appearance = UiAppearance.Glass;

        Assert.Equal(expected, Create().Current);
    }

    [Fact]
    public void TurningHighContrastOn_SwapsTheTonesAndReloadsTheSystemTheme()
    {
        var selector = Create();

        _highContrast = true;

        Assert.Equal(new ToneChange(Tones: true, HighContrast: true), selector.Recheck());
        Assert.Equal(ThemeTones.HighContrast, selector.Current);
    }

    [Fact]
    public void TurningHighContrastOff_GoesBackToTheAppMode()
    {
        _highContrast = true;
        _light = true;
        var selector = Create();

        _highContrast = false;

        Assert.Equal(new ToneChange(Tones: true, HighContrast: true), selector.Recheck());
        Assert.Equal(ThemeTones.Light, selector.Current);
    }

    [Fact]
    public void TurningHighContrastOff_GoesBackToGlass_WhenGlassIsChosen()
    {
        _highContrast = true;
        _appearance = UiAppearance.Glass;
        var selector = Create();

        _highContrast = false;

        Assert.Equal(new ToneChange(Tones: true, HighContrast: true), selector.Recheck());
        Assert.Equal(ThemeTones.GlassDark, selector.Current);
    }

    [Fact]
    public void AppModeChange_SwapsOnlyTheTones()
    {
        var selector = Create();

        _light = true;

        Assert.Equal(new ToneChange(Tones: true, HighContrast: false), selector.Recheck());
    }

    [Fact]
    public void AppModeChange_WhileInHighContrast_ChangesNothing()
    {
        _highContrast = true;
        var selector = Create();

        _light = true;

        Assert.Equal(new ToneChange(Tones: false, HighContrast: false), selector.Recheck());
        Assert.Equal(ThemeTones.HighContrast, selector.Current);
    }

    [Fact]
    public void ChoosingGlass_SwapsOnlyTheTones_WithoutReloadingTheSystemTheme()
    {
        _light = true;
        var selector = Create();

        _appearance = UiAppearance.Glass;

        Assert.Equal(new ToneChange(Tones: true, HighContrast: false), selector.Recheck());
        Assert.Equal(ThemeTones.GlassLight, selector.Current);
    }

    [Fact]
    public void GoingBackToFluent_SwapsTheTonesBack()
    {
        _appearance = UiAppearance.Glass;
        var selector = Create();

        _appearance = UiAppearance.Fluent;

        Assert.Equal(new ToneChange(Tones: true, HighContrast: false), selector.Recheck());
        Assert.Equal(ThemeTones.Dark, selector.Current);
    }

    [Fact]
    public void AppearanceChange_WhileInHighContrast_ChangesNothing()
    {
        _highContrast = true;
        var selector = Create();

        _appearance = UiAppearance.Glass;

        Assert.Equal(new ToneChange(Tones: false, HighContrast: false), selector.Recheck());
        Assert.Equal(ThemeTones.HighContrast, selector.Current);
    }

    [Fact]
    public void EveryTone_LoadsItsOwnDictionary()
    {
        var files = Enum.GetValues<ThemeTones>().Select(t => FileOf(AppTheme.SourceOf(t))).ToArray();

        Assert.Equal(
            ["Lunatic.Dark.xaml", "Lunatic.Light.xaml", "Lunatic.HighContrast.xaml", "Lunatic.Glass.Dark.xaml", "Lunatic.Glass.Light.xaml"],
            files);
    }

    [Theory]
    [InlineData("Lunatic.Light.xaml")]
    [InlineData("Lunatic.HighContrast.xaml")]
    [InlineData("Lunatic.Glass.Dark.xaml")]
    [InlineData("Lunatic.Glass.Light.xaml")]
    public void EveryToneDictionary_DefinesTheSameLunaticKeys(string file)
    {
        var dark = Keys("Lunatic.Dark.xaml");

        Assert.NotEmpty(dark);
        Assert.Equal(dark.Order(), Keys(file).Order());
    }

    private static string FileOf(Uri source) => source.OriginalString[(source.OriginalString.LastIndexOf('/') + 1)..];

    private static HashSet<string> Keys(string file, [CallerFilePath] string here = "")
    {
        var themes = Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "src", "BatteryDisplay.App", "Ui", "Themes");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(Path.Combine(themes, file)).Root!.Elements()
            .Select(e => (string?)e.Attribute(x + "Key"))
            .Where(key => key is not null && key.StartsWith("Lunatic.", StringComparison.Ordinal))
            .Select(key => key!)
            .ToHashSet();
    }
}
