using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Filtro (README "Filtro"): bloco de esteira sempre reto (entra por trás, sai pela frente) e sempre rápido, que
/// só deixa entrar o que a <see cref="Rule"/> aceita. O item que não bate fica parado no bloco anterior.
/// </summary>
/// <remarks>
/// Segura no máximo uma pilha e nunca junta itens. Um Inseridor com o filtro à frente só retira o que o filtro
/// aceita (filtro acoplado), porque o Inseridor só pega o que o destino aceita.
/// </remarks>
public sealed class FilterNode : LogisticsNode
{
    private IFilterRule rule;

    public FilterNode(TilePos position, Direction facing, IFilterRule rule)
        : base(position, facing)
    {
        this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public override NodeKind Kind => NodeKind.Filter;

    /// <summary><see cref="CommonFilterRule"/> ou <see cref="CompleteFilterRule"/>. Pode ser trocada a qualquer momento (interface).</summary>
    public IFilterRule Rule
    {
        get => this.rule;
        set => this.rule = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>True se é um Filtro Completo.</summary>
    public bool IsComplete => this.rule is CompleteFilterRule;

    /// <summary>Sempre a traseira: o filtro não faz curva.</summary>
    public Direction InputSide => this.Facing.Opposite();

    /// <summary>O único espaço do filtro: uma pilha por vez.</summary>
    public TransitSlot Slot { get; } = new();

    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot };

    public override bool ProvidesTo(TilePos neighbor)
    {
        return neighbor == this.Position.Offset(this.Facing);
    }

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? front = grid.Get(this.Position.Offset(this.Facing));
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
    }

    public override TransitSlot? GetOutgoingSlot(TilePos target)
    {
        return target == this.Position.Offset(this.Facing) && this.Slot.Item != null ? this.Slot : null;
    }

    public override bool CanReceiveFrom(Direction entrySide)
    {
        return entrySide == this.InputSide && this.Slot.IsEmpty;
    }

    public override bool CanAccept(Direction entrySide, ILogisticsItem item)
    {
        return this.CanReceiveFrom(entrySide) && this.rule.Matches(item);
    }

    /// <summary>Guarda a pilha inteira, se aceita; senão nada. Nunca junta.</summary>
    public override int Accept(Direction entrySide, ILogisticsItem item, TickContext ctx)
    {
        if (!this.CanAccept(entrySide, item))
            return 0;

        this.Slot.Put(item, entrySide);
        return item.Count;
    }

    public override void Update(TickContext ctx)
    {
        if (!this.Slot.IsEmpty && this.Slot.Advance(Balance.FilterSpeed))
            ctx.PushForward(this, this.Slot, this.Facing);
    }
}
