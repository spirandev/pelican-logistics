using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

/// <summary>
/// Montagem padrão: Inseridor em (1,0) virado para o leste. Atrás = (0,0), frente = (2,0), laterais = (1,-1) e (1,1).
/// </summary>
public class InserterNodeTests
{
    /// <summary>Ticks da ida da mão (origem → destino).</summary>
    private const int TravelTicks = Balance.End / Balance.InserterSpeed;

    // Ciclo e balance.

    [Fact]
    public void Inserter_ChestToBelt_OnePerCycle_30TicksBetweenPickups_15TicksInHand()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        // A esteira é esvaziada depois de cada tick, então só o ciclo do Inseridor limita.
        var pickups = new List<int>();
        var deliveries = new List<int>();
        int before = chest.CountTotal();
        for (int tick = 1; tick <= 120; tick++)
        {
            g.Run(1);
            if (chest.CountTotal() < before)
                pickups.Add(tick);
            before = chest.CountTotal();

            var item = g.BeltAt(2, 0).Slot.Clear();
            if (item != null)
            {
                Assert.Equal(1, item.Count);
                deliveries.Add(tick);
            }
        }

        Assert.Equal(new[] { 1, 31, 61, 91 }, pickups);
        Assert.Equal(new[] { 16, 46, 76, 106 }, deliveries);
        Assert.Equal(15, TravelTicks);
        Assert.Equal(1, chest.CountTotal());
    }

    [Fact]
    public void Inserter_ChestToBelt_ItemTravelsInHand_ThenEntersBeltFromBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);
        InserterNode inserter = g.InserterAt(1, 0);

        g.Run(1);
        Assert.Equal("wood", inserter.Hand.Item!.QualifiedItemId);
        Assert.Equal(0, inserter.Hand.Progress);
        Assert.Equal(Direction.West, inserter.Hand.EntrySide);
        Assert.Equal(Direction.West, inserter.LastInput);
        Assert.Equal(4, chest.CountTotal());

        g.Run(TravelTicks - 1);
        Assert.Equal((TravelTicks - 1) * Balance.InserterSpeed, inserter.Hand.Progress);
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);

        g.Run(1);
        Assert.True(inserter.Hand.IsEmpty);
        Assert.Equal(Balance.InserterReturnTicks, inserter.Cooldown);
        TransitSlot slot = g.BeltAt(2, 0).Slot;
        Assert.Equal("wood", slot.Item!.QualifiedItemId);
        Assert.Equal(Direction.West, slot.EntrySide);
    }

    [Fact]
    public void Inserter_BeltToChest_BeltStopsAtEnd_InserterPullsOneByOne()
    {
        var chest = new FakeChest();
        var stack = new FakeItem("wood", 5);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, chest)
            .Item(0, 0, stack, Balance.End);

        var counts = new List<int>();
        for (int tick = 1; tick <= 140; tick++)
        {
            g.Run(1);
            counts.Add(chest.CountTotal());
            if (tick == 10)
            {
                Assert.Same(stack, g.BeltAt(0, 0).Slot.Item);
                Assert.Equal(4, stack.Count);
                Assert.True(g.BeltAt(0, 0).Slot.Blocked);
            }
        }

        // Pega nos ticks 1, 31, 61, 91 e 121; entrega 15 ticks depois.
        Assert.Equal(0, counts[14]);
        Assert.Equal(1, counts[15]);
        Assert.Equal(1, counts[44]);
        Assert.Equal(2, counts[45]);
        Assert.Equal(4, counts[134]);
        Assert.Equal(5, counts[135]);
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Equal(5, chest.CountById("wood"));
    }

    [Fact]
    public void Inserter_ChestToChest_WithoutBelt()
    {
        var source = new FakeChest(36, new FakeItem("wood", 3), new FakeItem("stone", 2));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target);

        g.Run(TravelTicks + 1);
        Assert.Equal(1, target.CountById("wood"));

        g.Run(200);

        Assert.Equal(0, source.CountTotal());
        Assert.Equal(3, target.CountById("wood"));
        Assert.Equal(2, target.CountById("stone"));
    }

    [Fact]
    public void Inserter_ChestWithRoomInExistingStack_Accepts()
    {
        var target = new FakeChest(1, new FakeItem("wood", 10));
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target)
            .Item(0, 0, new FakeItem("wood", 2), Balance.End);

        g.Run(100);

        Assert.Equal(12, target.CountById("wood"));
        Assert.Single(target.Items);
    }

    // Só o que o destino aceita.

    [Fact]
    public void Inserter_MixedChestToFurnace_TakesOnlyOre_AtMostFive_WoodStays()
    {
        var chest = new FakeChest(36, new FakeItem("(O)388", 10), new FakeItem(FakeMachine.Ids.CopperOre, 8));
        var furnace = FakeMachine.Furnace(processingTicks: 10_000);
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(2, 0, furnace);

        g.Run(1);
        Assert.Equal(FakeMachine.Ids.CopperOre, g.InserterAt(1, 0).Hand.Item!.QualifiedItemId);

        g.Run(400);

        VirtualSlots slots = g.SlotsAt(2, 0)!;
        Assert.Equal(FakeMachine.Ids.CopperOre, slots.Main!.QualifiedItemId);
        Assert.Equal(5, slots.Main.Count);
        Assert.Equal(10, chest.CountById("(O)388"));
        Assert.Equal(3, chest.CountById(FakeMachine.Ids.CopperOre));
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        Assert.Empty(furnace.Starts);
    }

    [Fact]
    public void Inserter_MixedChestWithOreAndCoal_Auto_FillsMainAndAdditional_FurnaceStarts()
    {
        var chest = new FakeChest(
            36,
            new FakeItem("(O)388", 10),
            new FakeItem(FakeMachine.Ids.CopperOre, 7),
            new FakeItem(FakeMachine.Ids.Coal, 3));
        var furnace = FakeMachine.Furnace(processingTicks: 10_000);
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(2, 0, furnace);

        g.Run(400);

        var start = Assert.Single(furnace.Starts);
        Assert.Equal(FakeMachine.Ids.CopperOre, start.Id);
        Assert.Equal(5, start.Count);
        Assert.Equal(new[] { (FakeMachine.Ids.Coal, 1) }, start.Additional);
        Assert.Equal(10, chest.CountById("(O)388"));
        Assert.Equal(2, chest.CountById(FakeMachine.Ids.CopperOre) + (g.SlotsAt(2, 0)?.Main?.Count ?? 0));
    }

    [Fact]
    public void Inserter_ChestWithOnlyWood_ToFurnace_TakesNothing_DoesNotJam()
    {
        var chest = new FakeChest(36, new FakeItem("(O)388", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(2, 0, FakeMachine.Furnace());

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        Assert.Null(g.InserterAt(1, 0).LastInput);
        Assert.Null(g.SlotsAt(2, 0));
    }

    [Fact]
    public void Inserter_BeltWithCoal_MainMode_CoalStaysOnBelt()
    {
        var coal = new FakeItem(FakeMachine.Ids.Coal, 3);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main).Item(0, 0, coal, Balance.End);

        g.Run(200);

        Assert.Same(coal, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(3, coal.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.Null(g.SlotsAt(2, 0));
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Inserter_FrontBeltOccupied_DoesNotTakeUntilItIsFree()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Item(2, 0, new FakeItem("stone", 1));

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        Assert.Equal("stone", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);

        g.BeltAt(2, 0).Slot.Clear();
        g.Run(1);

        Assert.Equal("wood", g.InserterAt(1, 0).Hand.Item!.QualifiedItemId);
        Assert.Equal(4, chest.CountTotal());
    }

    [Fact]
    public void Inserter_FullChest_TakesNothing()
    {
        var source = new FakeChest(36, new FakeItem("wood", 5));
        var target = new FakeChest(1, new FakeItem("stone", 999));
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target);

        g.Run(200);

        Assert.Equal(5, source.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Inserter_StackTier_ChestWithRoomForThree_TakesOnlyThree()
    {
        var source = new FakeChest(36, new FakeItem("wood", 10));
        var target = new FakeChest(1, new FakeItem("wood", 996));
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, target);

        g.Run(1);
        Assert.Equal(3, g.InserterAt(1, 0).Hand.Item!.Count);

        g.Run(200);

        Assert.Equal(999, target.CountTotal());
        Assert.Equal(7, source.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    // Origens e lados.

    [Fact]
    public void Inserter_BeltEndingOnItsSide_WithNothingBehind_Works()
    {
        var target = new FakeChest();
        var g = new GridBuilder()
            .Belt(1, -1, Direction.South)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target)
            .Item(1, -1, new FakeItem("wood", 2), Balance.End);

        g.Run(1);
        Assert.Equal(new[] { Direction.North, Direction.West }, g.InserterAt(1, 0).Inputs);
        Assert.Equal(Direction.North, g.InserterAt(1, 0).Hand.EntrySide);

        g.Run(100);

        Assert.Equal(2, target.CountById("wood"));
        Assert.True(g.BeltAt(1, -1).Slot.IsEmpty);
    }

    [Fact]
    public void Inserter_BeltPassingBySide_DeliversNothing()
    {
        var target = new FakeChest();
        var stack = new FakeItem("wood", 2);
        var g = new GridBuilder()
            .Belt(1, -1, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target)
            .Item(1, -1, stack, Balance.End);

        g.Run(200);

        Assert.Equal(new[] { Direction.West }, g.InserterAt(1, 0).Inputs);
        Assert.Equal(0, target.CountTotal());
        Assert.Same(stack, g.BeltAt(1, -1).Slot.Item);
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void Inserter_BeltPointingAtItsFront_DeliversNothing()
    {
        var stack = new FakeItem("wood", 2);
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.West)
            .Item(2, 0, stack, Balance.End);

        g.Run(200);

        Assert.DoesNotContain(Direction.East, g.InserterAt(1, 0).Inputs);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        Assert.Same(stack, g.BeltAt(2, 0).Slot.Item);
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void Inserter_ChestOnItsSide_IsIgnored_ChestBehindWorks()
    {
        var side = new FakeChest(36, new FakeItem("stone", 5));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(1, -1, side)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target);

        g.Run(200);
        Assert.Equal(0, target.CountTotal());
        Assert.Equal(5, side.CountTotal());

        var back = new FakeChest(36, new FakeItem("wood", 5));
        g.Chest(0, 0, back).Run(TravelTicks + 1);

        Assert.Equal(1, target.CountById("wood"));
        Assert.Equal(5, side.CountTotal());
    }

    [Fact]
    public void Inserter_ChestBehindAndBeltOnSide_Alternates()
    {
        var back = new FakeChest(36, new FakeItem("stone", 5));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, back)
            .Belt(1, -1, Direction.South)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target)
            .Item(1, -1, new FakeItem("wood", 5), Balance.End);

        Assert.Equal(new[] { Direction.North, Direction.West, Direction.North, Direction.West }, PickupSides(g, 1, 0, 4));
        Assert.Equal(3, back.CountTotal());
        Assert.Equal(3, g.BeltAt(1, -1).Slot.Item!.Count);
    }

    [Fact]
    public void Inserter_BeltBehindAndTwoSides_AlternatesClockwiseFromNorth_SkippingEmptyInputs()
    {
        var target = new FakeChest(36);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, -1, Direction.South)
            .Belt(1, 1, Direction.North)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target)
            .Item(0, 0, new FakeItem("back", 5), Balance.End)
            .Item(1, -1, new FakeItem("north", 1), Balance.End)
            .Item(1, 1, new FakeItem("south", 5), Balance.End);
        _ = g.Grid.ProcessingOrder;

        Assert.Equal(new[] { Direction.North, Direction.South, Direction.West }, g.InserterAt(1, 0).Inputs);

        // A esteira do norte só tinha 1 item: depois dela, o rodízio continua sem ela.
        Assert.Equal(
            new[] { Direction.North, Direction.South, Direction.West, Direction.South, Direction.West },
            PickupSides(g, 1, 0, 5));
    }

    [Fact]
    public void Inserter_InFrontOfAnotherInserter_TheyDoNotConnect()
    {
        var source = new FakeChest(36, new FakeItem("wood", 5));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(-1, 0, source)
            .Inserter(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target);

        g.Run(200);

        Assert.Equal(5, source.CountTotal());
        Assert.Equal(0, target.CountTotal());
        Assert.True(g.InserterAt(0, 0).Hand.IsEmpty);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Inserter_SideBeltInFrontWithNothingBehind_BecomesCurveLeavingInserter()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.North);

        g.Run(TravelTicks + 1);

        BeltNode belt = g.BeltAt(2, 0);
        Assert.True(belt.IsCurve);
        Assert.Equal(Direction.West, belt.InputSide);
        Assert.Equal("wood", belt.Slot.Item!.QualifiedItemId);
        Assert.Equal(Direction.West, belt.Slot.EntrySide);
    }

    [Fact]
    public void Inserter_SideBeltInFrontWithBeltBehindIt_StaysStraight_InserterTakesNothing()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.North)
            .Belt(2, 1, Direction.North);

        g.Run(200);

        Assert.Equal(Direction.South, g.BeltAt(2, 0).InputSide);
        Assert.Equal(5, chest.CountTotal());
    }

    [Fact]
    public void Inserter_WithoutDestination_TakesNothing()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Inserter_EmptyChest_DoesNothing()
    {
        var g = new GridBuilder()
            .Chest(0, 0, new FakeChest())
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(200);

        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
        Assert.Equal(0, g.InserterAt(1, 0).Cooldown);
        Assert.Null(g.InserterAt(1, 0).LastInput);
    }

    [Fact]
    public void Inserter_FromChest_TakesFirstAcceptedItemInInventoryOrder()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 1), new FakeItem("stone", 1));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal("wood", g.InserterAt(1, 0).Hand.Item!.QualifiedItemId);
        Assert.Equal(new[] { "stone" }, chest.Items.Select(i => i.QualifiedItemId));
    }

    [Fact]
    public void Inserter_PositionsAndProvidesOnlyToTileInFront()
    {
        var inserter = new InserterNode(new TilePos(0, 0), Direction.South);

        Assert.Equal(new TilePos(0, 1), inserter.FrontPosition);
        Assert.Equal(new TilePos(0, -1), inserter.BackPosition);
        Assert.True(inserter.ProvidesTo(new TilePos(0, 1)));
        Assert.False(inserter.ProvidesTo(new TilePos(0, -1)));
        Assert.Same(inserter.Hand, Assert.Single(inserter.Slots));
        Assert.Null(inserter.GetOutgoingSlot(new TilePos(0, 1)));
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.South)]
    [InlineData(Direction.East)]
    [InlineData(Direction.West)]
    public void Inserter_IsNeverPushedInto_ItPulls(Direction entrySide)
    {
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, new FakeChest());
        var ctx = new TickContext(g.Grid, g.Endpoints);
        var item = new FakeItem("wood", 5);
        InserterNode inserter = g.InserterAt(1, 0);

        Assert.False(inserter.CanReceiveFrom(entrySide));
        Assert.False(inserter.CanAccept(entrySide, item));
        Assert.Equal(0, inserter.Accept(entrySide, item, ctx));
        Assert.Equal(5, item.Count);
    }

    // Máquinas como origem (migrados do Extrator). Montagem: máquina (0,0) → Inseridor (1,0) → baú (2,0).

    private static GridBuilder PickupLine(FakeMachine machine, PickupSource source, FakeChest target, TransferTier tier = TransferTier.Single)
    {
        return new GridBuilder()
            .Machine(0, 0, machine)
            .Inserter(1, 0, Direction.East, source, tier)
            .Chest(2, 0, target);
    }

    [Fact]
    public void Inserter_FurnaceProduct_ToChest_BarsComeOutOneByOne()
    {
        var furnace = FakeMachine.Furnace();
        furnace.Output = new FakeItem(FakeMachine.Ids.CopperBar, 3);
        var target = new FakeChest();
        var g = PickupLine(furnace, PickupSource.Product, target);

        var deliveries = new List<int>();
        for (int tick = 1; tick <= 120; tick++)
        {
            int before = target.CountTotal();
            g.Run(1);
            if (target.CountTotal() > before)
                deliveries.Add(tick);
        }

        Assert.Equal(new[] { 16, 46, 76 }, deliveries);
        Assert.Equal(3, target.CountById(FakeMachine.Ids.CopperBar));
        Assert.Null(furnace.Output);
    }

    [Fact]
    public void Inserter_Product_StackTier_TakesWholeOutput()
    {
        var furnace = FakeMachine.Furnace();
        var output = new FakeItem(FakeMachine.Ids.CopperBar, 3);
        furnace.Output = output;
        var target = new FakeChest();
        var g = PickupLine(furnace, PickupSource.Product, target, TransferTier.Stack);

        g.Run(TravelTicks + 1);

        Assert.Same(output, Assert.Single(target.Items));
        Assert.Null(furnace.Output);
    }

    [Fact]
    public void Inserter_Product_LimitedToWhatFitsInChest()
    {
        var furnace = FakeMachine.Furnace();
        furnace.Output = new FakeItem(FakeMachine.Ids.CopperBar, 5);
        var target = new FakeChest(1, new FakeItem(FakeMachine.Ids.CopperBar, 997));
        var g = PickupLine(furnace, PickupSource.Product, target, TransferTier.Stack);

        g.Run(200);

        Assert.Equal(999, target.CountTotal());
        Assert.Equal(3, furnace.Output!.Count);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Inserter_Product_DoesNotTake_WhileThereIsNoProduct()
    {
        var target = new FakeChest();
        var g = PickupLine(FakeMachine.Furnace(), PickupSource.Product, target);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        slots.MainRequired = 5;

        g.Run(200);

        Assert.Equal(0, target.CountTotal());
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        Assert.Equal(3, g.SlotsAt(0, 0)!.Main!.Count);
    }

    [Fact]
    public void Inserter_Main_ReturnsStoredOresToBelt()
    {
        var g = new GridBuilder()
            .Machine(0, 0, FakeMachine.Furnace())
            .Inserter(1, 0, Direction.East, PickupSource.Main)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(4, 0, Direction.East);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        slots.MainRequired = 5;

        g.Run(1);
        Assert.Equal(2, g.SlotsAt(0, 0)!.Main!.Count);
        Assert.Equal(1, g.InserterAt(1, 0).Hand.Item!.Count);

        g.Run(1000);

        Assert.Null(g.SlotsAt(0, 0));
        Assert.Equal(3, g.CountInTransit());
        Assert.Equal(FakeMachine.Ids.CopperOre, g.BeltAt(4, 0).Slot.Item!.QualifiedItemId);
    }

    [Fact]
    public void Inserter_Additional_ReturnsStoredCoal()
    {
        var target = new FakeChest();
        var g = PickupLine(FakeMachine.Furnace(), PickupSource.Additional, target);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(0, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 3);
        slots.MainRequired = 5;
        slots.Additional[FakeMachine.Ids.Coal] = new FakeItem(FakeMachine.Ids.Coal, 1);

        g.Run(TravelTicks + 1);

        Assert.Equal(1, target.CountById(FakeMachine.Ids.Coal));
        Assert.Empty(g.SlotsAt(0, 0)!.Additional);
        Assert.Equal(3, g.SlotsAt(0, 0)!.Main!.Count);
    }

    [Fact]
    public void Inserter_Main_DoesNotTakeProduct()
    {
        var furnace = FakeMachine.Furnace();
        furnace.Output = new FakeItem(FakeMachine.Ids.CopperBar, 1);
        var target = new FakeChest();
        var g = PickupLine(furnace, PickupSource.Main, target);

        g.Run(200);

        Assert.Equal(0, target.CountTotal());
        Assert.NotNull(furnace.Output);
    }

    [Fact]
    public void Inserter_SourceIsIgnoredForChests()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East, PickupSource.Main)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal(4, chest.CountTotal());
    }

    [Fact]
    public void Inserter_MachineOnItsSide_IsIgnored()
    {
        var furnace = FakeMachine.Furnace();
        furnace.Output = new FakeItem(FakeMachine.Ids.CopperBar, 1);
        var target = new FakeChest();
        var g = new GridBuilder()
            .Machine(1, -1, furnace)
            .Inserter(1, 0, Direction.East, PickupSource.Product)
            .Chest(2, 0, target);

        g.Run(200);

        Assert.Equal(0, target.CountTotal());
        Assert.NotNull(furnace.Output);
    }

    // Máquinas como destino (Fase 2). Montagem: esteiras (-1,0) e (0,0) → Inseridor (1,0) → máquina (2,0).

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

        g.Run(TravelTicks + 1);
        Assert.Equal(1, g.SlotsAt(2, 0)!.Main!.Count);

        g.Run(300);

        VirtualSlots slots = g.SlotsAt(2, 0)!;
        Assert.Equal(FakeMachine.Ids.CopperOre, slots.Main!.QualifiedItemId);
        Assert.Equal(5, slots.Main.Count);
        Assert.Equal(5, slots.MainRequired);
        Assert.Same(ore, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(3, ore.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
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
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Additional, TransferTier.Stack).Item(0, 0, coal, Balance.End);

        g.Run(200);

        Assert.Equal(1, g.SlotsAt(2, 0)!.Additional[FakeMachine.Ids.Coal].Count);
        Assert.Equal(2, coal.Count);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
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
    public void Inserter_StackTier_TakesOnlyWhatMainIsMissing()
    {
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 8);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main, TransferTier.Stack).Item(0, 0, ore, Balance.End);

        g.Run(1);
        Assert.Equal(5, g.InserterAt(1, 0).Hand.Item!.Count);
        Assert.Equal(3, ore.Count);

        g.Run(TravelTicks);
        Assert.Equal(5, g.SlotsAt(2, 0)!.Main!.Count);
    }

    [Fact]
    public void Inserter_StackTier_WholeStackThatFits_IsStoredAsSameInstance()
    {
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 5);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main, TransferTier.Stack).Item(0, 0, ore, Balance.End);

        g.Run(TravelTicks + 1);

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
    public void Inserter_MachineDoesNotAcceptItem_TakesNothing()
    {
        var wood = new FakeItem("(O)388", 5);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Auto).Item(0, 0, wood, Balance.End);

        g.Run(200);

        Assert.Equal(5, wood.Count);
        Assert.Null(g.SlotsAt(2, 0));
    }

    [Fact]
    public void Inserter_Crystalarium_AcceptsFirstGem_ThenRefusesWhileLoaded()
    {
        var machine = FakeMachine.Crystalarium(processingTicks: 50);
        var gems = new FakeItem(FakeMachine.Ids.Diamond, 3, category: FakeMachine.Ids.GemCategory);
        var g = MachineLine(machine, InserterSlotMode.Auto).Item(0, 0, gems, Balance.End);

        g.Run(TravelTicks + 1);
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
    public void Inserter_Crystalarium_ProductInserterTakesTheCopies()
    {
        var machine = FakeMachine.Crystalarium(processingTicks: 50);
        var gems = new FakeItem(FakeMachine.Ids.Diamond, 3, category: FakeMachine.Ids.GemCategory);
        var copies = new FakeChest();
        var g = MachineLine(machine, InserterSlotMode.Auto)
            .Inserter(2, 1, Direction.South, PickupSource.Product)
            .Chest(2, 2, copies)
            .Item(0, 0, gems, Balance.End);

        g.Run(1000);

        Assert.Single(machine.Starts);
        Assert.Equal(2, gems.Count);
        Assert.InRange(copies.CountById(FakeMachine.Ids.Diamond), 10, 20);
    }

    // Mão.

    [Fact]
    public void Inserter_TargetFillsDuringTravel_ItemWaitsInHand_ThenIsDelivered()
    {
        // Dois Inseridores abastecem o Principal da mesma fornalha, que tem 4/5 minérios e o carvão: os dois
        // pegam 1 minério (cabe 1); o primeiro a chegar completa o slot e o outro espera com o minério na mão.
        var furnace = FakeMachine.Furnace(processingTicks: 10_000);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, InserterSlotMode.Main)
            .Machine(2, 0, furnace)
            .Inserter(3, 0, Direction.West, InserterSlotMode.Main)
            .Belt(4, 0, Direction.West)
            .Item(0, 0, new FakeItem(FakeMachine.Ids.CopperOre, 1), Balance.End)
            .Item(4, 0, new FakeItem(FakeMachine.Ids.CopperOre, 1), Balance.End);
        VirtualSlots slots = g.Grid.MachineSlots.GetOrCreate(new TilePos(2, 0));
        slots.Main = new FakeItem(FakeMachine.Ids.CopperOre, 4);
        slots.MainRequired = 5;
        slots.Additional[FakeMachine.Ids.Coal] = new FakeItem(FakeMachine.Ids.Coal, 1);

        g.Run(1);
        Assert.NotNull(g.InserterAt(1, 0).Hand.Item);
        Assert.NotNull(g.InserterAt(3, 0).Hand.Item);

        // O primeiro completa o Principal e a fornalha liga no fim do tick, consumindo os 5 minérios.
        g.Run(TravelTicks);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
        TransitSlot waiting = g.InserterAt(3, 0).Hand;
        Assert.Equal(1, waiting.Item!.Count);
        Assert.True(waiting.Blocked);
        Assert.Single(furnace.Starts);

        // No tick seguinte o Principal está vazio de novo e o minério da mão entra.
        g.Run(1);
        Assert.True(waiting.IsEmpty);
        Assert.Equal(1, g.SlotsAt(2, 0)!.Main!.Count);
        Assert.Equal(Balance.InserterReturnTicks, g.InserterAt(3, 0).Cooldown);
    }

    [Fact]
    public void Inserter_FullChestWhileCarrying_ItemWaitsInHand_UntilThereIsRoom()
    {
        var source = new FakeChest(36, new FakeItem("wood", 5));
        var target = new FakeChest(1);
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, target);

        g.Run(1);
        target.Items.Add(new FakeItem("stone", 999)); // o baú enche durante a ida
        g.Run(200);

        InserterNode inserter = g.InserterAt(1, 0);
        Assert.Equal("wood", inserter.Hand.Item!.QualifiedItemId);
        Assert.True(inserter.Hand.Blocked);
        Assert.Equal(4, source.CountTotal());

        target.Items.Clear();
        g.Run(1);

        Assert.True(inserter.Hand.IsEmpty);
        Assert.Equal(1, target.CountById("wood"));
    }

    [Fact]
    public void Inserter_RemovedWithItemInHand_ItemComesInRemovalResult()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);
        g.Run(5);
        var inHand = g.InserterAt(1, 0).Hand.Item!;

        RemovalResult result = g.Grid.Remove(new TilePos(1, 0));

        Assert.Same(inHand, Assert.Single(result.Items));
        Assert.Equal(4, chest.CountTotal());
    }

    // Tiers.

    [Fact]
    public void Inserter_StackTier_FromChest_TakesWholeSlot()
    {
        var first = new FakeItem("wood", 50);
        var chest = new FakeChest(36, first, new FakeItem("wood", 20));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Same(first, g.InserterAt(1, 0).Hand.Item);
        Assert.Equal(20, chest.CountTotal());
    }

    [Fact]
    public void Inserter_StackTier_FromBelt_TakesWholeStack()
    {
        var target = new FakeChest();
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, target)
            .Item(0, 0, stack, Balance.End);

        g.Run(TravelTicks + 1);

        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
        Assert.Same(stack, Assert.Single(target.Items));
    }

    [Fact]
    public void Inserter_ConfigurableTier_JoinsTwoSlotsOfChest()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 10), new FakeItem("stone", 9), new FakeItem("wood", 10));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Inserter(1, 0, Direction.East, TransferTier.Configurable)
            .Belt(2, 0, Direction.East);
        g.InserterAt(1, 0).ConfiguredAmount = 15;

        g.Run(1);

        Assert.Equal("wood", g.InserterAt(1, 0).Hand.Item!.QualifiedItemId);
        Assert.Equal(15, g.InserterAt(1, 0).Hand.Item!.Count);
        Assert.Equal(5, chest.CountById("wood"));
        Assert.Equal(9, chest.CountById("stone"));
    }

    [Fact]
    public void Inserter_ConfigurableTier_FromBelt_TakesConfiguredAmount()
    {
        var target = new FakeChest();
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Configurable)
            .Chest(2, 0, target)
            .Item(0, 0, stack, Balance.End);
        g.InserterAt(1, 0).ConfiguredAmount = 20;

        g.Run(TravelTicks + 1);

        Assert.Equal(20, target.CountTotal());
        Assert.Equal(30, stack.Count);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5000, Balance.MaxConfigurableAmount)]
    public void Inserter_ConfiguredAmount_IsClamped(int value, int expected)
    {
        var inserter = new InserterNode(new TilePos(0, 0), Direction.East, TransferTier.Configurable)
        {
            ConfiguredAmount = value,
        };

        Assert.Equal(expected, inserter.ConfiguredAmount);
    }

    [Fact]
    public void Inserter_StackTier_BeltToAlmostFullChest_InsertsWhatFits_RestStaysOnBelt()
    {
        var target = new FakeChest(1, new FakeItem("wood", 970));
        var stack = new FakeItem("wood", 50);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, target)
            .Item(0, 0, stack, Balance.End);

        g.Run(200);

        Assert.Equal(999, target.CountTotal());
        Assert.Same(stack, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(21, stack.Count);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Inserter_ConfigurableTier_Amount5_StackOf12_EntersInThreeOperations()
    {
        var target = new FakeChest();
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Configurable)
            .Chest(2, 0, target)
            .Item(0, 0, new FakeItem("wood", 12), Balance.End);
        InserterNode inserter = g.InserterAt(1, 0);
        inserter.ConfiguredAmount = 5;

        var operations = new List<int>();
        for (int tick = 0; tick < 300; tick++)
        {
            bool wasEmpty = inserter.Hand.IsEmpty;
            g.Run(1);
            if (wasEmpty && !inserter.Hand.IsEmpty)
                operations.Add(inserter.Hand.Item!.Count);
        }

        Assert.Equal(new[] { 5, 5, 2 }, operations);
        Assert.Equal(12, target.CountTotal());
        Assert.True(g.BeltAt(0, 0).Slot.IsEmpty);
    }

    // Regulador (Fase 6).

    [Fact]
    public void Regulator_General50_ChestWith45_ReceivesOnly5OfAStackOf20_ThenStops()
    {
        var target = new FakeChest(36, new FakeItem("stone", 45));
        var stack = new FakeItem("wood", 20);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, target)
            .Regulator(1, 0, new RegulatorRule { Mode = RegulatorMode.General, GeneralLimit = 50 })
            .Item(0, 0, stack, Balance.End);

        g.Run(1);
        Assert.Equal(5, g.InserterAt(1, 0).Hand.Item!.Count);

        g.Run(300);

        Assert.Equal(50, target.CountTotal());
        Assert.Equal(5, target.CountById("wood"));
        Assert.Equal(15, stack.Count);
        Assert.Same(stack, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.InserterAt(1, 0).Hand.IsEmpty);
    }

    [Fact]
    public void Regulator_PerItem_WoodStopsAt50_StoneAt100_ClayIsNotLimited()
    {
        var source = new FakeChest(36, new FakeItem("wood", 80), new FakeItem("stone", 150), new FakeItem("clay", 40));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East, TransferTier.Configurable)
            .Chest(2, 0, target)
            .Regulator(1, 0, new RegulatorRule { Mode = RegulatorMode.PerItem, PerItemLimits = { ["wood"] = 50, ["stone"] = 100 } });
        g.InserterAt(1, 0).ConfiguredAmount = 30;

        g.Run(1000);

        Assert.Equal(50, target.CountById("wood"));
        Assert.Equal(100, target.CountById("stone"));
        Assert.Equal(40, target.CountById("clay"));
        Assert.Equal(30, source.CountById("wood"));
        Assert.Equal(50, source.CountById("stone"));
        Assert.Equal(0, source.CountById("clay"));
    }

    [Fact]
    public void Regulator_ChestChangesDuringTravel_DeliversOnlyWhatIsAllowed_RestWaitsInHand()
    {
        var target = new FakeChest(36, new FakeItem("stone", 40));
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, target)
            .Regulator(1, 0, new RegulatorRule { GeneralLimit = 50 })
            .Item(0, 0, new FakeItem("wood", 20), Balance.End);

        g.Run(1);
        Assert.Equal(10, g.InserterAt(1, 0).Hand.Item!.Count);

        // Outro Inseridor (ou o jogador) põe 7 itens no baú durante a ida.
        target.Insert(new FakeItem("clay", 7), 7);
        g.Run(TravelTicks);

        Assert.Equal(50, target.CountTotal());
        Assert.Equal(3, target.CountById("wood"));
        Assert.Equal(7, g.InserterAt(1, 0).Hand.Item!.Count);
        Assert.True(g.InserterAt(1, 0).Hand.Blocked);
    }

    [Fact]
    public void Regulator_IsIgnored_WhenTheDestinationIsAMachine()
    {
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 8);
        var g = MachineLine(FakeMachine.Furnace(), InserterSlotMode.Main, TransferTier.Stack)
            .Regulator(1, 0, new RegulatorRule { GeneralLimit = 0 })
            .Item(0, 0, ore, Balance.End);

        g.Run(TravelTicks + 1);

        Assert.Equal(5, g.SlotsAt(2, 0)!.Main!.Count);
        Assert.Equal(3, ore.Count);
    }

    [Fact]
    public void Regulator_Removed_InserterIsNoLongerLimited()
    {
        var target = new FakeChest(36, new FakeItem("stone", 50));
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Inserter(1, 0, Direction.East, TransferTier.Stack)
            .Chest(2, 0, target)
            .Regulator(1, 0, new RegulatorRule { GeneralLimit = 50 })
            .Item(0, 0, new FakeItem("wood", 20), Balance.End);
        g.Run(100);
        Assert.Equal(50, target.CountTotal());

        g.Grid.RemoveAccessory(new TilePos(1, 0));
        g.Run(100);

        Assert.Equal(70, target.CountTotal());
    }

    /// <summary>Roda até o Inseridor pegar 'count' vezes e devolve o lado de cada pegada.</summary>
    private static List<Direction> PickupSides(GridBuilder g, int x, int y, int count)
    {
        InserterNode inserter = g.InserterAt(x, y);
        var sides = new List<Direction>();
        for (int tick = 0; tick < 1000 && sides.Count < count; tick++)
        {
            bool wasEmpty = inserter.Hand.IsEmpty;
            g.Run(1);
            if (wasEmpty && !inserter.Hand.IsEmpty)
                sides.Add(inserter.Hand.EntrySide);
        }

        return sides;
    }
}
