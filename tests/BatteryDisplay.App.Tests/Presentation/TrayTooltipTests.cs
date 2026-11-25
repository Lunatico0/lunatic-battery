using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

public class TrayTooltipTests
{
    private const string Name = "Razer Cobra HyperSpeed";

    [Fact]
    public void Normal()
    {
        Assert.Equal("Razer Cobra HyperSpeed: 78%", TrayTooltip.For(Name, new BatteryStatus(78, ChargeState.Discharging)));
    }

    [Fact]
    public void Full_IsNotCharging()
    {
        Assert.Equal("Razer Cobra HyperSpeed: 100%", TrayTooltip.For(Name, new BatteryStatus(100, ChargeState.Full)));
    }

    [Fact]
    public void Charging()
    {
        Assert.Equal("Razer Cobra HyperSpeed: 78% (cargando)", TrayTooltip.For(Name, new BatteryStatus(78, ChargeState.Charging)));
    }

    [Fact]
    public void UnknownLevel()
    {
        Assert.Equal("Razer Cobra HyperSpeed: nivel desconocido", TrayTooltip.For(Name, new BatteryStatus(null, ChargeState.Unknown)));
    }

    [Fact]
    public void LongName_IsTrimmedTo127_KeepingTheLevel()
    {
        var longName = new string('X', 200);

        var text = TrayTooltip.For(longName, new BatteryStatus(78, ChargeState.Charging));

        Assert.Equal(TrayTooltip.MaxLength, text.Length);
        Assert.EndsWith("...: 78% (cargando)", text);
        Assert.StartsWith("XXXX", text);
    }

    [Fact]
    public void NameThatFitsExactly_IsNotTrimmed()
    {
        const string suffix = ": 78%";
        var name = new string('X', TrayTooltip.MaxLength - suffix.Length);

        Assert.Equal(name + suffix, TrayTooltip.For(name, new BatteryStatus(78, ChargeState.Discharging)));
    }
}
