using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

/// <summary>Montagem padrão: esteira em (0,0) alimentando a entrada do Túnel em (1,0), Esteiras Subterrâneas a partir de (2,0), tudo virado para o leste.</summary>
public class UndergroundSegmentNodeTests
{
    /// <summary>Ticks para atravessar um tile embaixo da terra (Túnel e cada Esteira Subterrânea).</summary>
    private const int TileTicks = Balance.End / Balance.UndergroundSpeed;

    /// <summary>Esteira (0,0) → entrada (1,0) → 'segments' Esteiras Subterrâneas → saída → esteira depois da saída.</summary>
    private static GridBuilder Line(int segments)
    {
        int exitX = 2 + segments;
        return new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Segments(2, 0, Direction.East, segments)
            .Tunnel(exitX, 0, Direction.East)
            .Belt(exitX + 1, 0, Direction.East);
    }

    [Fact]
    public void Item_CrossesThirtyTicksPerTile_TunnelSegmentsAndExit()
    {
        var item = new FakeItem("wood", 3);
        var g = Line(3).Item(0, 0, item, Balance.End);
        Assert.Equal(30, TileTicks);

        // Tick 1: entra na entrada. Depois: entrada, 3 Esteiras Subterrâneas e saída, 30 ticks cada.
        g.Run(1 + (5 * TileTicks) - 1);
        Assert.Same(item, g.TunnelAt(5, 0).Slot.Item);

        g.Run(1);
        Assert.True(g.TunnelAt(5, 0).Slot.IsEmpty);
        Assert.Same(item, g.BeltAt(6, 0).Slot.Item);
        Assert.Equal(Direction.West, g.BeltAt(6, 0).Slot.EntrySide);
    }

    [Fact]
    public void Item_FollowsACurveUnderground()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Segment(2, 0, Direction.East)
            .Segments(3, 0, Direction.South, 3)
            .Tunnel(3, 3, Direction.South)
            .Belt(3, 4, Direction.South)
            .Item(0, 0, item, Balance.End);

        g.Run(400);

