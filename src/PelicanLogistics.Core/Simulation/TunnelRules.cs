using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>Regras de colocação do Túnel (README "Túnel e Esteira Subterrânea"; plano mestre D21).</summary>
public static class TunnelRules
{
    /// <summary>
    /// Papel de um Túnel colocado em 'pos' com a direção 'facing', chamado pelo adaptador antes de criar o nó:
    /// <see cref="TunnelRole.Exit"/> se a Esteira Subterrânea do tile atrás aponta para 'pos'; senão
    /// <see cref="TunnelRole.Entrance"/>.
    /// </summary>
    public static TunnelRole DecideRole(LogisticsGrid grid, TilePos pos, Direction facing)
    {
        UndergroundSegmentNode? behind = grid.GetUnderground(pos.Offset(facing.Opposite()));
        return behind != null && behind.Facing == facing ? TunnelRole.Exit : TunnelRole.Entrance;
    }

    /// <summary>True se dá para colocar um Túnel no tile: não pode haver Esteira Subterrânea embaixo dele.</summary>
    public static bool CanPlaceTunnel(LogisticsGrid grid, TilePos pos)
    {
        return grid.GetUnderground(pos) == null;
    }
}
