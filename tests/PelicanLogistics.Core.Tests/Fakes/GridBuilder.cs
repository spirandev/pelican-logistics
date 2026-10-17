using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Fakes;

/// <summary>Monta grades de teste com pouco código e roda ticks.</summary>
public sealed class GridBuilder
{
    public LogisticsGrid Grid { get; } = new();

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

    /// <summary>Slots virtuais da máquina no tile, ou null se não há nada guardado.</summary>
    public VirtualSlots? SlotsAt(int x, int y) => this.Grid.MachineSlots.Get(new TilePos(x, y));

    public T NodeAt<T>(int x, int y)
        where T : LogisticsNode
    {
        return Assert.IsType<T>(this.Grid.Get(new TilePos(x, y)));
    }

    /// <summary>Soma das unidades em trânsito (todas as pilhas em todos os nós: esteiras e mãos dos Inseridores).</summary>
    public int CountInTransit()
    {
        return this.Grid.Nodes.SelectMany(n => n.Slots).Where(s => s.Item != null).Sum(s => s.Item!.Count);
    }
}
