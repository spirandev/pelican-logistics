using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>Papel e pareamento das Esteiras Subterrâneas (README "Transporte").</summary>
public static class UndergroundPairing
{
    /// <summary>Distância máxima entre a entrada e a saída de um par: o vão mais o tile da saída.</summary>
    public const int MaxDistance = Balance.UndergroundMaxGap + 1;

    /// <summary>
    /// Papel de uma Esteira Subterrânea colocada em 'pos' (ainda vazio) com a direção 'facing', chamado pelo adaptador
    /// antes de criar o nó: <see cref="UndergroundRole.Exit"/> se, olhando para trás até <see cref="MaxDistance"/>
    /// tiles, a primeira Subterrânea com a mesma direção é uma entrada sem par; senão <see cref="UndergroundRole.Entrance"/>.
    /// </summary>
    /// <remarks>
    /// "Sem par" é calculado na hora, como no <see cref="Recompute"/> (a primeira Subterrânea à frente da entrada não é
    /// uma saída), e não pelo <see cref="UndergroundBeltNode.Partner"/>, que pode estar desatualizado até o próximo tick.
    /// </remarks>
    public static UndergroundRole DecideRole(LogisticsGrid grid, TilePos pos, Direction facing)
    {
        UndergroundBeltNode? behind = FindFirst(grid, pos, facing.Opposite(), facing, out _);
        if (behind?.Role != UndergroundRole.Entrance)
            return UndergroundRole.Entrance;

        UndergroundBeltNode? ahead = FindFirst(grid, behind.Position, facing, facing, out _);
        return ahead?.Role == UndergroundRole.Exit ? UndergroundRole.Entrance : UndergroundRole.Exit;
    }

    /// <summary>
    /// Refaz os pares da grade: para cada entrada, a primeira Subterrânea com a mesma direção à frente, até
    /// <see cref="MaxDistance"/> tiles, se for uma saída ainda sem par. O túnel tem 'distância - 1' tiles.
    /// </summary>
    internal static void Recompute(LogisticsGrid grid, IEnumerable<LogisticsNode> nodes)
    {
        foreach (LogisticsNode node in nodes)
        {
            if (node is UndergroundBeltNode underground)
            {
                underground.Partner = null;
                underground.TunnelLength = 0;
            }
        }

        foreach (LogisticsNode node in nodes)
        {
            if (node is not UndergroundBeltNode { Role: UndergroundRole.Entrance } entrance)
                continue;

            UndergroundBeltNode? exit = FindFirst(grid, entrance.Position, entrance.Facing, entrance.Facing, out int distance);
            if (exit is not { Role: UndergroundRole.Exit, Partner: null })
                continue;

            entrance.Partner = exit;
            exit.Partner = entrance;
            entrance.TunnelLength = distance - 1;
        }
    }

    /// <summary>
    /// A primeira Subterrânea com a direção 'facing' andando de 'from' para 'step', até <see cref="MaxDistance"/> tiles,
    /// e a distância até ela (0 se não achou).
    /// </summary>
    private static UndergroundBeltNode? FindFirst(LogisticsGrid grid, TilePos from, Direction step, Direction facing, out int distance)
    {
        TilePos pos = from;
        for (distance = 1; distance <= MaxDistance; distance++)
        {
            pos = pos.Offset(step);
            if (grid.Get(pos) is UndergroundBeltNode underground && underground.Facing == facing)
                return underground;
        }

        distance = 0;
        return null;
    }
}
