using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

public class TransitSlotTests
{
    [Fact]
    public void Put_StartsAtZero_NotBlocked()
    {
        TransitSlot slot = BlockedSlot(new FakeItem("wood", 1));

        var item = new FakeItem("stone", 1);
        slot.Put(item, Direction.North);

        Assert.Same(item, slot.Item);
        Assert.Equal(0, slot.Progress);
        Assert.Equal(Direction.North, slot.EntrySide);
        Assert.False(slot.Blocked);
        Assert.False(slot.IsEmpty);
    }

    [Fact]
    public void Advance_ClampsAtEnd_AndReportsArrival()
    {
        var slot = new TransitSlot();
        slot.Put(new FakeItem("wood", 1), Direction.West);

        Assert.False(slot.Advance(Balance.End - 1));
        Assert.True(slot.Advance(100));
        Assert.Equal(Balance.End, slot.Progress);
    }

    [Fact]
    public void Clear_ReturnsItemAndResets()
    {
        var item = new FakeItem("wood", 1);
        TransitSlot slot = BlockedSlot(item);

        Assert.Same(item, slot.Clear());
        Assert.True(slot.IsEmpty);
        Assert.False(slot.Blocked);
        Assert.Null(slot.Clear());
    }

    /// <summary>Slot de uma esteira sem nada à frente, com a pilha parada no fim.</summary>
    private static TransitSlot BlockedSlot(FakeItem item)
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East).Item(0, 0, item, Balance.End).Run(1);
        TransitSlot slot = g.BeltAt(0, 0).Slot;
        Assert.True(slot.Blocked);
        return slot;
    }
}
