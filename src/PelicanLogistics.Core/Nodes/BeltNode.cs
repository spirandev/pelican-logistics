using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Esteira: move uma pilha por vez em um único sentido, com uma única saída (README "Regras de conexão").
/// Recebe só pelo <see cref="InputSide"/>: a traseira, ou uma lateral quando vira curva.
/// </summary>
public sealed class BeltNode : LogisticsNode
{
    public BeltNode(TilePos position, Direction facing, BeltTier tier = BeltTier.Basic)
        : base(position, facing)
    {
        this.Tier = tier;
        this.InputSide = facing.Opposite();
    }

    public override NodeKind Kind => NodeKind.Belt;

    public BeltTier Tier { get; }

    /// <summary>Lado por onde a esteira recebe. Calculado pela <see cref="Topology"/>.</summary>
    public Direction InputSide { get; internal set; }

    /// <summary>True se a esteira faz curva (recebe por uma lateral).</summary>
    public bool IsCurve => this.InputSide != this.Facing.Opposite();

    /// <summary>O único espaço da esteira (README: 1 pilha por tile).</summary>
    public TransitSlot Slot { get; } = new();

    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot };

    public override bool ProvidesTo(TilePos neighbor)
    {
        return neighbor == this.Position.Offset(this.Facing);
    }

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? front = grid.Get(this.Position.Offset(this.Facing));
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
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

    public override void Update(TickContext ctx)
    {
        if (!this.Slot.IsEmpty && this.Slot.Advance(Balance.SpeedOf(this.Tier)))
            ctx.PushForward(this, this.Slot, this.Facing);
    }
}
