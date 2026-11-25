using System.Globalization;
using System.Text.Json;
using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Catalog;

/// <summary>Loads the device catalog (Catalog/devices.json, embedded in this assembly).</summary>
public static class DeviceCatalog
{
    private const string ResourceName = "BatteryDisplay.Devices.Catalog.devices.json";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<IReadOnlyList<CatalogEntry>> Embedded = new(() =>
    {
        using var stream = typeof(DeviceCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    });

    public static IReadOnlyList<CatalogEntry> LoadEmbedded() => Embedded.Value;

    /// <summary>Parses a catalog document; throws FormatException on any malformed or duplicate entry.</summary>
    public static IReadOnlyList<CatalogEntry> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, JsonOptions);
            var entries = document.RootElement.GetProperty("devices").EnumerateArray().Select(ParseEntry).ToList();

            var duplicate = entries.GroupBy(e => e.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                throw new FormatException($"Duplicate catalog id '{duplicate.Key}'.");

            return entries;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or OverflowException)
        {
            throw new FormatException($"Invalid device catalog: {e.Message}", e);
        }
    }

    private static CatalogEntry ParseEntry(JsonElement element)
    {
        var id = RequireString(element, "id");
        var kindText = RequireString(element, "kind");
        if (!Enum.TryParse<DeviceKind>(kindText, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            throw new FormatException($"Catalog entry '{id}' has unknown kind '{kindText}'.");

        var productIds = element.GetProperty("productIds").EnumerateArray().Select(p => checked((int)ParseNumber(p))).ToList();
        if (productIds.Count == 0)
            throw new FormatException($"Catalog entry '{id}' has no product ids.");

        var options = element.TryGetProperty("options", out var optionsElement)
            ? optionsElement.EnumerateObject().ToDictionary(o => o.Name, o => ParseNumber(o.Value))
            : [];

        return new CatalogEntry(
            id, RequireString(element, "family"), RequireString(element, "name"), kind,
            checked((int)ParseNumber(element.GetProperty("vendorId"))), productIds, options);
    }

    private static string RequireString(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(value) ? throw new FormatException($"Catalog field '{name}' is empty.") : value;
    }

    /// <summary>Accepts a JSON number or a string holding a decimal or 0x-prefixed hex number.</summary>
    private static long ParseNumber(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.GetInt64();

        var text = element.GetString() ?? "";
        var parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            : long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        return parsed ? value : throw new FormatException($"'{text}' is not a number.");
    }
}
