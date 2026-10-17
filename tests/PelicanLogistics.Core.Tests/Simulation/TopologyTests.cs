using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class TopologyTests
{
    [Fact]
    public void Order_InALine_EachBeltComesBeforeTheOneFeedingIt()
    {
        var g = new GridBuilder()
            .Belt(2, 0, Direction.East)
            .Belt(0, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(1, 0, Direction.East);

        IReadOnlyList<LogisticsNode> order = Topology.Recompute(g.Grid);

        Assert.Equal(new[] { 3, 2, 1, 0 }, order.Select(n => n.Position.X));
    }

    [Fact]
    public void Order_ExtractorAfterBelt_InserterBeforeBeltFeedingIt()
    {
        var g = new GridBuilder()
            .Extractor(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Inserter(2, 0, Direction.East);

        IReadOnlyList<LogisticsNode> order = Topology.Recompute(g.Grid);

        Assert.Equal(new[] { NodeKind.Inserter, NodeKind.Belt, NodeKind.Extractor }, order.Select(n => n.Kind));
    }

    [Fact]
    public void Order_ContainsEveryNodeOnce_EvenWithLoopsAndBranches()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.South)
            .Belt(1, 1, Direction.West)
            .Belt(0, 1, Direction.North)
            .Belt(5, 5, Direction.North)
            .Belt(2, 0, Direction.West); // aponta para a lateral do laço

        IReadOnlyList<LogisticsNode> order = Topology.Recompute(g.Grid);

        Assert.Equal(6, order.Count);
        Assert.Equal(6, order.Distinct().Count());
    }

    [Fact]
    public void Loop_WithTwoStacks_Circulates()
    {
        var first = new FakeItem("wood", 1);
        var second = new FakeItem("stone", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.South)
            .Belt(1, 1, Direction.West)
            .Belt(0, 1, Direction.North)
            .Item(0, 0, first)
            .Item(1, 1, second);

        var visited = new HashSet<TilePos>();
        for (int i = 0; i < 600; i++)
        {
            g.Run(1);
            visited.Add(g.Grid.Nodes.OfType<BeltNode>().Single(b => b.Slot.Item == first).Position);
        }

        Assert.Equal(4, visited.Count);
        Assert.Equal(2, g.CountInTransit());
    }

    [Fact]
    public void Loop_Full_DoesNotMove()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.South)
            .Belt(1, 1, Direction.West)
            .Belt(0, 1, Direction.North)
            .Item(0, 0, new FakeItem("a", 1))
            .Item(1, 0, new FakeItem("b", 1))
            .Item(1, 1, new FakeItem("c", 1))
            .Item(0, 1, new FakeItem("d", 1));

        g.Run(600);

        Assert.Equal("a", g.BeltAt(0, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal("b", g.BeltAt(1, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal("c", g.BeltAt(1, 1).Slot.Item!.QualifiedItemId);
        Assert.Equal("d", g.BeltAt(0, 1).Slot.Item!.QualifiedItemId);
        Assert.All(g.Grid.Nodes.OfType<BeltNode>(), b => Assert.True(b.Slot.Blocked));
    }

    [Fact]
    public void InputSide_IsBack_ForLoneBelt()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.North);

        Topology.Recompute(g.Grid);

        Assert.Equal(Direction.South, g.BeltAt(0, 0).InputSide);
    }

    [Fact]
    public void InputSide_InserterOnTheSide_DoesNotMakeCurve()
    {
        var g = new GridBuilder()
            .Inserter(0, 0, Direction.East)
            .Belt(1, 0, Direction.South);

        Topology.Recompute(g.Grid);

        Assert.Equal(Direction.North, g.BeltAt(1, 0).InputSide);
    }
}
