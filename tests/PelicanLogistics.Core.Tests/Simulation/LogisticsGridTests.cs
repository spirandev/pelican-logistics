using PelicanLogistics.Core.Filters;
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
    public void TrySetAccessory_Bridge_FailsOnFilterSplitterInserterAndTunnel()
    {
        var g = new GridBuilder()
            .Filter(0, 0, Direction.East, "(O)388")
            .Splitter(2, 0)
            .Inserter(4, 0, Direction.East)
            .Tunnel(6, 0, Direction.East);

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
            .Tunnel(5, 0, Direction.East)
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

    // Camada subterrânea (Fase 6b).

    [Fact]
    public void Add_UndergroundSegment_Throws_AddUnderground_TwiceOnTheSameTile_Throws()
    {
        var grid = new LogisticsGrid();
        var segment = new UndergroundSegmentNode(new TilePos(0, 0), Direction.East);

        Assert.Throws<ArgumentException>(() => grid.Add(segment));
        grid.AddUnderground(segment);
        Assert.Throws<InvalidOperationException>(() => grid.AddUnderground(new UndergroundSegmentNode(new TilePos(0, 0), Direction.North)));
    }

    [Fact]
    public void Layers_ATileCanHaveASurfaceNodeAndASegment_CountsAreSeparate()
    {
        var g = new GridBuilder().Belt(0, 0, Direction.East).Segment(0, 0, Direction.North).Segment(1, 0, Direction.North);

        Assert.IsType<BeltNode>(g.Grid.Get(new TilePos(0, 0)));
        Assert.NotNull(g.Grid.GetUnderground(new TilePos(0, 0)));
        Assert.Equal(1, g.Grid.Count);
        Assert.Equal(3, g.Grid.TotalCount);
        Assert.Equal(3, g.Grid.ProcessingOrder.Count);
        Assert.Single(g.Grid.Nodes);
        Assert.Equal(2, g.Grid.UndergroundNodes.Count());
    }

    [Fact]
    public void CanPlaceUnderground_NotOnASegmentNorUnderATunnel_UnderOtherBlocksYes()
    {
        var g = new GridBuilder()
            .Segment(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Filter(3, 0, Direction.East, "(O)388")
            .Splitter(4, 0)
            .Inserter(5, 0, Direction.East);

        Assert.False(g.Grid.CanPlaceUnderground(new TilePos(0, 0)));
        Assert.False(g.Grid.CanPlaceUnderground(new TilePos(1, 0)));
        foreach (int x in new[] { 2, 3, 4, 5, 6 })
            Assert.True(g.Grid.CanPlaceUnderground(new TilePos(x, 0)));
    }

    [Fact]
    public void World_GridWithOnlySegments_StillTicks()
    {
        var world = new LogisticsWorld();
        var g = new GridBuilder(world.GetOrCreate("Farm")).Segments(0, 0, Direction.East, 2);
        var item = new FakeItem("wood", 1);
        g.SegmentAt(0, 0).Slot.Put(item, Direction.West);

        world.Tick(_ => g.Endpoints);

        Assert.Equal(Balance.UndergroundSpeed, g.SegmentAt(0, 0).Slot.Progress);
    }

    // Camada subterrânea com Filtros e Divisores (Fase 6c).

    [Fact]
    public void AddUnderground_AcceptsSegmentFilterAndSplitter_RefusesInserterBeltAndTunnel()
    {
        var grid = new LogisticsGrid();
        var segment = new UndergroundSegmentNode(new TilePos(0, 0), Direction.East);
        var filter = new FilterNode(new TilePos(1, 0), Direction.East, new CommonFilterRule());
        var splitter = new SplitterNode(new TilePos(2, 0));

        grid.AddUnderground(segment);
        grid.AddUnderground(filter);
        grid.AddUnderground(splitter);

        Assert.All(new LogisticsNode[] { segment, filter, splitter }, n => Assert.Equal(NodeLayer.Underground, n.Layer));
        Assert.Same(filter, grid.GetUnderground(new TilePos(1, 0)));
        Assert.Null(grid.Get(new TilePos(1, 0)));
        Assert.Throws<ArgumentException>(() => grid.AddUnderground(new InserterNode(new TilePos(3, 0), Direction.East)));
        Assert.Throws<ArgumentException>(() => grid.AddUnderground(new BeltNode(new TilePos(4, 0), Direction.East)));
        Assert.Throws<ArgumentException>(() => grid.AddUnderground(new TunnelNode(new TilePos(5, 0), Direction.East, TunnelRole.Entrance)));
        Assert.Throws<InvalidOperationException>(() => grid.AddUnderground(new SplitterNode(new TilePos(1, 0))));
        Assert.Equal(3, grid.UndergroundNodes.Count());

        var surface = new FilterNode(new TilePos(1, 0), Direction.East, new CommonFilterRule());
        grid.Add(surface);
        Assert.Equal(NodeLayer.Surface, surface.Layer);
    }

    [Fact]
    public void CanPlaceUnderground_FalseOnAFilterOrSplitterBelowOrUnderATunnel()
    {
        var g = new GridBuilder()
            .UndergroundFilter(0, 0, Direction.East, "(O)388")
            .UndergroundSplitter(1, 0)
            .Tunnel(2, 0, Direction.East);

        Assert.False(g.Grid.CanPlaceUnderground(new TilePos(0, 0)));
        Assert.False(g.Grid.CanPlaceUnderground(new TilePos(1, 0)));
        Assert.False(g.Grid.CanPlaceUnderground(new TilePos(2, 0)));
        Assert.True(g.Grid.CanPlaceUnderground(new TilePos(3, 0)));
    }

    [Fact]
    public void SurfaceSplitterAndUndergroundSplitter_OnTheSameTile_DoNotInteract()
    {
        // Superfície: esteira a oeste → Divisor → esteira ao norte. Embaixo: Esteira Subterrânea a oeste → Divisor → ao sul.
        var above = new FakeItem("above", 1);
        var below = new FakeItem("below", 1);
        var g = new GridBuilder()
            .Belt(4, 5, Direction.East)
            .Splitter(5, 5)
            .Belt(5, 4, Direction.North)
            .Segment(4, 5, Direction.East)
            .UndergroundSplitter(5, 5)
            .Segment(5, 6, Direction.South)
            .Item(4, 5, above, Balance.End);
        g.SegmentAt(4, 5).Slot.Put(below, Direction.West);
        g.SegmentAt(4, 5).Slot.Advance(Balance.End);

        g.Run(30);

        Assert.Equal(new[] { Direction.West }, g.SplitterAt(5, 5).Inputs);
        Assert.Equal(new[] { Direction.North }, g.SplitterAt(5, 5).Outputs);
        Assert.Equal(new[] { Direction.West }, g.UndergroundSplitterAt(5, 5).Inputs);
        Assert.Equal(new[] { Direction.South }, g.UndergroundSplitterAt(5, 5).Outputs);
        Assert.Same(above, g.BeltAt(5, 4).Slot.Item);
        Assert.Same(below, g.SegmentAt(5, 6).Slot.Item);
    }

    [Fact]
    public void SurfaceInserter_NextToAnUndergroundFilterOrSegment_DoesNotPullFromThem()
    {
        var chest = new FakeChest();
        var g = new GridBuilder()
            .UndergroundFilter(0, 0, Direction.East, "(O)388")
            .Segment(1, 1, Direction.North)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest);
        var inFilter = new FakeItem("(O)388", 1);
        var inSegment = new FakeItem("(O)388", 1);
        g.UndergroundFilterAt(0, 0).Slot.Put(inFilter, Direction.West);
        g.UndergroundFilterAt(0, 0).Slot.Advance(Balance.End);
        g.SegmentAt(1, 1).Slot.Put(inSegment, Direction.South);
        g.SegmentAt(1, 1).Slot.Advance(Balance.End);

        g.Run(120);

        Assert.Same(inFilter, g.UndergroundFilterAt(0, 0).Slot.Item);
        Assert.Same(inSegment, g.SegmentAt(1, 1).Slot.Item);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        Assert.Equal(0, chest.CountTotal());
    }

    [Fact]
    public void SurfaceBelt_PointingAtAnUndergroundFilter_DoesNotDeliver_AndTheFilterDoesNotDeliverToASurfaceBelt()
    {
        var wood = new FakeItem("(O)388", 1);
        var woodBelow = new FakeItem("(O)388", 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .UndergroundFilter(1, 0, Direction.East, "(O)388")
            .UndergroundFilter(0, 1, Direction.East, "(O)388")
            .Belt(1, 1, Direction.East)
            .Item(0, 0, wood, Balance.End);
        g.UndergroundFilterAt(0, 1).Slot.Put(woodBelow, Direction.West);

        g.Run(60);

        Assert.Same(wood, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.True(g.UndergroundFilterAt(1, 0).Slot.IsEmpty);
        Assert.Same(woodBelow, g.UndergroundFilterAt(0, 1).Slot.Item);
        Assert.True(g.UndergroundFilterAt(0, 1).Slot.Blocked);
        Assert.True(g.BeltAt(1, 1).Slot.IsEmpty);
    }
}
