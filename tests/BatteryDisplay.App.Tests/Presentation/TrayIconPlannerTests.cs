using System.Collections.Immutable;
using BatteryDisplay.App.Polling;
using BatteryDisplay.App.Presentation;
using BatteryDisplay.App.Settings;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

public class TrayIconPlannerTests
{
    private const int Threshold = DeviceSettings.DefaultLowBatteryThreshold;
    private static readonly ImmutableDictionary<string, DeviceSettings> NoSettings = AppSettings.Default.Devices;

    private static DeviceSnapshot Device(string id, int? percent, DeviceKind kind = DeviceKind.Mouse,
        ChargeState charge = ChargeState.Discharging, string? displayName = null) =>
        new(id, id, displayName ?? id, kind, "", new BatteryStatus(percent, charge), null);

    private static DeviceSnapshot Absent(string id, DeviceKind kind = DeviceKind.Mouse) => new(id, id, id, kind, "", null, null);

    private static ImmutableDictionary<string, DeviceSettings> Pinned(params string[] ids) =>
        ids.Aggregate(AppSettings.Default, (s, id) => s.WithDevice(id, DeviceSettings.Default with { Pinned = true })).Devices;

    private static ImmutableDictionary<string, DeviceSettings> Thresholds(params (string Id, int Threshold)[] devices) =>
        devices.Aggregate(AppSettings.Default, (s, d) => s.WithDevice(d.Id, DeviceSettings.Default with { LowBatteryThreshold = d.Threshold })).Devices;

    private static IReadOnlyList<TrayIconSlot> Plan(TrayIconMode mode, ImmutableDictionary<string, DeviceSettings>? settings, params DeviceSnapshot[] devices) =>
        TrayIconPlanner.Plan(mode, devices, settings ?? NoSettings);

    // PerDevice

    [Fact]
    public void PerDevice_NoDevices_NoIcons()
    {
        Assert.Empty(Plan(TrayIconMode.PerDevice, null));
    }

    [Fact]
    public void PerDevice_OneSlotPerDevice_KeyedById_InOrder_WithThePresenterModel()
    {
        var slots = Plan(TrayIconMode.PerDevice, null,
            Device("b", 78, DeviceKind.Headset, displayName: "Auriculares"),
            Device("a", 15));

        Assert.Equal(
            [
                new TrayIconSlot("b", TrayIconPresenter.Present("Auriculares", DeviceKind.Headset, new BatteryStatus(78, ChargeState.Discharging), Threshold)),
                new TrayIconSlot("a", TrayIconPresenter.Present("a", DeviceKind.Mouse, new BatteryStatus(15, ChargeState.Discharging), Threshold)),
            ],
            slots);
    }

    [Fact]
    public void PerDevice_DeviceWithoutReading_KeepsAHiddenSlot()
    {
        var slot = Assert.Single(Plan(TrayIconMode.PerDevice, null, Absent("a")));

        Assert.Equal(new TrayIconSlot("a", TrayIconModel.Hidden), slot);
    }

    [Fact]
    public void PerDevice_IgnoresPinning()
    {
        var slots = Plan(TrayIconMode.PerDevice, Pinned("a"), Device("a", 50), Device("b", 40));

        Assert.Equal(["a", "b"], slots.Select(s => s.Key));
    }

    [Fact]
    public void PerDevice_TooltipUsesTheDisplayName()
    {
        var slot = Assert.Single(Plan(TrayIconMode.PerDevice, null, Device("razer", 78, displayName: "Mi mouse")));

        Assert.Equal("Mi mouse: 78%", slot.Model.Tooltip);
    }

    // Lowest

