using System;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
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

    /// <summary>O nó da superfície no tile.</summary>
    public LogisticsNode? NodeAt(TilePos pos) => this.Grid.Get(pos);

    /// <summary>O bloco subterrâneo no tile.</summary>
    public LogisticsNode? UndergroundAt(TilePos pos) => this.Grid.GetUnderground(pos);

    /// <summary>O vizinho de 'from' no lado indicado, na camada dele (<see cref="LogisticsGrid.NeighborOf"/>).</summary>
    public LogisticsNode? NeighborAt(LogisticsNode from, Direction side) => this.Grid.NeighborOf(from, side);

    /// <summary>
    /// O slot que o vizinho do lado indicado está entregando para 'puller', na camada de 'puller', ou null. Embaixo da
    /// terra, a entrada de um Túnel com a frente virada para 'puller' oferece o slot dela.
    /// </summary>
    public TransitSlot? OutgoingSlot(LogisticsNode puller, Direction side)
    {
        LogisticsNode? neighbor = this.NeighborAt(puller, side);
        if (puller.Layer == NodeLayer.Underground && neighbor is TunnelNode tunnel)
            return tunnel.GetUndergroundOutgoingSlot();

        return neighbor?.GetOutgoingSlot(puller.Position);
    }

    /// <summary>
    /// True se 'to' (vizinho de 'from' no lado 'outSide') aceitaria agora o item. Embaixo da terra, 'to' pode ser a saída
    /// de um Túnel com a traseira virada para 'from'. Sem efeito colateral.
    /// </summary>
    public bool CanDeliver(LogisticsNode from, LogisticsNode to, Direction outSide, ILogisticsItem item)
    {
        if (from.Layer == NodeLayer.Underground && to is TunnelNode tunnel)
            return tunnel.CanReceiveFromUnderground();

        return to.CanAccept(outSide.Opposite(), item);
    }

    /// <summary>Entrega até <c>item.Count</c> unidades a 'to' (como <see cref="CanDeliver"/>) e retorna quantas entraram.</summary>
    public int Deliver(LogisticsNode from, LogisticsNode to, Direction outSide, ILogisticsItem item)
    {
        if (from.Layer == NodeLayer.Underground && to is TunnelNode tunnel)
            return tunnel.ReceiveFromUnderground(item) ? item.Count : 0;

        return to.Accept(outSide.Opposite(), item, this);
    }

    /// <summary>
    /// Tenta entregar a pilha do slot ao vizinho de 'from' na direção 'outSide', na camada de 'from'.
    /// Se o vizinho aceitar tudo, o slot é esvaziado. Senão o slot fica Blocked.
    /// </summary>
    public void PushForward(LogisticsNode from, TransitSlot slot, Direction outSide)
    {
        if (slot.Item == null)
            return;

        int before = slot.Item.Count;
        LogisticsNode? target = this.NeighborAt(from, outSide);
        if (target != null && this.Deliver(from, target, outSide, slot.Item) == before)
            slot.Clear();
        else
            slot.Blocked = true;
    }
}
