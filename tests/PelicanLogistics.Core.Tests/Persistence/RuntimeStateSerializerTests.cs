using System.Text.Json;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Persistence;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Persistence;

public class RuntimeStateSerializerTests
{
    private const string Wood = "(O)388";

    [Fact]
    public void RoundTrip_RestoresBeltsHandsCooldownPointersAndMachineSlots()
    {
        var world = new LogisticsWorld();
        var farm = new GridBuilder(world.GetOrCreate("Farm"));

        // Esteiras com progressos diferentes; a última termina no vazio e fica bloqueada.
        farm.Belt(0, 0, Direction.East).Belt(1, 0, Direction.East).Belt(2, 0, Direction.East).Belt(3, 0, Direction.East)
            .Item(0, 0, new FakeItem(Wood, 1), 300)
            .Item(1, 0, new FakeItem(Wood, 2), 100)
            .Item(3, 0, new FakeItem(Wood, 3), 590);

        // Inseridor que vai estar no meio da ida (o baú só recebe madeira depois de 16 ticks).
        var lateChest = new FakeChest();
        farm.Chest(0, 2, lateChest).Inserter(1, 2, Direction.East).Chest(2, 2, new FakeChest());

        // Inseridor que vai estar voltando (entregou no tick 16).
        farm.Chest(0, 4, new FakeChest(36, new FakeItem(Wood, 10))).Inserter(1, 4, Direction.East).Chest(2, 4, new FakeChest());

        farm.Run(16);
        lateChest.Items.Add(new FakeItem(Wood, 5));
        farm.Run(4);

        // Slots virtuais de uma máquina.
        VirtualSlots furnace = farm.Grid.MachineSlots.GetOrCreate(new TilePos(5, 5));
        furnace.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        furnace.MainRequired = 5;
        furnace.Additional[FakeMachine.Ids.Coal] = new FakeItem(FakeMachine.Ids.Coal, 1);

        // Pré-condições do cenário.
        Assert.True(farm.BeltAt(3, 0).Slot.Blocked);
        Assert.InRange(farm.InserterAt(1, 2).Hand.Progress, 1, Balance.End - 1);
        Assert.True(farm.InserterAt(1, 4).Cooldown > 0);
        Assert.True(farm.InserterAt(1, 4).Hand.IsEmpty);
        Assert.Equal(Direction.West, farm.InserterAt(1, 4).LastInput);

        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));

        // O estado sobrevive ao JSON (o SMAPI grava o save em JSON).
        WorldRuntimeState json = JsonSerializer.Deserialize<WorldRuntimeState>(JsonSerializer.Serialize(state))!;

        LogisticsWorld fresh = EmptyCopy(world);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(fresh, json, i => items[i], orphans);

        Assert.Empty(orphans);
        LogisticsGrid before = world.Get("Farm")!;
        LogisticsGrid after = fresh.Get("Farm")!;
        foreach (LogisticsNode node in before.Nodes)
        {
            LogisticsNode restored = after.Get(node.Position)!;
            List<TransitSlot> oldSlots = node.Slots.ToList();
            List<TransitSlot> newSlots = restored.Slots.ToList();
            Assert.Equal(oldSlots.Count, newSlots.Count);
            for (int i = 0; i < oldSlots.Count; i++)
            {
                Assert.Same(oldSlots[i].Item, newSlots[i].Item);
                Assert.Equal(oldSlots[i].Progress, newSlots[i].Progress);
                Assert.Equal(oldSlots[i].Blocked, newSlots[i].Blocked);

                // O lado de entrada de um slot vazio não tem significado.
                if (oldSlots[i].Item != null)
                    Assert.Equal(oldSlots[i].EntrySide, newSlots[i].EntrySide);
            }

            if (node is InserterNode inserter)
            {
                var restoredInserter = Assert.IsType<InserterNode>(restored);
                Assert.Equal(inserter.Cooldown, restoredInserter.Cooldown);
                Assert.Equal(inserter.LastInput, restoredInserter.LastInput);
            }
        }

        VirtualSlots restoredFurnace = after.MachineSlots.Get(new TilePos(5, 5))!;
        Assert.Same(furnace.Main, restoredFurnace.Main);
        Assert.Equal(5, restoredFurnace.MainRequired);
        Assert.Same(furnace.Additional[FakeMachine.Ids.Coal], restoredFurnace.Additional[FakeMachine.Ids.Coal]);
    }

    [Fact]
    public void RoundTrip_ItemsKeepMovingAfterImport()
    {
        var world = new LogisticsWorld();
        var farm = new GridBuilder(world.GetOrCreate("Farm"));
        var target = new FakeChest();
        farm.Belt(0, 0, Direction.East).Inserter(1, 0, Direction.East).Chest(2, 0, target)
            .Item(0, 0, new FakeItem(Wood, 1), 300);

        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));
        LogisticsWorld fresh = EmptyCopy(world);
        RuntimeStateSerializer.Import(fresh, state, i => items[i], new List<OrphanItem>());

        var endpoints = new FakeEndpoints();
        endpoints.Endpoints[new TilePos(2, 0)] = target;
        for (int i = 0; i < 60; i++)
            fresh.Tick(_ => endpoints);

        Assert.Single(target.Items);
        Assert.Same(items[0], target.Items[0]);
    }

    [Fact]
    public void Import_NodeNoLongerExists_ItemsBecomeOrphansWithLocationAndPosition()
    {
        var world = new LogisticsWorld();
        var wood = new FakeItem(Wood, 4);
        new GridBuilder(world.GetOrCreate("Farm")).Belt(7, 3, Direction.South).Item(7, 3, wood, 200);
        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));

        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(new LogisticsWorld(), state, i => items[i], orphans);

        OrphanItem orphan = Assert.Single(orphans);
        Assert.Equal("Farm", orphan.LocationKey);
        Assert.Equal(new TilePos(7, 3), orphan.Position);
        Assert.Same(wood, orphan.Item);
    }

    [Fact]
    public void Import_SamePlaceDifferentKind_ItemsBecomeOrphans()
    {
        var world = new LogisticsWorld();
        var wood = new FakeItem(Wood, 1);
        new GridBuilder(world.GetOrCreate("Farm")).Belt(0, 0, Direction.East).Item(0, 0, wood, 100);
        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));

        var fresh = new LogisticsWorld();
        fresh.GetOrCreate("Farm").Add(new InserterNode(new TilePos(0, 0), Direction.East));
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(fresh, state, i => items[i], orphans);

        Assert.Same(wood, Assert.Single(orphans).Item);
        Assert.True(Assert.IsType<InserterNode>(fresh.Get("Farm")!.Get(new TilePos(0, 0))).Hand.IsEmpty);
    }

    [Fact]
    public void Import_MoreSavedSlotsThanTheNodeHas_ExtraSlotsBecomeOrphans()
    {
        var first = new FakeItem(Wood, 1);
        var extra = new FakeItem(Wood, 2);
        var items = new List<ILogisticsItem> { first, extra };
        WorldRuntimeState state = StateWithBelt(new SlotState { ItemRef = 0, Progress = 100 }, new SlotState { ItemRef = 1, Progress = 200 });

        LogisticsWorld world = WorldWithBelt(out BeltNode belt);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(world, state, i => items[i], orphans);

        Assert.Same(first, belt.Slot.Item);
        Assert.Same(extra, Assert.Single(orphans).Item);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(9999, Balance.End)]
    public void Import_ProgressOutOfRange_IsClamped(int saved, int expected)
    {
        var items = new List<ILogisticsItem> { new FakeItem(Wood, 1) };
        WorldRuntimeState state = StateWithBelt(new SlotState { ItemRef = 0, Progress = saved, EntrySide = Direction.West });

        LogisticsWorld world = WorldWithBelt(out BeltNode belt);
        RuntimeStateSerializer.Import(world, state, i => items[i], new List<OrphanItem>());

        Assert.Equal(expected, belt.Slot.Progress);
    }

    [Fact]
    public void Import_BlockedBeforeTheEnd_IsNotBlocked()
    {
        var items = new List<ILogisticsItem> { new FakeItem(Wood, 1) };
        WorldRuntimeState state = StateWithBelt(new SlotState { ItemRef = 0, Progress = 100, EntrySide = Direction.West, Blocked = true });

        LogisticsWorld world = WorldWithBelt(out BeltNode belt);
        RuntimeStateSerializer.Import(world, state, i => items[i], new List<OrphanItem>());

        Assert.False(belt.Slot.Blocked);
    }

    [Fact]
    public void Import_UnknownItemRef_LeavesSlotEmptyWithoutThrowing()
    {
        WorldRuntimeState state = StateWithBelt(new SlotState { ItemRef = 42, Progress = 100 });
        state.Grids["Farm"].Machines.Add(new MachineSlotsState { X = 5, Y = 5, MainRef = 43, MainRequired = 5 });

        LogisticsWorld world = WorldWithBelt(out BeltNode belt);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(world, state, _ => null, orphans);

        Assert.True(belt.Slot.IsEmpty);
        Assert.Empty(orphans);
        Assert.Equal(0, world.Get("Farm")!.MachineSlots.Count);
    }

    [Fact]
    public void Import_SameItemRefTwice_RestoresItOnlyOnce()
    {
        var wood = new FakeItem(Wood, 1);
        var items = new List<ILogisticsItem> { wood };
        WorldRuntimeState state = StateWithBelt(new SlotState { ItemRef = 0, Progress = 100 });
        state.Grids["Farm"].Machines.Add(new MachineSlotsState { X = 5, Y = 5, MainRef = 0, MainRequired = 5 });

        LogisticsWorld world = WorldWithBelt(out BeltNode belt);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(world, state, i => items[i], orphans);

        Assert.Same(wood, belt.Slot.Item);
        Assert.Empty(orphans);
        Assert.Equal(0, world.Get("Farm")!.MachineSlots.Count);
    }

    [Fact]
    public void Import_UnknownVersion_ImportsNothingAndOrphansEverything()
    {
        var wood = new FakeItem(Wood, 1);
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        var coal = new FakeItem(FakeMachine.Ids.Coal, 1);
        var items = new List<ILogisticsItem> { wood, ore, coal };
        WorldRuntimeState state = StateWithBelt(new SlotState { ItemRef = 0, Progress = 100 });
        state.Version = 2;
        state.Grids["Farm"].Nodes[0].InputPointer = (int)Direction.North;
        state.Grids["Farm"].Machines.Add(new MachineSlotsState
        {
            X = 5,
            Y = 5,
            MainRef = 1,
            MainRequired = 5,
            AdditionalRefs = { [FakeMachine.Ids.Coal] = 2 },
        });

        LogisticsWorld world = WorldWithBelt(out BeltNode belt);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(world, state, i => items[i], orphans);

        Assert.True(belt.Slot.IsEmpty);
        Assert.Equal(0, world.Get("Farm")!.MachineSlots.Count);
        Assert.Equal(new ILogisticsItem[] { wood, ore, coal }, orphans.Select(o => o.Item));
        Assert.All(orphans.Skip(1), o => Assert.Equal(new TilePos(5, 5), o.Position));
    }

    [Fact]
    public void Import_InvalidInserterPointer_ClearsLastInput()
    {
        var state = new WorldRuntimeState();
        state.Grids["Farm"] = new GridRuntimeState
        {
            Nodes = { new NodeRuntimeState { X = 0, Y = 0, Kind = NodeKind.Inserter, Slots = { new SlotState() }, InputPointer = 7, Cooldown = 99 } },
        };

        var world = new LogisticsWorld();
        var inserter = new InserterNode(new TilePos(0, 0), Direction.East);
        world.GetOrCreate("Farm").Add(inserter);
        RuntimeStateSerializer.Import(world, state, _ => null, new List<OrphanItem>());

        Assert.Null(inserter.LastInput);
        Assert.Equal(Balance.InserterReturnTicks, inserter.Cooldown);
    }

    [Fact]
    public void Import_SeveralLocations_EachGridRestoresItsOwnState()
    {
        var world = new LogisticsWorld();
        var farmWood = new FakeItem(Wood, 1);
        var greenhouseWood = new FakeItem(Wood, 2);
        new GridBuilder(world.GetOrCreate("Farm")).Belt(0, 0, Direction.East).Item(0, 0, farmWood, 100);
        new GridBuilder(world.GetOrCreate("Greenhouse")).Belt(0, 0, Direction.East).Item(0, 0, greenhouseWood, 400);
        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));

        LogisticsWorld fresh = EmptyCopy(world);
        RuntimeStateSerializer.Import(fresh, state, i => items[i], new List<OrphanItem>());

        BeltNode farmBelt = Assert.IsType<BeltNode>(fresh.Get("Farm")!.Get(new TilePos(0, 0)));
        BeltNode greenhouseBelt = Assert.IsType<BeltNode>(fresh.Get("Greenhouse")!.Get(new TilePos(0, 0)));
        Assert.Same(farmWood, farmBelt.Slot.Item);
        Assert.Equal(100, farmBelt.Slot.Progress);
        Assert.Same(greenhouseWood, greenhouseBelt.Slot.Item);
        Assert.Equal(400, greenhouseBelt.Slot.Progress);
    }

    [Fact]
    public void Import_MachineSlotsWithoutAnyNode_AreRestoredInANewGrid()
    {
        var world = new LogisticsWorld();
        VirtualSlots slots = world.GetOrCreate("Cellar").MachineSlots.GetOrCreate(new TilePos(2, 2));
        slots.Main = new FakeItem(FakeMachine.Ids.Diamond, 1);
        slots.MainRequired = 1;
        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));

        var fresh = new LogisticsWorld();
        RuntimeStateSerializer.Import(fresh, state, i => items[i], new List<OrphanItem>());

        Assert.Same(slots.Main, fresh.Get("Cellar")!.MachineSlots.Get(new TilePos(2, 2))!.Main);
    }

    [Fact]
    public void RoundTrip_SplitterPointersAndItem_AndFilterSlot()
    {
        var world = new LogisticsWorld();
        var farm = new GridBuilder(world.GetOrCreate("Farm"));

        // Entrada a oeste; saídas ao norte (filtro de madeira) e ao sul. Duas madeiras passam, a terceira fica no meio.
        farm.Splitter(5, 5)
            .Belt(4, 5, Direction.East)
            .Filter(5, 4, Direction.North, Wood)
            .Belt(5, 6, Direction.South);
        farm.Item(4, 5, new FakeItem(Wood, 1), Balance.End).Run(1 + (Balance.End / Balance.SplitterSpeed));
        farm.Item(4, 5, new FakeItem(Wood, 1), Balance.End).Run(1 + (Balance.End / Balance.SplitterSpeed));
        farm.Item(4, 5, new FakeItem(Wood, 1), Balance.End).Run(5);

        SplitterNode splitter = farm.SplitterAt(5, 5);
        FilterNode filter = farm.FilterAt(5, 4);
        Assert.Equal(Direction.West, splitter.LastInput);
        Assert.Equal(Direction.South, splitter.LastOutput);
        Assert.NotNull(splitter.Slot.Item);
        Assert.NotNull(filter.Slot.Item);

        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));
        WorldRuntimeState json = JsonSerializer.Deserialize<WorldRuntimeState>(JsonSerializer.Serialize(state))!;
        LogisticsWorld fresh = EmptyCopy(world);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(fresh, json, i => items[i], orphans);

        Assert.Empty(orphans);
        var restoredSplitter = Assert.IsType<SplitterNode>(fresh.Get("Farm")!.Get(new TilePos(5, 5)));
        var restoredFilter = Assert.IsType<FilterNode>(fresh.Get("Farm")!.Get(new TilePos(5, 4)));
        Assert.Equal(Direction.West, restoredSplitter.LastInput);
        Assert.Equal(Direction.South, restoredSplitter.LastOutput);
        Assert.Same(splitter.Slot.Item, restoredSplitter.Slot.Item);
        Assert.Equal(splitter.Slot.Progress, restoredSplitter.Slot.Progress);
        Assert.Equal(Direction.West, restoredSplitter.Slot.EntrySide);
        Assert.Same(filter.Slot.Item, restoredFilter.Slot.Item);
        Assert.Equal(filter.Slot.Progress, restoredFilter.Slot.Progress);
        Assert.Equal(Direction.South, restoredFilter.Slot.EntrySide);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(4, 99)]
    public void Import_SplitterPointersInvalidOrUnset_BecomeNull(int input, int output)
    {
        static WorldRuntimeState StateWithSplitter(int input, int output)
        {
            var state = new WorldRuntimeState();
            state.Grids["Farm"] = new GridRuntimeState
            {
                Nodes = { new NodeRuntimeState { X = 0, Y = 0, Kind = NodeKind.Splitter, InputPointer = input, OutputPointer = output } },
            };
            return state;
        }

        var world = new LogisticsWorld();
        var splitter = new SplitterNode(new TilePos(0, 0));
        world.GetOrCreate("Farm").Add(splitter);
        RuntimeStateSerializer.Import(world, StateWithSplitter((int)Direction.East, (int)Direction.West), _ => null, new List<OrphanItem>());
        Assert.Equal(Direction.East, splitter.LastInput);
        Assert.Equal(Direction.West, splitter.LastOutput);

        RuntimeStateSerializer.Import(world, StateWithSplitter(input, output), _ => null, new List<OrphanItem>());

        Assert.Null(splitter.LastInput);
        Assert.Null(splitter.LastOutput);
    }

    [Fact]
    public void RoundTrip_ItemsInTunnelsAndUndergroundSegments_SurviveInTheRightLayer_AndKeepMoving()
    {
        var world = new LogisticsWorld();
        var farm = new GridBuilder(world.GetOrCreate("Farm"));

        // Entrada (1,0), 3 Esteiras Subterrâneas (2..4) embaixo de uma esteira em (3,0), saída (5,0), nada depois dela.
        farm.Belt(0, 0, Direction.East).Tunnel(1, 0, Direction.East).Segments(2, 0, Direction.East, 3).Tunnel(5, 0, Direction.East)
            .Belt(3, 0, Direction.South)
            .Item(3, 0, new FakeItem("surface", 1), 100);
        for (int i = 0; i < 5; i++)
        {
            for (int tick = 0; tick < 500 && !farm.BeltAt(0, 0).Slot.IsEmpty; tick++)
                farm.Run(1);
            farm.Item(0, 0, new FakeItem($"item{i + 1}", 1), Balance.End);
        }

        farm.Run(400);
        Assert.Equal("item1", farm.TunnelAt(5, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal("item2", farm.SegmentAt(4, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal("item4", farm.SegmentAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal("item5", farm.TunnelAt(1, 0).Slot.Item!.QualifiedItemId);

        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));
        WorldRuntimeState json = JsonSerializer.Deserialize<WorldRuntimeState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(3, json.Grids["Farm"].Nodes.Count(n => n.Layer == NodeRuntimeState.UndergroundLayer));
        LogisticsWorld fresh = EmptyCopy(world);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(fresh, json, i => items[i], orphans);

        Assert.Empty(orphans);
        LogisticsGrid restored = fresh.Get("Farm")!;
        for (int x = 2; x <= 4; x++)
        {
            Assert.Same(farm.SegmentAt(x, 0).Slot.Item, ((UndergroundSegmentNode)restored.GetUnderground(new TilePos(x, 0))!).Slot.Item);
            Assert.Equal(farm.SegmentAt(x, 0).Slot.Progress, ((UndergroundSegmentNode)restored.GetUnderground(new TilePos(x, 0))!).Slot.Progress);
        }

        Assert.Same(farm.BeltAt(3, 0).Slot.Item, ((BeltNode)restored.Get(new TilePos(3, 0))!).Slot.Item);
        Assert.Same(farm.TunnelAt(5, 0).Slot.Item, ((TunnelNode)restored.Get(new TilePos(5, 0))!).Slot.Item);

        // Depois de carregar, liberar a saída faz a fila andar, na mesma ordem.
        var after = new GridBuilder(restored);
        for (int x = 6; x <= 10; x++)
            after.Belt(x, 0, Direction.East);
        after.Run(600);
        Assert.Equal(
            new[] { "item1", "item2", "item3", "item4", "item5" },
            Enumerable.Range(6, 5).Reverse().Select(x => after.BeltAt(x, 0).Slot.Item!.QualifiedItemId));
    }

    [Fact]
    public void Import_StateWithoutLayer_IsSurface_OldUndergroundBeltKind_BecomesOrphans()
    {
        var item = new FakeItem(Wood, 2);
        var old = new FakeItem(Wood, 3);
        var world = WorldWithBelt(out BeltNode belt);
        world.Get("Farm")!.Add(new TunnelNode(new TilePos(4, 0), Direction.East, TunnelRole.Entrance));
        // Estado salvo antes da Fase 6b: sem "Layer"; o Kind 4 era a Esteira Subterrânea de vão fixo.
        const string json =
            "{\"Version\":1,\"Grids\":{\"Farm\":{\"Nodes\":["
            + "{\"X\":0,\"Y\":0,\"Kind\":0,\"Slots\":[{\"ItemRef\":0,\"Progress\":300,\"EntrySide\":3,\"Blocked\":false}]},"
            + "{\"X\":4,\"Y\":0,\"Kind\":4,\"Slots\":[{\"ItemRef\":1,\"Progress\":0,\"EntrySide\":3,\"Blocked\":false}]}"
            + "],\"Machines\":[]}}}";
        var orphans = new List<OrphanItem>();

        RuntimeStateSerializer.Import(world, JsonSerializer.Deserialize<WorldRuntimeState>(json)!, i => i == 0 ? item : old, orphans);

        Assert.Same(item, belt.Slot.Item);
        Assert.Same(old, Assert.Single(orphans).Item);
    }

    [Fact]
    public void ExportAndImport_ConserveItems()
    {
        var world = new LogisticsWorld();
        var farm = new GridBuilder(world.GetOrCreate("Farm"));
        farm.Belt(0, 0, Direction.East).Belt(1, 0, Direction.East).Belt(2, 0, Direction.East)
            .Item(0, 0, new FakeItem(Wood, 1), 100)
            .Item(1, 0, new FakeItem(Wood, 2), 200)
            .Item(2, 0, new FakeItem(Wood, 3), 300);
        new GridBuilder(world.GetOrCreate("Shed")).Belt(4, 4, Direction.North).Item(4, 4, new FakeItem(Wood, 4), 50);
        VirtualSlots furnace = farm.Grid.MachineSlots.GetOrCreate(new TilePos(5, 5));
        furnace.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        furnace.MainRequired = 5;
        furnace.Additional[FakeMachine.Ids.Coal] = new FakeItem(FakeMachine.Ids.Coal, 1);

        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));

        // No mundo novo, a esteira do meio e a do galpão foram removidas.
        var fresh = new LogisticsWorld();
        fresh.GetOrCreate("Farm").Add(new BeltNode(new TilePos(0, 0), Direction.East));
        fresh.GetOrCreate("Farm").Add(new BeltNode(new TilePos(2, 0), Direction.East));
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(fresh, state, i => items[i], orphans);

        List<ILogisticsItem> restored = fresh.Grids
            .SelectMany(g => g.Value.Nodes.SelectMany(n => n.Slots).Where(s => s.Item != null).Select(s => s.Item!)
                .Concat(g.Value.MachineSlots.Entries.SelectMany(e => e.Value.AllItems())))
            .ToList();
        Assert.Equal(6, items.Count);
        Assert.Equal(items.Count, restored.Count + orphans.Count);
        Assert.Equal(
            items.OrderBy(i => i.GetHashCode()),
            restored.Concat(orphans.Select(o => o.Item)).OrderBy(i => i.GetHashCode()));
    }

    [Fact]
    public void RoundTrip_ItemsInUndergroundFilterAndSplitter_AndSplitterPointers_SurviveInTheRightLayer()
    {
        var world = new LogisticsWorld();
        var farm = new GridBuilder(world.GetOrCreate("Farm"));

        // Esteira → entrada (1,0) → Divisor Subterrâneo (2,0) → Filtro Subterrâneo (3,0) → Esteira Subterrânea (4,0), sem
        // nada depois. Um Filtro da superfície vazio no mesmo tile do Filtro Subterrâneo. Qualidades diferentes para a
        // Esteira Subterrânea não juntar as pilhas (o Filtro Comum deixa passar qualquer qualidade).
        farm.Belt(0, 0, Direction.East).Tunnel(1, 0, Direction.East).UndergroundSplitter(2, 0)
            .UndergroundFilter(3, 0, Direction.East, Wood).Segment(4, 0, Direction.East)
            .Filter(3, 0, Direction.North, Wood);
        for (int i = 0; i < 4; i++)
        {
            for (int tick = 0; tick < 500 && !farm.BeltAt(0, 0).Slot.IsEmpty; tick++)
                farm.Run(1);
            farm.Item(0, 0, new FakeItem(Wood, i + 1, quality: i), Balance.End);
        }

        farm.Run(400);
        SplitterNode splitter = farm.UndergroundSplitterAt(2, 0);
        FilterNode filter = farm.UndergroundFilterAt(3, 0);
        Assert.Equal(1, farm.SegmentAt(4, 0).Slot.Item!.Count);
        Assert.Equal(2, filter.Slot.Item!.Count);
        Assert.Equal(3, splitter.Slot.Item!.Count);
        Assert.Equal(4, farm.TunnelAt(1, 0).Slot.Item!.Count);
        Assert.Equal(Direction.West, splitter.LastInput);
        Assert.Equal(Direction.East, splitter.LastOutput);

        var items = new List<ILogisticsItem>();
        WorldRuntimeState state = RuntimeStateSerializer.Export(world, it => Register(items, it));
        WorldRuntimeState json = JsonSerializer.Deserialize<WorldRuntimeState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(3, json.Grids["Farm"].Nodes.Count(n => n.Layer == NodeRuntimeState.UndergroundLayer));
        LogisticsWorld fresh = EmptyCopy(world);
        var orphans = new List<OrphanItem>();
        RuntimeStateSerializer.Import(fresh, json, i => items[i], orphans);

        Assert.Empty(orphans);
        LogisticsGrid restored = fresh.Get("Farm")!;
        var restoredSplitter = Assert.IsType<SplitterNode>(restored.GetUnderground(new TilePos(2, 0)));
        var restoredFilter = Assert.IsType<FilterNode>(restored.GetUnderground(new TilePos(3, 0)));
        Assert.Same(splitter.Slot.Item, restoredSplitter.Slot.Item);
        Assert.Equal(splitter.Slot.Progress, restoredSplitter.Slot.Progress);
        Assert.True(restoredSplitter.Slot.Blocked);
        Assert.Equal(Direction.West, restoredSplitter.LastInput);
        Assert.Equal(Direction.East, restoredSplitter.LastOutput);
        Assert.Same(filter.Slot.Item, restoredFilter.Slot.Item);
        Assert.True(restoredFilter.Slot.Blocked);
        Assert.True(((FilterNode)restored.Get(new TilePos(3, 0))!).Slot.IsEmpty);
    }

    private static int Register(List<ILogisticsItem> items, ILogisticsItem item)
    {
        items.Add(item);
        return items.Count - 1;
    }

    /// <summary>Um mundo com os mesmos nós (posição, tipo, direção, tier), vazios, como o adaptador recria ao carregar.</summary>
    private static LogisticsWorld EmptyCopy(LogisticsWorld world)
    {
        var copy = new LogisticsWorld();
        foreach ((string key, LogisticsGrid grid) in world.Grids)
        {
            LogisticsGrid target = copy.GetOrCreate(key);
            foreach (LogisticsNode node in grid.Nodes)
            {
                target.Add(node switch
                {
                    BeltNode belt => new BeltNode(belt.Position, belt.Facing, belt.Tier),
                    InserterNode inserter => new InserterNode(inserter.Position, inserter.Facing, inserter.Tier),
                    FilterNode filter => new FilterNode(filter.Position, filter.Facing, filter.Rule),
                    SplitterNode splitter => new SplitterNode(splitter.Position),
                    TunnelNode tunnel => new TunnelNode(tunnel.Position, tunnel.Facing, tunnel.Role),
                    _ => throw new InvalidOperationException($"Nó inesperado: {node.Kind}."),
                });
            }

            foreach (LogisticsNode node in grid.UndergroundNodes)
            {
                target.AddUnderground(node switch
                {
                    UndergroundSegmentNode segment => new UndergroundSegmentNode(segment.Position, segment.Facing),
                    FilterNode filter => new FilterNode(filter.Position, filter.Facing, filter.Rule),
                    SplitterNode splitter => new SplitterNode(splitter.Position),
                    _ => throw new InvalidOperationException($"Nó subterrâneo inesperado: {node.Kind}."),
                });
            }
        }

        return copy;
    }

    private static WorldRuntimeState StateWithBelt(params SlotState[] slots)
    {
        var node = new NodeRuntimeState { X = 0, Y = 0, Kind = NodeKind.Belt };
        node.Slots.AddRange(slots);
        var state = new WorldRuntimeState();
        state.Grids["Farm"] = new GridRuntimeState { Nodes = { node } };
        return state;
    }

    private static LogisticsWorld WorldWithBelt(out BeltNode belt)
    {
        var world = new LogisticsWorld();
        belt = new BeltNode(new TilePos(0, 0), Direction.East);
        world.GetOrCreate("Farm").Add(belt);
        return world;
    }
}
