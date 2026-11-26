using BatteryDisplay.App.Ui.Diagnostics;
using BatteryDisplay.Core;
using BatteryDisplay.Devices.Bluetooth;
using BatteryDisplay.Devices.Catalog;
using BatteryDisplay.Devices.Diagnostics;
using BatteryDisplay.Devices.XInput;

namespace BatteryDisplay.App.Tests.Ui.Diagnostics;

public class DiagnosticReportTests
{
    private static readonly CatalogEntry Cobra = new("razer-cobra-hyperspeed", "razer-hid", "Razer Cobra HyperSpeed",
        DeviceKind.Mouse, 0x1532, [0x00DA, 0x00DB], new Dictionary<string, long>());

    private static DiagnosticSnapshot Snapshot(
        HidDeviceDiagnostic[]? hid = null, BluetoothDeviceDiagnostic[]? bluetooth = null, XInputSlotDiagnostic[]? xinput = null) =>
        new(new(hid ?? []), new(bluetooth ?? []), new(xinput ?? []));

    private static string Build(DiagnosticSnapshot snapshot) =>
        DiagnosticReport.Build("1.2.3", "Microsoft Windows 10.0.26300", snapshot);

    private static string[] Lines(string report) => report.ReplaceLineEndings("\n").Split('\n');

    [Fact]
    public void Header_NamesTheApp_ItsVersion_AndWindows()
    {
        var lines = Lines(Build(Snapshot()));

        Assert.Equal("## Diagnóstico de Lunatic Battery", lines[0]);
        Assert.Contains("- Versión de la app: 1.2.3", lines);
        Assert.Contains("- Windows: Microsoft Windows 10.0.26300", lines);
    }

    [Fact]
    public void EverySection_HasItsHeading()
    {
        var lines = Lines(Build(Snapshot()));

        Assert.Contains("### Dispositivos HID", lines);
        Assert.Contains("### Bluetooth", lines);
        Assert.Contains("### Controles XInput", lines);
    }

    [Fact]
    public void EmptySection_SaysNone()
    {
        var lines = Lines(Build(Snapshot()));

        Assert.Equal(3, lines.Count(l => l == "Ninguno."));
    }

    [Fact]
    public void FailedSection_ShowsTheErrorType()
    {
        var snapshot = Snapshot() with { Bluetooth = new([], "UnauthorizedAccessException") };

        Assert.Contains("No se pudo leer (UnauthorizedAccessException).", Lines(Build(snapshot)));
    }

    [Fact]
    public void HidDevice_InTheCatalog_ShowsIdsInHex_Interfaces_Usages_AndTheEntry()
    {
        var device = new HidDeviceDiagnostic(0x1532, 0x00DB, 3, [new(0x01, 0x02), new(0xFF00, 0x01)], "Razer", "Cobra Dongle", Cobra);

        Assert.Contains(
            "- VID 0x1532, PID 0x00DB: Razer, Cobra Dongle. 3 interfaces, usages (page:usage) 0x0001:0x0002, 0xFF00:0x0001. "
            + "En el catálogo: Razer Cobra HyperSpeed (razer-cobra-hyperspeed).",
            Lines(Build(Snapshot(hid: [device]))));
    }

    [Fact]
    public void HidDevice_NotInTheCatalog_SaysSo_AndUsesTheSingularForOneInterface()
    {
        var device = new HidDeviceDiagnostic(0x046D, 0xC52B, 1, [new(0x01, 0x06)], "Logitech", "USB Receiver", null);

        Assert.Contains(
            "- VID 0x046D, PID 0xC52B: Logitech, USB Receiver. 1 interfaz, usages (page:usage) 0x0001:0x0006. No está en el catálogo.",
            Lines(Build(Snapshot(hid: [device]))));
    }

    [Fact]
    public void HidDevice_WithoutNamesOrUsages_SaysTheyAreUnknown()
    {
        var device = new HidDeviceDiagnostic(0x1234, 0xABCD, 2, [], null, null, null);

        Assert.Contains(
            "- VID 0x1234, PID 0xABCD: fabricante desconocido, producto desconocido. 2 interfaces, usage desconocido. No está en el catálogo.",
            Lines(Build(Snapshot(hid: [device]))));
    }

