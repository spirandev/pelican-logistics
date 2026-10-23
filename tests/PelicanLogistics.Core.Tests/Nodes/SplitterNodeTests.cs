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
    public void Ports_InserterPointingAtIsInput_InserterWithBackToItIsOutput_SideInserterAndSplitterAreNothing()
    {
        var g = new GridBuilder()
            .Splitter(5, 5)
            .Splitter(4, 5)                       // oeste: Divisor vizinho → nada
            .Inserter(6, 5, Direction.East)       // leste: traseira virada para o Divisor → saída
            .Inserter(5, 4, Direction.East)       // norte: Divisor na lateral do Inseridor → nada
            .Inserter(5, 6, Direction.North);     // sul: aponta para o Divisor → entrada

        Topology.Recompute(g.Grid);

        Assert.Equal(new[] { Direction.South }, g.SplitterAt(5, 5).Inputs);
        Assert.Equal(new[] { Direction.East }, g.SplitterAt(5, 5).Outputs);
        Assert.Empty(g.SplitterAt(4, 5).Inputs);
        Assert.Empty(g.SplitterAt(4, 5).Outputs);
        Assert.Equal(new[] { Direction.West }, g.InserterAt(5, 4).Inputs);
    }

    [Fact]
    public void Splitter_IsNeverPushedInto_ItPulls()
    {
        var g = new GridBuilder().Splitter(5, 5).Belt(4, 5, Direction.East);
        Topology.Recompute(g.Grid);
        var ctx = new TickContext(g.Grid, g.Endpoints);

        // Aceitaria puxar pela entrada (oeste), mas nunca recebe empurrado; por um lado que não é entrada, nem isso.
        Assert.Equal(0, g.SplitterAt(5, 5).Accept(Direction.West, new FakeItem(Wood, 1), ctx));
        Assert.True(g.SplitterAt(5, 5).CanAccept(Direction.West, new FakeItem(Wood, 1)));
        Assert.False(g.SplitterAt(5, 5).CanAccept(Direction.North, new FakeItem(Wood, 1)));
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

    // Inseridores como portas.

    [Fact]
    public void InserterOutput_ChestToInserterToBeltToSplitterToInserterToFurnace_ProducesBars()
    {
        // Baú → Inseridor → esteira → Divisor → Inseridor (Automático) → fornalha → Inseridor (Produto) → baú.
        // Como numa esteira só, o carvão precisa vir entre os minérios: 6 minérios seguidos travariam a linha.
        var source = new FakeChest(
            36,
            new FakeItem(FakeMachine.Ids.CopperOre, 5),
            new FakeItem(FakeMachine.Ids.Coal, 1),
            new FakeItem(FakeMachine.Ids.CopperOre, 5),
            new FakeItem(FakeMachine.Ids.Coal, 1));
        var bars = new FakeChest();
        var furnace = FakeMachine.Furnace(processingTicks: 300);
        var g = new GridBuilder()
            .Chest(0, 5, source)
            .Inserter(1, 5, Direction.East)
            .Belt(2, 5, Direction.East)
            .Splitter(3, 5)
            .Inserter(4, 5, Direction.East, InserterSlotMode.Auto)
            .Machine(5, 5, furnace)
            .Inserter(5, 6, Direction.South, PickupSource.Product)
            .Chest(5, 7, bars);

        g.Run(5000);

        Assert.Equal(new[] { Direction.East }, g.SplitterAt(3, 5).Outputs);
        Assert.Equal(2, furnace.Starts.Count);
        Assert.Equal(2, bars.CountById(FakeMachine.Ids.CopperBar));
        Assert.Equal(0, source.CountTotal());
        Assert.Equal(0, g.CountInTransit());
    }

    [Fact]
    public void InserterOutput_ChestToInserterToFilterToSplitterToInserterToFurnace_OnlyOreEntersTheFurnace()
    {
        var source = new FakeChest(36, new FakeItem(Wood, 3), new FakeItem(FakeMachine.Ids.CopperOre, 5));
        var furnace = FakeMachine.Furnace();
        var g = new GridBuilder()
            .Chest(0, 5, source)
            .Inserter(1, 5, Direction.East)
            .Filter(2, 5, Direction.East, FakeMachine.Ids.CopperOre)
            .Splitter(3, 5)
            .Inserter(4, 5, Direction.East)
            .Machine(5, 5, furnace);

        g.Run(2000);

        Assert.Equal(5, g.SlotsAt(5, 5)!.Main!.Count);
        Assert.Equal(3, source.CountById(Wood));
        Assert.Equal(0, g.CountInTransit());
    }

    [Fact]
    public void InserterOutput_TakesOnlyItsTierAmount_RestStaysInTheSplitter_ThenGoesOut()
    {
        // A esteira de entrada traz uma pilha de 3; o Inseridor da saída leva 1 por ciclo.
        var target = new FakeChest();
        var g = new GridBuilder()
            .Belt(4, 5, Direction.East)
            .Splitter(5, 5)
            .Inserter(6, 5, Direction.East)
            .Chest(7, 5, target);
        Feed(g, new FakeItem(Wood, 3));

        g.Run(PassTicks);
        Assert.Equal(2, g.SplitterAt(5, 5).Slot.Item!.Count);
        Assert.True(g.SplitterAt(5, 5).Slot.Blocked);
        Assert.Equal(1, g.InserterAt(6, 5).Hand.Item!.Count);
        Assert.Equal(Direction.West, g.InserterAt(6, 5).Hand.EntrySide);

        g.Run(200);
        Assert.Equal(3, target.CountById(Wood));
        Assert.True(g.SplitterAt(5, 5).Slot.IsEmpty);
    }

    [Fact]
    public void InserterOutput_DestinationRefusesTheItem_InserterIsNotACandidate()
    {
        // O Inseridor de saída abastece uma fornalha, que não aceita madeira: a madeira vai para a esteira.
        var furnace = FakeMachine.Furnace();
        var g = new GridBuilder()
            .Belt(4, 5, Direction.East)
            .Splitter(5, 5)
            .Inserter(5, 4, Direction.North)
            .Machine(5, 3, furnace)
            .Belt(6, 5, Direction.East);
        Feed(g, new FakeItem(Wood, 2));

        g.Run(PassTicks);

        Assert.True(g.InserterAt(5, 4).Hand.IsEmpty);
        Assert.Equal(2, g.BeltAt(6, 5).Slot.Item!.Count);
    }

    [Fact]
    public void InserterInputAndOutputs_ChestToChests_Alternate_NothingLostOrCreated()
    {
        // Baú → Inseridor → Divisor → Inseridores ao norte e ao sul → baús.
        var source = new FakeChest(36, new FakeItem(Wood, 6));
        var north = new FakeChest();
        var south = new FakeChest();
        var g = new GridBuilder()
            .Chest(3, 5, source)
            .Inserter(4, 5, Direction.East)
            .Splitter(5, 5)
            .Inserter(5, 4, Direction.North)
            .Chest(5, 3, north)
            .Inserter(5, 6, Direction.South)
            .Chest(5, 7, south);

        int total = source.CountTotal();
        for (int tick = 0; tick < 1000; tick++)
        {
            g.Run(1);
            Assert.Equal(total, source.CountTotal() + g.CountInTransit() + north.CountTotal() + south.CountTotal());
        }

        Assert.Equal(new[] { Direction.West }, g.SplitterAt(5, 5).Inputs);
        Assert.Equal(new[] { Direction.North, Direction.South }, g.SplitterAt(5, 5).Outputs);
        Assert.Equal(3, north.CountById(Wood));
        Assert.Equal(3, south.CountById(Wood));
    }

    [Fact]
    public void InserterInput_SplitterPullsFromTheHand_ThenTheArmReturnsEmpty()
    {
        // Sem saídas: o Divisor fica com o primeiro item, e o Inseridor não pega outro enquanto ele está ocupado.
        var source = new FakeChest(36, new FakeItem(Wood, 5));
        var g = new GridBuilder()
            .Chest(3, 5, source)
            .Inserter(4, 5, Direction.East)
            .Splitter(5, 5);
        int travel = Balance.End / Balance.InserterSpeed;

        // Pega no tick 1, chega no fim no tick 1 + 15 e o Divisor puxa no tick seguinte.
        g.Run(1 + travel + 1);
        Assert.Equal(1, g.SplitterAt(5, 5).Slot.Item!.Count);
        Assert.True(g.InserterAt(4, 5).Hand.IsEmpty);
        Assert.Equal(Balance.InserterReturnTicks, g.InserterAt(4, 5).Cooldown);

        g.Run(200);
        Assert.Equal(1, g.SplitterAt(5, 5).Slot.Item!.Count);
        Assert.True(g.SplitterAt(5, 5).Slot.Blocked);
        Assert.True(g.InserterAt(4, 5).Hand.IsEmpty);
        Assert.Equal(4, source.CountById(Wood));
    }

    // Divisor Subterrâneo (Fase 6c).

    [Fact]
    public void Underground_ClassicSorter_EachItemEndsInTheRightChest()
    {
        // Baú → Inseridor → entrada → Esteira Subterrânea → Divisor Subterrâneo. Saídas: norte = Filtro (madeira), leste =
        // Filtro (pedra), sul = Esteira Subterrânea (resto). Cada uma: → saída de Túnel → Inseridor → baú.
        var source = new FakeChest(36, new FakeItem(Wood, 3), new FakeItem(Clay, 2), new FakeItem(Stone, 4), new FakeItem(Wood, 1));
        var woodChest = new FakeChest();
        var stoneChest = new FakeChest();
        var restChest = new FakeChest();
        var g = new GridBuilder()
            .Chest(1, 5, source)
            .Inserter(2, 5, Direction.East, TransferTier.Stack)
            .Tunnel(3, 5, Direction.East)
            .Segment(4, 5, Direction.East)
            .UndergroundSplitter(5, 5)
            .UndergroundFilter(5, 4, Direction.North, Wood)
            .Segment(5, 3, Direction.North)
            .Tunnel(5, 2, Direction.North)
            .Inserter(5, 1, Direction.North, TransferTier.Stack)
            .Chest(5, 0, woodChest)
            .UndergroundFilter(6, 5, Direction.East, Stone)
            .Segment(7, 5, Direction.East)
            .Tunnel(8, 5, Direction.East)
            .Inserter(9, 5, Direction.East, TransferTier.Stack)
            .Chest(10, 5, stoneChest)
            .Segments(5, 6, Direction.South, 2)
            .Tunnel(5, 8, Direction.South)
            .Inserter(5, 9, Direction.South, TransferTier.Stack)
            .Chest(5, 10, restChest);

        g.Run(1500);

        SplitterNode splitter = g.UndergroundSplitterAt(5, 5);
        Assert.Equal(new[] { Direction.West }, splitter.Inputs);
        Assert.Equal(new[] { Direction.North, Direction.East, Direction.South }, splitter.Outputs);
        Assert.Equal(0, source.CountTotal());
        Assert.Equal(0, g.CountInTransit());
        Assert.Equal(4, woodChest.CountById(Wood));
        Assert.Equal(4, woodChest.CountTotal());
        Assert.Equal(4, stoneChest.CountById(Stone));
        Assert.Equal(4, stoneChest.CountTotal());
        Assert.Equal(2, restChest.CountById(Clay));
        Assert.Equal(2, restChest.CountTotal());
    }

    /// <summary>Divisor Subterrâneo em (5,5) com uma Esteira Subterrânea de entrada a oeste.</summary>
    private static GridBuilder UndergroundSplitter()
    {
        return new GridBuilder().UndergroundSplitter(5, 5).Segment(4, 5, Direction.East);
    }

    private static GridBuilder FeedUnderground(GridBuilder g, ILogisticsItem item)
    {
        UndergroundSegmentNode input = g.SegmentAt(4, 5);
        input.Slot.Put(item, Direction.West);
        input.Slot.Advance(Balance.End);
        return g;
    }

    [Fact]
    public void Underground_SevenItemsTwoSegmentOutputs_FourAndThree()
    {
        GridBuilder g = UndergroundSplitter()
            .Segment(5, 4, Direction.North)
            .Segment(5, 6, Direction.South);

        FeedUnderground(g, new FakeItem(Wood, 7)).Run(PassTicks);

        Assert.Equal(4, g.SegmentAt(5, 4).Slot.Item!.Count);
        Assert.Equal(3, g.SegmentAt(5, 6).Slot.Item!.Count);
        Assert.True(g.UndergroundSplitterAt(5, 5).Slot.IsEmpty);
    }

    [Fact]
    public void Underground_FilteredOutputBusy_WoodGoesToTheUnfilteredOutputAsOverflow()
    {
        GridBuilder g = UndergroundSplitter()
            .UndergroundFilter(5, 4, Direction.North, Wood)
            .Segment(5, 6, Direction.South);
        g.UndergroundFilterAt(5, 4).Slot.Put(new FakeItem(Wood, 1), Direction.South);
        g.Run(SplitTicks);
        Assert.True(g.UndergroundFilterAt(5, 4).Slot.Blocked);

        var wood = new FakeItem(Wood, 4);
        FeedUnderground(g, wood).Run(PassTicks);

        Assert.Same(wood, g.SegmentAt(5, 6).Slot.Item);
        Assert.Equal(4, wood.Count);
        Assert.Equal(1, g.UndergroundFilterAt(5, 4).Slot.Item!.Count);
    }

    [Fact]
    public void Underground_Ports_TunnelEntranceIsInput_TunnelExitIsOutput_LoneSideSegmentCurvesOut_SplitterNextToSplitterIsNothing()
    {
        var g = new GridBuilder()
            .UndergroundSplitter(5, 5)
            .Tunnel(4, 5, Direction.East)      // oeste: entrada com a frente para o Divisor → entrada
            .Tunnel(5, 4, Direction.North)     // norte: Divisor atrás → vira saída, com a traseira para ele → saída
            .Segment(6, 5, Direction.South)    // leste: nada atrás, Divisor na lateral → curva saindo dele
            .UndergroundSplitter(5, 6)         // sul: Divisor encostado → nada
            .Tunnel(4, 6, Direction.West, TunnelRole.Entrance); // entrada com a traseira para o Divisor de baixo → nada

        Topology.Recompute(g.Grid);

        SplitterNode splitter = g.UndergroundSplitterAt(5, 5);
        Assert.Equal(TunnelRole.Entrance, g.TunnelAt(4, 5).Role);
        Assert.Equal(TunnelRole.Exit, g.TunnelAt(5, 4).Role);
        Assert.Equal(new[] { Direction.West }, splitter.Inputs);
        Assert.Equal(new[] { Direction.North, Direction.East }, splitter.Outputs);
        Assert.True(g.SegmentAt(6, 5).IsCurve);
        Assert.Equal(Direction.West, g.SegmentAt(6, 5).InputSide);
        Assert.Empty(g.UndergroundSplitterAt(5, 6).Inputs);
        Assert.Empty(g.UndergroundSplitterAt(5, 6).Outputs);
    }

    [Fact]
    public void Underground_SplitterPullsFromTheTunnelEntrance_ASurfaceInserterInFrontOfTheEntranceDoesNot()
    {
        var chest = new FakeChest();
        var wood = new FakeItem(Wood, 2);
        var g = new GridBuilder()
            .Belt(3, 5, Direction.East)
            .Tunnel(4, 5, Direction.East)
            .UndergroundSplitter(5, 5)
            .Segment(6, 5, Direction.East)
            .Inserter(5, 5, Direction.East)
            .Chest(6, 5, chest)
            .Item(3, 5, wood, Balance.End);

        // 1 tick para entrar no Túnel, a travessia dele, 1 tick para o Divisor puxar e a travessia do Divisor.
        g.Run(1 + (Balance.End / Balance.UndergroundSpeed) + PassTicks);

        Assert.True(g.TunnelAt(4, 5).Slot.IsEmpty);
        Assert.Equal(Direction.West, g.UndergroundSplitterAt(5, 5).LastInput);
        Assert.Same(wood, g.SegmentAt(6, 5).Slot.Item);
        Assert.True(g.InserterAt(5, 5).Hand.IsEmpty);
        Assert.Equal(0, chest.CountTotal());
    }
}
