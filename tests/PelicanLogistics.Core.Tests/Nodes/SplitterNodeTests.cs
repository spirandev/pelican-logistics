using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

/// <summary>
/// Montagem padrão: Divisor em (5,5). Vizinhos: norte (5,4), leste (6,5), sul (5,6), oeste (4,5).
/// </summary>
public class SplitterNodeTests
{
    private const string Wood = "(O)388";
    private const string Stone = "(O)390";
    private const string Clay = "(O)330";

    /// <summary>Ticks do item no Divisor, da entrada até distribuir.</summary>
    private const int SplitTicks = Balance.End / Balance.SplitterSpeed;

    /// <summary>Do item parado no fim da entrada até chegar às saídas: 1 tick para puxar e a travessia.</summary>
    private const int PassTicks = 1 + SplitTicks;

    /// <summary>Entrada a oeste e três saídas sem filtro (norte, leste, sul), sem nada depois delas.</summary>
    private static GridBuilder ThreeOutputs()
    {
        return new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Belt(5, 4, Direction.North)
            .Belt(6, 5, Direction.East)
            .Belt(5, 6, Direction.South);
    }

    private static GridBuilder Feed(GridBuilder g, ILogisticsItem item)
    {
        return g.Item(4, 5, item, Balance.End);
    }

    // Portas.

