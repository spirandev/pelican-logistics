using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Fakes;

/// <summary>Monta grades de teste com pouco código e roda ticks.</summary>
public sealed class GridBuilder
{
    /// <param name="grid">Grade a montar (ex.: a de um <see cref="LogisticsWorld"/>); null cria uma nova.</param>
    public GridBuilder(LogisticsGrid? grid = null)
    {
        this.Grid = grid ?? new LogisticsGrid();
    }

    public LogisticsGrid Grid { get; }

    public FakeEndpoints Endpoints { get; } = new();

    /// <summary>Máquinas da grade; <see cref="FakeMachine.Advance"/> é chamado a cada tick, depois da grade.</summary>
    public List<FakeMachine> Machines { get; } = new();

    public GridBuilder Belt(int x, int y, Direction facing, BeltTier tier = BeltTier.Basic)
    {
        this.Grid.Add(new BeltNode(new TilePos(x, y), facing, tier));
        return this;
    }

    public GridBuilder Inserter(int x, int y, Direction facing, TransferTier tier = TransferTier.Single)
    {
        this.Grid.Add(new InserterNode(new TilePos(x, y), facing, tier));
        return this;
    }

    public GridBuilder Inserter(int x, int y, Direction facing, InserterSlotMode mode, TransferTier tier = TransferTier.Single)
    {
        this.Grid.Add(new InserterNode(new TilePos(x, y), facing, tier) { SlotMode = mode });
        return this;
    }

    public GridBuilder Inserter(int x, int y, Direction facing, PickupSource source, TransferTier tier = TransferTier.Single)
    {
        this.Grid.Add(new InserterNode(new TilePos(x, y), facing, tier) { Source = source });
        return this;
    }

    /// <summary>Filtro Comum que só deixa passar o item indicado.</summary>
    public GridBuilder Filter(int x, int y, Direction facing, string qualifiedItemId)
    {
        return this.Filter(x, y, facing, new CommonFilterRule { QualifiedItemId = qualifiedItemId });
    }

    public GridBuilder Filter(int x, int y, Direction facing, IFilterRule rule)
    {
        this.Grid.Add(new FilterNode(new TilePos(x, y), facing, rule));
        return this;
    }

    public GridBuilder Splitter(int x, int y)
    {
        this.Grid.Add(new SplitterNode(new TilePos(x, y)));
        return this;
    }

    /// <summary>Túnel com o papel decidido como o adaptador faz ao colocar (<see cref="TunnelRules.DecideRole"/>).</summary>
    public GridBuilder Tunnel(int x, int y, Direction facing)
    {
        return this.Tunnel(x, y, facing, TunnelRules.DecideRole(this.Grid, new TilePos(x, y), facing));
    }

    public GridBuilder Tunnel(int x, int y, Direction facing, TunnelRole role)
    {
        this.Grid.Add(new TunnelNode(new TilePos(x, y), facing, role));
        return this;
    }

    /// <summary>Esteira Subterrânea (camada subterrânea).</summary>
    public GridBuilder Segment(int x, int y, Direction facing)
    {
        this.Grid.AddUnderground(new UndergroundSegmentNode(new TilePos(x, y), facing));
        return this;
    }

    /// <summary>Esteiras Subterrâneas em linha reta, de (x, y) andando 'count' tiles na direção indicada.</summary>
    public GridBuilder Segments(int x, int y, Direction facing, int count)
    {
        var pos = new TilePos(x, y);
        for (int i = 0; i < count; i++)
        {
            this.Grid.AddUnderground(new UndergroundSegmentNode(pos, facing));
            pos = pos.Offset(facing);
        }

        return this;
    }

    /// <summary>Filtro Comum Subterrâneo que só deixa passar o item indicado.</summary>
    public GridBuilder UndergroundFilter(int x, int y, Direction facing, string qualifiedItemId)
    {
        return this.UndergroundFilter(x, y, facing, new CommonFilterRule { QualifiedItemId = qualifiedItemId });
    }

