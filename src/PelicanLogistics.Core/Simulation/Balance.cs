using System;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>Constantes de balanceamento da alpha (plano mestre, seção 4.6). Nunca use números mágicos fora daqui.</summary>
public static class Balance
{
    /// <summary>Ticks do jogo por segundo.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>Unidades de progresso para atravessar um tile.</summary>
    public const int ProgressPerTile = 600;

    /// <summary>Progresso de um item que chegou ao fim do tile.</summary>
    public const int End = ProgressPerTile;

    /// <summary>Esteira básica: 1 tile/s.</summary>
    public const int BeltSpeedBasic = 10;

    /// <summary>Esteira rápida: 2 tiles/s.</summary>
    public const int BeltSpeedFast = 20;

    /// <summary>Esteira expressa: 4 tiles/s.</summary>
    public const int BeltSpeedExpress = 40;

    /// <summary>Filtro: velocidade máxima.</summary>
    public const int FilterSpeed = 40;

    /// <summary>Divisor: velocidade máxima.</summary>
    public const int SplitterSpeed = 40;

    /// <summary>Esteira subterrânea: velocidade máxima.</summary>
    public const int UndergroundSpeed = 40;

    /// <summary>Extrator e Inseridor: 1 operação a cada 0,5 s (todas as versões).</summary>
    public const int TransferCooldownTicks = 30;

    /// <summary>Tiles que a Esteira Subterrânea atravessa.</summary>
    public const int UndergroundMaxGap = 5;

    /// <summary>Limite das versões Configuráveis.</summary>
    public const int MaxConfigurableAmount = 999;

    /// <summary>Velocidade (progresso por tick) de uma esteira do tier indicado.</summary>
    public static int SpeedOf(BeltTier tier)
    {
        return tier switch
        {
            BeltTier.Basic => BeltSpeedBasic,
            BeltTier.Fast => BeltSpeedFast,
            BeltTier.Express => BeltSpeedExpress,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Tier de esteira inválido."),
        };
    }
}
