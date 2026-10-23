using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class TunnelRulesTests
{
    [Fact]
    public void DecideRole_NoSegmentBehind_IsEntrance()
    {
        var g = new GridBuilder().Segment(5, 5, Direction.East);

        Assert.Equal(TunnelRole.Entrance, TunnelRules.DecideRole(g.Grid, new TilePos(0, 0), Direction.East));
        Assert.Equal(TunnelRole.Entrance, TunnelRules.DecideRole(g.Grid, new TilePos(5, 6), Direction.East)); // segmento na lateral
    }

    [Fact]
    public void DecideRole_SegmentBehindPointingAtTheTile_IsExit()
    {
        var g = new GridBuilder().Segment(4, 0, Direction.East);

        Assert.Equal(TunnelRole.Exit, TunnelRules.DecideRole(g.Grid, new TilePos(5, 0), Direction.East));
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void DecideRole_SegmentBehindPointingElsewhere_IsEntrance(Direction segmentFacing)
    {
        var g = new GridBuilder().Segment(4, 0, segmentFacing);

        Assert.Equal(TunnelRole.Entrance, TunnelRules.DecideRole(g.Grid, new TilePos(5, 0), Direction.East));
    }

    [Fact]
    public void CanPlaceTunnel_NotOverASegment()
    {
        var g = new GridBuilder().Segment(1, 0, Direction.East);

        Assert.False(TunnelRules.CanPlaceTunnel(g.Grid, new TilePos(1, 0)));
        Assert.True(TunnelRules.CanPlaceTunnel(g.Grid, new TilePos(2, 0)));
    }
}
