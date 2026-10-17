using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Machines;

public class VirtualSlotsTests
{
    private static readonly AdditionalRequirement Coal = new(FakeMachine.Ids.Coal, 1, i => i.QualifiedItemId == FakeMachine.Ids.Coal);

    [Fact]
    public void NewSlots_AreEmptyAndIncomplete()
    {
        var slots = new VirtualSlots();

        Assert.True(slots.IsEmpty);
        Assert.False(slots.IsComplete(Array.Empty<AdditionalRequirement>()));
        Assert.Empty(slots.AllItems());
    }

    [Fact]
    public void IsComplete_RequiresMainCountAndEveryAdditional()
    {
        var slots = new VirtualSlots { Main = new FakeItem(FakeMachine.Ids.CopperOre, 4), MainRequired = 5 };
        var reqs = new[] { Coal };

        Assert.False(slots.IsComplete(reqs));

        ((FakeItem)slots.Main).Count = 5;
        Assert.False(slots.IsComplete(reqs));

        slots.Additional[Coal.Id] = new FakeItem(FakeMachine.Ids.Coal, 1);
        Assert.True(slots.IsComplete(reqs));
    }

    [Fact]
    public void IsComplete_WithoutAdditionalRequirements_OnlyNeedsMain()
    {
        var slots = new VirtualSlots { Main = new FakeItem("(O)613", 1), MainRequired = 1 };

        Assert.True(slots.IsComplete(Array.Empty<AdditionalRequirement>()));
    }

    [Fact]
    public void AllItems_ReturnsMainAndAdditional_AndClearEmptiesEverything()
    {
        var main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        var coal = new FakeItem(FakeMachine.Ids.Coal, 1);
        var slots = new VirtualSlots { Main = main, MainRequired = 5 };
        slots.Additional[Coal.Id] = coal;

        Assert.False(slots.IsEmpty);
        Assert.Equal(new ILogisticsItem[] { main, coal }, slots.AllItems());
        Assert.Equal(1, slots.CountOf(Coal));

        slots.Clear();

        Assert.True(slots.IsEmpty);
        Assert.Equal(0, slots.MainRequired);
        Assert.Equal(0, slots.CountOf(Coal));
    }
}
