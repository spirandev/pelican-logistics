using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Extrator: retira itens da origem ATRÁS dele (baú ou máquina) e coloca no nó À FRENTE (plano mestre D2).
/// Em máquinas, retira do slot escolhido em <see cref="Source"/> (README "Retirada (Extrator)").
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

    /// <summary>De onde retirar quando a origem é uma máquina. Ignorado em baús.</summary>
    public ExtractorSource Source { get; set; } = ExtractorSource.Product;

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

        bool moved = ctx.Endpoints.GetEndpoint(this.SourcePosition) switch
        {
            IChestEndpoint chest => this.ExtractFromChest(chest, front, entrySide, ctx),
            IMachineEndpoint machine => this.ExtractFromMachine(machine, front, entrySide, ctx),
            _ => false,
        };

        if (moved)
            this.Cooldown = Balance.TransferCooldownTicks;
    }

    private bool ExtractFromChest(IChestEndpoint chest, LogisticsNode front, Direction entrySide, TickContext ctx)
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
            return false;

        int count = item.Count;
        int taken = front.Accept(entrySide, item, ctx);
        if (taken < count)
            chest.Insert(item, item.Count); // não deveria acontecer: o nó da frente confirmou que recebe

        return true;
    }

    /// <summary>Retira da máquina conforme <see cref="Source"/>. A posição do Extrator em volta da máquina não importa.</summary>
    private bool ExtractFromMachine(IMachineEndpoint machine, LogisticsNode front, Direction entrySide, TickContext ctx)
    {
        Func<ILogisticsItem, bool> predicate = this.GetExtractionPredicate(ctx);
        switch (this.Source)
        {
            case ExtractorSource.Product:
            {
                ILogisticsItem? output = machine.PeekOutput();
                if (output == null || output.Count <= 0 || !predicate(output) || !front.CanAccept(entrySide, output))
                    return false;

                ILogisticsItem? item = machine.TakeOutput(this.AmountFor(output));
                if (item == null)
                    return false;

                // Não deveria sobrar: o nó da frente confirmou que recebe. Se sobrar, volta para o produto guardado.
                if (!Deliver(front, entrySide, item, ctx))
                    machine.PeekOutput()?.MergeFrom(item, item.Count);
                return true;
            }

            case ExtractorSource.Main:
            {
                VirtualSlots? slots = ctx.Grid.MachineSlots.Get(this.SourcePosition);
                if (slots?.Main == null || !predicate(slots.Main))
                    return false;

                int required = slots.MainRequired;
                ILogisticsItem item = slots.TakeFromMain(this.AmountFor(slots.Main))!;
                if (!Deliver(front, entrySide, item, ctx))
                    slots.AddToMain(item, item.Count, required);
                return true;
            }

            case ExtractorSource.Additional:
            {
                VirtualSlots? slots = ctx.Grid.MachineSlots.Get(this.SourcePosition);
                if (slots == null)
                    return false;

                foreach (AdditionalRequirement req in machine.AdditionalRequirements)
                {
                    if (!slots.Additional.TryGetValue(req.Id, out ILogisticsItem? stored) || !predicate(stored))
                        continue;

                    ILogisticsItem item = slots.TakeFromAdditional(req.Id, this.AmountFor(stored))!;
                    if (!Deliver(front, entrySide, item, ctx))
                        slots.AddToAdditional(req.Id, item, item.Count);
                    return true;
                }

                return false;
            }

            default:
                throw new InvalidOperationException($"Origem inválida: {this.Source}.");
        }
    }

    /// <summary>Entrega a pilha ao nó da frente. Retorna false se sobrou algo (que continua em 'item').</summary>
    private static bool Deliver(LogisticsNode front, Direction entrySide, ILogisticsItem item, TickContext ctx)
    {
        int count = item.Count;
        return front.Accept(entrySide, item, ctx) >= count;
    }

    /// <summary>Quantidade por operação ao retirar da pilha indicada: Single = 1, Stack = tudo, Configurable = a configurada.</summary>
    private int AmountFor(ILogisticsItem stack)
    {
        return this.Tier switch
        {
            TransferTier.Single => 1,
            TransferTier.Stack => stack.Count,
            TransferTier.Configurable => this.ConfiguredAmount,
            _ => throw new InvalidOperationException($"Tier inválido: {this.Tier}."),
        };
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
