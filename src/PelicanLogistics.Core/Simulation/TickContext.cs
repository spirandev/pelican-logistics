using System;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>O que um nó enxerga durante um tick: a grade e os endpoints (baús, máquinas) do local.</summary>
public sealed class TickContext
{
    public TickContext(LogisticsGrid grid, IEndpointProvider endpoints)
    {
        this.Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        this.Endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
    }

    public LogisticsGrid Grid { get; }

    public IEndpointProvider Endpoints { get; }

    public LogisticsNode? NodeAt(TilePos pos) => this.Grid.Get(pos);

    /// <summary>
    /// Tenta entregar a pilha do slot ao nó vizinho na direção 'outSide'.
    /// Se o vizinho aceitar tudo, o slot é esvaziado. Senão o slot fica Blocked.
    /// </summary>
    public void PushForward(LogisticsNode from, TransitSlot slot, Direction outSide)
    {
        if (slot.Item == null)
            return;

        int before = slot.Item.Count;
        LogisticsNode? target = this.NodeAt(from.Position.Offset(outSide));
        if (target == null)
        {
            slot.Blocked = true;
            return;
        }

        int taken = target.Accept(outSide.Opposite(), slot.Item, this);
        if (taken == before)
            slot.Clear();
        else
            slot.Blocked = true;
    }
}