    [Fact]
    public void Bluetooth_ShowsConnection_AndBattery()
    {
        var lines = Lines(Build(Snapshot(bluetooth:
        [
            new("Buds", true, 80),
            new("Keys", false, null),
            new("Lost", null, 40),
            new("Odd", true, 255),
        ])));

        Assert.Contains("- Buds: conectado, batería 80%.", lines);
        Assert.Contains("- Keys: desconectado, no expone batería.", lines);
        Assert.Contains("- Lost: conexión desconocida, batería 40%.", lines);
        Assert.Contains("- Odd: conectado, batería con valor inválido (255).", lines);
    }

    [Fact]
    public void XInput_ShowsEachSlotBattery()
    {
        var lines = Lines(Build(Snapshot(xinput:
        [
            new(0, new(XInputBatteryType.NiMH, XInputBatteryLevel.Medium)),
            new(1, new(XInputBatteryType.Wired, XInputBatteryLevel.Full)),
            new(2, null),
            new(3, new(XInputBatteryType.Unknown, XInputBatteryLevel.Empty)),
        ])));

        Assert.Contains("- Control 1 (slot 0): batería NiMH, nivel Medium (50%).", lines);
        Assert.Contains("- Control 2 (slot 1): cableado, sin nivel de batería.", lines);
        Assert.Contains("- Control 3 (slot 2): no informa batería.", lines);
        Assert.Contains("- Control 4 (slot 3): tipo de batería Unknown.", lines);
    }

    [Theory]
    [InlineData("LE-AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("Device A1B2C3D4E5F6")]
    public void MacAddressInAName_IsHidden(string name)
    {
        var report = Build(Snapshot(
            hid: [new(0x1234, 0x0001, 1, [], name, name, null)],
            bluetooth: [new(name, true, 50)]));

        Assert.DoesNotContain(name, report);
        Assert.Contains("[dirección oculta]", report);
    }

    private sealed class FakeHid(params HidInterfaceInfo[] interfaces) : IHidInventory
    {
        public IReadOnlyList<HidInterfaceInfo> GetInterfaces() => interfaces;
    }

    private sealed class FakeBluetooth(BluetoothNode node, string endpointId) : IBluetoothDeviceApi
    {
        public IReadOnlyList<BluetoothNode> GetPresentNodes() => [node];
        public BluetoothEndpoint? GetEndpoint(string id) => id == endpointId ? new(true, 0x240404, null) : null;
    }

    private sealed class FakeXInput : IXInputApi
    {
        public bool IsConnected(uint userIndex) => userIndex == 0;
        public XInputBatteryInformation? GetGamepadBattery(uint userIndex) => new(XInputBatteryType.Alkaline, XInputBatteryLevel.Full);
    }

    [Fact]
    public void Report_NeverCarriesInstancePaths_EndpointIds_ContainerIds_OrAddresses()
    {
        var container = Guid.Parse("7c1d9f3e-2b4a-4c55-9e1f-0a2b3c4d5e6f");
        const string address = "A1B2C3D4E5F6";
        const string instance = $@"BTHENUM\Dev_{address}\7&1&0&BluetoothDevice_{address}";
        const string endpoint = $"Bluetooth#Bluetooth-{address}";
        var diagnostics = new DeviceDiagnostics(
            new FakeHid(new HidInterfaceInfo(0x1532, 0x00DB, new(1, 2), "Razer", "Cobra")),
            new FakeBluetooth(new BluetoothNode(instance, container, "Buds", 70, endpoint), endpoint),
            new FakeXInput(),
            [Cobra]);

        var report = Build(diagnostics.Collect());

        Assert.Contains("- Buds: conectado, batería 70%.", Lines(report));
        Assert.DoesNotContain(address, report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BTHENUM", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(endpoint, report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(container.ToString(), report, StringComparison.OrdinalIgnoreCase);
    }
}
