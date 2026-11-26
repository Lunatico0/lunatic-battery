using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Diagnostics;
using BatteryDisplay.Devices.Tests.Bluetooth;
using BatteryDisplay.Devices.Tests.XInput;
using BatteryDisplay.Devices.XInput;
using static BatteryDisplay.Devices.Tests.Catalog.CatalogMatcherTests;

namespace BatteryDisplay.Devices.Tests.Diagnostics;

public class DeviceDiagnosticsTests
{
    private sealed class FakeHidInventory(params HidInterfaceInfo[] interfaces) : IHidInventory
    {
        public Exception? Failure { get; init; }

        public IReadOnlyList<HidInterfaceInfo> GetInterfaces() => Failure is null ? interfaces : throw Failure;
    }

    private static readonly CatalogEntry Mouse = Entry("mouse", 0x1532, 0x00DA, 0x00DB);

    private readonly FakeBluetoothApi _bluetooth = new();
    private FakeHidInventory _hid = new();
    private FakeXInputApi _xinput = new([]);

    private DiagnosticSnapshot Collect() => new DeviceDiagnostics(_hid, _bluetooth, _xinput, [Mouse]).Collect();

    private static HidInterfaceInfo Hid(int vid, int pid, int page = 1, int usage = 2, string? maker = "Maker", string? product = "Thing") =>
        new(vid, pid, new HidUsage(page, usage), maker, product);

    [Fact]
    public void Collections_of_the_same_vid_and_pid_fold_into_one_device_with_their_count()
    {
        _hid = new(Hid(0x1532, 0x00DB, 1, 2), Hid(0x1532, 0x00DB, 1, 6), Hid(0x1532, 0x00DB, 1, 2), Hid(0x046D, 0xC52B));

        var devices = Collect().Hid.Items;

        Assert.Equal(2, devices.Count);
        var razer = devices.Single(d => d.ProductId == 0x00DB);
        Assert.Equal(3, razer.InterfaceCount);
    }

    [Fact]
    public void A_folded_device_lists_each_distinct_usage_once()
    {
        _hid = new(Hid(0x1532, 0x00DB, 1, 2), Hid(0x1532, 0x00DB, 1, 6), Hid(0x1532, 0x00DB, 1, 2),
            new HidInterfaceInfo(0x1532, 0x00DB, null, null, null));

        var razer = Collect().Hid.Items.Single();

        Assert.Equal([new HidUsage(1, 2), new HidUsage(1, 6)], razer.Usages);
    }

    [Fact]
    public void A_folded_device_takes_the_first_manufacturer_and_product_any_collection_gives()
    {
        _hid = new(Hid(0x1532, 0x00DB, maker: null, product: null), Hid(0x1532, 0x00DB, maker: "Razer", product: "Cobra"));

        var razer = Collect().Hid.Items.Single();

        Assert.Equal(("Razer", "Cobra"), (razer.Manufacturer, razer.Product));
    }

    [Fact]
    public void Devices_are_ordered_by_vendor_then_product()
    {
        _hid = new(Hid(0x1532, 0x00DB), Hid(0x046D, 0xC52B), Hid(0x1532, 0x00DA));

        var ids = Collect().Hid.Items.Select(d => (d.VendorId, d.ProductId));

        Assert.Equal([(0x046D, 0xC52B), (0x1532, 0x00DA), (0x1532, 0x00DB)], ids);
    }

    [Fact]
    public void A_device_in_the_catalog_carries_its_entry_and_an_unknown_one_none()
    {
        _hid = new(Hid(0x1532, 0x00DB), Hid(0x046D, 0xC52B));

        var devices = Collect().Hid.Items;

        Assert.Same(Mouse, devices.Single(d => d.VendorId == 0x1532).CatalogEntry);
        Assert.Null(devices.Single(d => d.VendorId == 0x046D).CatalogEntry);
    }

    [Fact]
    public void Bluetooth_lists_each_paired_device_with_its_connection_and_raw_battery()
    {
        _bluetooth.AddClassic("Buds", 80, connected: true);
        _bluetooth.AddLowEnergy("Keys", null, connected: false);

        var devices = Collect().Bluetooth.Items;

        Assert.Equal([new("Buds", true, 80), new("Keys", false, null)], devices);
    }

    [Fact]
    public void Bluetooth_keeps_a_battery_value_outside_the_valid_range()
    {
        _bluetooth.AddLowEnergy("Odd", 255, connected: true);

        Assert.Equal(255, Collect().Bluetooth.Items.Single().RawBattery);
    }

    [Fact]
    public void Bluetooth_device_without_endpoint_has_unknown_connection()
    {
        var container = Guid.NewGuid();
        _bluetooth.Nodes.Add(new(@"BTHENUM\Dev_A1B2C3D4E5F6\x", container, "Lost", null, null));

        Assert.Null(Collect().Bluetooth.Items.Single().IsConnected);
    }

    [Fact]
    public void Bluetooth_devices_are_ordered_by_name()
    {
        _bluetooth.AddClassic("Zeta", 10, connected: true);
        _bluetooth.AddClassic("alfa", 20, connected: true);

        Assert.Equal(["alfa", "Zeta"], Collect().Bluetooth.Items.Select(d => d.Name));
    }

    [Fact]
    public void XInput_lists_only_connected_slots_with_their_battery()
    {
        var pad = new XInputBatteryInformation(XInputBatteryType.NiMH, XInputBatteryLevel.Medium);
        _xinput = new(new() { [0] = pad, [2] = null });

        var slots = Collect().XInput.Items;

        Assert.Equal([new(0, pad), new(2, null)], slots);
    }

    [Fact]
    public void A_failing_enumeration_reports_its_error_type_and_keeps_the_others()
    {
        _hid = new() { Failure = new IOException(@"\\?\hid#vid_1532&pid_00db secret path") };
        _bluetooth.AddClassic("Buds", 80, connected: true);

        var snapshot = Collect();

        Assert.Equal("IOException", snapshot.Hid.Error);
        Assert.Empty(snapshot.Hid.Items);
        Assert.Single(snapshot.Bluetooth.Items);
        Assert.Null(snapshot.Bluetooth.Error);
    }

    [Fact]
    public void Bluetooth_and_XInput_failures_are_reported_too()
    {
        _bluetooth.NodesFailure = new UnauthorizedAccessException();
        _xinput = new([]) { Failure = new DllNotFoundException() };

        var snapshot = Collect();

        Assert.Equal(("UnauthorizedAccessException", "DllNotFoundException"), (snapshot.Bluetooth.Error, snapshot.XInput.Error));
    }
}