        Assert.True(g.SegmentAt(3, 0).IsCurve);
        Assert.Equal(Direction.West, g.SegmentAt(3, 0).InputSide);
        Assert.Equal(TunnelRole.Exit, g.TunnelAt(3, 3).Role);
        Assert.Same(item, g.BeltAt(3, 4).Slot.Item);
    }

    [Fact]
    public void SurfaceBeltAndSegmentOnTheSameTile_DoNotInteract()
    {
        var below = new FakeItem("below", 1);
        var above = new FakeItem("above", 1);
        var g = Line(2)
            .Belt(2, 1, Direction.North)
            .Belt(2, 0, Direction.North)
            .Belt(2, -1, Direction.North)
            .Item(0, 0, below, Balance.End)
            .Item(2, 1, above);

        g.Run(300);

        Assert.Same(above, g.BeltAt(2, -1).Slot.Item);
        Assert.Same(below, g.BeltAt(5, 0).Slot.Item);
        Assert.False(g.BeltAt(2, 0).IsCurve);
        Assert.Equal(Direction.West, g.SegmentAt(2, 0).InputSide);
    }

    [Fact]
    public void ChainWithoutExit_StacksStopAtTheLastSegment_SameItemsMerge()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Segments(2, 0, Direction.East, 3);
        for (int i = 0; i < 3; i++)
        {
            for (int tick = 0; tick < 500 && !g.BeltAt(0, 0).Slot.IsEmpty; tick++)
                g.Run(1);
            g.Item(0, 0, new FakeItem("wood", 1), Balance.End);
        }

        g.Run(500);

        Assert.Equal(3, g.SegmentAt(4, 0).Slot.Item!.Count);
        Assert.True(g.SegmentAt(4, 0).Slot.Blocked);
        Assert.True(g.SegmentAt(3, 0).Slot.IsEmpty);
        Assert.True(g.SegmentAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Segment_PointingAtTheSideOfTheExit_DoesNotDeliver_BehindIt_Does()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Tunnel(5, 0, Direction.East, TunnelRole.Exit)
            .Segment(5, -1, Direction.South);
        g.SegmentAt(5, -1).Slot.Put(item, Direction.North);

        g.Run(100);
        Assert.Same(item, g.SegmentAt(5, -1).Slot.Item);
        Assert.True(g.SegmentAt(5, -1).Slot.Blocked);

        var other = new FakeItem("stone", 1);
        g.Segment(4, 0, Direction.East);
        g.SegmentAt(4, 0).Slot.Put(other, Direction.West);
        g.Run(TileTicks);

        Assert.Same(other, g.TunnelAt(5, 0).Slot.Item);
    }

    [Fact]
    public void LongChain_ThirtySegmentsWithThreeCurves_EverythingArrivesInOrder()
    {
        // Leste 10, sul 10, oeste 5, norte 5 (3 curvas); a saída fica virada para o norte em (6,5).
        var source = new FakeChest(36, Enumerable.Range(1, 5).Select(i => new FakeItem($"i{i}", 1)).ToArray());
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(-2, 0, source)
            .Inserter(-1, 0, Direction.East)
            .Tunnel(0, 0, Direction.East)
            .Segments(1, 0, Direction.East, 10)
            .Segments(11, 0, Direction.South, 10)
            .Segments(11, 10, Direction.West, 5)
            .Segments(6, 10, Direction.North, 5)
            .Tunnel(6, 5, Direction.North)
            .Inserter(6, 4, Direction.North)
            .Chest(6, 3, target);

        g.Run(3000);

        Assert.Equal(30, g.Grid.UndergroundNodes.Count());
        Assert.True(g.SegmentAt(11, 0).IsCurve);
        Assert.True(g.SegmentAt(11, 10).IsCurve);
        Assert.True(g.SegmentAt(6, 10).IsCurve);
        Assert.Equal(new[] { "i1", "i2", "i3", "i4", "i5" }, target.Items.Select(i => i.QualifiedItemId));
        Assert.Equal(0, g.CountInTransit());
    }

    [Fact]
    public void RotatingASegment_RecomputesTheChainOnTheNextTick()
    {
        var item = new FakeItem("wood", 1);
        var g = Line(2).Item(0, 0, item, Balance.End);
        g.SegmentAt(3, 0).Facing = Direction.South;
        g.Grid.MarkDirty();

        g.Run(300);

        Assert.True(g.SegmentAt(3, 0).IsCurve);
        Assert.Same(item, g.SegmentAt(3, 0).Slot.Item);
        Assert.True(g.SegmentAt(3, 0).Slot.Blocked);
        Assert.True(g.TunnelAt(4, 0).Slot.IsEmpty);
    }

    [Fact]
    public void TwoParallelChains_DoNotInterfere()
    {
        var wood = new FakeItem("wood", 1);
        var stone = new FakeItem("stone", 1);
        var g = Line(3)
            .Belt(0, 1, Direction.East).Tunnel(1, 1, Direction.East).Segments(2, 1, Direction.East, 3).Tunnel(5, 1, Direction.East).Belt(6, 1, Direction.East)
            .Item(0, 0, wood, Balance.End)
            .Item(0, 1, stone, Balance.End);

        g.Run(300);

        Assert.Same(wood, g.BeltAt(6, 0).Slot.Item);
        Assert.Same(stone, g.BeltAt(6, 1).Slot.Item);
        Assert.All(g.Grid.UndergroundNodes, s => Assert.False(s.IsCurve));
    }

    [Fact]
    public void RemovingASegment_ReturnsItsItem_TheTunnelOnlyItsOwnSlot()
    {
        var g = Line(2);
        var inSegment = new FakeItem("a", 1);
        var inTunnel = new FakeItem("b", 1);
        g.SegmentAt(2, 0).Slot.Put(inSegment, Direction.West);
        g.TunnelAt(1, 0).Slot.Put(inTunnel, Direction.West);

        RemovalResult segment = g.Grid.RemoveUnderground(new TilePos(2, 0));
        RemovalResult tunnel = g.Grid.Remove(new TilePos(1, 0));

        Assert.Same(inSegment, Assert.Single(segment.Items));
        Assert.Equal(AccessoryKind.None, segment.Accessory);
        Assert.Same(inTunnel, Assert.Single(tunnel.Items));
        Assert.Null(g.Grid.GetUnderground(new TilePos(2, 0)));
        Assert.NotNull(g.Grid.GetUnderground(new TilePos(3, 0)));
        Assert.Equal(RemovalResult.Empty, g.Grid.RemoveUnderground(new TilePos(9, 9)));
    }

    [Fact]
    public void Segment_IsNeverPulledFromTheSurface_AndHasTheTier2Speed()
    {
        var segment = new UndergroundSegmentNode(new TilePos(0, 0), Direction.East);
        segment.Slot.Put(new FakeItem("wood", 1), Direction.West);
        segment.Slot.Advance(Balance.End);

        Assert.Null(segment.GetOutgoingSlot(new TilePos(1, 0)));
        Assert.Equal(Balance.BeltSpeedFast, segment.Speed);
        Assert.Equal(NodeKind.UndergroundSegment, segment.Kind);
    }
}
