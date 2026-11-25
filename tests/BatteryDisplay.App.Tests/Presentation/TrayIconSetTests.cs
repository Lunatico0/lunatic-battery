using BatteryDisplay.App.Presentation;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Tests.Presentation;

internal sealed class FakeIcon(string key) : ITrayIcon
{
    public string Key => key;
    public List<TrayIconModel> Applied { get; } = [];
    public TrayIconModel? Last => Applied.LastOrDefault();
    public bool Disposed { get; private set; }
    public void Apply(TrayIconModel model) => Applied.Add(model);
    public void Dispose() => Disposed = true;
}

public class TrayIconSetTests
{
    private static readonly TrayIconModel Shown = new(true, DeviceKind.Mouse, 4, LevelColor.Medium, true, "a: 50%");
    private static readonly TrayIconModel ShownToo = new(true, DeviceKind.Headset, 8, LevelColor.High, true, "b: 100%");

    private readonly List<FakeIcon> _created = [];
    private readonly TrayIconSet _set;

    public TrayIconSetTests()
    {
        _set = new TrayIconSet(key =>
        {
            var icon = new FakeIcon(key);
            _created.Add(icon);
            return icon;
        });
    }

    private FakeIcon IconOf(string key) => _created.Last(i => i.Key == key);

    private static TrayIconSlot[] Slots(params (string Key, TrayIconModel Model)[] slots) =>
        slots.Select(s => new TrayIconSlot(s.Key, s.Model)).ToArray();

    [Fact]
    public void NewKeys_GetAnIcon_InSlotOrder_WithTheirModel()
    {
        _set.Apply(Slots(("b", ShownToo), ("a", Shown)));

        Assert.Equal(["b", "a"], _created.Select(i => i.Key));
        Assert.Equal(ShownToo, IconOf("b").Last);
        Assert.Equal(Shown, IconOf("a").Last);
        Assert.Equal(["b", "a"], _set.Keys);
    }

    [Fact]
    public void ExistingKeys_KeepTheirIcon_AndTakeTheNewModel()
    {
        _set.Apply(Slots(("a", Shown)));
        _set.Apply(Slots(("a", ShownToo)));

        Assert.Single(_created);
        Assert.Equal([Shown, ShownToo], IconOf("a").Applied);
    }

    [Fact]
    public void VanishedKeys_AreDisposed_AndNoLongerUpdated()
    {
        _set.Apply(Slots(("a", Shown), ("b", ShownToo)));
        var gone = IconOf("b");

        _set.Apply(Slots(("a", Shown)));
        _set.Apply(Slots(("a", Shown)));

        Assert.True(gone.Disposed);
        Assert.Single(gone.Applied);
        Assert.False(IconOf("a").Disposed);
        Assert.Equal(["a"], _set.Keys);
    }

    [Fact]
    public void Apply_ReportsAnIconShownForTheFirstTime_OnlyOnce()
    {
        Assert.False(_set.Apply(Slots(("a", TrayIconModel.Hidden))));
        Assert.True(_set.Apply(Slots(("a", Shown))));
        Assert.False(_set.Apply(Slots(("a", ShownToo))));
        Assert.False(_set.Apply(Slots(("a", TrayIconModel.Hidden))));
        Assert.False(_set.Apply(Slots(("a", Shown))));
    }

    [Fact]
    public void ReturningKey_GetsAFreshIcon_AndIsReportedAsShownAgain()
    {
        Assert.True(_set.Apply(Slots(("a", Shown))));
        var original = IconOf("a");
        Assert.False(_set.Apply([]));

        Assert.True(_set.Apply(Slots(("a", Shown))));

        Assert.True(original.Disposed);
        Assert.NotSame(original, IconOf("a"));
    }

    [Fact]
    public void ExistingIcons_KeepTheirPlace_NewOnesGoAtTheEnd()
    {
        _set.Apply(Slots(("a", Shown), ("b", Shown)));

        _set.Apply(Slots(("c", Shown), ("b", Shown), ("a", Shown)));

        Assert.Equal(["a", "b", "c"], _set.Keys);
    }

    [Fact]
    public void RepeatedKey_FirstSlotWins()
    {
        _set.Apply(Slots(("a", Shown), ("a", ShownToo)));

        Assert.Single(_created);
        Assert.Equal([Shown], IconOf("a").Applied);
    }

    [Fact]
    public void Dispose_DisposesEveryIcon_AndLaterAppliesCreateNone()
    {
        _set.Apply(Slots(("a", Shown)));

        _set.Dispose();

        Assert.False(_set.Apply(Slots(("a", Shown), ("b", Shown))));
        Assert.Single(_created);
        Assert.True(_created[0].Disposed);
    }
}
