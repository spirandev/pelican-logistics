using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

/// <summary>Montagem padrão: esteira em (0,0) alimentando a entrada em (1,0), todos virados para o leste.</summary>
public class UndergroundBeltNodeTests
{
    /// <summary>Ticks para atravessar um tile da Subterrânea (entrada, cada tile do túnel e saída).</summary>
    private const int TileTicks = Balance.End / Balance.UndergroundSpeed;

    /// <summary>Esteira (0,0) → entrada (1,0) → vão de 'gap' tiles → saída → esteira depois da saída.</summary>
    private static GridBuilder Line(int gap)
    {
        int exitX = 2 + gap;
        return new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(exitX, 0, Direction.East)
            .Belt(exitX + 1, 0, Direction.East);
    }

    [Fact]
    public void Item_CrossesFifteenTicksPerTile_EntranceTunnelAndExit()
    {
        var item = new FakeItem("wood", 3);
        var g = Line(2).Item(0, 0, item, Balance.End);

        // Tick 1: entra na entrada. Depois: entrada, 2 tiles de túnel e saída, 15 ticks cada.
        g.Run(1 + (4 * TileTicks) - 1);
        Assert.Same(item, g.UndergroundAt(4, 0).Slot.Item);

        g.Run(1);
        Assert.True(g.UndergroundAt(4, 0).Slot.IsEmpty);
        Assert.Same(item, g.BeltAt(5, 0).Slot.Item);
        Assert.Equal(Direction.West, g.BeltAt(5, 0).Slot.EntrySide);
    }

    [Fact]
    public void Item_PassesUnderAnyBlockInTheGap_BlocksAreUntouched()
    {
        var item = new FakeItem("wood", 3);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Splitter(2, 0)
            .Inserter(3, 0, Direction.North)
            .Underground(4, 0, Direction.East)
            .Belt(5, 0, Direction.East)
            .Item(0, 0, item, Balance.End);

        g.Run(200);

        Assert.Same(item, g.BeltAt(5, 0).Slot.Item);
        Assert.True(g.SplitterAt(2, 0).Slot.IsEmpty);
        Assert.True(g.InserterAt(3, 0).Hand.IsEmpty);
        Assert.Empty(g.SplitterAt(2, 0).Inputs);
        Assert.Empty(g.SplitterAt(2, 0).Outputs);
    }

    [Fact]
    public void Entrance_WithoutPartner_ItemStopsAtTheEnd()
    {
        var item = new FakeItem("wood", 3);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Item(0, 0, item, Balance.End);

        g.Run(100);

        UndergroundBeltNode entrance = g.UndergroundAt(1, 0);
        Assert.Equal(UndergroundRole.Entrance, entrance.Role);
        Assert.Same(item, entrance.Slot.Item);
        Assert.Equal(Balance.End, entrance.Slot.Progress);
        Assert.True(entrance.Slot.Blocked);
        Assert.All(entrance.Tunnel, s => Assert.True(s.IsEmpty));
    }

    [Fact]
    public void Entrance_ReceivesOnlyFromBehind_WhenEmpty_NeverMerges()
    {
        var entrance = new UndergroundBeltNode(new TilePos(0, 0), Direction.East, UndergroundRole.Entrance);
        var ctx = new TickContext(new LogisticsGrid(), new FakeEndpoints());

        Assert.False(entrance.CanReceiveFrom(Direction.North));
        Assert.Equal(0, entrance.Accept(Direction.South, new FakeItem("wood", 1), ctx));
        Assert.Equal(5, entrance.Accept(Direction.West, new FakeItem("wood", 5), ctx));
        Assert.False(entrance.CanAccept(Direction.West, new FakeItem("wood", 1)));
        Assert.Equal(0, entrance.Accept(Direction.West, new FakeItem("wood", 1), ctx));
        Assert.Equal(5, entrance.Slot.Item!.Count);
    }

