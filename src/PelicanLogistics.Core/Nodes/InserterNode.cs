using System;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Inseridor: recebe da esteira ATRÁS dele e coloca no destino À FRENTE (baú) (plano mestre D2).
/// É passivo: quem entrega é a esteira, chamando <see cref="Accept"/>.
/// Faz uma operação a cada <see cref="Balance.TransferCooldownTicks"/> ticks.
/// </summary>
/// <remarks>
/// Pela ordem de processamento (<see cref="Topology"/>), o Inseridor é atualizado antes da esteira que o
/// alimenta, então o cooldown já diminuiu quando a esteira tenta entregar.
/// </remarks>
public sealed class InserterNode : LogisticsNode
{
    private int configuredAmount = 1;

    public InserterNode(TilePos position, Direction facing, TransferTier tier = TransferTier.Single)
        : base(position, facing)
    {
        this.Tier = tier;
    }

    public override NodeKind Kind => NodeKind.Inserter;

    public TransferTier Tier { get; }

    /// <summary>Quantidade por operação da versão Configurável (Fase 5). Limitada a 1..<see cref="Balance.MaxConfigurableAmount"/>.</summary>
    public int ConfiguredAmount
    {
        get => this.configuredAmount;
        set => this.configuredAmount = Math.Clamp(value, 1, Balance.MaxConfigurableAmount);
    }

    /// <summary>Ticks até a próxima operação.</summary>
    public int Cooldown { get; internal set; }

    /// <summary>Tile do destino (baú ou máquina).</summary>
    public TilePos TargetPosition => this.Position.Offset(this.Facing);

    public override int Accept(Direction entrySide, ILogisticsItem item, TickContext ctx)
    {
        if (entrySide != this.Facing.Opposite() || this.Cooldown > 0)
            return 0;

        int max = this.Tier switch
        {
            TransferTier.Single => 1,
            TransferTier.Stack => item.Count,
            TransferTier.Configurable => Math.Min(this.ConfiguredAmount, item.Count),
            _ => throw new InvalidOperationException($"Tier inválido: {this.Tier}."),
        };

        int inserted = 0;
        if (ctx.Endpoints.GetEndpoint(this.TargetPosition) is IChestEndpoint chest)
            inserted = chest.Insert(item, max);

        // Máquinas: Fase 2.

        if (inserted > 0)
            this.Cooldown = Balance.TransferCooldownTicks;
        return inserted;
    }

    public override void Update(TickContext ctx)
    {
        if (this.Cooldown > 0)
            this.Cooldown--;
    }
}
