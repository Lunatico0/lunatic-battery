using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BatteryDisplay.App.Ui.Settings;
using BatteryDisplay.Devices.Diagnostics;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.App.Ui.Diagnostics;

/// <summary>
/// Turns a <see cref="DiagnosticSnapshot"/> into the plain text, simple markdown report the user pastes into an issue.
/// The snapshot carries no paths, serials or addresses; names still go through <see cref="Redact"/>, because Windows
/// names an unnamed Bluetooth device after its address.
/// </summary>
public static partial class DiagnosticReport
{
    private const string HiddenAddress = "[dirección oculta]";

    public static string Build(string appVersion, string windowsVersion, DiagnosticSnapshot snapshot)
    {
        var text = new StringBuilder();
        text.AppendLine($"## Diagnóstico de {SettingsViewModel.AppName}");
        text.AppendLine();
        text.AppendLine($"- Versión de la app: {appVersion}");
        text.AppendLine($"- Windows: {windowsVersion}");
        AppendSection(text, "Dispositivos HID", snapshot.Hid, HidLine);
        AppendSection(text, "Bluetooth", snapshot.Bluetooth, BluetoothLine);
        AppendSection(text, "Controles XInput", snapshot.XInput, XInputLine);
        return text.ToString();
    }

    private static void AppendSection<T>(StringBuilder text, string title, DiagnosticSection<T> section, Func<T, string> line)
    {
        text.AppendLine();
        text.AppendLine($"### {title}");
        text.AppendLine();
        if (section.Error is { } error)
            text.AppendLine($"No se pudo leer ({error}).");
        else if (section.Items.Count == 0)
            text.AppendLine("Ninguno.");
        foreach (var item in section.Items)
            text.AppendLine($"- {line(item)}");
    }

    private static string HidLine(HidDeviceDiagnostic d)
    {
        var interfaces = d.InterfaceCount == 1 ? "1 interfaz" : $"{d.InterfaceCount} interfaces";
        var usages = d.Usages.Count == 0
            ? "usage desconocido"
            : "usages (page:usage) " + string.Join(", ", d.Usages.Select(u => $"{Hex(u.Page)}:{Hex(u.Id)}"));
        var catalog = d.CatalogEntry is { } entry
            ? $"En el catálogo: {entry.Name} ({entry.Id})."
            : "No está en el catálogo.";
        var maker = Redact(d.Manufacturer) ?? "fabricante desconocido";
        var product = Redact(d.Product) ?? "producto desconocido";
        return $"VID {Hex(d.VendorId)}, PID {Hex(d.ProductId)}: {maker}, {product}. {interfaces}, {usages}. {catalog}";
    }

    private static string BluetoothLine(BluetoothDeviceDiagnostic d)
    {
        var connection = d.IsConnected switch
        {
            true => "conectado",
            false => "desconectado",
            null => "conexión desconocida",
        };
        var battery = d.RawBattery switch
        {
            null => "no expone batería",
            >= 0 and <= 100 => $"batería {d.RawBattery}%",
            _ => $"batería con valor inválido ({d.RawBattery})",
        };
        return $"{Redact(d.Name)}: {connection}, {battery}.";
    }

    private static string XInputLine(XInputSlotDiagnostic d)
    {
        var battery = d.Battery switch
        {
            null => "no informa batería",
            { Type: XInputBatteryType.Wired } => "cableado, sin nivel de batería",
            { Type: XInputBatteryType.Alkaline or XInputBatteryType.NiMH } b =>
                $"batería {b.Type}, nivel {b.Level}{Percent(XInputBatteryMapper.Map(b.Type, b.Level)?.Percent)}",
            { } b => $"tipo de batería {b.Type}",
        };
        return $"Control {d.Slot + 1} (slot {d.Slot}): {battery}.";
    }

    private static string Percent(int? percent) => percent is { } p ? $" ({p}%)" : "";

    private static string Hex(int value) => "0x" + value.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Hides MAC addresses, with or without separators.</summary>
    private static string? Redact(string? name) => name is null ? null : MacAddress().Replace(name, HiddenAddress);

    [GeneratedRegex(@"\b[0-9A-Fa-f]{2}([:-])[0-9A-Fa-f]{2}(\1[0-9A-Fa-f]{2}){4}\b|\b[0-9A-Fa-f]{12}\b")]
    private static partial Regex MacAddress();
}
