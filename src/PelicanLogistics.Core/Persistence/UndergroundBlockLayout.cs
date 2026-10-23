using System;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Persistence;

/// <summary>
/// Um bloco subterrâneo lido do layout do local (<see cref="UndergroundLayoutSerializer"/>): tipo, posição, direção e, no
/// filtro, a regra (<see cref="CommonFilterRule"/> ou <see cref="CompleteFilterRule"/>). No Divisor, a direção é ignorada.
/// </summary>
public sealed record UndergroundBlockLayout(TilePos Position, NodeKind Kind, Direction Facing, IFilterRule? Rule = null)
{
    /// <summary>Cria o nó (ainda fora da grade) que este bloco descreve.</summary>
    public LogisticsNode CreateNode()
    {
        return this.Kind switch
        {
            NodeKind.UndergroundSegment => new UndergroundSegmentNode(this.Position, this.Facing),
            NodeKind.Filter => new FilterNode(this.Position, this.Facing, this.Rule ?? new CommonFilterRule()),
            NodeKind.Splitter => new SplitterNode(this.Position),
            _ => throw new InvalidOperationException($"{this.Kind} não é um bloco subterrâneo."),
        };
    }
}
