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
}
