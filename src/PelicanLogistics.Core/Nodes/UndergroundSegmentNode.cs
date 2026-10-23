using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Esteira Subterrânea (README "Túnel e Esteira Subterrânea"): uma esteira na camada subterrânea da grade, com velocidade
/// fixa <see cref="Balance.UndergroundSpeed"/>. Não ocupa o tile da superfície e nunca interage com os nós de lá, exceto
/// com o <see cref="TunnelNode"/>.
/// </summary>
/// <remarks>
/// Recebe de outro bloco subterrâneo (Esteira, Filtro ou Divisor) ou da entrada de um Túnel (curva automática, como a
/// esteira). Entrega ao bloco subterrâneo à frente ou, se na superfície do tile à frente há a saída de um Túnel com a
/// traseira virada para ela, à saída (<see cref="LogisticsGrid.NeighborOf"/>). Qualquer outra coisa à frente: a pilha
/// para no fim.
/// </remarks>
public sealed class UndergroundSegmentNode : ConveyorNode
{
    public UndergroundSegmentNode(TilePos position, Direction facing)
        : base(position, facing)
    {
    }

    public override NodeKind Kind => NodeKind.UndergroundSegment;

    public override int Speed => Balance.UndergroundSpeed;
}
