using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Tests.Fakes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Nodes;

public class InserterNodeTests
{
    [Fact]
    public void Inserter_InsertsOneEvery30Ticks_RestWaitsOnBelt()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        var counts = new List<int>();
        for (int tick = 1; tick <= 130; tick++)
        {
            g.Run(1);
            counts.Add(chest.CountTotal());
        }

        // Insere nos ticks 1, 31, 61, 91 e 121.
        Assert.Equal(1, counts[0]);
        Assert.Equal(1, counts[29]);
        Assert.Equal(2, counts[30]);
        Assert.Equal(3, counts[60]);
        Assert.Equal(4, counts[90]);
        Assert.Equal(4, counts[119]);
        Assert.Equal(5, counts[120]);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Equal(5, chest.CountById("wood"));
    }

    [Fact]
    public void Inserter_StackStaysBlockedOnBelt_WhileInserting()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        g.Run(10);

        Assert.Same(stack, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(4, stack.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
    }

    [Fact]
    public void Inserter_FullChest_InsertsNothingAndStackStaysBlocked()
    {
        var chest = new FakeChest(1, new FakeItem("stone", 999));
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        g.Run(200);

        Assert.Equal(0, chest.CountById("wood"));
        Assert.Equal(5, stack.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
    }

    [Fact]
    public void Inserter_ChestWithRoomInExistingStack_Accepts()
    {
        var chest = new FakeChest(1, new FakeItem("wood", 10));
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, new FakeItem("wood", 2), Balance.End);

        g.Run(40);

        Assert.Equal(12, chest.CountById("wood"));
        Assert.Single(chest.Items);
    }

    [Fact]
    public void Inserter_RefusesBeltPointingAtItsSide()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Belt(1, -1, Direction.South)
            .Item(1, -1, stack, Balance.End);

        g.Run(200);

        Assert.Equal(0, chest.CountTotal());
        Assert.Equal(5, stack.Count);
        Assert.True(g.BeltAt(1, -1).Slot.Blocked);
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.South)]
    [InlineData(Direction.East)]
    public void Inserter_RefusesFromSideOrFront(Direction entrySide)
    {
        var chest = new FakeChest();
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest);
        var ctx = new TickContext(g.Grid, g.Endpoints);
        var item = new FakeItem("wood", 5);

        int taken = g.NodeAt<InserterNode>(1, 0).Accept(entrySide, item, ctx);

        Assert.Equal(0, taken);
        Assert.Equal(5, item.Count);
        Assert.Equal(0, chest.CountTotal());
    }

    [Fact]
    public void Inserter_WithoutChest_InsertsNothing()
    {
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Item(0, 0, stack, Balance.End);

        g.Run(100);

        Assert.Equal(5, stack.Count);
        Assert.Equal(0, g.NodeAt<InserterNode>(1, 0).Cooldown);
    }

    [Fact]
    public void Inserter_StackTier_InsertsWholeStack()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        g.Run(1);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Same(stack, Assert.Single(chest.Items));
    }

    [Fact]
    public void Inserter_ConfigurableTier_InsertsConfiguredAmount()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Configurable)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);
        g.NodeAt<InserterNode>(1, 0).ConfiguredAmount = 20;

        g.Run(1);

        Assert.Equal(20, chest.CountTotal());
        Assert.Equal(30, stack.Count);
    }

    // Máquinas (Fase 2). Montagem padrão: esteira (0,0) → Inseridor (1,0) → máquina (2,0).

    private static GridBuilder MachineLine(FakeMachine machine, InserterSlotMode mode, TransferTier tier = TransferTier.Single)
    {
        return new GridBuilder()
            .Belt(-1, 0, Direction.East)
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, mode, tier)
            .Machine(2, 0, machine);
    }

    [Fact]
    public void Inserter_Auto_FillsFurnaceMainOneByOneUpToFive_RestWaitsOnBelt()
    {
        var machine = FakeMachine.Furnace();
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 8);
        var g = MachineLine(machine, InserterSlotMode.Auto).Item(0, 0, ore, Balance.End);

        g.Run(1);
        Assert.Equal(1, g.SlotsAt(2, 0)!.Main!.Count);

        g.Run(300);

        VirtualSlots slots = g.SlotsAt(2, 0)!;
        Assert.Equal(FakeMachine.Ids.CopperOre, slots.Main!.QualifiedItemId);
        Assert.Equal(5, slots.Main.Count);
        Assert.Equal(5, slots.MainRequired);
        Assert.Same(ore, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(3, ore.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.Empty(machine.Starts);
    }

    [Fact]
    public void Inserter_Auto_CoalGoesToAdditional_OreGoesToMain()
    {
        var machine = FakeMachine.Furnace();
        var g = MachineLine(machine, InserterSlotMode.Auto)
            .Item(0, 0, new FakeItem(FakeMachine.Ids.Coal, 1), Balance.End)
            .Item(-1, 0, new FakeItem(FakeMachine.Ids.CopperOre, 3), Balance.End);

        g.Run(400);

        VirtualSlots slots = g.SlotsAt(2, 0)!;
        Assert.Equal(1, slots.Additional[FakeMachine.Ids.Coal].Count);
        Assert.Equal(FakeMachine.Ids.CopperOre, slots.Main!.QualifiedItemId);
        Assert.Equal(3, slots.Main.Count);
        Assert.Equal(0, g.CountInTransit());
        Assert.Empty(machine.Starts);
    }

    [Fact]
    public void Inserter_MainMode_RefusesCoal_CoalStaysOnBelt()
    {
        var coal = new FakeItem(FakeMachine.Ids.Coal, 3);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main).Item(0, 0, coal, Balance.End);

        g.Run(200);

        Assert.Same(coal, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(3, coal.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.Null(g.SlotsAt(2, 0));
        Assert.Equal(0, g.NodeAt<InserterNode>(1, 0).Cooldown);
    }

    [Fact]
    public void Inserter_AdditionalMode_RefusesOre()
    {
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 5);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Additional).Item(0, 0, ore, Balance.End);

        g.Run(200);

        Assert.Same(ore, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(5, ore.Count);
        Assert.Null(g.SlotsAt(2, 0));
    }

    [Fact]
    public void Inserter_AdditionalMode_InsertsOnlyWhatIsMissing()
    {
        var coal = new FakeItem(FakeMachine.Ids.Coal, 3);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Additional).Item(0, 0, coal, Balance.End);

        g.Run(200);

        Assert.Equal(1, g.SlotsAt(2, 0)!.Additional[FakeMachine.Ids.Coal].Count);
        Assert.Equal(2, coal.Count);
    }

    [Fact]
    public void Inserter_AutoMode_CoalWithAdditionalComplete_IsRefused()
    {
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Auto)
            .Item(0, 0, new FakeItem(FakeMachine.Ids.Coal, 3), Balance.End);

        g.Run(200);

        Assert.Equal(1, g.SlotsAt(2, 0)!.Additional[FakeMachine.Ids.Coal].Count);
        Assert.Null(g.SlotsAt(2, 0)!.Main);
        Assert.Equal(2, g.BeltAt(0, 0).Slot.Item!.Count);
    }

    [Fact]
    public void Inserter_StackTier_InsertsOnlyWhatMainIsMissing()
    {
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 8);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main, TransferTier.Stack).Item(0, 0, ore, Balance.End);

        g.Run(1);

        Assert.Equal(5, g.SlotsAt(2, 0)!.Main!.Count);
        Assert.Equal(3, ore.Count);
    }

    [Fact]
    public void Inserter_StackTier_WholeStackThatFits_IsStoredAsSameInstance()
    {
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 5);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main, TransferTier.Stack).Item(0, 0, ore, Balance.End);

        g.Run(1);

        Assert.Same(ore, g.SlotsAt(2, 0)!.Main);
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Inserter_MainWithCopper_AcceptsOnlyCopperUntilComplete_RefusesIron()
    {
        var iron = new FakeItem(FakeMachine.Ids.IronOre, 5);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main)
            .Item(0, 0, new FakeItem(FakeMachine.Ids.CopperOre, 2), Balance.End)
            .Item(-1, 0, iron, Balance.End);

        g.Run(400);

        VirtualSlots slots = g.SlotsAt(2, 0)!;
        Assert.Equal(FakeMachine.Ids.CopperOre, slots.Main!.QualifiedItemId);
        Assert.Equal(2, slots.Main.Count);
        Assert.Same(iron, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(5, iron.Count);
    }

    [Fact]
    public void Inserter_Crystalarium_AcceptsFirstGem_ThenRefusesWhileLoaded()
    {
        var machine = FakeMachine.Crystalarium(processingTicks: 50);
        var gems = new FakeItem(FakeMachine.Ids.Diamond, 3, category: FakeMachine.Ids.GemCategory);
        var g = MachineLine(machine, InserterSlotMode.Auto).Item(0, 0, gems, Balance.End);

        g.Run(1);
        Assert.Single(machine.Starts);
        Assert.True(machine.HasLoadedInput);
        Assert.Null(g.SlotsAt(2, 0)); // o slot está vazio, mas o Cristalário está carregado

        g.Run(300);

        Assert.Single(machine.Starts);
        Assert.Equal(2, gems.Count);
        Assert.Null(g.SlotsAt(2, 0));

        // Coletar o produto faz o Cristalário recomeçar sozinho, e ele continua recusando gemas.
        machine.TakeOutput(int.MaxValue);
        g.Run(300);
        Assert.Single(machine.Starts);
        Assert.Equal(2, gems.Count);
    }

    [Fact]
    public void Inserter_Crystalarium_RefusesSecondGemWhileFirstIsInSlot()
    {
        // Ocupado (com produto guardado), o Cristalário não inicia; o slot guarda a gema e recusa outra.
        var machine = FakeMachine.Crystalarium();
        machine.Output = new FakeItem(FakeMachine.Ids.Diamond, 1, category: FakeMachine.Ids.GemCategory);
        var gems = new FakeItem(FakeMachine.Ids.Diamond, 3, category: FakeMachine.Ids.GemCategory);
        var g = MachineLine(machine, InserterSlotMode.Main).Item(0, 0, gems, Balance.End);

        g.Run(200);

        Assert.Equal(1, g.SlotsAt(2, 0)!.Main!.Count);
        Assert.Equal(2, gems.Count);
        Assert.Empty(machine.Starts);
    }

    [Fact]
    public void Inserter_MachineDoesNotAcceptItem_InsertsNothing()
    {
        var wood = new FakeItem("(O)388", 5);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Auto).Item(0, 0, wood, Balance.End);

        g.Run(200);

        Assert.Equal(5, wood.Count);
        Assert.Null(g.SlotsAt(2, 0));
    }
}