    [Fact]
    public void Ports_BeltIntoIsInput_BeltOutIsOutput_SideBeltWithBeltBehindIsNothing_SideBeltAloneCurvesOut()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)   // oeste: aponta para o Divisor → entrada
            .Belt(6, 5, Direction.East)   // leste: sai do Divisor → saída
            .Belt(4, 4, Direction.East)
            .Belt(5, 4, Direction.East)   // norte: passa ao lado, com esteira atrás → nada
            .Belt(5, 6, Direction.East);  // sul: nada atrás, Divisor na lateral → curva saindo dele

        Topology.Recompute(g.Grid);

        SplitterNode splitter = g.SplitterAt(5, 5);
        Assert.Equal(new[] { Direction.West }, splitter.Inputs);
        Assert.Equal(new[] { Direction.East, Direction.South }, splitter.Outputs);
        Assert.False(g.BeltAt(5, 4).IsCurve);
        Assert.True(g.BeltAt(5, 6).IsCurve);
        Assert.Equal(Direction.North, g.BeltAt(5, 6).InputSide);
    }

    [Fact]
    public void Ports_FiltersConnectLikeBelts()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Filter(4, 5, Direction.East, Wood)   // entrada
            .Filter(5, 4, Direction.North, Wood)  // saída
            .Filter(6, 5, Direction.North, Wood); // de lado: o filtro não faz curva → nada

        Topology.Recompute(g.Grid);

        Assert.Equal(new[] { Direction.West }, g.SplitterAt(5, 5).Inputs);
        Assert.Equal(new[] { Direction.North }, g.SplitterAt(5, 5).Outputs);
    }

    [Fact]
    public void Ports_SplitterAndInserterNeighbors_DoNotConnect()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Splitter(4, 5)                       // Divisor vizinho
            .Inserter(6, 5, Direction.East)       // Inseridor com o Divisor atrás
            .Inserter(5, 4, Direction.East)       // Inseridor com o Divisor na lateral (sul)
            .Inserter(5, 6, Direction.North);     // Inseridor apontando para o Divisor

        Topology.Recompute(g.Grid);

        Assert.Empty(g.SplitterAt(5, 5).Inputs);
        Assert.Empty(g.SplitterAt(5, 5).Outputs);
        Assert.Empty(g.SplitterAt(4, 5).Outputs);
        Assert.Equal(new[] { Direction.West }, g.InserterAt(5, 4).Inputs);
    }

    [Fact]
    public void Splitter_IsNeverPushedInto_ItPulls()
    {
        var g = new GridBuilder().Splitter(5, 5);
        var ctx = new TickContext(g.Grid, g.Endpoints);

        Assert.Equal(0, g.SplitterAt(5, 5).Accept(Direction.West, new FakeItem(Wood, 1), ctx));
        Assert.False(g.SplitterAt(5, 5).CanAccept(Direction.West, new FakeItem(Wood, 1)));
    }

    [Fact]
    public void Splitter_ItemCrossesAtSplitterSpeed_FromTheEntrySide()
    {
        var wood = new FakeItem(Wood, 1);
        GridBuilder g = Feed(ThreeOutputs(), wood);

        g.Run(1);
        Assert.Same(wood, g.SplitterAt(5, 5).Slot.Item);
        Assert.Equal(Direction.West, g.SplitterAt(5, 5).Slot.EntrySide);
        Assert.Equal(Direction.West, g.SplitterAt(5, 5).LastInput);

        g.Run(SplitTicks - 1);
        Assert.Equal(Balance.End - Balance.SplitterSpeed, g.SplitterAt(5, 5).Slot.Progress);

        g.Run(1);
        Assert.True(g.SplitterAt(5, 5).Slot.IsEmpty);
        Assert.Same(wood, g.BeltAt(5, 4).Slot.Item);
    }

    // Regra 2: divisão igual.

    [Fact]
    public void Rule2_SevenItemsThreeFreeOutputs_ThreeTwoTwo_ClockwiseFromNorth()
    {
        var wood = new FakeItem(Wood, 7);
        GridBuilder g = Feed(ThreeOutputs(), wood);

        g.Run(PassTicks);

        Assert.Equal(3, g.BeltAt(5, 4).Slot.Item!.Count);
        Assert.Equal(2, g.BeltAt(6, 5).Slot.Item!.Count);
        Assert.Equal(2, g.BeltAt(5, 6).Slot.Item!.Count);
        Assert.Same(wood, g.BeltAt(5, 6).Slot.Item);
        Assert.Equal(Direction.West, g.BeltAt(6, 5).Slot.EntrySide);
        Assert.Equal(Direction.South, g.SplitterAt(5, 5).LastOutput);
        Assert.True(g.SplitterAt(5, 5).Slot.IsEmpty);
    }

    [Fact]
    public void Rule2_OccupiedOutputIsSkipped_SplitsAmongTheFreeOnes()
    {
        GridBuilder g = ThreeOutputs().Item(6, 5, new FakeItem(Stone, 1));
        Feed(g, new FakeItem(Wood, 7));

        g.Run(PassTicks);

        Assert.Equal(4, g.BeltAt(5, 4).Slot.Item!.Count);
        Assert.Equal(Stone, g.BeltAt(6, 5).Slot.Item!.QualifiedItemId);
        Assert.Equal(3, g.BeltAt(5, 6).Slot.Item!.Count);
    }

    // Regra 3: pilha menor que o número de saídas.

    [Fact]
    public void Rule3_SingleItemStacks_GoOneToEachOutput_Clockwise()
    {
        var a = new FakeItem("a", 1);
        var b = new FakeItem("b", 1);
        var c = new FakeItem("c", 1);
        GridBuilder g = ThreeOutputs();

        Feed(g, a).Run(PassTicks);
        Feed(g, b).Run(PassTicks);
        Feed(g, c).Run(PassTicks);

        Assert.Same(a, g.BeltAt(5, 4).Slot.Item);
        Assert.Same(b, g.BeltAt(6, 5).Slot.Item);
        Assert.Same(c, g.BeltAt(5, 6).Slot.Item);
    }

    [Fact]
    public void Rule3_TwoItemsThreeOutputs_OneEachToTheFirstTwo()
    {
        GridBuilder g = Feed(ThreeOutputs(), new FakeItem(Wood, 2));

        g.Run(PassTicks);

        Assert.Equal(1, g.BeltAt(5, 4).Slot.Item!.Count);
        Assert.Equal(1, g.BeltAt(6, 5).Slot.Item!.Count);
        Assert.True(g.BeltAt(5, 6).Slot.IsEmpty);
        Assert.Equal(Direction.East, g.SplitterAt(5, 5).LastOutput);
    }

    [Fact]
    public void RoundRobin_StartsAtNorth_AndContinuesAfterTheLastOutputUsed()
    {
        // Entrada ao norte; saídas a leste, sul e oeste. A primeira é a primeira saída depois do norte: leste.
        var items = new[] { new FakeItem("a", 1), new FakeItem("b", 1), new FakeItem("c", 1), new FakeItem("d", 1) };
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(5, 4, Direction.South)
            .Belt(6, 5, Direction.East)
            .Belt(5, 6, Direction.South)
            .Belt(4, 5, Direction.West);

        var received = new List<(string Id, Direction Side)>();
        foreach (FakeItem item in items)
        {
            g.Item(5, 4, item, Balance.End).Run(PassTicks);
            foreach ((Direction side, int x, int y) in new[] { (Direction.East, 6, 5), (Direction.South, 5, 6), (Direction.West, 4, 5) })
            {
                if (g.BeltAt(x, y).Slot.Clear() is { } got)
                    received.Add((got.QualifiedItemId, side));
            }
        }

        Assert.Equal(new[] { ("a", Direction.East), ("b", Direction.South), ("c", Direction.West), ("d", Direction.East) }, received);
    }

    // Regra 1: prioridade para filtros.

    /// <summary>Saídas: norte = Filtro (madeira), leste = Filtro (pedra), sul = esteira sem filtro. Depois dos filtros, esteiras.</summary>
    private static GridBuilder FilteredOutputs()
    {
        return new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Filter(5, 4, Direction.North, Wood)
            .Belt(5, 3, Direction.North)
            .Filter(6, 5, Direction.East, Stone)
            .Belt(7, 5, Direction.East)
            .Belt(5, 6, Direction.South);
    }

    [Fact]
    public void Rule1_ItemGoesWholeToTheFilterThatAcceptsIt_OthersToTheUnfilteredOutput()
    {
        var wood = new FakeItem(Wood, 4);
        var stone = new FakeItem(Stone, 3);
        var clay = new FakeItem(Clay, 5);
        GridBuilder g = FilteredOutputs();

        Feed(g, wood).Run(PassTicks);
        Feed(g, stone).Run(PassTicks);
        Feed(g, clay).Run(PassTicks);
        g.Run(SplitTicks);

        Assert.Same(wood, g.BeltAt(5, 3).Slot.Item);
        Assert.Equal(4, wood.Count);
        Assert.Same(stone, g.BeltAt(7, 5).Slot.Item);
        Assert.Equal(3, stone.Count);
        Assert.Same(clay, g.BeltAt(5, 6).Slot.Item);
        Assert.Equal(5, clay.Count);
    }

    [Fact]
    public void Rule1_TwoOutputsWithTheSameFilter_SplitBetweenThem()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Filter(5, 4, Direction.North, Wood)
            .Filter(6, 5, Direction.East, Wood)
            .Belt(5, 6, Direction.South);
        Feed(g, new FakeItem(Wood, 5));

        g.Run(PassTicks);

        Assert.Equal(3, g.FilterAt(5, 4).Slot.Item!.Count);
        Assert.Equal(2, g.FilterAt(6, 5).Slot.Item!.Count);
        Assert.True(g.BeltAt(5, 6).Slot.IsEmpty);
    }

    // Regra 4: saída bloqueada.

    [Fact]
    public void Rule4_FilteredOutputBlocked_WoodGoesToTheUnfilteredOutputAsOverflow()
    {
        // O filtro de madeira não tem saída e já está parado com uma madeira (ex.: baú de madeira cheio).
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Filter(5, 4, Direction.North, Wood)
            .Belt(5, 6, Direction.South);
        g.FilterAt(5, 4).Slot.Put(new FakeItem(Wood, 1), Direction.South);
        g.Run(SplitTicks);
        Assert.True(g.FilterAt(5, 4).Slot.Blocked);

        var wood = new FakeItem(Wood, 4);
        Feed(g, wood).Run(PassTicks);

        Assert.Same(wood, g.BeltAt(5, 6).Slot.Item);
        Assert.Equal(4, wood.Count);
        Assert.Equal(1, g.FilterAt(5, 4).Slot.Item!.Count);
    }

    [Fact]
    public void Rule4_FilterThatDoesNotAcceptIsNeverACandidate_EvenWhenFree()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Filter(5, 4, Direction.North, Stone)
            .Belt(6, 5, Direction.East);
        Feed(g, new FakeItem(Wood, 6));

        g.Run(PassTicks);

        Assert.True(g.FilterAt(5, 4).Slot.IsEmpty);
        Assert.Equal(6, g.BeltAt(6, 5).Slot.Item!.Count);
    }

    // Regra 5: sem destino.

    [Fact]
    public void Rule5_OnlyFiltersThatRefuseTheItem_SplitterJams_ThenMovesWhenAValidOutputAppears()
    {
        var wood = new FakeItem(Wood, 3);
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Filter(5, 4, Direction.North, Stone)
            .Filter(6, 5, Direction.East, Stone);
        Feed(g, wood).Run(PassTicks + 100);

        Assert.Same(wood, g.SplitterAt(5, 5).Slot.Item);
        Assert.True(g.SplitterAt(5, 5).Slot.Blocked);

        // A esteira de trás continua acumulando: o Divisor não puxa enquanto está travado.
        var next = new FakeItem(Clay, 1);
        Feed(g, next).Run(100);
        Assert.Same(next, g.BeltAt(4, 5).Slot.Item);

        g.Belt(5, 6, Direction.South).Run(1);

        Assert.Same(wood, g.BeltAt(5, 6).Slot.Item);
        Assert.Equal(3, wood.Count);
    }

    // Regra 6: entradas.

    [Fact]
    public void Rule6_TwoInputsWithContinuousFlow_Alternate_StartingAtNorth()
    {
        // Entradas a oeste ("w") e ao norte ("n"), sempre cheias; saída a leste, esvaziada a cada tick.
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Belt(5, 4, Direction.South)
            .Belt(6, 5, Direction.East);

        var received = new List<string>();
        for (int tick = 0; tick < 20 * PassTicks; tick++)
        {
            if (g.BeltAt(4, 5).Slot.IsEmpty)
                g.Item(4, 5, new FakeItem("w", 1), Balance.End);
            if (g.BeltAt(5, 4).Slot.IsEmpty)
                g.Item(5, 4, new FakeItem("n", 1), Balance.End);

            g.Run(1);
            if (g.BeltAt(6, 5).Slot.Clear() is { } got)
                received.Add(got.QualifiedItemId);
        }

        Assert.True(received.Count >= 18);
        for (int i = 0; i < received.Count; i++)
            Assert.Equal(i % 2 == 0 ? "n" : "w", received[i]);
    }

    [Fact]
    public void Rule6_InputRoundRobin_SkipsInputsWithNothingReady()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Belt(5, 4, Direction.South)
            .Belt(5, 6, Direction.North)
            .Belt(6, 5, Direction.East);
        var west = new FakeItem("w", 1);
        g.Item(4, 5, west, Balance.End);

        g.Run(1);

        Assert.Same(west, g.SplitterAt(5, 5).Slot.Item);
        Assert.Equal(new[] { Direction.North, Direction.South, Direction.West }, g.SplitterAt(5, 5).Inputs);
        Assert.Equal(Direction.West, g.SplitterAt(5, 5).LastInput);
    }

    [Fact]
    public void Junction_TwoInputsOneOutput_MergesBothFlowsIntoOneChest_NothingLostOrCreated()
    {
        var chestA = new FakeChest(36, new FakeItem(Wood, 6));
        var chestB = new FakeChest(36, new FakeItem(Stone, 5));
        var chestC = new FakeChest();
        var g = new GridBuilder()
            .Chest(2, 5, chestA)
            .Inserter(3, 5, Direction.East)
            .Belt(4, 5, Direction.East)
            .Chest(5, 8, chestB)
            .Inserter(5, 7, Direction.North)
            .Belt(5, 6, Direction.North)
            .Splitter(5, 5)
            .Belt(6, 5, Direction.East)
            .Inserter(7, 5, Direction.East)
            .Chest(8, 5, chestC);

        int total = chestA.CountTotal() + chestB.CountTotal();
        for (int tick = 0; tick < 3000; tick++)
        {
            g.Run(1);
            Assert.Equal(total, chestA.CountTotal() + chestB.CountTotal() + g.CountInTransit() + chestC.CountTotal());
        }

        Assert.Equal(6, chestC.CountById(Wood));
        Assert.Equal(5, chestC.CountById(Stone));
    }

    [Fact]
    public void Splitter_RemovedWithItemInside_ItemComesInRemovalResult()
    {
        var wood = new FakeItem(Wood, 2);
        GridBuilder g = Feed(new GridBuilder().Splitter(5, 5).Belt(4, 5, Direction.East), wood);
        g.Run(5);

        RemovalResult result = g.Grid.Remove(new TilePos(5, 5));

        Assert.Same(wood, Assert.Single(result.Items));
    }
}
