using System.Text.Json;
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
                    _ => throw new InvalidOperationException($"Nó inesperado: {node.Kind}."),
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
