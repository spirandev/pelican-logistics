using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Inseridor (README "Inseridores"): pega atrás (baú, máquina ou esteira que termina nele) ou de uma esteira
/// que termina nele pela lateral, e entrega na frente (baú, máquina ou esteira que começa nele).
/// </summary>
/// <remarks>
/// <para>
/// Puxa as entradas (plano mestre D16): a esteira que termina nele fica parada no fim até ser puxada. Com várias
/// entradas, alterna em ordem horária a partir da última usada. Só pega o que o destino aceita agora, e só a
/// quantidade que cabe, sem reservar espaço (D17): se o destino encher durante a ida, o item espera na mão.
/// </para>
/// <para>
/// Ciclo: pega (mão no início), a mão vai até o destino em <c>End / InserterSpeed</c> ticks, entrega e o braço
/// volta vazio em <see cref="Balance.InserterReturnTicks"/> ticks. Ida + volta = 1 operação a cada 0,5 s.
/// </para>
/// </remarks>
public sealed class InserterNode : LogisticsNode
{
    private int configuredAmount = 1;

    public InserterNode(TilePos position, Direction facing, TransferTier tier = TransferTier.Single)
        : base(position, facing)
    {
        this.Tier = tier;
        this.Inputs = new[] { facing.Opposite() };
    }

    public override NodeKind Kind => NodeKind.Inserter;

    public TransferTier Tier { get; }

    /// <summary>Quantidade por operação da versão Configurável (Fase 6). Limitada a 1..<see cref="Balance.MaxConfigurableAmount"/>.</summary>
    public int ConfiguredAmount
    {
        get => this.configuredAmount;
        set => this.configuredAmount = Math.Clamp(value, 1, Balance.MaxConfigurableAmount);
    }

    /// <summary>De onde retirar quando a origem atrás é uma máquina. Ignorado em baús e esteiras.</summary>
    public PickupSource Source { get; set; } = PickupSource.Product;

    /// <summary>Qual slot virtual abastecer quando o destino é uma máquina.</summary>
    public InserterSlotMode SlotMode { get; set; } = InserterSlotMode.Auto;

    /// <summary>A mão. <see cref="TransitSlot.EntrySide"/> é o lado da origem; o progresso vai de 0 a <see cref="Balance.End"/> durante a ida.</summary>
    public TransitSlot Hand { get; } = new();

    /// <summary>Ticks restantes da volta do braço (0 = pronto para pegar).</summary>
    public int Cooldown { get; internal set; }

    /// <summary>Última entrada usada no rodízio. Null = nunca pegou (o rodízio começa pelo norte).</summary>
    public Direction? LastInput { get; internal set; }

    /// <summary>
    /// Lados de onde o Inseridor pode pegar, em ordem horária N → E → S → W. Calculado pela <see cref="Topology"/>:
    /// a traseira sempre, e cada lateral com uma esteira que termina nele.
    /// </summary>
    public IReadOnlyList<Direction> Inputs { get; internal set; }

    /// <summary>Tile do destino.</summary>
    public TilePos FrontPosition => this.Position.Offset(this.Facing);

    /// <summary>Tile da origem (baú ou máquina, ou uma esteira).</summary>
    public TilePos BackPosition => this.Position.Offset(this.Facing.Opposite());

    public override IEnumerable<TransitSlot> Slots => new[] { this.Hand };