    [Fact]
    public void Exit_ReceivesNothingFromNeighbors_OnlyItsPartner()
    {
        var exit = new UndergroundBeltNode(new TilePos(0, 0), Direction.East, UndergroundRole.Exit);
        var ctx = new TickContext(new LogisticsGrid(), new FakeEndpoints());

        foreach (Direction side in DirectionExtensions.All)
        {
            Assert.False(exit.CanReceiveFrom(side));
            Assert.Equal(0, exit.Accept(side, new FakeItem("wood", 1), ctx));
        }

        Assert.Empty(exit.Tunnel);
        Assert.Single(exit.Slots);
        Assert.True(exit.ProvidesTo(new TilePos(1, 0)));
        Assert.False(exit.ProvidesTo(new TilePos(-1, 0)));
    }

    [Fact]
    public void FullTunnel_EntranceBlocks_FreeingTheExit_TheQueueMovesTogether()
    {
        // Entrada (1,0), saída (5,0): túnel de 3 tiles; nada depois da saída.
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(5, 0, Direction.East);
        FakeItem[] items = FillAll(g, "a", "b", "c", "d", "e", "f");

        UndergroundBeltNode entrance = g.UndergroundAt(1, 0);
        UndergroundBeltNode exit = g.UndergroundAt(5, 0);
        Assert.Equal(3, entrance.TunnelLength);
        Assert.Same(items[0], exit.Slot.Item);
        Assert.Same(items[1], entrance.Tunnel[2].Item);
        Assert.Same(items[2], entrance.Tunnel[1].Item);
        Assert.Same(items[3], entrance.Tunnel[0].Item);
        Assert.Same(items[4], entrance.Slot.Item);
        Assert.Same(items[5], g.BeltAt(0, 0).Slot.Item);
        Assert.True(entrance.Slot.Blocked);

        exit.Slot.Clear();
        g.Run(1);

        Assert.Same(items[1], exit.Slot.Item);
        Assert.Same(items[2], entrance.Tunnel[2].Item);
        Assert.Same(items[3], entrance.Tunnel[1].Item);
        Assert.Same(items[4], entrance.Tunnel[0].Item);
        Assert.Same(items[5], entrance.Slot.Item);
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Equal(0, exit.Slot.Progress);
    }

    [Fact]
    public void RemovingTheEntrance_ReturnsItsItemAndTheWholeTunnel()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(5, 0, Direction.East);
        FakeItem[] items = FillAll(g, "a", "b", "c", "d", "e");

        RemovalResult result = g.Grid.Remove(new TilePos(1, 0));

