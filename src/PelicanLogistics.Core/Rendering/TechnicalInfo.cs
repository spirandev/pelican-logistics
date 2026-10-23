using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Rendering;

/// <summary>
/// Números das descrições técnicas, mostradas com o Capacete de Engenheiro (README "Capacete de Engenheiro"), calculados
/// de <see cref="Balance"/>. O adaptador formata os textos com o i18n.
/// </summary>
public static class TechnicalInfo
{
    /// <summary>Tiles por segundo de um item que avança 'speed' unidades de progresso por tick.</summary>
    public static double TilesPerSecond(int speed)
    {
        return speed * (double)Balance.TicksPerSecond / Balance.ProgressPerTile;
    }

    public static double BeltTilesPerSecond(BeltTier tier)
    {
        return TilesPerSecond(Balance.SpeedOf(tier));
    }

    public static double FilterTilesPerSecond => TilesPerSecond(Balance.FilterSpeed);

    public static double SplitterTilesPerSecond => TilesPerSecond(Balance.SplitterSpeed);

    /// <summary>Túnel e Esteira Subterrânea.</summary>
    public static double UndergroundTilesPerSecond => TilesPerSecond(Balance.UndergroundSpeed);

    /// <summary>Segundos de uma operação do Inseridor (ida da mão + volta do braço), igual em todas as versões.</summary>
    public static double InserterCycleSeconds => ((Balance.End / (double)Balance.InserterSpeed) + Balance.InserterReturnTicks) / Balance.TicksPerSecond;
}
