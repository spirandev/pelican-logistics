using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class LogisticsGridTests
{
    [Fact]
    public void Remove_ReturnsItemFromSlot()
    {
        var item = new FakeItem("wood", 7);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Item(0, 0, item);

        RemovalResult result = g.Grid.Remove(new TilePos(0, 0));

        Assert.Same(item, Assert.Single(result.Items));
        Assert.Equal(AccessoryKind.None, result.Accessory);
        Assert.Null(g.Grid.Get(new TilePos(0, 0)));
        Assert.Equal(0, g.Grid.Count);
    }

    [Fact]
    public void Remove_ReturnsAccessory()
    {
        var grid = new LogisticsGrid();
        grid.Add(new BeltNode(new TilePos(0, 0), Direction.East) { Accessory = AccessoryKind.Bridge });

        RemovalResult result = grid.Remove(new TilePos(0, 0));

        Assert.Empty(result.Items);
        Assert.Equal(AccessoryKind.Bridge, result.Accessory);
    }

    [Fact]
    public void Remove_EmptyPosition_ReturnsEmpty()
    {
        var grid = new LogisticsGrid();

        Assert.Same(RemovalResult.Empty, grid.Remove(new TilePos(3, 3)));
    }

    [Fact]
    public void Add_OccupiedPosition_Throws()
    {
        var grid = new LogisticsGrid();
        grid.Add(new BeltNode(new TilePos(0, 0), Direction.East));

        Assert.Throws<InvalidOperationException>(() => grid.Add(new ExtractorNode(new TilePos(0, 0), Direction.East)));
    }

    [Fact]
    public void Remove_RecomputesTopology_OnNextTick()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.South)
            .Belt(1, -1, Direction.South);
        Assert.Equal(3, g.Grid.ProcessingOrder.Count);
        Assert.Equal(Direction.North, g.BeltAt(1, 0).InputSide);

        g.Grid.Remove(new TilePos(1, -1));

        Assert.Equal(2, g.Grid.ProcessingOrder.Count);
        Assert.Equal(Direction.West, g.BeltAt(1, 0).InputSide);
    }

    [Fact]
    public void Tick_EmptyGrid_DoesNothing()
    {
        var grid = new LogisticsGrid();

        grid.Tick(new FakeEndpoints());

        Assert.Empty(grid.ProcessingOrder);
    }
}
