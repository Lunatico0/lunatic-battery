using BatteryDisplay.App.Settings;

namespace BatteryDisplay.App.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Default_IsPerDevice_WithNoDevices()
    {
        Assert.Equal(TrayIconMode.PerDevice, AppSettings.Default.TrayIconMode);
        Assert.Empty(AppSettings.Default.Devices);
    }

    [Fact]
    public void Default_NotifiesLowBattery()
    {
        Assert.True(AppSettings.Default.NotifyLowBattery);
    }

    [Fact]
    public void Default_LooksLikeWindows()
    {
        Assert.Equal(UiAppearance.Fluent, AppSettings.Default.Appearance);
    }

    [Fact]
    public void WithDevice_ReturnsACopy_LeavingTheOriginalUntouched()
    {
        var original = AppSettings.Default;

        var changed = original.WithDevice("a", new DeviceSettings("Mouse", true, 20, false));

        Assert.True(changed.For("a").Pinned);
        Assert.False(original.For("a").Pinned);
    }

    [Theory]
    [InlineData(null, "Razer Cobra")]
    [InlineData("", "Razer Cobra")]
    [InlineData("   ", "Razer Cobra")]
    [InlineData("Mi mouse", "Mi mouse")]
    public void DisplayNameOf_PrefersTheUserName_OverTheProviderName(string? displayName, string expected)
    {
        var settings = AppSettings.Default.WithDevice("a", DeviceSettings.Default with { DisplayName = displayName });

        Assert.Equal(expected, settings.DisplayNameOf("a", "Razer Cobra"));
    }

    [Fact]
    public void Default_HasNoDeviceAliases()
    {
        Assert.Empty(AppSettings.Default.DeviceAliases);
        Assert.Equal("a", AppSettings.Default.CanonicalIdOf("a"));
    }

    [Fact]
    public void WithAlias_SendsTheAliasToTheCanonicalId()
    {
        var settings = AppSettings.Default.WithAlias("bt-mouse", "razer");

        Assert.Equal("razer", settings.CanonicalIdOf("bt-mouse"));
        Assert.Equal("razer", settings.CanonicalIdOf("razer"));
    }

    [Fact]
    public void WithAlias_MovesTheAliasSettings_ToTheCanonicalId_WhenItHasNone()
    {
        var mine = DeviceSettings.Default with { DisplayName = "Mi mouse", Pinned = true };

        var settings = AppSettings.Default.WithDevice("bt-mouse", mine).WithAlias("bt-mouse", "razer");

        Assert.Equal(mine, settings.For("razer"));
        Assert.False(settings.Devices.ContainsKey("bt-mouse"));
    }

    [Fact]
    public void WithAlias_KeepsTheCanonicalSettings_WhenBothHaveSome()
    {
        var canonical = DeviceSettings.Default with { DisplayName = "Cobra" };

        var settings = AppSettings.Default
            .WithDevice("razer", canonical)
            .WithDevice("bt-mouse", DeviceSettings.Default with { DisplayName = "Mi mouse" })
            .WithAlias("bt-mouse", "razer");

        Assert.Equal(canonical, settings.For("razer"));
        Assert.False(settings.Devices.ContainsKey("bt-mouse"));
    }

    [Fact]
    public void WithAlias_RepointsTheAliasesOfTheAlias_SoNoneChains()
    {
        var settings = AppSettings.Default.WithAlias("bt-mouse", "xinput-0").WithAlias("xinput-0", "razer");

        Assert.Equal("razer", settings.CanonicalIdOf("bt-mouse"));
        Assert.Equal("razer", settings.CanonicalIdOf("xinput-0"));
    }

    [Fact]
    public void WithAlias_TheOtherWayRound_ReplacesTheOldDirection_WithoutACycle()
    {
        var settings = AppSettings.Default.WithAlias("razer", "bt-mouse").WithAlias("bt-mouse", "razer");

        Assert.Equal("razer", settings.CanonicalIdOf("bt-mouse"));
        Assert.Equal("razer", settings.CanonicalIdOf("razer"));
        Assert.Equal(["bt-mouse"], settings.DeviceAliases.Keys);
    }

    [Fact]
    public void WithAlias_AlreadyKnown_ReturnsTheSameSettings()
    {
        var settings = AppSettings.Default.WithAlias("bt-mouse", "razer");

        Assert.Same(settings, settings.WithAlias("bt-mouse", "razer"));
        Assert.Same(settings, settings.WithAlias("razer", "razer"));
    }
}