    [Fact]
    public void Lowest_OneCombinedSlot_ShowingTheLowestKnownPercent_WithThatDevicesGlyph()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null,
            Device("mouse", 80),
            Device("headset", 12, DeviceKind.Headset),
            Device("pad", null, DeviceKind.Gamepad)));

        Assert.Equal(TrayIconPlanner.CombinedKey, slot.Key);
        Assert.True(slot.Model.Visible);
        Assert.Equal((DeviceKind.Headset, 1, LevelColor.Low, true), (slot.Model.Kind, slot.Model.LitSegments, slot.Model.Color, slot.Model.LevelKnown));
    }

    [Theory]
    [InlineData(30, LevelColor.Low)]
    [InlineData(20, LevelColor.Medium)]
    public void Combined_ColorsWithTheThresholdOfTheDeviceItShows(int shownThreshold, LevelColor expected)
    {
        // "b" has a 40 threshold, but "a" is the one shown, so only a's threshold counts.
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, Thresholds(("a", shownThreshold), ("b", 40)), Device("a", 25), Device("b", 35)));

        Assert.Equal(expected, slot.Model.Color);
    }

    [Theory]
    [InlineData(TrayIconMode.PerDevice)]
    [InlineData(TrayIconMode.LowestWithPinned)]
    public void OwnIcons_ColorWithTheirDevicesThreshold(TrayIconMode mode)
    {
        var settings = AppSettings.Default.Devices
            .SetItem("a", DeviceSettings.Default with { LowBatteryThreshold = 30, Pinned = true });

        var slot = Plan(mode, settings, Device("a", 25)).Single(s => s.Key == "a");

        Assert.Equal(LevelColor.Low, slot.Model.Color);
    }

    [Fact]
    public void Lowest_TooltipListsEveryDeviceWithAReading_OnePerLine_InDeviceOrder()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null,
            Device("razer", 80, displayName: "Mi mouse"),
            Device("nova", 18, DeviceKind.Headset, ChargeState.Charging, displayName: "Auriculares"),
            Absent("off"),
            Device("pad", null, DeviceKind.Gamepad, displayName: "Control 1")));

        Assert.Equal("Mi mouse 80%\nAuriculares 18% (cargando)\nControl 1 nivel desconocido", slot.Model.Tooltip);
    }

    [Fact]
    public void Lowest_ChargingLowestDevice_TakesTheChargingColor()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null, Device("a", 60), Device("b", 10, charge: ChargeState.Charging)));

        Assert.Equal(LevelColor.Charging, slot.Model.Color);
    }

    [Fact]
    public void Lowest_Tie_PicksTheFirstDeviceInOrder()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null, Device("a", 30, DeviceKind.Keyboard), Device("b", 30, DeviceKind.Headset)));

        Assert.Equal(DeviceKind.Keyboard, slot.Model.Kind);
    }

    [Fact]
    public void Lowest_IncludesPinnedDevices_AndGivesThemNoIconOfTheirOwn()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, Pinned("a"), Device("a", 10), Device("b", 90, DeviceKind.Headset)));

        Assert.Equal(TrayIconPlanner.CombinedKey, slot.Key);
        Assert.Equal(DeviceKind.Mouse, slot.Model.Kind);
    }

    [Fact]
    public void Lowest_AllUnknown_ShowsTheUnknownState_LikeThePresenter()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null, Device("pad", null, DeviceKind.Gamepad), Device("b", null)));

        var expected = TrayIconPresenter.Present("pad", DeviceKind.Gamepad, new BatteryStatus(null, ChargeState.Discharging), Threshold)
            with { Tooltip = "pad nivel desconocido\nb nivel desconocido" };
        Assert.Equal(expected, slot.Model);
        Assert.True(slot.Model.Visible);
        Assert.False(slot.Model.LevelKnown);
    }

    [Fact]
    public void Lowest_NoDevices_ShowsTheUnknownState()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null));

        Assert.Equal(TrayIconPlanner.CombinedKey, slot.Key);
        Assert.Equal(
            new TrayIconModel(true, TrayIconPlanner.NoDeviceKind, 0, LevelColor.Unknown, false, TrayIconPlanner.NoDevicesTooltip),
            slot.Model);
    }

    [Fact]
    public void Lowest_OnlyDevicesWithoutReading_ShowsTheUnknownState_WithTheNoDevicesTooltip()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, null, Absent("a")));

        Assert.Equal((true, false, TrayIconPlanner.NoDevicesTooltip), (slot.Model.Visible, slot.Model.LevelKnown, slot.Model.Tooltip));
    }

    // LowestWithPinned

    [Fact]
    public void LowestWithPinned_CombinedCoversNonPinned_PinnedKeepTheirOwnIcon()
    {
        var slots = Plan(TrayIconMode.LowestWithPinned, Pinned("pad"),
            Device("mouse", 70),
            Device("pad", 5, DeviceKind.Gamepad, displayName: "Control"),
            Device("headset", 40, DeviceKind.Headset));

        Assert.Equal([TrayIconPlanner.CombinedKey, "pad"], slots.Select(s => s.Key));
        var combined = slots[0].Model;
        Assert.Equal((DeviceKind.Headset, "mouse 70%\nheadset 40%"), (combined.Kind, combined.Tooltip));
        Assert.Equal(TrayIconPresenter.Present("Control", DeviceKind.Gamepad, new BatteryStatus(5, ChargeState.Discharging), Threshold), slots[1].Model);
    }

    [Fact]
    public void LowestWithPinned_PinnedDeviceDisconnected_HasNoSlot()
    {
        var slots = Plan(TrayIconMode.LowestWithPinned, Pinned("pad"), Device("mouse", 70));

        Assert.Equal([TrayIconPlanner.CombinedKey], slots.Select(s => s.Key));
    }

    [Fact]
    public void LowestWithPinned_PinnedDeviceWithoutReading_KeepsAHiddenSlot()
    {
        var slots = Plan(TrayIconMode.LowestWithPinned, Pinned("pad"), Device("mouse", 70), Absent("pad", DeviceKind.Gamepad));

        Assert.Equal(new TrayIconSlot("pad", TrayIconModel.Hidden), slots[1]);
    }

    [Fact]
    public void LowestWithPinned_EveryDevicePinned_CombinedShowsTheUnknownState()
    {
        var slots = Plan(TrayIconMode.LowestWithPinned, Pinned("a", "b"), Device("a", 70), Device("b", 30));

        Assert.Equal([TrayIconPlanner.CombinedKey, "a", "b"], slots.Select(s => s.Key));
        Assert.Equal((false, TrayIconPlanner.NoDevicesTooltip), (slots[0].Model.LevelKnown, slots[0].Model.Tooltip));
    }

    // Hidden devices

    private static ImmutableDictionary<string, DeviceSettings> Hidden(string id, bool pinned = false) =>
        AppSettings.Default.WithDevice(id, DeviceSettings.Default with { Hidden = true, Pinned = pinned }).Devices;

    [Fact]
    public void PerDevice_HiddenDevice_GetsNoSlotAtAll()
    {
        var slots = Plan(TrayIconMode.PerDevice, Hidden("xion"), Device("mouse", 70), Device("xion", 10), Device("pad", 40));

        Assert.Equal(["mouse", "pad"], slots.Select(s => s.Key));
    }

    [Fact]
    public void PerDevice_HiddenDeviceWithoutReading_GetsNoSlotEither()
    {
        Assert.Empty(Plan(TrayIconMode.PerDevice, Hidden("xion"), Absent("xion")));
    }

    [Fact]
    public void Lowest_IgnoresAHiddenDevice_ForTheIconAndTheTooltip()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, Hidden("xion"),
            Device("mouse", 70),
            Device("xion", 10, DeviceKind.Headset, displayName: "XION")));

        Assert.Equal((DeviceKind.Mouse, LevelColor.High, "mouse 70%"), (slot.Model.Kind, slot.Model.Color, slot.Model.Tooltip));
    }

    [Fact]
    public void Lowest_OnlyHiddenDevices_ShowsTheNoDevicesState()
    {
        var slot = Assert.Single(Plan(TrayIconMode.Lowest, Hidden("xion"), Device("xion", 10)));

        Assert.Equal((TrayIconPlanner.CombinedKey, false, TrayIconPlanner.NoDevicesTooltip), (slot.Key, slot.Model.LevelKnown, slot.Model.Tooltip));
    }

    [Fact]
    public void LowestWithPinned_HiddenPinnedDevice_GetsNoOwnIcon_AndStaysOutOfTheCombined()
    {
        var slots = Plan(TrayIconMode.LowestWithPinned, Hidden("xion", pinned: true), Device("mouse", 70), Device("xion", 10));

        var combined = Assert.Single(slots);
        Assert.Equal((TrayIconPlanner.CombinedKey, "mouse 70%"), (combined.Key, combined.Model.Tooltip));
    }

    [Fact]
    public void LowestWithPinned_HiddenUnpinnedDevice_StaysOutOfTheCombined()
    {
        var slots = Plan(TrayIconMode.LowestWithPinned, Hidden("xion"), Device("mouse", 70), Device("xion", 10));

        Assert.Equal("mouse 70%", Assert.Single(slots).Model.Tooltip);
    }

    // Tooltip limit

    [Fact]
    public void CombinedTooltip_TooManyDevices_StaysUnderTheLimit_EndingWithAnEllipsisLine()
    {
        var devices = Enumerable.Range(0, 20).Select(i => Device($"device{i}", 50, displayName: $"Dispositivo numero {i}")).ToArray();

        var tooltip = Assert.Single(Plan(TrayIconMode.Lowest, null, devices)).Model.Tooltip;

        Assert.InRange(tooltip.Length, 1, TrayTooltip.MaxLength);
        Assert.StartsWith("Dispositivo numero 0 50%\nDispositivo numero 1 50%\n", tooltip);
        Assert.EndsWith("\n...", tooltip);
        Assert.All(tooltip.Split('\n')[..^1], line => Assert.Matches(@"^Dispositivo numero \d+ 50%$", line));
    }

    [Fact]
    public void CombinedTooltip_SingleHugeName_IsTrimmed_KeepingTheLevel()
    {
        var tooltip = Assert.Single(Plan(TrayIconMode.Lowest, null, Device("a", 42, displayName: new string('X', 300)))).Model.Tooltip;

        Assert.Equal(TrayTooltip.MaxLength, tooltip.Length);
        Assert.EndsWith("... 42%", tooltip);
    }

    [Fact]
    public void CombinedTooltip_HugeFirstName_WithMoreDevices_StillFitsAndMarksTheRest()
    {
        var tooltip = Assert.Single(Plan(TrayIconMode.Lowest, null,
            Device("a", 42, displayName: new string('X', 300)),
            Device("b", 60))).Model.Tooltip;

        Assert.InRange(tooltip.Length, 1, TrayTooltip.MaxLength);
        Assert.EndsWith("... 42%\n...", tooltip);
    }

    [Fact]
    public void CombinedTooltip_NeverSplitsASurrogatePair()
    {
        // Each emoji is two UTF-16 chars; try every alignment of the cut point.
        for (var pad = 0; pad < 4; pad++)
        {
            var name = new string('a', pad) + string.Concat(Enumerable.Repeat("\U0001F3A7", 80));
            var tooltip = Assert.Single(Plan(TrayIconMode.Lowest, null, Device("a", 42, displayName: name))).Model.Tooltip;

            Assert.InRange(tooltip.Length, 1, TrayTooltip.MaxLength);
            for (var i = 0; i < tooltip.Length; i++)
            {
                if (char.IsHighSurrogate(tooltip[i]))
                    Assert.True(i + 1 < tooltip.Length && char.IsLowSurrogate(tooltip[i + 1]), $"lone high surrogate at {i} (pad {pad})");
                else if (char.IsLowSurrogate(tooltip[i]))
                    Assert.True(i > 0 && char.IsHighSurrogate(tooltip[i - 1]), $"lone low surrogate at {i} (pad {pad})");
            }
        }
    }
}
