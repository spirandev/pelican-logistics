using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Esteira Subterrânea (README "Túnel e Esteira Subterrânea"): uma esteira na camada subterrânea da grade, com velocidade
/// fixa <see cref="Balance.UndergroundSpeed"/>. Não ocupa o tile da superfície e nunca interage com os nós de lá, exceto
/// com o <see cref="TunnelNode"/>.
/// </summary>
/// <remarks>
/// Recebe de outra Esteira Subterrânea ou da entrada de um Túnel (curva automática, como a esteira). Entrega à Esteira
/// Subterrânea à frente ou, se na superfície do tile à frente há a saída de um Túnel com a traseira virada para ela, à
/// saída. Qualquer outra coisa à frente: a pilha para no fim.
/// </remarks>
public sealed class UndergroundSegmentNode : ConveyorNode
{
    public UndergroundSegmentNode(TilePos position, Direction facing)
        : base(position, facing)
    {
    }

    public override NodeKind Kind => NodeKind.UndergroundSegment;

    public override int Speed => Balance.UndergroundSpeed;

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? next = (LogisticsNode?)grid.GetUnderground(this.FrontPosition) ?? this.ExitAhead(grid.Get(this.FrontPosition));
        return next == null ? Array.Empty<LogisticsNode>() : new[] { next };
    }

    protected override void DeliverForward(TickContext ctx)
    {
        if (ctx.UndergroundAt(this.FrontPosition) is { } next)
        {
            int before = this.Slot.Item!.Count;
            if (next.Accept(this.Facing.Opposite(), this.Slot.Item, ctx) == before)
                this.Slot.Clear();
            else
                this.Slot.Blocked = true;
            return;
        }

        if (this.ExitAhead(ctx.NodeAt(this.FrontPosition)) is { } exit && exit.ReceiveFromUnderground(this.Slot.Item!))
        {
            this.Slot.Clear();
            return;
        }

        this.Slot.Blocked = true;
    }

    /// <summary>O nó da superfície à frente, se for a saída de um Túnel com a traseira virada para esta esteira.</summary>
    private TunnelNode? ExitAhead(LogisticsNode? front)
    {
        return front is TunnelNode { Role: TunnelRole.Exit } exit && exit.Facing == this.Facing ? exit : null;
    }
}
