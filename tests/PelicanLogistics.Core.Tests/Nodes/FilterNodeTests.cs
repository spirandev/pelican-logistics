using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

/// <summary>Montagem padrão: esteira em (0,0) → Filtro (madeira) em (1,0) → esteira em (2,0), tudo para o leste.</summary>
public class FilterNodeTests
{
    private const string Wood = "(O)388";
    private const string Stone = "(O)390";

    /// <summary>Ticks para atravessar o filtro (velocidade máxima).</summary>
    private const int FilterTicks = Balance.End / Balance.FilterSpeed;

    private static GridBuilder Line()
    {
        return new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Filter(1, 0, Direction.East, Wood)
            .Belt(2, 0, Direction.East);
    }

    [Fact]
    public void Filter_AcceptedItem_CrossesAtFilterSpeed()
    {
        var wood = new FakeItem(Wood, 3);
        GridBuilder g = Line().Item(0, 0, wood, Balance.End);

        // Tick 1: entra no filtro, no início do tile.
        g.Run(1);
        Assert.Same(wood, g.FilterAt(1, 0).Slot.Item);
        Assert.Equal(0, g.FilterAt(1, 0).Slot.Progress);
        Assert.Equal(Direction.West, g.FilterAt(1, 0).Slot.EntrySide);

        g.Run(FilterTicks - 1);
        Assert.Equal(Balance.End - Balance.FilterSpeed, g.FilterAt(1, 0).Slot.Progress);

        g.Run(1);
        Assert.True(g.FilterAt(1, 0).Slot.IsEmpty);
        Assert.Same(wood, g.BeltAt(2, 0).Slot.Item);
        Assert.Equal(3, wood.Count);
    }

