using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Tests.Fakes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Nodes;

public class InserterNodeTests
{
    [Fact]
    public void Inserter_InsertsOneEvery30Ticks_RestWaitsOnBelt()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        var counts = new List<int>();
        for (int tick = 1; tick <= 130; tick++)
        {
            g.Run(1);
            counts.Add(chest.CountTotal());
        }

        // Insere nos ticks 1, 31, 61, 91 e 121.
        Assert.Equal(1, counts[0]);
        Assert.Equal(1, counts[29]);
        Assert.Equal(2, counts[30]);
        Assert.Equal(3, counts[60]);
        Assert.Equal(4, counts[90]);
        Assert.Equal(4, counts[119]);
        Assert.Equal(5, counts[120]);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Equal(5, chest.CountById("wood"));
    }

    [Fact]
    public void Inserter_StackStaysBlockedOnBelt_WhileInserting()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        g.Run(10);

        Assert.Same(stack, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(4, stack.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
    }

    [Fact]
    public void Inserter_FullChest_InsertsNothingAndStackStaysBlocked()
    {
        var chest = new FakeChest(1, new FakeItem("stone", 999));
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        g.Run(200);

        Assert.Equal(0, chest.CountById("wood"));
        Assert.Equal(5, stack.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
    }

    [Fact]
    public void Inserter_ChestWithRoomInExistingStack_Accepts()
    {
        var chest = new FakeChest(1, new FakeItem("wood", 10));
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, new FakeItem("wood", 2), Balance.End);

        g.Run(40);

        Assert.Equal(12, chest.CountById("wood"));
        Assert.Single(chest.Items);
    }

    [Fact]
    public void Inserter_RefusesBeltPointingAtItsSide()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Belt(1, -1, Direction.South)
            .Item(1, -1, stack, Balance.End);

        g.Run(200);

        Assert.Equal(0, chest.CountTotal());
        Assert.Equal(5, stack.Count);
        Assert.True(g.BeltAt(1, -1).Slot.Blocked);
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.South)]
    [InlineData(Direction.East)]
    public void Inserter_RefusesFromSideOrFront(Direction entrySide)
    {
        var chest = new FakeChest();
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest);
        var ctx = new TickContext(g.Grid, g.Endpoints);
        var item = new FakeItem("wood", 5);

        int taken = g.NodeAt<InserterNode>(1, 0).Accept(entrySide, item, ctx);

        Assert.Equal(0, taken);
        Assert.Equal(5, item.Count);
        Assert.Equal(0, chest.CountTotal());
    }

    [Fact]
    public void Inserter_WithoutChest_InsertsNothing()
    {
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Item(0, 0, stack, Balance.End);

        g.Run(100);

        Assert.Equal(5, stack.Count);
        Assert.Equal(0, g.NodeAt<InserterNode>(1, 0).Cooldown);
    }

    [Fact]
    public void Inserter_StackTier_InsertsWholeStack()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        g.Run(1);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Same(stack, Assert.Single(chest.Items));
    }

    [Fact]
    public void Inserter_ConfigurableTier_InsertsConfiguredAmount()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Configurable)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);
        g.NodeAt<InserterNode>(1, 0).ConfiguredAmount = 20;

        g.Run(1);

        Assert.Equal(20, chest.CountTotal());
        Assert.Equal(30, stack.Count);
    }
}
