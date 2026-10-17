using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class TickContextTests
{
    [Fact]
    public void PushForward_WithoutNeighbor_BlocksSlot()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East).Item(0, 0, new FakeItem("wood", 1), Balance.End);
        var ctx = new TickContext(g.Grid, g.Endpoints);
        BeltNode belt = g.BeltAt(0, 0);

        ctx.PushForward(belt, belt.Slot, Direction.East);

        Assert.True(belt.Slot.Blocked);
        Assert.False(belt.Slot.IsEmpty);
    }

    [Fact]
    public void PushForward_WhenNeighborTakesAll_ClearsSlot()
    {
        var item = new FakeItem("wood", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Item(0, 0, item, Balance.End);
        var ctx = new TickContext(g.Grid, g.Endpoints);
        BeltNode belt = g.BeltAt(0, 0);

        ctx.PushForward(belt, belt.Slot, Direction.East);

        Assert.True(belt.Slot.IsEmpty);
        Assert.Same(item, g.BeltAt(1, 0).Slot.Item);
    }

    [Fact]
    public void NodeAt_ReturnsGridNode()
    {
        var g = new GridBuilder().Belt(2, 3, Direction.East);
        var ctx = new TickContext(g.Grid, g.Endpoints);

        Assert.Same(g.BeltAt(2, 3), ctx.NodeAt(new TilePos(2, 3)));
        Assert.Null(ctx.NodeAt(new TilePos(0, 0)));
    }
}