    [Fact]
    public void Filter_RejectedItem_StaysOnPreviousBelt_Blocked()
    {
        var stone = new FakeItem(Stone, 1);
        GridBuilder g = Line().Item(0, 0, stone, Balance.End);

        g.Run(100);

        Assert.Same(stone, g.BeltAt(0, 0).Slot.Item);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
        Assert.True(g.FilterAt(1, 0).Slot.IsEmpty);
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Filter_DoesNotAcceptFromTheSide_AndNeverCurves()
    {
        // Esteira descendo do norte até a lateral do filtro, sem nada atrás do filtro.
        var wood = new FakeItem(Wood, 1);
        var g = new GridBuilder()
            .Belt(1, -1, Direction.South)
            .Filter(1, 0, Direction.East, Wood)
            .Belt(2, 0, Direction.East)
            .Item(1, -1, wood, Balance.End);

        g.Run(100);

        FilterNode filter = g.FilterAt(1, 0);
        Assert.Equal(Direction.West, filter.InputSide);
        Assert.False(filter.CanReceiveFrom(Direction.North));
        Assert.Same(wood, g.BeltAt(1, -1).Slot.Item);
        Assert.True(g.BeltAt(1, -1).Slot.Blocked);
        Assert.True(filter.Slot.IsEmpty);
    }

    [Fact]
    public void Filter_NeverMerges_SameItemArrivingBehindAStoppedStack()
    {
        // Sem nada à frente do filtro: a primeira madeira para no fim dele.
        var first = new FakeItem(Wood, 1);
        var second = new FakeItem(Wood, 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Filter(1, 0, Direction.East, Wood)
            .Item(0, 0, first, Balance.End);

        g.Run(1 + FilterTicks);
        Assert.True(g.FilterAt(1, 0).Slot.Blocked);

        g.BeltAt(0, 0).Slot.Put(second, Direction.West);
        g.Run(200);

        Assert.Same(first, g.FilterAt(1, 0).Slot.Item);
        Assert.Equal(1, first.Count);
        Assert.Same(second, g.BeltAt(0, 0).Slot.Item);
        Assert.Equal(1, second.Count);
        Assert.True(g.BeltAt(0, 0).Slot.Blocked);
    }

    [Fact]
    public void Filter_SeveralInSequence_Work()
    {
        var wood = new FakeItem(Wood, 2, category: -16);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Filter(1, 0, Direction.East, Wood)
            .Filter(2, 0, Direction.East, new CompleteFilterRule { Categories = { -16 } })
            .Filter(3, 0, Direction.East, Wood)
            .Belt(4, 0, Direction.East)
            .Item(0, 0, wood, Balance.End);

        // 1 tick para entrar no primeiro e 15 em cada filtro.
        g.Run(1 + (3 * FilterTicks));

        Assert.Same(wood, g.BeltAt(4, 0).Slot.Item);
        Assert.Equal(0, g.CountInTransit() - wood.Count);
    }

    [Fact]
    public void Filter_FollowedBySlowBelt_WaitsForTheTileToFree()
    {
        // A esteira à frente acabou de receber uma pedra e leva 60 ticks para liberar.
        var stone = new FakeItem(Stone, 1);
        var wood = new FakeItem(Wood, 1);
        var g = new GridBuilder()
            .Filter(0, 0, Direction.East, Wood)
            .Belt(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Item(1, 0, stone);
        g.FilterAt(0, 0).Slot.Put(wood, Direction.West);

        int beltTicks = Balance.End / Balance.BeltSpeedBasic;
        g.Run(beltTicks - 1);
        Assert.Same(wood, g.FilterAt(0, 0).Slot.Item);
        Assert.Equal(Balance.End, g.FilterAt(0, 0).Slot.Progress);
        Assert.True(g.FilterAt(0, 0).Slot.Blocked);

        // A pedra sai da esteira e a madeira entra no mesmo tick (de jusante para montante).
        g.Run(1);
        Assert.Same(stone, g.BeltAt(2, 0).Slot.Item);
        Assert.Same(wood, g.BeltAt(1, 0).Slot.Item);
        Assert.True(g.FilterAt(0, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Filter_ProvidesOnlyToTheTileInFront_AndBeltInFrontStaysStraight()
    {
        var g = new GridBuilder()
            .Filter(1, 0, Direction.East, Wood)
            .Belt(2, 0, Direction.East)
            .Belt(1, 1, Direction.East); // filtro na lateral (norte) desta esteira: não entrega nela

        Topology.Recompute(g.Grid);

        Assert.True(g.FilterAt(1, 0).ProvidesTo(new TilePos(2, 0)));
        Assert.False(g.FilterAt(1, 0).ProvidesTo(new TilePos(1, 1)));
        Assert.False(g.BeltAt(2, 0).IsCurve);
        Assert.False(g.BeltAt(1, 1).IsCurve);
    }

    // Filtro acoplado ao Inseridor (README "Filtro acoplado").

    [Fact]
    public void Coupled_InserterWithFilterInFront_TakesOnlyWood_StoneStaysInChest_LineNeverJams()
    {
        var source = new FakeChest(36, new FakeItem(Stone, 3), new FakeItem(Wood, 3), new FakeItem(Stone, 2));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Filter(2, 0, Direction.East, Wood)
            .Belt(3, 0, Direction.East)
            .Inserter(4, 0, Direction.East)
            .Chest(5, 0, target);

        g.Run(2000);

        Assert.Equal(3, target.CountById(Wood));
        Assert.Equal(0, target.CountById(Stone));
        Assert.Equal(0, source.CountById(Wood));
        Assert.Equal(5, source.CountById(Stone));
        Assert.Equal(0, g.CountInTransit());
    }

    [Fact]
    public void NotCoupled_FilterAfterABelt_TheStoneTakenFromTheChestJamsTheLine()
    {
        var source = new FakeChest(36, new FakeItem(Stone, 1), new FakeItem(Wood, 3));
        var target = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, source)
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Filter(3, 0, Direction.East, Wood)
            .Belt(4, 0, Direction.East)
            .Inserter(5, 0, Direction.East)
            .Chest(6, 0, target);

        g.Run(2000);

        Assert.Equal(Stone, g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.True(g.BeltAt(2, 0).Slot.Blocked);
        Assert.Equal(0, target.CountTotal());
        Assert.Equal(3, source.CountById(Wood));
    }

    [Fact]
    public void Coupled_FilterBehindInserter_OnlyOreEntersTheFurnace()
    {
        // Esteira com minério na frente e carvão atrás → Filtro (minério) → Inseridor (Automático) → fornalha.
        var ore = new FakeItem(FakeMachine.Ids.CopperOre, 5);
        var coal = new FakeItem(FakeMachine.Ids.Coal, 1);
        var furnace = FakeMachine.Furnace();
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Belt(1, 0, Direction.East)
            .Filter(2, 0, Direction.East, FakeMachine.Ids.CopperOre)
            .Inserter(3, 0, Direction.East, InserterSlotMode.Auto)
            .Machine(4, 0, furnace)
            .Item(1, 0, ore, Balance.End)
            .Item(0, 0, coal, Balance.End);

        g.Run(1000);

        Assert.Equal(5, g.SlotsAt(4, 0)!.Main!.Count);
        Assert.Empty(g.SlotsAt(4, 0)!.Additional);
        Assert.Empty(furnace.Starts);
        Assert.Same(coal, g.BeltAt(1, 0).Slot.Item);
        Assert.True(g.BeltAt(1, 0).Slot.Blocked);
    }

    [Fact]
    public void Filter_RuleCanBeChanged_WhileRunning()
    {
        var stone = new FakeItem(Stone, 1);
        GridBuilder g = Line().Item(0, 0, stone, Balance.End);
        g.Run(10);
        Assert.Same(stone, g.BeltAt(0, 0).Slot.Item);

        g.FilterAt(1, 0).Rule = new CommonFilterRule { QualifiedItemId = Stone };
        g.Run(1 + FilterTicks);

        Assert.Same(stone, g.BeltAt(2, 0).Slot.Item);
    }

    [Fact]
    public void Filter_IsComplete_FollowsTheRuleType()
    {
        var common = new FilterNode(new TilePos(0, 0), Direction.East, new CommonFilterRule());
        var complete = new FilterNode(new TilePos(1, 0), Direction.East, new CompleteFilterRule());

        Assert.False(common.IsComplete);
        Assert.True(complete.IsComplete);
        Assert.Equal(NodeKind.Filter, complete.Kind);
    }

    // Filtro Subterrâneo (Fase 6c).

    [Fact]
    public void Underground_WoodPasses_StoneStaysOnTheSegmentBeforeTheFilter()
    {
        // Esteira → entrada → Esteira Subterrânea → Filtro Subterrâneo (madeira) → Esteira Subterrânea → saída → esteira.
        var wood = new FakeItem(Wood, 2);
        var stone = new FakeItem(Stone, 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .Segment(2, 0, Direction.East)
            .UndergroundFilter(3, 0, Direction.East, Wood)
            .Segment(4, 0, Direction.East)
            .Tunnel(5, 0, Direction.East)
            .Belt(6, 0, Direction.East)
            .Item(0, 0, wood, Balance.End);

        g.Run(300);
        Assert.Equal(TunnelRole.Exit, g.TunnelAt(5, 0).Role);
        Assert.Same(wood, g.BeltAt(6, 0).Slot.Item);

        g.Item(0, 0, stone, Balance.End).Run(300);

        Assert.Same(stone, g.SegmentAt(2, 0).Slot.Item);
        Assert.True(g.SegmentAt(2, 0).Slot.Blocked);
        Assert.True(g.UndergroundFilterAt(3, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Underground_TunnelEntranceDeliversIntoTheFilter_AndTheFilterDeliversIntoTheTunnelExitAhead()
    {
        var wood = new FakeItem(Wood, 1);
        var g = new GridBuilder()
            .Belt(0, 0, Direction.East)
            .Tunnel(1, 0, Direction.East)
            .UndergroundFilter(2, 0, Direction.East, Wood)
            .Tunnel(3, 0, Direction.East)
            .Belt(4, 0, Direction.East)
            .Item(0, 0, wood, Balance.End);

        // Entrada (30 ticks) e filtro (15 ticks).
        g.Run(1 + (Balance.End / Balance.UndergroundSpeed));
        Assert.Same(wood, g.UndergroundFilterAt(2, 0).Slot.Item);
        Assert.Equal(Direction.West, g.UndergroundFilterAt(2, 0).Slot.EntrySide);

        g.Run(FilterTicks);
        Assert.Equal(TunnelRole.Exit, g.TunnelAt(3, 0).Role);
        Assert.Same(wood, g.TunnelAt(3, 0).Slot.Item);

        g.Run(100);
        Assert.Same(wood, g.BeltAt(4, 0).Slot.Item);
    }

    [Fact]
    public void Underground_FilterDoesNotDeliverIntoATunnelExitFacingAnotherWay()
    {
        var wood = new FakeItem(Wood, 1);
        var g = new GridBuilder()
            .UndergroundFilter(0, 0, Direction.East, Wood)
            .Tunnel(1, 0, Direction.North, TunnelRole.Exit);
        g.UndergroundFilterAt(0, 0).Slot.Put(wood, Direction.West);

        g.Run(60);

        Assert.Same(wood, g.UndergroundFilterAt(0, 0).Slot.Item);
        Assert.True(g.TunnelAt(1, 0).Slot.IsEmpty);
    }
}
