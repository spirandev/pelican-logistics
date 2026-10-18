using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class FullFlowTests
{
    [Fact]
    public void ChestToChest_WithCurve_MovesEverythingAndConservesItems()
    {
        // Baú A → Inseridor → 2 esteiras para o leste → curva para o sul → esteira → Inseridor → baú B.
        var chestA = new FakeChest(36, new FakeItem("wood", 10), new FakeItem("stone", 3));
        var chestB = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, chestA)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(4, 0, Direction.South)
            .Belt(4, 1, Direction.South)
            .Inserter(4, 2, Direction.South)
            .Chest(4, 3, chestB);

        int total = chestA.CountTotal();
        for (int tick = 0; tick < 3000; tick++)
        {
            g.Run(1);
            Assert.Equal(total, chestA.CountTotal() + g.CountInTransit() + chestB.CountTotal());
        }

        Assert.True(g.BeltAt(4, 0).IsCurve);
        Assert.Equal(0, chestA.CountTotal());
        Assert.Equal(0, g.CountInTransit());
        Assert.Equal(10, chestB.CountById("wood"));
        Assert.Equal(3, chestB.CountById("stone"));
        Assert.Equal(13, chestB.CountTotal());
    }

    [Fact]
    public void WithoutInserter_ItemsAccumulate_SameItemsMerge_OtherItemWaitsBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 4), new FakeItem("stone", 1));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(4, 0, Direction.East);

        g.Run(2000);

        Assert.Equal(0, chest.CountTotal());
        Assert.Equal("wood", g.BeltAt(4, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal(4, g.BeltAt(4, 0).Slot.Item!.Count);
        Assert.Equal("stone", g.BeltAt(3, 0).Slot.Item!.QualifiedItemId);
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Furnace_OreAndCoalFromChests_BarsEndInChest_NothingLostOrCreated()
    {
        // Baú de minério → Inseridor → esteiras → Inseridor (Principal) → fornalha ← Inseridor (Adicional) ← esteiras ← Inseridor ← baú de carvão.
        // Fornalha → Inseridor (Produto) → esteiras → Inseridor → baú de barras.
        var oreChest = new FakeChest(36, new FakeItem(FakeMachine.Ids.CopperOre, 10));
        var coalChest = new FakeChest(36, new FakeItem(FakeMachine.Ids.Coal, 2));
        var barChest = new FakeChest();
        var furnace = FakeMachine.Furnace(processingTicks: 300);
        var g = new GridBuilder()
            .Chest(0, 0, oreChest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Inserter(4, 0, Direction.East, InserterSlotMode.Main)
            .Machine(5, 0, furnace)
            .Inserter(6, 0, Direction.West, InserterSlotMode.Additional)
            .Belt(7, 0, Direction.West)
            .Belt(8, 0, Direction.West)
            .Inserter(9, 0, Direction.West)
            .Chest(10, 0, coalChest)
            .Inserter(5, 1, Direction.South, PickupSource.Product)
            .Belt(5, 2, Direction.South)
            .Belt(5, 3, Direction.South)
            .Inserter(5, 4, Direction.South)
            .Chest(5, 5, barChest);

        int inputs = oreChest.CountTotal() + coalChest.CountTotal();
        for (int tick = 0; tick < 5000; tick++)
        {
            g.Run(1);

            int inSlots = g.Grid.MachineSlots.Entries.SelectMany(e => e.Value.AllItems()).Sum(i => i.Count);
            int inputsInTransit = CountInTransit(g, FakeMachine.Ids.CopperOre) + CountInTransit(g, FakeMachine.Ids.Coal);
            Assert.Equal(inputs, oreChest.CountTotal() + coalChest.CountTotal() + inputsInTransit + inSlots + furnace.Consumed);

            int barsOutside = barChest.CountTotal() + CountInTransit(g, FakeMachine.Ids.CopperBar) + (furnace.Output?.Count ?? 0);
            Assert.Equal(furnace.Produced, barsOutside);
        }

        Assert.Equal(2, furnace.Starts.Count);
        Assert.Equal(2, barChest.CountById(FakeMachine.Ids.CopperBar));
        Assert.Equal(2, barChest.CountTotal());
        Assert.Equal(0, oreChest.CountTotal());
        Assert.Equal(0, coalChest.CountTotal());
        Assert.Equal(0, g.CountInTransit());
        Assert.Null(g.SlotsAt(5, 0));
        Assert.True(furnace.IsIdle);
    }

    [Fact]
    public void Furnace_AutoInserter_OreAndCoalMixedOnOneBelt_ProducesBars()
    {
        var chest = new FakeChest(
            36,
            new FakeItem(FakeMachine.Ids.CopperOre, 5),
            new FakeItem(FakeMachine.Ids.Coal, 1),
            new FakeItem(FakeMachine.Ids.CopperOre, 5),
            new FakeItem(FakeMachine.Ids.Coal, 1));
        var barChest = new FakeChest();
        var furnace = FakeMachine.Furnace(processingTicks: 300);
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Inserter(4, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(5, 0, furnace)
            .Inserter(5, 1, Direction.South, PickupSource.Product)
            .Belt(5, 2, Direction.South)
            .Inserter(5, 3, Direction.South)
            .Chest(5, 4, barChest);

        g.Run(5000);

        Assert.Equal(2, barChest.CountById(FakeMachine.Ids.CopperBar));
        Assert.Equal(0, chest.CountTotal());
        Assert.Equal(0, g.CountInTransit());
    }

    [Fact]
    public void Furnace_DirectFromMixedChest_WithCoalFromSideBelt_ConservesItemsFor3000Ticks()
    {
        // Baú de minério → Inseridor → esteiras → Inseridor (Automático) → fornalha → Inseridor (Produto) → baú de barras.
        // Baú de carvão → Inseridor → esteira que termina na lateral norte do Inseridor da fornalha.
        var oreChest = new FakeChest(36, new FakeItem(FakeMachine.Ids.CopperOre, 15));
        var coalChest = new FakeChest(36, new FakeItem(FakeMachine.Ids.Coal, 3));
        var barChest = new FakeChest();
        var furnace = FakeMachine.Furnace(processingTicks: 300);
        var g = new GridBuilder()
            .Chest(0, 0, oreChest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Inserter(4, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(5, 0, furnace)
            .Inserter(5, 1, Direction.South, PickupSource.Product)
            .Chest(5, 2, barChest)
            .Chest(4, -3, coalChest)
            .Inserter(4, -2, Direction.South)
            .Belt(4, -1, Direction.South);

        Assert.Equal(new[] { Direction.North, Direction.West }, g.Grid.ProcessingOrder.OfType<InserterNode>().Single(n => n.Position == new TilePos(4, 0)).Inputs);

        int Total() =>
            oreChest.CountTotal() + coalChest.CountTotal() + barChest.CountTotal() + g.CountInTransit()
            + g.Grid.MachineSlots.Entries.SelectMany(e => e.Value.AllItems()).Sum(i => i.Count)
            + (furnace.Output?.Count ?? 0);

        int initial = Total();
        for (int tick = 0; tick < 3000; tick++)
        {
            g.Run(1);

            // Só a fornalha muda a soma: consome 5 minérios + 1 carvão e produz 1 barra.
            Assert.Equal(initial - furnace.Consumed + furnace.Produced, Total());
        }

        Assert.Equal(3, furnace.Starts.Count);
        Assert.Equal(3, barChest.CountById(FakeMachine.Ids.CopperBar));
        Assert.Equal(0, oreChest.CountTotal());
        Assert.Equal(0, coalChest.CountTotal());
        Assert.Equal(0, g.CountInTransit());
    }

    [Fact]
    public void ClassicSorter_SplitterWithThreeFilteredOutputs_30ItemsOf4Types_EachEndsInTheRightChest_NothingLostOrCreated()
    {
        // Baú misto → Inseridor → esteiras → Divisor. Saídas: norte = Filtro (madeira), leste = Filtro (pedra),
        // sul = Filtro Completo em lista negra (tudo menos madeira e pedra). Cada saída: esteira → Inseridor → baú.
        const string wood = "(O)388";
        const string stone = "(O)390";
        const string clay = "(O)330";
        var source = new FakeChest(
            36,
            new FakeItem(wood, 4),
            new FakeItem(FakeMachine.Ids.Coal, 5),
            new FakeItem(stone, 6),
            new FakeItem(clay, 3),
            new FakeItem(wood, 4),
            new FakeItem(FakeMachine.Ids.Coal, 2),
            new FakeItem(stone, 6));
        var woodChest = new FakeChest();
        var stoneChest = new FakeChest();
        var restChest = new FakeChest();
        var rest = new CompleteFilterRule { Mode = FilterMode.Blacklist, ItemIds = { wood, stone } };
        var g = new GridBuilder()
            .Chest(0, 5, source)
            .Inserter(1, 5, Direction.East)
            .Belt(2, 5, Direction.East)
            .Belt(3, 5, Direction.East)
            .Belt(4, 5, Direction.East)
            .Splitter(5, 5)
            .Filter(5, 4, Direction.North, wood)
            .Belt(5, 3, Direction.North)
            .Inserter(5, 2, Direction.North)
            .Chest(5, 1, woodChest)
            .Filter(6, 5, Direction.East, stone)
            .Belt(7, 5, Direction.East)
            .Inserter(8, 5, Direction.East)
            .Chest(9, 5, stoneChest)
            .Filter(5, 6, Direction.South, rest)
            .Belt(5, 7, Direction.South)
            .Inserter(5, 8, Direction.South)
            .Chest(5, 9, restChest);

        int total = source.CountTotal();
        Assert.Equal(30, total);
        for (int tick = 0; tick < 4000; tick++)
        {
            g.Run(1);
            Assert.Equal(total, source.CountTotal() + g.CountInTransit() + woodChest.CountTotal() + stoneChest.CountTotal() + restChest.CountTotal());
        }

        Assert.Equal(0, source.CountTotal());
        Assert.Equal(0, g.CountInTransit());
        Assert.Equal(8, woodChest.CountById(wood));
        Assert.Equal(8, woodChest.CountTotal());
        Assert.Equal(12, stoneChest.CountById(stone));
        Assert.Equal(12, stoneChest.CountTotal());
        Assert.Equal(7, restChest.CountById(FakeMachine.Ids.Coal));
        Assert.Equal(3, restChest.CountById(clay));
        Assert.Equal(10, restChest.CountTotal());
    }

    [Fact]
    public void ClassicSorter_WoodChestFull_WoodOverflowsToTheUnfilteredOutput()
    {
        // Norte = Filtro (madeira) → Inseridor → baú de madeira com 1 slot já cheio; sul = esteira → Inseridor → baú do resto.
        const string wood = "(O)388";
        var source = new FakeChest(36, new FakeItem(wood, 6));
        var woodChest = new FakeChest(1, new FakeItem("(O)390", 1, maxStack: 1));
        var restChest = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 5, source)
            .Inserter(1, 5, Direction.East)
            .Belt(2, 5, Direction.East)
            .Belt(3, 5, Direction.East)
            .Belt(4, 5, Direction.East)
            .Splitter(5, 5)
            .Filter(5, 4, Direction.North, wood)
            .Inserter(5, 3, Direction.North)
            .Chest(5, 2, woodChest)
            .Belt(5, 6, Direction.South)
            .Inserter(5, 7, Direction.South)
            .Chest(5, 8, restChest);

        g.Run(3000);

        // A primeira madeira fica parada no filtro; as outras vão como excedente para o sul.
        Assert.Equal(wood, g.FilterAt(5, 4).Slot.Item!.QualifiedItemId);
        Assert.Equal(5, restChest.CountById(wood));
        Assert.Equal(0, source.CountTotal());
    }

    private static int CountInTransit(GridBuilder g, string id)
    {
        return g.Grid.Nodes.SelectMany(n => n.Slots).Where(s => s.Item?.QualifiedItemId == id).Sum(s => s.Item!.Count);
    }
}
