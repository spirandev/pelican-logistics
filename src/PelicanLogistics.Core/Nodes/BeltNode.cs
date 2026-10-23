using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Esteira: move uma pilha por vez em um único sentido, com uma única saída (README "Regras de conexão").
/// Recebe só pelo <see cref="ConveyorNode.InputSide"/>: a traseira, ou uma lateral quando vira curva.
/// </summary>
public sealed class BeltNode : ConveyorNode
{
    public BeltNode(TilePos position, Direction facing, BeltTier tier = BeltTier.Basic)
        : base(position, facing)
    {
        this.Tier = tier;
    }

    public override NodeKind Kind => NodeKind.Belt;

    public BeltTier Tier { get; }

    public override int Speed => Balance.SpeedOf(this.Tier);

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? front = grid.Get(this.FrontPosition);
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
    }

    public override TransitSlot? GetOutgoingSlot(TilePos target)
    {
        return target == this.FrontPosition && this.Slot.Item != null ? this.Slot : null;
    }

    protected override void DeliverForward(TickContext ctx)
    {
        ctx.PushForward(this, this.Slot, this.Facing);
    }
}
