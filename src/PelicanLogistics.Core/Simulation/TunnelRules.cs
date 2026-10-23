using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>Regras de colocação do Túnel (README "Túnel e Esteira Subterrânea"; plano mestre D21).</summary>
public static class TunnelRules
{
    /// <summary>
    /// Papel de um Túnel colocado em 'pos' com a direção 'facing', chamado pelo adaptador antes de criar o nó:
    /// <see cref="TunnelRole.Exit"/> se o bloco subterrâneo do tile atrás entrega em 'pos' (uma Esteira ou um Filtro
    /// Subterrâneo apontando para 'pos', ou um Divisor Subterrâneo, que entrega para qualquer lado); senão
    /// <see cref="TunnelRole.Entrance"/>.
    /// </summary>
    public static TunnelRole DecideRole(LogisticsGrid grid, TilePos pos, Direction facing)
    {
        LogisticsNode? behind = grid.GetUnderground(pos.Offset(facing.Opposite()));
        return behind != null && behind.ProvidesTo(pos) ? TunnelRole.Exit : TunnelRole.Entrance;
    }

    /// <summary>True se dá para colocar um Túnel no tile: não pode haver bloco subterrâneo embaixo dele.</summary>
    public static bool CanPlaceTunnel(LogisticsGrid grid, TilePos pos)
    {
        return grid.GetUnderground(pos) == null;
    }
}
