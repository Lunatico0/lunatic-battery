using BatteryDisplay.Core;
using BatteryDisplay.Devices.Catalog;

namespace BatteryDisplay.Devices.Tests.Catalog;

public class DeviceCatalogTests
{
    private const string TwoDevices = """
        {
          // comments are allowed to cite where each id comes from
          "devices": [
            {
              "id": "mouse-a",
              "family": "fam-x",
              "name": "Mouse A",
              "kind": "Mouse",
              "vendorId": "0x1532",
              "productIds": ["0x00DA", "0x00DB"],
              "options": { "transactionId": "0x1F", "interface": 3 }
            },
            {
              "id": "pad-b",
              "family": "fam-y",
              "name": "Pad B",
              "kind": "gamepad",
              "vendorId": 4152,
              "productIds": [8754]
            }
          ]
        }
        """;

    [Fact]
    public void Parses_entries_with_hex_and_decimal_ids()
    {
        var entries = DeviceCatalog.Parse(TwoDevices);

        Assert.Equal(2, entries.Count);
        var mouse = entries[0];
        Assert.Equal("mouse-a", mouse.Id);
        Assert.Equal("fam-x", mouse.Family);
        Assert.Equal("Mouse A", mouse.Name);
        Assert.Equal(DeviceKind.Mouse, mouse.Kind);
        Assert.Equal(0x1532, mouse.VendorId);
        Assert.Equal(new[] { 0x00DA, 0x00DB }, mouse.ProductIds);

        var pad = entries[1];
        Assert.Equal(DeviceKind.Gamepad, pad.Kind);
        Assert.Equal(0x1038, pad.VendorId);
        Assert.Equal(new[] { 0x2232 }, pad.ProductIds);
        Assert.Empty(pad.Options);
    }

    [Fact]
    public void Keeps_family_options_as_numbers()
    {
        var mouse = DeviceCatalog.Parse(TwoDevices)[0];

        Assert.Equal(0x1F, mouse.RequireOption("transactionId"));
        Assert.Equal(3, mouse.RequireOption("interface"));
    }

    [Fact]
    public void Missing_option_throws_format_exception()
    {
        var mouse = DeviceCatalog.Parse(TwoDevices)[0];

        Assert.Throws<FormatException>(() => mouse.RequireOption("usage"));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("family")]
    [InlineData("name")]
    [InlineData("kind")]
    [InlineData("vendorId")]
    [InlineData("productIds")]
    public void Entry_without_a_required_field_is_rejected(string field)
    {
        var json = Without(field);

        Assert.Throws<FormatException>(() => DeviceCatalog.Parse(json));
    }

    [Fact]
    public void Unknown_kind_is_rejected()
    {
        var json = TwoDevices.Replace("\"Mouse\"", "\"Toaster\"");

        Assert.Throws<FormatException>(() => DeviceCatalog.Parse(json));
    }

    [Fact]
    public void Malformed_hex_is_rejected()
    {
        var json = TwoDevices.Replace("\"0x1532\"", "\"0xZZ\"");

        Assert.Throws<FormatException>(() => DeviceCatalog.Parse(json));
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var json = TwoDevices.Replace("\"pad-b\"", "\"mouse-a\"");

        Assert.Throws<FormatException>(() => DeviceCatalog.Parse(json));
    }

    [Fact]
    public void Invalid_json_is_rejected()
    {
        Assert.Throws<FormatException>(() => DeviceCatalog.Parse("{ \"devices\": [ "));
    }

    [Fact]
    public void Embedded_catalog_has_only_the_verified_models()
    {
        var entries = DeviceCatalog.LoadEmbedded();

        Assert.Equal(
            new[] { "razer-cobra-hyperspeed", "steelseries-arctis-nova-5", "rapoo-gaming-mouse", "redragon-k673" },
            entries.Select(e => e.Id));
    }