        Assert.Equal(new ILogisticsItem[] { items[4], items[3], items[2], items[1] }, result.Items);
        Assert.Same(items[0], g.UndergroundAt(5, 0).Slot.Item);
    }

    [Fact]
    public void RemovingTheExit_ItemsInTheTunnelWait_NewExitFurtherAway_TheyContinue()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(3, 0, Direction.East);
        FakeItem[] items = FillAll(g, "a", "b", "c");
        Assert.Same(items[1], g.UndergroundAt(1, 0).Tunnel[0].Item);

        g.Grid.Remove(new TilePos(3, 0));
        g.Run(100);
        Assert.Same(items[1], g.UndergroundAt(1, 0).Tunnel[0].Item);
        Assert.Same(items[2], g.UndergroundAt(1, 0).Slot.Item);

        g.Underground(6, 0, Direction.East).Belt(7, 0, Direction.East).Belt(8, 0, Direction.East);
        g.Run(300);

        Assert.Same(items[1], g.BeltAt(8, 0).Slot.Item);
        Assert.Same(items[2], g.BeltAt(7, 0).Slot.Item);
    }

    [Fact]
    public void ExitReplacedCloser_ItemsBeyondTheNewGapGoStraightToTheExit_NothingIsLost()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(7, 0, Direction.East);
        FillAll(g, "a", "b", "c", "d", "e", "f", "g");
        Assert.Equal(5, g.UndergroundAt(1, 0).TunnelLength);

        g.Grid.Remove(new TilePos(7, 0));
        g.Underground(3, 0, Direction.East);
        for (int x = 4; x <= 12; x++)
            g.Belt(x, 0, Direction.East);
        g.Run(1000);

        // a saiu com a saída removida; as outras 6 pilhas chegam todas às esteiras depois da nova saída.
        Assert.Equal(1, g.UndergroundAt(1, 0).TunnelLength);
        var arrived = Enumerable.Range(4, 9).Select(x => g.BeltAt(x, 0).Slot.Item).Where(i => i != null).Select(i => i!.QualifiedItemId).ToList();
        Assert.Equal(new[] { "g", "f", "e", "d", "c", "b" }, arrived);
    }

    [Fact]
    public void TwoParallelUndergrounds_DoNotInterfere()
    {
        var wood = new FakeItem("wood", 1);
        var stone = new FakeItem("stone", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East).Underground(1, 0, Direction.East).Underground(4, 0, Direction.East).Belt(5, 0, Direction.East)
            .Belt(0, 1, Direction.East).Underground(1, 1, Direction.East).Underground(4, 1, Direction.East).Belt(5, 1, Direction.East)
            .Item(0, 0, wood, Balance.End)
            .Item(0, 1, stone, Balance.End);

        g.Run(200);

        Assert.Same(g.UndergroundAt(4, 0), g.UndergroundAt(1, 0).Partner);
        Assert.Same(g.UndergroundAt(4, 1), g.UndergroundAt(1, 1).Partner);
        Assert.Same(wood, g.BeltAt(5, 0).Slot.Item);
        Assert.Same(stone, g.BeltAt(5, 1).Slot.Item);
    }

    // Com outros blocos.

    [Fact]
    public void Exit_FeedsACurve_AndIsAnInputOfTheInserterSide()
    {
        var target = new FakeChest();
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(3, 0, Direction.East)
            .Belt(4, 0, Direction.South)                // sem nada atrás: curva saindo da saída
            .Underground(4, 1, Direction.South)
            .Underground(4, 3, Direction.South)         // saída apontando para a lateral de um Inseridor
            .Inserter(4, 4, Direction.East)
            .Chest(5, 4, target)
            .Item(0, 0, new FakeItem("wood", 4), Balance.End);

        g.Run(400);

        Assert.True(g.BeltAt(4, 0).IsCurve);
        Assert.Contains(Direction.North, g.InserterAt(4, 4).Inputs);
        Assert.Equal(4, target.CountTotal());
    }

    [Fact]
    public void Inserter_DeliversIntoTheEntrance_ButNotIntoTheExit()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Underground(2, 0, Direction.East)
            .Underground(4, 0, Direction.East)
            .Belt(5, 0, Direction.East)
            .Chest(0, 2, new FakeChest(36, new FakeItem("stone", 5)))
            .Inserter(1, 2, Direction.East)
            .Underground(2, 2, Direction.East, UndergroundRole.Exit);

        g.Run(30);

        Assert.Equal(4, chest.CountTotal());
        Assert.True(g.InserterAt(1, 2).Hand.IsEmpty);
        Assert.True(g.UndergroundAt(2, 2).Slot.IsEmpty);
    }

    [Fact]
    public void Splitter_ExitIsAnInput_EntranceIsAnOutput()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Underground(1, 0, Direction.East)
            .Underground(3, 0, Direction.East)       // saída apontando para o Divisor: entrada
            .Splitter(4, 0)
            .Underground(5, 0, Direction.East)       // entrada com a traseira virada para o Divisor: saída
            .Underground(7, 0, Direction.East)
            .Belt(8, 0, Direction.East)
            .Underground(4, -1, Direction.North, UndergroundRole.Exit) // saída com a traseira virada para o Divisor: nada
            .Item(0, 0, new FakeItem("wood", 2), Balance.End);

        g.Run(300);

        SplitterNode splitter = g.SplitterAt(4, 0);
        Assert.Equal(new[] { Direction.West }, splitter.Inputs);
        Assert.Equal(new[] { Direction.East }, splitter.Outputs);
        Assert.Equal(2, g.BeltAt(8, 0).Slot.Item!.Count);
    }

    /// <summary>
    /// Alimenta a esteira (0,0) com uma pilha de 1 de cada ID, esperando a esteira esvaziar entre uma e outra, e roda
    /// até tudo parar. Devolve as pilhas na ordem.
    /// </summary>
    private static FakeItem[] FillAll(GridBuilder g, params string[] ids)
    {
        FakeItem[] items = ids.Select(id => new FakeItem(id, 1)).ToArray();
        foreach (FakeItem item in items)
        {
            for (int tick = 0; tick < 500 && !g.BeltAt(0, 0).Slot.IsEmpty; tick++)
                g.Run(1);
            g.Item(0, 0, item, Balance.End);
        }

        g.Run(300);
        return items;
    }
}
