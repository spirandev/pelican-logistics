using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

public class BeltNodeTests
{
    [Fact]
    public void Belt_ItemReachesEnd_InExactly60Ticks()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Item(0, 0, new FakeItem("wood", 1));

        g.Run(59);
        Assert.Equal(590, g.BeltAt(0, 0).Slot.Progress);
        Assert.False(g.BeltAt(0, 0).Slot.Blocked);

        g.Run(1);
        Assert.Equal(Balance.End, g.BeltAt(0, 0).Slot.Progress);
    }

    [Fact]
    public void Belt_ItemCrossesThreeBelts_StartingAtZeroWithCorrectEntrySide()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Item(0, 0, item);

        g.Run(60);
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Same(item, g.BeltAt(1, 0).Slot.Item);
        Assert.Equal(0, g.BeltAt(1, 0).Slot.Progress);
        Assert.Equal(Direction.West, g.BeltAt(1, 0).Slot.EntrySide);

        g.Run(60);
        Assert.True(g.BeltAt(1, 0).Slot.IsEmpty);
        Assert.Same(item, g.BeltAt(2, 0).Slot.Item);
        Assert.Equal(0, g.BeltAt(2, 0).Slot.Progress);
        Assert.Equal(Direction.West, g.BeltAt(2, 0).Slot.EntrySide);
    }

    [Fact]
    public void Belt_StopsAtEnd_WhenNoDownstream()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Item(0, 0, new FakeItem("wood", 1));

        g.Run(200);

        TransitSlot slot = g.BeltAt(0, 0).Slot;
        Assert.False(slot.IsEmpty);
        Assert.Equal(Balance.End, slot.Progress);
        Assert.True(slot.Blocked);
    }

    [Fact]
    public void Belt_CompactedQueue_MovesTogetherInOneTick()
    {
        var first = new FakeItem("wood", 1);
        var second = new FakeItem("stone", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Item(0, 0, second, Balance.End)
            .Item(1, 0, first, Balance.End);

        g.Run(1);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Same(second, g.BeltAt(1, 0).Slot.Item);
        Assert.Same(first, g.BeltAt(2, 0).Slot.Item);
    }

    [Fact]
    public void Belt_PointingAtSideOfStraightBelt_IsBlocked()
    {
        var side = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)   // atrás de B: B fica reta
            .Belt(1, 0, Direction.East)   // B
            .Belt(2, 0, Direction.East)
            .Belt(1, -1, Direction.South) // A, apontando para a lateral de B
            .Item(1, -1, side);

        g.Run(200);

        Assert.Equal(Direction.West, g.BeltAt(1, 0).InputSide);
        Assert.Same(side, g.BeltAt(1, -1).Slot.Item);
        Assert.True(g.BeltAt(1, -1).Slot.Blocked);
        Assert.True(g.BeltAt(1, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Belt_WithNothingBehind_TurnsIntoCurveFromSide()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)   // A, entrega pela lateral oeste de B
            .Belt(1, 0, Direction.South)  // B, nada atrás (ao norte)
            .Belt(1, 1, Direction.South)
            .Item(0, 0, item);

        g.Run(60);
        BeltNode b = g.BeltAt(1, 0);
        Assert.Equal(Direction.West, b.InputSide);
        Assert.True(b.IsCurve);
        Assert.Same(item, b.Slot.Item);
        Assert.Equal(Direction.West, b.Slot.EntrySide);

        g.Run(60);
        Assert.Same(item, g.BeltAt(1, 1).Slot.Item);
        Assert.Equal(Direction.North, g.BeltAt(1, 1).Slot.EntrySide);
    }

    [Fact]
    public void Belt_WithTwoSidesAndNothingBehind_DoesNotCurve()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.South)
            .Belt(2, 0, Direction.West)
            .Item(0, 0, new FakeItem("wood", 1))
            .Item(2, 0, new FakeItem("stone", 1));

        g.Run(200);

        Assert.Equal(Direction.North, g.BeltAt(1, 0).InputSide);
        Assert.False(g.BeltAt(1, 0).IsCurve);
        Assert.True(g.BeltAt(1, 0).Slot.IsEmpty);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.True(g.BeltAt(2, 0).Slot.Blocked);
    }

    [Fact]
    public void Belt_SameItemBehindBlockedStack_MergesIntoIt()
    {
        var front = new FakeItem("wood", 5);
        var back = new FakeItem("wood", 3);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Item(1, 0, front, Balance.End);

        g.Run(1);
        Assert.True(g.BeltAt(1, 0).Slot.Blocked);

        g.Item(0, 0, back).Run(60);

        Assert.Same(front, g.BeltAt(1, 0).Slot.Item);
        Assert.Equal(8, front.Count);
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Belt_PartialMerge_FillsFrontAndKeepsRestBlocked()
    {
        var front = new FakeItem("wood", 990);
        var back = new FakeItem("wood", 20);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Item(1, 0, front, Balance.End)
            .Item(0, 0, back, Balance.End);

        g.Run(1);

        Assert.Equal(999, front.Count);
        Assert.Equal(11, back.Count);
        Assert.Same(back, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);

        g.Run(100);
        Assert.Equal(999, front.Count);
        Assert.Equal(11, back.Count);
    }

    [Theory]
    [InlineData("stone", 0)]
    [InlineData("wood", 1)]
    public void Belt_DifferentItemOrQuality_DoesNotMerge(string backId, int backQuality)
    {
        var front = new FakeItem("wood", 5);
        var back = new FakeItem(backId, 3, backQuality);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Item(1, 0, front, Balance.End);

        g.Run(1).Item(0, 0, back).Run(200);

        Assert.Equal(5, front.Count);
        Assert.Equal(3, back.Count);
        Assert.Same(back, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
    }

    [Fact]
    public void Belt_DoesNotMerge_WithMovingFrontStack()
    {
        var front = new FakeItem("wood", 5);
        var back = new FakeItem("wood", 3);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Item(1, 0, front)
            .Item(0, 0, back, Balance.End);

        g.Run(1);

        Assert.Equal(5, front.Count);
        Assert.Equal(3, back.Count);
        Assert.Same(back, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.False(g.BeltAt(1, 0).Slot.Blocked);
    }

    [Fact]
    public void Belt_Rotation_ChangesFlowOnNextTick()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(0, 1, Direction.South)
            .Item(0, 0, item, Balance.End);

        g.Run(10);
        Assert.Same(item, g.BeltAt(0, 0).Slot.Item);

        g.BeltAt(0, 0).Facing = Direction.South;
        g.Grid.MarkDirty();
        g.Run(1);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Same(item, g.BeltAt(0, 1).Slot.Item);
        Assert.Equal(Direction.North, g.BeltAt(0, 1).InputSide);
    }

    [Fact]
    public void Belt_ProvidesOnlyToTileInFront()
    {
        var belt = new BeltNode(new TilePos(0, 0), Direction.North);

        Assert.True(belt.ProvidesTo(new TilePos(0, -1)));
        Assert.False(belt.ProvidesTo(new TilePos(1, 0)));
        Assert.False(belt.ProvidesTo(new TilePos(0, 1)));
    }

    [Fact]
    public void Belt_CanReceiveFrom_OnlyInputSideWhenEmpty()
    {
        var belt = new BeltNode(new TilePos(0, 0), Direction.East);

        Assert.True(belt.CanReceiveFrom(Direction.West));
        Assert.False(belt.CanReceiveFrom(Direction.North));
        Assert.False(belt.CanReceiveFrom(Direction.East));

        belt.Slot.Put(new FakeItem("wood", 1), Direction.West);
        Assert.False(belt.CanReceiveFrom(Direction.West));
    }

    [Theory]
    [InlineData(BeltTier.Basic, 60)]
    [InlineData(BeltTier.Fast, 30)]
    [InlineData(BeltTier.Express, 15)]
    public void Belt_SpeedDependsOnTier(BeltTier tier, int ticksPerTile)
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East, tier)
            .Item(0, 0, new FakeItem("wood", 1));

        g.Run(ticksPerTile - 1);
        Assert.True(g.BeltAt(0, 0).Slot.Progress < Balance.End);

        g.Run(1);
        Assert.Equal(Balance.End, g.BeltAt(0, 0).Slot.Progress);
    }

    [Fact]
    public void GetOutgoingSlot_OnlyForTileInFront_AndOnlyWithItem()
    {
        var belt = new BeltNode(new TilePos(0, 0), Direction.East);

        Assert.Null(belt.GetOutgoingSlot(new TilePos(1, 0)));

        belt.Slot.Put(new FakeItem("wood", 1), Direction.West);

        Assert.Same(belt.Slot, belt.GetOutgoingSlot(new TilePos(1, 0)));
        Assert.Null(belt.GetOutgoingSlot(new TilePos(-1, 0)));
        Assert.Null(belt.GetOutgoingSlot(new TilePos(0, 1)));
    }
}
