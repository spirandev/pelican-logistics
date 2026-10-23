using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

public class TunnelNodeTests
{
    [Fact]
    public void Entrance_WithoutSegmentAhead_ItemStopsAtTheEnd()
    {
        var item = new FakeItem("wood", 3);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Item(0, 0, item, Balance.End);

        g.Run(100);

        TunnelNode entrance = g.TunnelAt(1, 0);
        Assert.Equal(TunnelRole.Entrance, entrance.Role);
        Assert.Same(item, entrance.Slot.Item);
        Assert.Equal(Balance.End, entrance.Slot.Progress);
        Assert.True(entrance.Slot.Blocked);
    }

    [Fact]
    public void Entrance_ReceivesOnlyFromBehind_WhenEmpty_NeverMerges_ProvidesNothingOnTheSurface()
    {
        var entrance = new TunnelNode(new TilePos(0, 0), Direction.East, TunnelRole.Entrance);
        var ctx = new TickContext(new LogisticsGrid(), new FakeEndpoints());

        Assert.False(entrance.CanReceiveFrom(Direction.North));
        Assert.Equal(0, entrance.Accept(Direction.South, new FakeItem("wood", 1), ctx));
        Assert.Equal(5, entrance.Accept(Direction.West, new FakeItem("wood", 5), ctx));
        Assert.False(entrance.CanAccept(Direction.West, new FakeItem("wood", 1)));
        Assert.Equal(0, entrance.Accept(Direction.West, new FakeItem("wood", 1), ctx));
        Assert.Equal(5, entrance.Slot.Item!.Count);
        Assert.False(entrance.ProvidesTo(new TilePos(1, 0)));
        Assert.Null(entrance.GetOutgoingSlot(new TilePos(1, 0)));
    }

    [Fact]
    public void Exit_ReceivesNothingFromSurfaceNeighbors_ProvidesToTheFront()
    {
        var exit = new TunnelNode(new TilePos(0, 0), Direction.East, TunnelRole.Exit);
        var ctx = new TickContext(new LogisticsGrid(), new FakeEndpoints());

        foreach (Direction side in DirectionExtensions.All)
        {
            Assert.False(exit.CanReceiveFrom(side));
            Assert.Equal(0, exit.Accept(side, new FakeItem("wood", 1), ctx));
        }

        Assert.True(exit.ProvidesTo(new TilePos(1, 0)));
        Assert.False(exit.ProvidesTo(new TilePos(-1, 0)));
        Assert.Equal(NodeKind.Tunnel, exit.Kind);
    }

    [Fact]
    public void SurfaceBeltBehindTheExit_DoesNotPushIntoIt()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East, TunnelRole.Exit)
            .Item(0, 0, item, Balance.End);

        g.Run(50);

        Assert.Same(item, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.TunnelAt(1, 0).Slot.IsEmpty);
    }

    // Com outros blocos.

    [Fact]
    public void Inserter_DeliversIntoTheEntrance_ExitMakesTheBeltCurve_BeltFeedsAnInserterSide()
    {
        var source = new FakeChest(36, new FakeItem("wood", 4));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Tunnel(2, 0, Direction.East)
            .Segment(3, 0, Direction.East)
            .Tunnel(4, 0, Direction.East)
            .Belt(5, 0, Direction.South)                // sem nada atrás: curva saindo da saída
            .Belt(5, 1, Direction.South)
            .Inserter(5, 2, Direction.East)            // a esteira chega pela lateral norte
            .Chest(6, 2, target);

        g.Run(800);

        Assert.True(g.BeltAt(5, 0).IsCurve);
        Assert.Equal(4, target.CountTotal());
        Assert.Equal(0, source.CountTotal());
    }

    [Fact]
    public void Exit_PointingAtTheSideOfAnInserter_IsAnInput()
    {
        var target = new FakeChest();
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Segment(2, 0, Direction.East)
            .Segment(3, 0, Direction.South)
            .Tunnel(3, 1, Direction.South)
            .Inserter(3, 2, Direction.East)
            .Chest(4, 2, target)
            .Item(0, 0, new FakeItem("wood", 3), Balance.End);

        g.Run(600);

        Assert.Contains(Direction.North, g.InserterAt(3, 2).Inputs);
        Assert.Equal(3, target.CountTotal());
    }

    [Fact]
    public void Inserter_InFrontOfTheExit_CannotDeliverIntoIt()
    {
        var source = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Tunnel(2, 0, Direction.East, TunnelRole.Exit);

        g.Run(60);

        Assert.Equal(5, source.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Splitter_ExitIsAnInput_EntranceIsAnOutput_ExitWithTheBackToItIsNothing()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Segment(2, 0, Direction.East)
            .Tunnel(3, 0, Direction.East)            // saída apontando para o Divisor: entrada
            .Splitter(4, 0)
            .Tunnel(5, 0, Direction.East)            // entrada com a traseira virada para o Divisor: saída
            .Segment(6, 0, Direction.East)
            .Tunnel(7, 0, Direction.East)
            .Belt(8, 0, Direction.East)
            .Tunnel(4, -1, Direction.North, TunnelRole.Exit) // saída com a traseira virada para o Divisor: nada
            .Item(0, 0, new FakeItem("wood", 2), Balance.End);

        g.Run(400);

        SplitterNode splitter = g.SplitterAt(4, 0);
        Assert.Equal(new[] { Direction.West }, splitter.Inputs);
        Assert.Equal(new[] { Direction.East }, splitter.Outputs);
        Assert.Equal(2, g.BeltAt(8, 0).Slot.Item!.Count);
    }
}
