using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Machines;

public class MachineSlotStoreTests
{
    [Fact]
    public void GetOrCreate_ReturnsSameEntryForSamePosition()
    {
        var store = new MachineSlotStore();

        VirtualSlots a = store.GetOrCreate(new TilePos(1, 2));
        VirtualSlots b = store.GetOrCreate(new TilePos(1, 2));

        Assert.Same(a, b);
        Assert.Same(a, store.Get(new TilePos(1, 2)));
        Assert.Null(store.Get(new TilePos(2, 1)));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void RemoveMachine_ReturnsAllItemsFromSlots()
    {
        var store = new MachineSlotStore();
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        var coal = new FakeItem(FakeMachine.Ids.Coal, 1);
        VirtualSlots slots = store.GetOrCreate(new TilePos(0, 0));
        slots.Main = ore;
        slots.MainRequired = 5;
        slots.Additional[FakeMachine.Ids.Coal] = coal;

        IReadOnlyList<ILogisticsItem> items = store.RemoveMachine(new TilePos(0, 0));

        Assert.Equal(new ILogisticsItem[] { ore, coal }, items);
        Assert.Null(store.Get(new TilePos(0, 0)));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void RemoveMachine_UnknownPosition_ReturnsNothing()
    {
        var store = new MachineSlotStore();

        Assert.Empty(store.RemoveMachine(new TilePos(0, 0)));
    }
}
