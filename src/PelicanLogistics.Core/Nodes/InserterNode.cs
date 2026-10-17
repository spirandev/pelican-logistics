using System;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Inseridor: recebe da esteira ATRÁS dele e coloca no destino À FRENTE (baú ou máquina) (plano mestre D2).
/// Em máquinas, abastece os slots virtuais (<see cref="VirtualSlots"/>) conforme o <see cref="SlotMode"/>.
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

    /// <summary>Qual slot virtual abastecer quando o destino é uma máquina.</summary>
    public InserterSlotMode SlotMode { get; set; } = InserterSlotMode.Auto;

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

        int inserted = ctx.Endpoints.GetEndpoint(this.TargetPosition) switch
        {
            IChestEndpoint chest => chest.Insert(item, max),
            IMachineEndpoint machine => this.InsertIntoMachine(machine, item, max, ctx),
            _ => 0,
        };

        if (inserted > 0)
            this.Cooldown = Balance.TransferCooldownTicks;
        return inserted;
    }

    public override void Update(TickContext ctx)
    {
        if (this.Cooldown > 0)
            this.Cooldown--;
    }

    /// <summary>
    /// Guarda até 'max' unidades nos slots virtuais da máquina, só o que falta para o slot (README "Inseridor em
    /// máquina"). Retorna quantas entraram; o resto espera na esteira.
    /// </summary>
    private int InsertIntoMachine(IMachineEndpoint machine, ILogisticsItem item, int max, TickContext ctx)
    {
        VirtualSlots slots = ctx.Grid.MachineSlots.GetOrCreate(this.TargetPosition);
        return this.SlotMode switch
        {
            InserterSlotMode.Auto => FindAdditional(machine, slots, item) != null
                ? InsertAdditional(machine, slots, item, max)
                : InsertMain(machine, slots, item, max),
            InserterSlotMode.Main => InsertMain(machine, slots, item, max),
            InserterSlotMode.Additional => InsertAdditional(machine, slots, item, max),
            _ => throw new InvalidOperationException($"Modo de slot inválido: {this.SlotMode}."),
        };
    }

    private static int InsertMain(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item, int max)
    {
        // Cristalário: só recebe uma gema quando está vazio (README "Máquinas suportadas").
        if (machine.IsRepeating && (machine.HasLoadedInput || slots.Main != null))
            return 0;

        if (slots.Main == null)
        {
            int? required = machine.GetMainRequiredCount(item);
            if (required is not > 0)
                return 0;

            return slots.AddToMain(item, Math.Min(max, required.Value), required.Value);
        }

        // O Principal só aceita o mesmo item até completar.
        if (!slots.Main.CanStackWith(item))
            return 0;

        int n = Math.Min(max, slots.MainRequired - slots.Main.Count);
        return n > 0 ? slots.AddToMain(item, n, slots.MainRequired) : 0;
    }

    private static int InsertAdditional(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item, int max)
    {
        AdditionalRequirement? req = FindAdditional(machine, slots, item);
        if (req == null)
            return 0;

        int n = Math.Min(max, req.RequiredCount - slots.CountOf(req));
        return slots.AddToAdditional(req.Id, item, n);
    }

    /// <summary>Primeiro requisito adicional (em ordem) que aceita o item e ainda não está completo.</summary>
    private static AdditionalRequirement? FindAdditional(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item)
    {
        foreach (AdditionalRequirement req in machine.AdditionalRequirements)
        {
            if (slots.CountOf(req) < req.RequiredCount && req.Matches(item) && CanJoin(slots, req, item))
                return req;
        }

        return null;
    }

    /// <summary>True se o item pode se juntar ao que já está guardado para o requisito (ex.: mesma qualidade).</summary>
    private static bool CanJoin(VirtualSlots slots, AdditionalRequirement req, ILogisticsItem item)
    {
        return !slots.Additional.TryGetValue(req.Id, out ILogisticsItem? current) || current.CanStackWith(item);
    }
}
