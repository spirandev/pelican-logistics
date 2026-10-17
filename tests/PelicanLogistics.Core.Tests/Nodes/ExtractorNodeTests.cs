using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

public class ExtractorNodeTests
{
    [Fact]
    public void Extractor_TakesOneItemEvery30Ticks()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        // A esteira é esvaziada depois de cada tick, então só o cooldown limita o Extrator.
        var taken = new List<int>();
        for (int tick = 1; tick <= 120; tick++)
        {
            g.Run(1);
            var item = g.BeltAt(2, 0).Slot.Clear();
            if (item != null)
            {
                Assert.Equal(1, item.Count);
                taken.Add(tick);
            }
        }

        Assert.Equal(new[] { 1, 31, 61, 91 }, taken);
        Assert.Equal(1, chest.CountTotal());
    }

    [Fact]
    public void Extractor_PutsItemOnBelt_WithEntryFromBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        TransitSlot slot = g.BeltAt(2, 0).Slot;
        Assert.Equal("wood", slot.Item!.QualifiedItemId);
        Assert.Equal(1, slot.Item.Count);
        Assert.Equal(Direction.West, slot.EntrySide);
        Assert.Equal(4, chest.CountTotal());
    }

    [Fact]
    public void Extractor_DoesNotTake_WhenFrontBeltIsOccupied()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Item(2, 0, new FakeItem("stone", 1));

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.Equal("stone", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
    }

    [Fact]
    public void Extractor_DoesNotTake_WhenFrontBeltDoesNotReceiveFromThatSide()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.North)  // de lado para o Extrator
            .Belt(2, 1, Direction.North); // atrás dela: ela fica reta e ignora a lateral

        g.Run(200);

        Assert.Equal(Direction.South, g.BeltAt(2, 0).InputSide);
        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Extractor_DoesNotTake_WhenFrontBeltFacesIt()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.West);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
    }

    [Fact]
    public void Extractor_TurnsSideBeltWithNothingBehindIntoCurve()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.North);

        g.Run(1);

        Assert.Equal(Direction.West, g.BeltAt(2, 0).InputSide);
        Assert.Equal(4, chest.CountTotal());
    }

    [Fact]
    public void Extractor_DoesNotTake_WithoutChestBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(1, 1, chest) // ao lado, não atrás
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Extractor_DoesNothing_WithEmptyChest()
    {
        var chest = new FakeChest(36);
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(200);

        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
        Assert.Equal(0, g.NodeAt<ExtractorNode>(1, 0).Cooldown);
    }

    [Fact]
    public void Extractor_DoesNotTake_WithoutBeltInFront()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Inserter(2, 0, Direction.East);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
    }

    [Fact]
    public void Extractor_TakesItemsInInventoryOrder()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 1), new FakeItem("stone", 1));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal("wood", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal(new[] { "stone" }, chest.Items.Select(i => i.QualifiedItemId));
    }

    [Fact]
    public void Extractor_StackTier_TakesWholeStack()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 50), new FakeItem("wood", 20));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East, TransferTier.Stack)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal(50, g.BeltAt(2, 0).Slot.Item!.Count);
        Assert.Equal(20, chest.CountTotal());
    }

    [Fact]
    public void Extractor_ConfigurableTier_TakesConfiguredAmountAcrossSlots()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 3), new FakeItem("stone", 9), new FakeItem("wood", 10));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East, TransferTier.Configurable)
            .Belt(2, 0, Direction.East);
        g.NodeAt<ExtractorNode>(1, 0).ConfiguredAmount = 7;

        g.Run(1);

        Assert.Equal("wood", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal(7, g.BeltAt(2, 0).Slot.Item!.Count);
        Assert.Equal(6, chest.CountById("wood"));
        Assert.Equal(9, chest.CountById("stone"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5000, Balance.MaxConfigurableAmount)]
    public void Extractor_ConfiguredAmount_IsClamped(int value, int expected)
    {
        var extractor = new ExtractorNode(new TilePos(0, 0), Direction.East, TransferTier.Configurable)
        {
            ConfiguredAmount = value,
        };

        Assert.Equal(expected, extractor.ConfiguredAmount);
    }

    [Fact]
    public void Extractor_ProvidesOnlyToTileInFront()
    {
        var extractor = new ExtractorNode(new TilePos(0, 0), Direction.South);

        Assert.True(extractor.ProvidesTo(new TilePos(0, 1)));
        Assert.False(extractor.ProvidesTo(new TilePos(0, -1)));
        Assert.Equal(new TilePos(0, -1), extractor.SourcePosition);
    }

    // Máquinas (Fase 2). Montagem padrão: máquina (0,0) → Extrator (1,0) → esteira (2,0).

    private static GridBuilder MachineLine(FakeMachine machine, ExtractorSource source, TransferTier tier = TransferTier.Single)
    {
        return new GridBuilder()
            .Machine(0, 0, machine)
            .Extractor(1, 0, Direction.East, source, tier)
            .Belt(2, 0, Direction.East);
    }

    [Fact]
    public void Extractor_Product_SingleTier_TakesOnePerOperation()
    {
        var machine = FakeMachine.Furnace();
        machine.Output = new FakeItem(FakeMachine.Ids.CopperBar, 3);
        var g = MachineLine(machine, ExtractorSource.Product);

        var taken = new List<int>();
        for (int tick = 1; tick <= 120; tick++)
        {
            g.Run(1);
            var item = g.BeltAt(2, 0).Slot.Clear();
            if (item != null)
            {
                Assert.Equal(FakeMachine.Ids.CopperBar, item.QualifiedItemId);
                Assert.Equal(1, item.Count);
                taken.Add(tick);
            }
        }

        Assert.Equal(new[] { 1, 31, 61 }, taken);
        Assert.Null(machine.Output);
    }

    [Fact]
    public void Extractor_Product_StackTier_TakesWholeOutput()
    {
        var machine = FakeMachine.Furnace();
        var output = new FakeItem(FakeMachine.Ids.CopperBar, 3);
        machine.Output = output;
        var g = MachineLine(machine, ExtractorSource.Product, TransferTier.Stack);

        g.Run(1);

        Assert.Same(output, g.BeltAt(2, 0).Slot.Item);
        Assert.Null(machine.Output);
    }

    [Fact]
    public void Extractor_Product_DoesNotTake_WhileThereIsNoProduct()
    {
        var machine = FakeMachine.Furnace();
        var g = MachineLine(machine, ExtractorSource.Product);
        g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0)).Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0)).MainRequired = 5;

        g.Run(200);

        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
        Assert.Equal(0, g.NodeAt<ExtractorNode>(1, 0).Cooldown);
        Assert.Equal(3, g.SlotsAt(0, 0)!.Main!.Count);
    }

    [Fact]
    public void Extractor_Product_DoesNotTake_WhenFrontBeltIsOccupied()
    {
        var machine = FakeMachine.Furnace();
        machine.Output = new FakeItem(FakeMachine.Ids.CopperBar, 1);
        var g = MachineLine(machine, ExtractorSource.Product).Item(2, 0, new FakeItem("(O)388", 1));

        g.Run(200);

        Assert.Equal(1, machine.Output!.Count);
    }

    [Fact]
    public void Extractor_Main_ReturnsStoredOresToBelt()
    {
        var g = new GridBuilder()
            .Machine(0, 0, FakeMachine.Furnace())
            .Extractor(1, 0, Direction.East, ExtractorSource.Main)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(4, 0, Direction.East);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        slots.MainRequired = 5;

        g.Run(1);
        Assert.Equal(2, g.SlotsAt(0, 0)!.Main!.Count);
        Assert.Equal(1, g.BeltAt(2, 0).Slot.Item!.Count);

        g.Run(1000);

        Assert.Null(g.SlotsAt(0, 0));
        Assert.Equal(3, g.CountInTransit());
        Assert.Equal(FakeMachine.Ids.CopperOre, g.BeltAt(4, 0).Slot.Item!.QualifiedItemId);
    }

    [Fact]
    public void Extractor_Additional_ReturnsStoredCoalToBelt()
    {
        var g = MachineLine(FakeMachine.Furnace(), ExtractorSource.Additional);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        slots.MainRequired = 5;
        slots.Additional[FakeMachine.Ids.Coal] = new FakeItem(FakeMachine.Ids.Coal, 1);

        g.Run(1);

        Assert.Equal(FakeMachine.Ids.Coal, g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.Empty(g.SlotsAt(0, 0)!.Additional);
        Assert.Equal(3, g.SlotsAt(0, 0)!.Main!.Count);
    }

    [Fact]
    public void Extractor_Main_DoesNotTakeProduct()
    {
        var machine = FakeMachine.Furnace();
        machine.Output = new FakeItem(FakeMachine.Ids.CopperBar, 1);
        var g = MachineLine(machine, ExtractorSource.Main);

        g.Run(200);

        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
        Assert.NotNull(machine.Output);
    }

    [Fact]
    public void Extractor_SourceIsIgnoredForChests()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East, ExtractorSource.Main)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal(4, chest.CountTotal());
    }
}
