using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.App.Ui.Devices;
using BatteryDisplay.Core;
using static BatteryDisplay.App.Tests.Ui.Snapshots;

namespace BatteryDisplay.App.Tests.Ui;

public class DeviceRowViewModelTests
{
    private static DeviceRowViewModel Row(int? percent, ChargeState charge, int threshold = 15) =>
        DeviceRowViewModel.From(Device("d", percent, charge), DeviceSettings.Default with { LowBatteryThreshold = threshold });

    [Theory]
    [InlineData(60, ChargeState.Discharging, DeviceState.Discharging, "Descargando")]
    [InlineData(60, ChargeState.Unknown, DeviceState.Discharging, "Descargando")]
    [InlineData(15, ChargeState.Discharging, DeviceState.Low, "Batería baja")]
    [InlineData(10, ChargeState.Unknown, DeviceState.Low, "Batería baja")]
    [InlineData(16, ChargeState.Discharging, DeviceState.Discharging, "Descargando")]
    [InlineData(45, ChargeState.Charging, DeviceState.Charging, "Cargando")]
    [InlineData(100, ChargeState.Full, DeviceState.Charged, "Cargada")]
    [InlineData(null, ChargeState.Unknown, DeviceState.NoData, "Sin datos")]
    public void State_FollowsTheReading(int? percent, ChargeState charge, DeviceState state, string text)
    {
        var row = Row(percent, charge);

        Assert.Equal((state, text), (row.State, row.StateText));
    }

    [Fact]
    public void Charging_WinsOverLow()
    {
        Assert.Equal(DeviceState.Charging, Row(5, ChargeState.Charging).State);
    }

    [Fact]
    public void Low_UsesTheDeviceThreshold()
    {
        Assert.Equal(DeviceState.Low, Row(25, ChargeState.Discharging, threshold: 25).State);
        Assert.Equal(DeviceState.Discharging, Row(25, ChargeState.Discharging, threshold: 20).State);
    }

    [Fact]
    public void NoReading_IsSinDatos_WithADash()
    {
        var row = DeviceRowViewModel.From(Unread("pad", DeviceKind.Gamepad), DeviceSettings.Default);

        Assert.Equal((DeviceState.NoData, "Sin datos", "-"), (row.State, row.StateText, row.ValueText));
        Assert.Equal((0, LevelColor.Unknown, false), (row.LitSegments, row.Tone, row.LevelKnown));
    }

    [Theory]
    [InlineData(82, "82%")]
    [InlineData(100, "100%")]
    [InlineData(null, "-")]
    public void Value_IsThePercentOrADash(int? percent, string expected)
    {
        Assert.Equal(expected, Row(percent, ChargeState.Discharging).ValueText);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(12, 1)]
    [InlineData(13, 2)]
    [InlineData(82, 7)]
    [InlineData(88, 8)]
    [InlineData(100, 8)]
    [InlineData(null, 0)]
    public void Ring_LightsEighths_RoundedUp(int? percent, int segments)
    {
        Assert.Equal(segments, Row(percent, ChargeState.Discharging).LitSegments);
    }

    [Theory]
    [InlineData(82, ChargeState.Discharging, LevelColor.High)]
    [InlineData(39, ChargeState.Discharging, LevelColor.Medium)]
    [InlineData(10, ChargeState.Discharging, LevelColor.Low)]
    [InlineData(45, ChargeState.Charging, LevelColor.Charging)]
    [InlineData(null, ChargeState.Unknown, LevelColor.Unknown)]
    public void Tone_MatchesTheTrayRing(int? percent, ChargeState charge, LevelColor tone)
    {
        Assert.Equal(tone, Row(percent, charge).Tone);
    }

    [Theory]
    [InlineData(25, 30, LevelColor.Low)]
    [InlineData(31, 30, LevelColor.Medium)]
    [InlineData(20, 15, LevelColor.Medium)]
    [InlineData(20, 10, LevelColor.Medium)]
    public void Tone_TurnsRedAtTheDevicesOwnThreshold_LikeTheState(int percent, int threshold, LevelColor tone)
    {
        var row = Row(percent, ChargeState.Discharging, threshold);

        Assert.Equal(tone, row.Tone);
        Assert.Equal(tone == LevelColor.Low, row.State == DeviceState.Low);
    }

    [Fact]
    public void Row_CarriesNameSourceAndKindGlyph()
    {
        var row = DeviceRowViewModel.From(Device("cobra", 82, kind: DeviceKind.Headset, name: "Cobra", source: "Dongle Razer"), DeviceSettings.Default);

        Assert.Equal(("cobra", "Cobra", "Dongle Razer", DeviceGlyphs.Headset, true), (row.Id, row.Name, row.Source, row.Glyph, row.LevelKnown));
    }

    [Theory]
    [InlineData(DeviceKind.Mouse, "")]
    [InlineData(DeviceKind.Headset, "")]
    [InlineData(DeviceKind.Keyboard, "")]
    [InlineData(DeviceKind.Gamepad, "")]
    [InlineData(DeviceKind.Other, "")]
    public void Glyph_IsTheSegoeFluentIconOfTheKind(DeviceKind kind, string glyph)
    {
        Assert.Equal(glyph, DeviceGlyphs.For(kind));
    }
}
