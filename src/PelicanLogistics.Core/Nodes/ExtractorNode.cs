using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Extrator: retira itens da origem ATRÁS dele (baú) e coloca no nó À FRENTE (plano mestre D2).
/// Faz uma operação a cada <see cref="Balance.TransferCooldownTicks"/> ticks.
/// </summary>
public sealed class ExtractorNode : LogisticsNode
{
    private int configuredAmount = 1;

    public ExtractorNode(TilePos position, Direction facing, TransferTier tier = TransferTier.Single)
        : base(position, facing)
    {
        this.Tier = tier;
    }

    public override NodeKind Kind => NodeKind.Extractor;

    public TransferTier Tier { get; }

    /// <summary>Quantidade por operação da versão Configurável (Fase 5). Limitada a 1..<see cref="Balance.MaxConfigurableAmount"/>.</summary>
    public int ConfiguredAmount
    {
        get => this.configuredAmount;
        set => this.configuredAmount = Math.Clamp(value, 1, Balance.MaxConfigurableAmount);
    }

    /// <summary>Ticks até a próxima operação.</summary>
    public int Cooldown { get; internal set; }

    /// <summary>Tile da origem (baú ou máquina).</summary>
    public TilePos SourcePosition => this.Position.Offset(this.Facing.Opposite());

    /// <summary>Tile da saída.</summary>
    public TilePos OutputPosition => this.Position.Offset(this.Facing);

    public override bool ProvidesTo(TilePos neighbor)
    {
        return neighbor == this.OutputPosition;
    }

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? front = grid.Get(this.OutputPosition);
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
    }

    public override void Update(TickContext ctx)
    {
        // Decrementa antes de testar: assim há exatamente TransferCooldownTicks ticks entre duas operações.
        if (this.Cooldown > 0)
        {
            this.Cooldown--;
            if (this.Cooldown > 0)
                return;
        }

        LogisticsNode? front = ctx.NodeAt(this.OutputPosition);
        if (front == null || !IsValidOutput(front))
            return;

        Direction entrySide = this.Facing.Opposite();
        if (!front.CanReceiveFrom(entrySide))
            return;

        if (ctx.Endpoints.GetEndpoint(this.SourcePosition) is IChestEndpoint chest)
        {
            (ExtractAmount mode, int amount) = this.Tier switch
            {
                TransferTier.Single => (ExtractAmount.One, 1),
                TransferTier.Stack => (ExtractAmount.Stack, 0),
                TransferTier.Configurable => (ExtractAmount.Exactly, this.ConfiguredAmount),
                _ => throw new InvalidOperationException($"Tier inválido: {this.Tier}."),
            };

            ILogisticsItem? item = chest.Extract(this.GetExtractionPredicate(ctx), mode, amount);
            if (item == null)
                return;

            int count = item.Count;
            int taken = front.Accept(entrySide, item, ctx);
            if (taken < count)
                chest.Insert(item, item.Count); // não deveria acontecer: o nó da frente confirmou que recebe

            this.Cooldown = Balance.TransferCooldownTicks;
        }

        // Máquinas: Fase 2.
    }

    /// <summary>Nós para os quais o Extrator entrega (plano mestre D3). Fases 4 e 5 acrescentam Filtro e entrada de Subterrânea.</summary>
    private static bool IsValidOutput(LogisticsNode node)
    {
        return node is BeltNode;
    }

    /// <summary>Quais itens o Extrator retira. Nesta fase aceita tudo; a Fase 4 acopla o filtro.</summary>
    private Func<ILogisticsItem, bool> GetExtractionPredicate(TickContext ctx)
    {
        return _ => true;
    }
}