    public GridBuilder UndergroundFilter(int x, int y, Direction facing, IFilterRule rule)
    {
        this.Grid.AddUnderground(new FilterNode(new TilePos(x, y), facing, rule));
        return this;
    }

    public GridBuilder UndergroundSplitter(int x, int y)
    {
        this.Grid.AddUnderground(new SplitterNode(new TilePos(x, y)));
        return this;
    }

    /// <summary>Coloca um Regulador com a regra indicada sobre o Inseridor do tile.</summary>
    public GridBuilder Regulator(int x, int y, RegulatorRule rule)
    {
        Assert.True(this.Grid.TrySetAccessory(new TilePos(x, y), AccessoryKind.Regulator, rule));
        return this;
    }

    public GridBuilder Machine(int x, int y, FakeMachine machine)
    {
        this.Endpoints.Endpoints[new TilePos(x, y)] = machine;
        this.Machines.Add(machine);
        return this;
    }

    public GridBuilder Chest(int x, int y, FakeChest chest)
    {
        this.Endpoints.Endpoints[new TilePos(x, y)] = chest;
        return this;
    }

    /// <summary>Coloca uma pilha na esteira, entrando por trás, com o progresso indicado.</summary>
    public GridBuilder Item(int x, int y, ILogisticsItem item, int progress = 0)
    {
        BeltNode belt = this.BeltAt(x, y);
        belt.Slot.Put(item, belt.Facing.Opposite());
        belt.Slot.Advance(progress);
        return this;
    }

    public GridBuilder Run(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            this.Grid.Tick(this.Endpoints);
            foreach (FakeMachine machine in this.Machines)
                machine.Advance();
        }

        return this;
    }

    public BeltNode BeltAt(int x, int y) => this.NodeAt<BeltNode>(x, y);

    public InserterNode InserterAt(int x, int y) => this.NodeAt<InserterNode>(x, y);

    public FilterNode FilterAt(int x, int y) => this.NodeAt<FilterNode>(x, y);

    public SplitterNode SplitterAt(int x, int y) => this.NodeAt<SplitterNode>(x, y);

    public TunnelNode TunnelAt(int x, int y) => this.NodeAt<TunnelNode>(x, y);

    /// <summary>A Esteira Subterrânea do tile (falha o teste se não houver).</summary>
    public UndergroundSegmentNode SegmentAt(int x, int y) => Assert.IsType<UndergroundSegmentNode>(this.Grid.GetUnderground(new TilePos(x, y)));

    /// <summary>O Filtro Subterrâneo do tile (falha o teste se não houver).</summary>
    public FilterNode UndergroundFilterAt(int x, int y) => Assert.IsType<FilterNode>(this.Grid.GetUnderground(new TilePos(x, y)));

    /// <summary>O Divisor Subterrâneo do tile (falha o teste se não houver).</summary>
    public SplitterNode UndergroundSplitterAt(int x, int y) => Assert.IsType<SplitterNode>(this.Grid.GetUnderground(new TilePos(x, y)));

    /// <summary>Slots virtuais da máquina no tile, ou null se não há nada guardado.</summary>
    public VirtualSlots? SlotsAt(int x, int y) => this.Grid.MachineSlots.Get(new TilePos(x, y));

    public T NodeAt<T>(int x, int y)
        where T : LogisticsNode
    {
        return Assert.IsType<T>(this.Grid.Get(new TilePos(x, y)));
    }

    /// <summary>Soma das unidades em trânsito (todas as pilhas em todos os nós das duas camadas: esteiras, filtros, Divisores, Túneis, blocos subterrâneos e mãos dos Inseridores).</summary>
    public int CountInTransit()
    {
        return this.Grid.Nodes.Concat(this.Grid.UndergroundNodes).SelectMany(n => n.Slots).Where(s => s.Item != null).Sum(s => s.Item!.Count);
    }
}