    [Fact]
    public void Embedded_cobra_uses_razer_hid_with_its_wired_and_dongle_ids()
    {
        var cobra = DeviceCatalog.LoadEmbedded().Single(e => e.Id == "razer-cobra-hyperspeed");

        Assert.Equal("razer-hid", cobra.Family);
        Assert.Equal("Razer Cobra HyperSpeed", cobra.Name);
        Assert.Equal(DeviceKind.Mouse, cobra.Kind);
        Assert.Equal(0x1532, cobra.VendorId);
        Assert.Equal(new[] { 0x00DA, 0x00DB }, cobra.ProductIds.Order());
        Assert.Equal(0x1F, cobra.RequireOption("transactionId"));
    }

    [Fact]
    public void Embedded_nova5_uses_its_base_station_ids_and_status_interface()
    {
        var nova = DeviceCatalog.LoadEmbedded().Single(e => e.Id == "steelseries-arctis-nova-5");

        Assert.Equal("steelseries-arctis-nova-5", nova.Family);
        Assert.Equal("Arctis Nova 5", nova.Name);
        Assert.Equal(DeviceKind.Headset, nova.Kind);
        Assert.Equal(0x1038, nova.VendorId);
        Assert.Equal(new[] { 0x2232, 0x2253 }, nova.ProductIds);
        Assert.Equal(3, nova.RequireOption("interface"));
        Assert.Equal(0xFFC00001L, nova.RequireOption("usage"));
    }

    [Fact]
    public void Embedded_rapoo_mouse_uses_its_dongle_id_and_battery_collection()
    {
        var rapoo = DeviceCatalog.LoadEmbedded().Single(e => e.Id == "rapoo-gaming-mouse");

        Assert.Equal("rapoo-hid", rapoo.Family);
        Assert.Equal("Rapoo Gaming Mouse", rapoo.Name);
        Assert.Equal(DeviceKind.Mouse, rapoo.Kind);
        Assert.Equal(0x24AE, rapoo.VendorId);
        Assert.Equal(new[] { 0x1211 }, rapoo.ProductIds);
        Assert.Equal(1, rapoo.RequireOption("interface"));
        Assert.Equal(0xFF000002L, rapoo.RequireOption("usage"));
    }

    [Fact]
    public void Embedded_catalog_matches_the_rapoo_dongle()
    {
        var present = new[] { new HidId(0x24AE, 0x1211) };

        var match = Assert.Single(CatalogMatcher.Match(DeviceCatalog.LoadEmbedded(), present));
        Assert.Equal("rapoo-gaming-mouse", match.Id);
    }

    [Fact]
    public void Embedded_redragon_k673_uses_its_receiver_id_and_battery_collection()
    {
        var k673 = DeviceCatalog.LoadEmbedded().Single(e => e.Id == "redragon-k673");

        Assert.Equal("redragon-hid", k673.Family);
        Assert.Equal("Redragon K673", k673.Name);
        Assert.Equal(DeviceKind.Keyboard, k673.Kind);
        Assert.Equal(0x3554, k673.VendorId);
        Assert.Equal(new[] { 0xFA09 }, k673.ProductIds);
        Assert.Equal(1, k673.RequireOption("interface"));
        Assert.Equal(0xFF020002L, k673.RequireOption("usage"));
    }

    [Fact]
    public void Embedded_catalog_matches_the_compx_receiver_of_the_k673()
    {
        var present = new[] { new HidId(0x3554, 0xFA09) };

        var match = Assert.Single(CatalogMatcher.Match(DeviceCatalog.LoadEmbedded(), present));
        Assert.Equal("redragon-k673", match.Id);
    }

    private static string Without(string field)
    {
        var lines = TwoDevices.Split('\n');
        var start = Array.FindIndex(lines, l => l.Contains("\"id\": \"mouse-a\""));
        var index = Array.FindIndex(lines, start, l => l.TrimStart().StartsWith($"\"{field}\""));
        return string.Join('\n', lines.Where((_, i) => i != index));
    }
}
