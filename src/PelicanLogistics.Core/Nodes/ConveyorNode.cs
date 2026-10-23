using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Base das esteiras (<see cref="BeltNode"/> na superfície e <see cref="UndergroundSegmentNode"/> embaixo da terra): uma
/// pilha por tile, sentido único, uma única saída (README "Regras de conexão"). Recebe só pelo <see cref="InputSide"/>: a
/// traseira, ou uma lateral quando vira curva. Entrega ao vizinho da frente na própria camada
/// (<see cref="LogisticsGrid.NeighborOf"/>).
/// </summary>
public abstract class ConveyorNode : LogisticsNode
{
    protected ConveyorNode(TilePos position, Direction facing)
        : base(position, facing)
    {
        this.InputSide = facing.Opposite();
    }

    /// <summary>Lado por onde a esteira recebe. Calculado pela <see cref="Topology"/>.</summary>
    public Direction InputSide { get; internal set; }

    /// <summary>True se a esteira faz curva (recebe por uma lateral).</summary>
    public bool IsCurve => this.InputSide != this.Facing.Opposite();

    /// <summary>O único espaço da esteira (README: 1 pilha por tile).</summary>
    public TransitSlot Slot { get; } = new();

    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot };

    /// <summary>Progresso por tick.</summary>
    public abstract int Speed { get; }

    /// <summary>Tile para onde a esteira entrega.</summary>
    public TilePos FrontPosition => this.Position.Offset(this.Facing);

    public override bool ProvidesTo(TilePos neighbor)
    {
        return neighbor == this.FrontPosition;
    }

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? front = grid.NeighborOf(this, this.Facing);
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
    }

    public override TransitSlot? GetOutgoingSlot(TilePos target)
    {
        return target == this.FrontPosition && this.Slot.Item != null ? this.Slot : null;
    }

    public override bool CanReceiveFrom(Direction entrySide)
    {
        return entrySide == this.InputSide && this.Slot.IsEmpty;
    }

    public override bool CanAccept(Direction entrySide, ILogisticsItem item)
    {
        return this.CanReceiveFrom(entrySide);
    }

    public override int Accept(Direction entrySide, ILogisticsItem item, TickContext ctx)
    {
        // Lateral ignorada (README "Regras de conexão").
        if (entrySide != this.InputSide)
            return 0;

        if (this.Slot.IsEmpty)
        {
            this.Slot.Put(item, entrySide);
            return item.Count;
        }

        // Junção: só quando a esteira está acumulando (README; plano mestre D11).
        ILogisticsItem current = this.Slot.Item!;
        if (this.Slot.Blocked && this.Slot.Progress == Balance.End && current.CanStackWith(item))
            return current.MergeFrom(item, item.Count);

        return 0;
    }

    /// <summary>A pilha chegou ao fim do tile: tenta entregá-la à frente. Se não sair toda, o slot fica <see cref="TransitSlot.Blocked"/>.</summary>
    public override void Update(TickContext ctx)
    {
        if (!this.Slot.IsEmpty && this.Slot.Advance(this.Speed))
            ctx.PushForward(this, this.Slot, this.Facing);
    }
}
