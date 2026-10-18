using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class UndergroundPairingTests
{
    [Fact]
    public void DecideRole_FirstPlacedIsEntrance_SecondUpToSixTilesAheadIsExit()
    {
        var g = new GridBuilder();
        Assert.Equal(UndergroundRole.Entrance, UndergroundPairing.DecideRole(g.Grid, new TilePos(0, 0), Direction.East));
        g.Underground(0, 0, Direction.East);

        for (int x = 1; x <= 6; x++)
            Assert.Equal(UndergroundRole.Exit, UndergroundPairing.DecideRole(g.Grid, new TilePos(x, 0), Direction.East));

        Assert.Equal(UndergroundRole.Entrance, UndergroundPairing.DecideRole(g.Grid, new TilePos(7, 0), Direction.East));
        Assert.Equal(UndergroundPairing.MaxDistance, Balance.UndergroundMaxGap + 1);
    }

    [Fact]
    public void DecideRole_OtherDirectionOrAlreadyPaired_IsEntrance()
    {
        var g = new GridBuilder()
            .Underground(0, 0, Direction.East)
            .Underground(3, 0, Direction.East);
        Assert.Equal(UndergroundRole.Exit, g.UndergroundAt(3, 0).Role);

        // Outra direção, atrás da entrada, ou depois de uma saída: entrada.
        Assert.Equal(UndergroundRole.Entrance, UndergroundPairing.DecideRole(g.Grid, new TilePos(2, 0), Direction.West));
        Assert.Equal(UndergroundRole.Entrance, UndergroundPairing.DecideRole(g.Grid, new TilePos(-2, 0), Direction.East));
        Assert.Equal(UndergroundRole.Entrance, UndergroundPairing.DecideRole(g.Grid, new TilePos(5, 0), Direction.East));

        // Entre o par: a primeira atrás é a entrada (0,0), mas ela já tem a saída (3,0).
        Assert.Equal(UndergroundRole.Entrance, UndergroundPairing.DecideRole(g.Grid, new TilePos(2, 0), Direction.East));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void Pairing_GapUpToFive_Pairs_TunnelHasGapTiles(int gap)
    {
        var g = new GridBuilder()
            .Underground(0, 0, Direction.East, UndergroundRole.Entrance)
            .Underground(gap + 1, 0, Direction.East, UndergroundRole.Exit);

        Topology.Recompute(g.Grid);

        UndergroundBeltNode entrance = g.UndergroundAt(0, 0);
        UndergroundBeltNode exit = g.UndergroundAt(gap + 1, 0);
        Assert.Same(exit, entrance.Partner);
        Assert.Same(entrance, exit.Partner);
        Assert.Equal(gap, entrance.TunnelLength);
    }

    [Fact]
    public void Pairing_GapOfSix_DoesNotPair()
    {
        var g = new GridBuilder()
            .Underground(0, 0, Direction.East, UndergroundRole.Entrance)
            .Underground(7, 0, Direction.East, UndergroundRole.Exit);

        Topology.Recompute(g.Grid);

        Assert.Null(g.UndergroundAt(0, 0).Partner);
        Assert.Null(g.UndergroundAt(7, 0).Partner);
    }

    [Fact]
    public void Pairing_FirstSameDirectionUndergroundAheadIsAnEntrance_DoesNotPair()
    {
        var g = new GridBuilder()
            .Underground(0, 0, Direction.East, UndergroundRole.Entrance)
            .Underground(2, 0, Direction.East, UndergroundRole.Entrance)
            .Underground(4, 0, Direction.East, UndergroundRole.Exit);

        Topology.Recompute(g.Grid);

        Assert.Null(g.UndergroundAt(0, 0).Partner);
        Assert.Same(g.UndergroundAt(4, 0), g.UndergroundAt(2, 0).Partner);
        Assert.Equal(1, g.UndergroundAt(2, 0).TunnelLength);
    }

    [Fact]
    public void Pairing_UndergroundsOfOtherDirectionsInTheGap_DoNotInterfere()
    {
        var g = new GridBuilder()
            .Underground(0, 0, Direction.East, UndergroundRole.Entrance)
            .Underground(1, 0, Direction.West, UndergroundRole.Entrance)
            .Underground(2, 0, Direction.North, UndergroundRole.Exit)
            .Underground(3, 0, Direction.East, UndergroundRole.Exit);

        Topology.Recompute(g.Grid);

        Assert.Same(g.UndergroundAt(3, 0), g.UndergroundAt(0, 0).Partner);
        Assert.Equal(2, g.UndergroundAt(0, 0).TunnelLength);
    }

    [Fact]
    public void Pairing_ExitRemoved_EntranceLosesItsPartner()
    {
        var g = new GridBuilder()
            .Underground(0, 0, Direction.East)
            .Underground(3, 0, Direction.East);
        Topology.Recompute(g.Grid);
        Assert.NotNull(g.UndergroundAt(0, 0).Partner);

        g.Grid.Remove(new TilePos(3, 0));
        Topology.Recompute(g.Grid);

        Assert.Null(g.UndergroundAt(0, 0).Partner);
        Assert.Equal(0, g.UndergroundAt(0, 0).TunnelLength);
    }
}