    /// <summary>Assim a esteira da frente fica reta, ou faz curva saindo dele.</summary>
    public override bool ProvidesTo(TilePos neighbor)
    {
        return neighbor == this.FrontPosition;
    }

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? front = grid.Get(this.FrontPosition);
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
    }

    public override void Update(TickContext ctx)
    {
        // 1. Carregando: a mão vai até o destino e entrega.
        if (this.Hand.Item != null)
        {
            if (this.Hand.Progress < Balance.End && !this.Hand.Advance(Balance.InserterSpeed))
                return;

            this.DeliverHand(this.ResolveTarget(ctx), ctx);
            return;
        }

        // 2. Voltando. Se chegar a 0, já tenta pegar neste tick, para o ciclo ser exatamente ida + volta.
        if (this.Cooldown > 0)
        {
            this.Cooldown--;
            if (this.Cooldown > 0)
                return;
        }

        // 3. Pegando.
        Target target = this.ResolveTarget(ctx);
        if (!target.IsValid)
            return;

        Direction start = this.LastInput?.Clockwise() ?? Direction.North;
        foreach (Direction side in DirectionExtensions.ClockwiseFrom(start))
        {
            if (!this.IsInput(side))
                continue;

            ILogisticsItem? item = this.TakeFrom(side, target, ctx);
            if (item == null)
                continue;

            this.Hand.Put(item, side);
            this.LastInput = side;
            return;
        }
    }

    // Persistência.

    internal override int ExportCooldown()
    {
        return this.Cooldown;
    }

    internal override void ImportCooldown(int ticks)
    {
        this.Cooldown = Math.Clamp(ticks, 0, Balance.InserterReturnTicks);
    }

    /// <summary>Entrada = <see cref="LastInput"/> (-1 = null, senão o valor de <see cref="Direction"/>). Não há saída.</summary>
    internal override (int Input, int Output) ExportPointers()
    {
        return (this.LastInput is { } last ? (int)last : -1, -1);
    }

    internal override void ImportPointers(int input, int output)
    {
        this.LastInput = Enum.IsDefined(typeof(Direction), input) ? (Direction)input : null;
    }

    // Destino (frente).

    /// <summary>O que está na frente: um nó tem prioridade sobre o endpoint (um tile tem um objeto só).</summary>
    private Target ResolveTarget(TickContext ctx)
    {
        LogisticsNode? front = ctx.NodeAt(this.FrontPosition);
        return front != null ? new Target(front, null) : new Target(null, ctx.Endpoints.GetEndpoint(this.FrontPosition));
    }

    /// <summary>Quantas unidades de 'item' o destino aceitaria agora. Sem efeito colateral.</summary>
    /// <remarks>
    /// Um nó que recebe guarda uma pilha inteira, então o limite é o tamanho máximo da pilha (assim o Inseridor
    /// Configurável junta vários slots de um baú).
    /// </remarks>
    private int Fits(ILogisticsItem item, Target target, TickContext ctx)
    {
        if (target.Node != null)
            return target.IsValid && target.Node.CanAccept(this.Facing.Opposite(), item) ? item.MaxStack : 0;

        return target.Endpoint switch
        {
            IChestEndpoint chest => chest.SpaceFor(item),
            IMachineEndpoint machine => this.MachineFits(machine, ctx.Grid.MachineSlots.Get(this.FrontPosition) ?? new VirtualSlots(), item),
            _ => 0,
        };
    }

    /// <summary>Entrega o que couber. Tudo entregue → mão vazia e o braço volta; sobrou → espera na mão (D17).</summary>
    private void DeliverHand(Target target, TickContext ctx)
    {
        ILogisticsItem item = this.Hand.Item!;
        int count = item.Count;
        int delivered = this.Deliver(item, target, ctx);
        if (delivered >= count)
        {
            this.Hand.Clear();
            this.Cooldown = Balance.InserterReturnTicks;
        }
        else
        {
            this.Hand.Blocked = true;
        }
    }

    /// <summary>Entrega até <c>item.Count</c> unidades ao destino. Retorna quantas entraram (contrato de <see cref="ILogisticsItem"/>).</summary>
    private int Deliver(ILogisticsItem item, Target target, TickContext ctx)
    {
        if (target.Node != null)
            return target.IsValid ? target.Node.Accept(this.Facing.Opposite(), item, ctx) : 0;

        return target.Endpoint switch
        {
            IChestEndpoint chest => chest.Insert(item, item.Count),
            IMachineEndpoint machine => this.InsertIntoMachine(machine, ctx.Grid.MachineSlots.GetOrCreate(this.FrontPosition), item),
            _ => 0,
        };
    }

    // Origens.

    private bool IsInput(Direction side)
    {
        foreach (Direction input in this.Inputs)
        {
            if (input == side)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Retira da entrada no lado 'side' o que o destino aceita, na quantidade do tier. Um nó vizinho entrega o
    /// slot que termina no Inseridor; sem nó, só a traseira olha baús e máquinas. Null se não pegou nada.
    /// </summary>
    private ILogisticsItem? TakeFrom(Direction side, Target target, TickContext ctx)
    {
        TilePos pos = this.Position.Offset(side);
        LogisticsNode? neighbor = ctx.NodeAt(pos);
        if (neighbor != null)
            return this.TakeFromNode(neighbor, target, ctx);

        if (side != this.Facing.Opposite())
            return null;

        return ctx.Endpoints.GetEndpoint(pos) switch
        {
            IChestEndpoint chest => this.TakeFromChest(chest, target, ctx),
            IMachineEndpoint machine => this.TakeFromMachine(machine, pos, target, ctx),
            _ => null,
        };
    }

    private ILogisticsItem? TakeFromNode(LogisticsNode neighbor, Target target, TickContext ctx)
    {
        TransitSlot? slot = neighbor.GetOutgoingSlot(this.Position);
        if (slot?.Item == null || slot.Progress != Balance.End)
            return null;

        ILogisticsItem stack = slot.Item;
        int amount = Math.Min(stack.Count, Math.Min(this.TierAmount(stack), this.Fits(stack, target, ctx)));
        if (amount <= 0)
            return null;

        if (amount < stack.Count)
            return stack.Split(amount);

        slot.Clear();
        return stack;
    }

    /// <summary>De um baú, a primeira pilha que o destino aceita (qualquer posição do inventário).</summary>
    private ILogisticsItem? TakeFromChest(IChestEndpoint chest, Target target, TickContext ctx)
    {
        ILogisticsItem? stack = chest.Find(i => this.Fits(i, target, ctx) > 0);
        if (stack == null)
            return null;

        int amount = Math.Min(this.TierAmount(stack), this.Fits(stack, target, ctx));
        return amount > 0 ? chest.Extract(i => i.CanStackWith(stack), ExtractAmount.Exactly, amount) : null;
    }

    /// <summary>Retira da máquina conforme <see cref="Source"/>. O lado da máquina onde o Inseridor está não importa.</summary>
    private ILogisticsItem? TakeFromMachine(IMachineEndpoint machine, TilePos pos, Target target, TickContext ctx)
    {
        switch (this.Source)
        {
            case PickupSource.Product:
            {
                ILogisticsItem? output = machine.PeekOutput();
                if (output == null || output.Count <= 0)
                    return null;

                int amount = Math.Min(this.TierAmount(output), this.Fits(output, target, ctx));
                return amount > 0 ? machine.TakeOutput(amount) : null;
            }

            case PickupSource.Main:
            {
                VirtualSlots? slots = ctx.Grid.MachineSlots.Get(pos);
                if (slots?.Main == null)
                    return null;

                int amount = Math.Min(this.TierAmount(slots.Main), this.Fits(slots.Main, target, ctx));
                return amount > 0 ? slots.TakeFromMain(amount) : null;
            }

            case PickupSource.Additional:
            {
                VirtualSlots? slots = ctx.Grid.MachineSlots.Get(pos);
                if (slots == null)
                    return null;

                foreach (AdditionalRequirement req in machine.AdditionalRequirements)
                {
                    if (!slots.Additional.TryGetValue(req.Id, out ILogisticsItem? stored))
                        continue;

                    int amount = Math.Min(this.TierAmount(stored), this.Fits(stored, target, ctx));
                    if (amount > 0)
                        return slots.TakeFromAdditional(req.Id, amount);
                }

                return null;
            }

            default:
                throw new InvalidOperationException($"Origem inválida: {this.Source}.");
        }
    }

    /// <summary>Quantidade por operação ao pegar da pilha indicada: Single = 1, Stack = a pilha, Configurable = a configurada.</summary>
    private int TierAmount(ILogisticsItem stack)
    {
        return this.Tier switch
        {
            TransferTier.Single => 1,
            TransferTier.Stack => stack.Count,
            TransferTier.Configurable => this.ConfiguredAmount,
            _ => throw new InvalidOperationException($"Tier inválido: {this.Tier}."),
        };
    }

    // Máquina como destino (README "Slots virtuais"): só o que falta, Principal só com o mesmo item, regra do Cristalário.

    /// <summary>Quantas unidades de 'item' o slot do <see cref="SlotMode"/> aceitaria agora. Sem efeito colateral.</summary>
    private int MachineFits(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item)
    {
        return this.SlotFor(machine, slots, item) switch
        {
            InserterSlotMode.Main => MainFits(machine, slots, item),
            InserterSlotMode.Additional => FindAdditional(machine, slots, item) is { } req ? req.RequiredCount - slots.CountOf(req) : 0,
            _ => 0,
        };
    }

    /// <summary>Guarda nos slots virtuais o que couber de 'item'. Retorna quantas entraram.</summary>
    private int InsertIntoMachine(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item)
    {
        int n = Math.Min(item.Count, this.MachineFits(machine, slots, item));
        if (n <= 0)
            return 0;

        if (this.SlotFor(machine, slots, item) == InserterSlotMode.Additional)
            return slots.AddToAdditional(FindAdditional(machine, slots, item)!.Id, item, n);

        int required = slots.Main == null ? machine.GetMainRequiredCount(item)!.Value : slots.MainRequired;
        return slots.AddToMain(item, n, required);
    }

    /// <summary>Slot (Principal ou Adicional) para o item: o forçado, ou no Automático o Adicional se algum requisito aceita o item.</summary>
    private InserterSlotMode SlotFor(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item)
    {
        return this.SlotMode switch
        {
            InserterSlotMode.Auto => FindAdditional(machine, slots, item) != null ? InserterSlotMode.Additional : InserterSlotMode.Main,
            InserterSlotMode.Main => InserterSlotMode.Main,
            InserterSlotMode.Additional => InserterSlotMode.Additional,
            _ => throw new InvalidOperationException($"Modo de slot inválido: {this.SlotMode}."),
        };
    }

    private static int MainFits(IMachineEndpoint machine, VirtualSlots slots, ILogisticsItem item)
    {
        // Cristalário: só recebe uma gema quando está vazio (README "Máquinas suportadas").
        if (machine.IsRepeating && (machine.HasLoadedInput || slots.Main != null))
            return 0;

        if (slots.Main == null)
            return machine.GetMainRequiredCount(item) is > 0 and int required ? required : 0;

        // O Principal só aceita o mesmo item até completar.
        return slots.Main.CanStackWith(item) ? Math.Max(0, slots.MainRequired - slots.Main.Count) : 0;
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

    /// <summary>O destino na frente, resolvido uma vez por tick: o nó (com prioridade) ou o endpoint.</summary>
    private readonly record struct Target(LogisticsNode? Node, IEndpoint? Endpoint)
    {
        /// <summary>
        /// Há algo que recebe: um nó que não é Inseridor nem Divisor (plano mestre D3), um baú ou uma máquina.
        /// </summary>
        public bool IsValid => this.Node != null
            ? this.Node.Kind is not (NodeKind.Inserter or NodeKind.Splitter)
            : this.Endpoint is IChestEndpoint or IMachineEndpoint;
    }
}
