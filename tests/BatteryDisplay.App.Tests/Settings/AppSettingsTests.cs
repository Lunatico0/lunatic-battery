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
}
