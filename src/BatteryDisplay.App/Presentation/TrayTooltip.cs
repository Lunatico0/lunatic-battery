using System.Globalization;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Presentation;

public static class TrayTooltip
{
    /// <summary>NotifyIcon.Text throws above this length.</summary>
    public const int MaxLength = 127;

    private const string Ellipsis = "...";

    public static string For(string deviceName, BatteryStatus status)
    {
        var suffix = ": " + Level(status);
        var room = MaxLength - suffix.Length;
        var name = deviceName.Length <= room ? deviceName : deviceName[..(room - Ellipsis.Length)] + Ellipsis;
        return name + suffix;
    }

    private static string Level(BatteryStatus status) => status switch
    {
        { Percent: null } => "nivel desconocido",
        { Charge: ChargeState.Charging } => string.Create(CultureInfo.InvariantCulture, $"{status.Percent}% (cargando)"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{status.Percent}%"),
    };
}
