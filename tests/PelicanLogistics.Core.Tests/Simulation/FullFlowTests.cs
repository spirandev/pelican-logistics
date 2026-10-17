using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class FullFlowTests
{
    [Fact]
    public void ChestToChest_WithCurve_MovesEverythingAndConservesItems()
    {
        // Baú A → Extrator → 2 esteiras para o leste → curva para o sul → esteira → Inseridor → baú B.
        var chestA = new FakeChest(36, new FakeItem("wood", 10), new FakeItem("stone", 3));
        var chestB = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, chestA)
            .Extractor(1, 0, Direction.East)
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
            .Extractor(1, 0, Direction.East)
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
        // Baú de minério → Extrator → esteiras → Inseridor (Principal) → fornalha ← Inseridor (Adicional) ← esteiras ← Extrator ← baú de carvão.
        // Fornalha → Extrator (Produto) → esteiras → Inseridor → baú de barras.
        var oreChest = new FakeChest(36, new FakeItem(FakeMachine.Ids.CopperOre, 10));
        var coalChest = new FakeChest(36, new FakeItem(FakeMachine.Ids.Coal, 2));
        var barChest = new FakeChest();
        var furnace = FakeMachine.Furnace(processingTicks: 300);
        var g = new GridBuilder()
            .Chest(0, 0, oreChest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Inserter(4, 0, Direction.East, InserterSlotMode.Main)
            .Machine(5, 0, furnace)
            .Inserter(6, 0, Direction.West, InserterSlotMode.Additional)
            .Belt(7, 0, Direction.West)
            .Belt(8, 0, Direction.West)
            .Extractor(9, 0, Direction.West)
            .Chest(10, 0, coalChest)
            .Extractor(5, 1, Direction.South, ExtractorSource.Product)
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
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Inserter(4, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(5, 0, furnace)
            .Extractor(5, 1, Direction.South, ExtractorSource.Product)
            .Belt(5, 2, Direction.South)
            .Inserter(5, 3, Direction.South)
            .Chest(5, 4, barChest);

        g.Run(5000);

        Assert.Equal(2, barChest.CountById(FakeMachine.Ids.CopperBar));
        Assert.Equal(0, chest.CountTotal());
        Assert.Equal(0, g.CountInTransit());
    }

    private static int CountInTransit(GridBuilder g, string id)
    {
        return g.Grid.Nodes.SelectMany(n => n.Slots).Where(s => s.Item?.QualifiedItemId == id).Sum(s => s.Item!.Count);
    }
}
