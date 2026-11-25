using System.Globalization;
using System.Text;
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

    /// <summary>
    /// One "name level" line per device, in order. Lines that do not fit are replaced by a final "..." line;
    /// a first line too long on its own keeps its level and trims the name. Never splits a surrogate pair.
    /// </summary>
    public static string ForMany(IReadOnlyList<(string Name, BatteryStatus Status)> devices)
    {
        const string More = "\n...";
        var text = new StringBuilder();
        for (var i = 0; i < devices.Count; i++)
        {
            var (name, status) = devices[i];
            var suffix = " " + Level(status);
            var separator = text.Length == 0 ? "" : "\n";
            var room = MaxLength - text.Length - separator.Length - (i == devices.Count - 1 ? 0 : More.Length);
            if (name.Length + suffix.Length <= room)
            {
                text.Append(separator).Append(name).Append(suffix);
                continue;
            }
            // Earlier lines left room for More; a first line that does not fit is trimmed instead of dropped.
            if (text.Length > 0)
                text.Append(More);
            else
            {
                text.Append(Trim(name, room - suffix.Length)).Append(suffix);
                if (i < devices.Count - 1)
                    text.Append(More);
            }
            break;
        }
        return text.ToString();
    }

    private static string Trim(string name, int room)
    {
        var keep = Math.Max(0, room - Ellipsis.Length);
        if (keep > 0 && char.IsHighSurrogate(name[keep - 1]))
            keep--;
        return name[..keep] + Ellipsis;
    }

    private static string Level(BatteryStatus status) => status switch
    {
        { Percent: null } => "nivel desconocido",
        { Charge: ChargeState.Charging } => string.Create(CultureInfo.InvariantCulture, $"{status.Percent}% (cargando)"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{status.Percent}%"),
    };
}
