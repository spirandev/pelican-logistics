using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;
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
        grid.Add(new BeltNode(new TilePos(0, 0), Direction.East));
        Assert.True(grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Bridge));

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

        Assert.Throws<InvalidOperationException>(() => grid.Add(new InserterNode(new TilePos(0, 0), Direction.East)));
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

    // Acionamento das máquinas (Fase 2).

    /// <summary>Fornalha em (2,0): minério pela esteira (0,0) e Inseridor Principal (1,0); carvão pela esteira (4,0) e Inseridor Adicional (3,0).</summary>
    private static GridBuilder FurnaceWithTwoInserters(FakeMachine furnace, FakeItem ore, FakeItem coal)
    {
        return new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, InserterSlotMode.Main)
            .Machine(2, 0, furnace)
            .Inserter(3, 0, Direction.West, InserterSlotMode.Additional)
            .Belt(4, 0, Direction.West)
            .Item(0, 0, ore, Balance.End)
            .Item(4, 0, coal, Balance.End);
    }

    [Fact]
    public void Machine_SlotsCompleteAndIdle_StartsOnceWithFiveOresAndOneCoal_SlotsEmpty()
    {
        var furnace = FakeMachine.Furnace(processingTicks: 10_000);
        var g = FurnaceWithTwoInserters(furnace, new FakeItem(FakeMachine.Ids.CopperOre, 5), new FakeItem(FakeMachine.Ids.Coal, 1));

        g.Run(300);

        var start = Assert.Single(furnace.Starts);
        Assert.Equal(FakeMachine.Ids.CopperOre, start.Id);
        Assert.Equal(5, start.Count);
        Assert.Equal(new[] { (FakeMachine.Ids.Coal, 1) }, start.Additional);
        Assert.Null(g.SlotsAt(2, 0));
        Assert.Equal(0, g.Grid.MachineSlots.Count);
    }

    [Fact]
    public void Machine_Busy_SlotsCompleteAndWait_StartsOnlyAfterProductIsTaken()
    {
        var furnace = FakeMachine.Furnace();
        furnace.Output = new FakeItem(FakeMachine.Ids.CopperBar, 1);
        var g = FurnaceWithTwoInserters(furnace, new FakeItem(FakeMachine.Ids.CopperOre, 5), new FakeItem(FakeMachine.Ids.Coal, 1));

        g.Run(300);

        Assert.Empty(furnace.Starts);
        Assert.True(g.SlotsAt(2, 0)!.IsComplete(furnace.AdditionalRequirements));

        furnace.TakeOutput(int.MaxValue);
        g.Run(1);

        Assert.Single(furnace.Starts);
        Assert.Null(g.SlotsAt(2, 0));
    }

    [Fact]
    public void Machine_Keg_OneFruitStartsImmediately_StackIsConsumedOnePerCycle()
    {
        var keg = FakeMachine.Keg(processingTicks: 50);
        var wineChest = new FakeChest();
        var fruit = new FakeItem("(O)613", 10, category: FakeMachine.Ids.FruitCategory);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(2, 0, keg)
            .Inserter(2, 1, Direction.South, PickupSource.Product)
            .Belt(2, 2, Direction.South)
            .Inserter(2, 3, Direction.South)
            .Chest(2, 4, wineChest)
            .Item(0, 0, fruit, Balance.End);

        g.Run(Balance.End / Balance.InserterSpeed + 1);
        Assert.Single(keg.Starts);
        Assert.Equal(9, fruit.Count);

        g.Run(3000);

        Assert.Equal(10, keg.Starts.Count);
        Assert.All(keg.Starts, s => Assert.Equal(1, s.Count));
        Assert.Equal(10, wineChest.CountById(FakeMachine.Ids.Wine));
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Machine_WhatTheMachineDoesNotConsume_StaysInSlots()
    {
        var furnace = FakeMachine.Furnace(processingTicks: 10_000);
        var g = new GridBuilder().Machine(2, 0, furnace).Belt(0, 0, Direction.East);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(2, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 7);
        slots.MainRequired = 5;
        slots.Additional[FakeMachine.Ids.Coal] = new FakeItem(FakeMachine.Ids.Coal, 1);

        g.Run(1);

        Assert.Single(furnace.Starts);
        Assert.Equal(2, g.SlotsAt(2, 0)!.Main!.Count);
        Assert.Empty(g.SlotsAt(2, 0)!.Additional);
    }

    [Fact]
    public void Machine_NotInEndpoints_SlotsAreKept()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East);
        g.Grid.MachineSlots.GetOrCreate(new TilePos(2, 0)).Main = new FakeItem(FakeMachine.Ids.CopperOre, 5);

        g.Run(10);

        Assert.Equal(5, g.SlotsAt(2, 0)!.Main!.Count);
    }

    [Fact]
    public void Machine_EmptySlotEntries_AreRemovedAtEndOfTick()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East);
        g.Grid.MachineSlots.GetOrCreate(new TilePos(2, 0));

        g.Run(1);

        Assert.Equal(0, g.Grid.MachineSlots.Count);
    }

    // Acessórios (Fase 6).

    [Theory]
    [InlineData(BeltTier.Basic)]
    [InlineData(BeltTier.Fast)]
    [InlineData(BeltTier.Express)]
    public void TrySetAccessory_Bridge_WorksOnBeltsOfEverySpeed(BeltTier tier)
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East, tier);

        Assert.True(g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Bridge));

        Assert.Equal(AccessoryKind.Bridge, g.BeltAt(0, 0).Accessory);
    }

    [Fact]
    public void TrySetAccessory_Bridge_FailsOnFilterSplitterInserterAndUnderground()
    {
        var g = new GridBuilder()
            .Filter(0, 0, Direction.East, "(O)388")
            .Splitter(2, 0)
            .Inserter(4, 0, Direction.East)
            .Underground(6, 0, Direction.East);

        foreach (int x in new[] { 0, 2, 4, 6 })
        {
            Assert.False(g.Grid.TrySetAccessory(new TilePos(x, 0), AccessoryKind.Bridge));
            Assert.Equal(AccessoryKind.None, g.Grid.Get(new TilePos(x, 0))!.Accessory);
        }
    }

    [Fact]
    public void TrySetAccessory_Regulator_OnlyOnInserters_WithDefaultRule()
    {
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Filter(1, 0, Direction.East, "(O)388")
            .Splitter(3, 0)
            .Underground(5, 0, Direction.East)
            .Inserter(7, 0, Direction.East, TransferTier.Configurable);

        foreach (int x in new[] { 0, 1, 3, 5 })
            Assert.False(g.Grid.TrySetAccessory(new TilePos(x, 0), AccessoryKind.Regulator));

        Assert.True(g.Grid.TrySetAccessory(new TilePos(7, 0), AccessoryKind.Regulator));
        InserterNode inserter = g.InserterAt(7, 0);
        Assert.Equal(AccessoryKind.Regulator, inserter.Accessory);
        Assert.NotNull(inserter.Regulator);
        Assert.Equal(RegulatorMode.General, inserter.Regulator!.Mode);
        Assert.Equal(Balance.RegulatorDefaultLimit, inserter.Regulator.GeneralLimit);
    }

    [Fact]
    public void TrySetAccessory_Regulator_KeepsTheGivenRule()
    {
        var rule = new RegulatorRule { Mode = RegulatorMode.PerItem, PerItemLimits = { ["(O)388"] = 30 } };
        var g = new GridBuilder().Inserter(0, 0, Direction.East);

        Assert.True(g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Regulator, rule));

        Assert.Same(rule, g.InserterAt(0, 0).Regulator);
    }

    [Fact]
    public void TrySetAccessory_SecondAccessoryOnTheSameTile_Fails()
    {
        var original = new RegulatorRule { GeneralLimit = 7 };
        var g = new GridBuilder().Belt(0, 0, Direction.East).Inserter(1, 0, Direction.East);
        Assert.True(g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Bridge));
        Assert.True(g.Grid.TrySetAccessory(new TilePos(1, 0), AccessoryKind.Regulator, original));

        Assert.False(g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Bridge));
        Assert.False(g.Grid.TrySetAccessory(new TilePos(1, 0), AccessoryKind.Regulator, new RegulatorRule()));

        Assert.Equal(AccessoryKind.Bridge, g.BeltAt(0, 0).Accessory);
        Assert.Same(original, g.InserterAt(1, 0).Regulator);
    }

    [Fact]
    public void TrySetAccessory_NoneOrEmptyTile_Fails()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East);

        Assert.False(g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.None));
        Assert.False(g.Grid.TrySetAccessory(new TilePos(5, 5), AccessoryKind.Bridge));
    }

    [Fact]
    public void RemoveAccessory_ReturnsItAndClearsTheRegulatorRule()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East).Inserter(1, 0, Direction.East);
        g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Bridge);
        g.Grid.TrySetAccessory(new TilePos(1, 0), AccessoryKind.Regulator);

        Assert.Equal(AccessoryKind.Bridge, g.Grid.RemoveAccessory(new TilePos(0, 0)));
        Assert.Equal(AccessoryKind.Regulator, g.Grid.RemoveAccessory(new TilePos(1, 0)));

        Assert.Equal(AccessoryKind.None, g.BeltAt(0, 0).Accessory);
        Assert.Equal(AccessoryKind.None, g.InserterAt(1, 0).Accessory);
        Assert.Null(g.InserterAt(1, 0).Regulator);
        Assert.Equal(AccessoryKind.None, g.Grid.RemoveAccessory(new TilePos(0, 0)));
        Assert.Equal(AccessoryKind.None, g.Grid.RemoveAccessory(new TilePos(9, 9)));

        // Sem acessório, o tile aceita outro.
        Assert.True(g.Grid.TrySetAccessory(new TilePos(0, 0), AccessoryKind.Bridge));
    }

    [Fact]
    public void Bridge_DoesNotChangeTheFlow_RemoveReturnsBridgeAndItem()
    {
        FakeItem Run(bool bridge, out GridBuilder g)
        {
            var item = new FakeItem("wood", 3);
            g = new GridBuilder()
                .Belt(0, 0, Direction.East)
                .Belt(1, 0, Direction.East)
                .Belt(2, 0, Direction.East)
                .Item(0, 0, item);
            if (bridge)
                g.Grid.TrySetAccessory(new TilePos(1, 0), AccessoryKind.Bridge);
            g.Run(90);
            return item;
        }

        Run(false, out GridBuilder without);
        FakeItem item = Run(true, out GridBuilder with);

        Assert.Same(item, with.BeltAt(1, 0).Slot.Item);
        Assert.Equal(without.BeltAt(1, 0).Slot.Progress, with.BeltAt(1, 0).Slot.Progress);

        RemovalResult result = with.Grid.Remove(new TilePos(1, 0));

        Assert.Equal(AccessoryKind.Bridge, result.Accessory);
        Assert.Same(item, Assert.Single(result.Items));
    }
}
