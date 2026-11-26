using BatteryDisplay.App.Discovery;
using BatteryDisplay.App.Tests.Polling;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Discovery;

public class DeviceDiscoveryTests
{
    [Fact]
    public void Concatenates_InSourceOrder()
    {
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null), FakeProvider.Returning("b", null)),
            new FakeSource(FakeProvider.Returning("c", null)),
        ]);

        Assert.Equal(["a", "b", "c"], discovery.Discover().Select(p => p.Id));
    }

    [Fact]
    public void DuplicateId_FirstSourceWins()
    {
        var first = FakeProvider.Returning("same", null);
        var second = FakeProvider.Returning("same", null);
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(first),
            new FakeSource(second, FakeProvider.Returning("other", null)),
        ]);

        var found = discovery.Discover();

        Assert.Equal(["same", "other"], found.Select(p => p.Id));
        Assert.Same(first, found[0]);
    }

    [Fact]
    public void ThrowingSource_IsLoggedAndSkipped()
    {
        var logs = new List<string>();
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null)),
            new ThrowingSource(),
            new FakeSource(FakeProvider.Returning("b", null)),
        ], logs.Add);

        Assert.Equal(["a", "b"], discovery.Discover().Select(p => p.Id));
        Assert.Contains(logs, l => l.Contains(nameof(ThrowingSource)) && l.Contains("enumeration exploded"));
    }

    [Fact]
    public void FirstRound_LogsEachSource_WithItsCountAndDuration()
    {
        var logs = new List<string>();
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null), FakeProvider.Returning("b", null)),
            new FakeSource(),
        ], logs.Add);

        discovery.Discover();

        Assert.Contains(logs, l => l.StartsWith("FakeSource: 2 device(s) in ", StringComparison.Ordinal) && l.EndsWith(" ms", StringComparison.Ordinal));
        Assert.Contains(logs, l => l.StartsWith("FakeSource: 0 device(s) in ", StringComparison.Ordinal));
    }

    [Fact]
    public void LaterRounds_LogNothing_WhenEverySourceAnswers()
    {
        var logs = new List<string>();
        var discovery = new DeviceDiscovery([new FakeSource(FakeProvider.Returning("a", null))], logs.Add);
        discovery.Discover();
        logs.Clear();

        discovery.Discover();

        Assert.Empty(logs);
    }

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);

    // A Discover that never returns turns into a failed assertion instead of a hung test run.
    private static Task<IReadOnlyList<IBatteryProvider>> DiscoverWithin(DeviceDiscovery discovery) =>
        Task.Run(discovery.Discover).WaitAsync(TimeSpan.FromSeconds(5));

    [Fact]
    public async Task HangingSource_IsSkippedAfterItsTimeout_AndTheOthersStillShow()
    {
        var logs = new List<string>();
        using var stuck = new BlockingSource(FakeProvider.Returning("stuck", null));
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null)),
            stuck,
            new FakeSource(FakeProvider.Returning("b", null)),
        ], logs.Add, ShortTimeout);

        var found = await DiscoverWithin(discovery);

        Assert.Equal(["a", "b"], found.Select(p => p.Id));
        Assert.Contains(logs, l => l.Contains(nameof(BlockingSource)) && l.Contains("timed out"));
    }

    [Fact]
    public async Task SourceStillRunningFromAnEarlierRound_IsNotCalledAgain_UntilItFinishes()
    {
        using var slow = new BlockingSource(FakeProvider.Returning("slow", null));
        var discovery = new DeviceDiscovery([slow, new FakeSource(FakeProvider.Returning("a", null))], log: _ => { }, ShortTimeout);

        await DiscoverWithin(discovery);
        var second = await DiscoverWithin(discovery);
        Assert.Equal(["a"], second.Select(p => p.Id));
        Assert.Equal(1, slow.Calls);

        slow.Release();
        var third = await DiscoverWithin(discovery);

        Assert.Equal(["slow", "a"], third.Select(p => p.Id));
    }

    [Fact]
    public async Task SourceThatTimesOut_ContributesItsLastSuccessfulResult_InPlace()
    {
        var logs = new List<string>();
        using var flaky = new BlockingSource(FakeProvider.Returning("slow", null)) { FreeCalls = 1 };
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null)),
            flaky,
            new FakeSource(FakeProvider.Returning("b", null)),
        ], logs.Add, ShortTimeout);

        Assert.Equal(["a", "slow", "b"], (await DiscoverWithin(discovery)).Select(p => p.Id));
        var timedOut = await DiscoverWithin(discovery);

        Assert.Equal(["a", "slow", "b"], timedOut.Select(p => p.Id));
        Assert.Contains(logs, l => l.Contains(nameof(BlockingSource)) && l.Contains("timed out"));
    }

    [Fact]
    public async Task SourceThatThrowsAfterASuccess_ContributesNothing()
    {
        var source = new FakeSource(FakeProvider.Returning("x", null));
        var discovery = new DeviceDiscovery([source, new FakeSource(FakeProvider.Returning("a", null))], log: _ => { }, ShortTimeout);
        await DiscoverWithin(discovery);

        source.OnDiscover = () => throw new InvalidOperationException("enumeration exploded");

        Assert.Equal(["a"], (await DiscoverWithin(discovery)).Select(p => p.Id));
    }

    private static readonly Guid Mouse = new("c5cc23df-e5b5-11f1-beb0-f44efc8ff591");
    private static readonly Guid Pad = new("4f7958df-d9f1-56f3-8815-33261b4548ad");

    [Fact]
    public void SameDevice_SeenByTwoSources_ShowsOnce_AsTheMostDirect_InTheFirstOnesPlace()
    {
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Returning("a", null), FakeProvider.Device($"bt-{Mouse}", Mouse, DeviceChannel.Bluetooth)),
            new FakeSource(FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid), FakeProvider.Returning("b", null)),
        ]);

        var found = discovery.Discover();

        Assert.Equal(["a", "razer", "b"], found.Select(p => p.Id));
        var merged = Assert.IsType<MergedProvider>(found[1]);
        Assert.Equal([$"bt-{Mouse}"], merged.MergedIds);
    }

    [Theory]
    [InlineData(DeviceChannel.Bluetooth, DeviceChannel.XInput, DeviceChannel.VendorHid, "p3")]
    [InlineData(DeviceChannel.VendorHid, DeviceChannel.Bluetooth, DeviceChannel.XInput, "p1")]
    [InlineData(DeviceChannel.Bluetooth, DeviceChannel.XInput, DeviceChannel.Other, "p2")]
    [InlineData(DeviceChannel.Other, DeviceChannel.Bluetooth, DeviceChannel.Other, "p2")]
    public void Precedence_IsVendorHid_ThenXInput_ThenBluetooth_WhateverTheSourceOrder(
        DeviceChannel first, DeviceChannel second, DeviceChannel third, string expected)
    {
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Device("p1", Pad, first)),
            new FakeSource(FakeProvider.Device("p2", Pad, second)),
            new FakeSource(FakeProvider.Device("p3", Pad, third)),
        ]);

        Assert.Equal(expected, Assert.Single(discovery.Discover()).Id);
    }

    [Fact]
    public void ProvidersWithoutIdentity_AreNeverMerged()
    {
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Device("xinput-0", null, DeviceChannel.XInput)),
            new FakeSource(FakeProvider.Device($"bt-{Pad}", Pad, DeviceChannel.Bluetooth)),
            new FakeSource(FakeProvider.Device("razer", null, DeviceChannel.VendorHid)),
        ]);

        var found = discovery.Discover();

        Assert.Equal(["xinput-0", $"bt-{Pad}", "razer"], found.Select(p => p.Id));
        Assert.DoesNotContain(found, p => p is MergedProvider);
    }

    [Theory]
    [InlineData("00000000-0000-0000-ffff-ffffffffffff")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GenericContainer_IdentifiesNoDevice_AndIsNotMerged(string container)
    {
        var generic = Guid.Parse(container);
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Device("razer", generic, DeviceChannel.VendorHid)),
            new FakeSource(FakeProvider.Device("nova", generic, DeviceChannel.VendorHid), FakeProvider.Device("bt", generic, DeviceChannel.Bluetooth)),
        ]);

        Assert.Equal(["razer", "nova", "bt"], discovery.Discover().Select(p => p.Id));
    }

    [Fact]
    public void DifferentContainers_StayApart()
    {
        var discovery = new DeviceDiscovery(
        [
            new FakeSource(FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid)),
            new FakeSource(FakeProvider.Device($"bt-{Pad}", Pad, DeviceChannel.Bluetooth)),
        ]);

        Assert.Equal(["razer", $"bt-{Pad}"], discovery.Discover().Select(p => p.Id));
    }

    [Fact]
    public async Task MergedDevice_ReadsThroughTheMostDirectSource_SoAnEmptyBluetoothReadingNeverHidesIt()
    {
        var hid = FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid, new BatteryStatus(80, ChargeState.Discharging));
        var bluetooth = FakeProvider.Device($"bt-{Mouse}", Mouse, DeviceChannel.Bluetooth, new BatteryStatus(null, ChargeState.Unknown));
        var discovery = new DeviceDiscovery([new FakeSource(bluetooth), new FakeSource(hid)]);

        var merged = Assert.Single(discovery.Discover());

        Assert.Equal(new BatteryStatus(80, ChargeState.Discharging), await merged.ReadAsync(CancellationToken.None));
        Assert.Equal(0, bluetooth.Reads);
        Assert.Equal(DeviceChannel.VendorHid, merged.Channel);
        Assert.Equal(Mouse, merged.ContainerId);
    }

    [Fact]
    public async Task SourceThatTimesOut_StillMergesWithItsLastResult()
    {
        using var flaky = new BlockingSource(FakeProvider.Device("razer", Mouse, DeviceChannel.VendorHid)) { FreeCalls = 1 };
        var discovery = new DeviceDiscovery(
            [new FakeSource(FakeProvider.Device($"bt-{Mouse}", Mouse, DeviceChannel.Bluetooth)), flaky], log: _ => { }, ShortTimeout);

        Assert.Equal(["razer"], (await DiscoverWithin(discovery)).Select(p => p.Id));
        var timedOut = await DiscoverWithin(discovery);

        Assert.Equal(["razer"], timedOut.Select(p => p.Id));
    }

    [Fact]
    public void FixedSource_ReturnsItsProviders()
    {
        IBatteryProvider[] providers = [FakeProvider.Returning("a", null), FakeProvider.Returning("b", null)];

        Assert.Equal(providers, new FixedDeviceSource(providers).Discover());
    }
}
