using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Túnel (README "Túnel e Esteira Subterrânea"): bloco da superfície que liga a superfície à camada subterrânea. A
/// entrada recebe por trás e entrega ao bloco subterrâneo do tile à frente; a saída recebe do bloco subterrâneo do tile
/// atrás (que entrega nela) e entrega à frente como uma esteira.
/// </summary>
/// <remarks>
/// Anda com <see cref="Balance.UndergroundSpeed"/>. A entrada recebe só por trás (sem curva) e nunca junta pilhas; a saída
/// não recebe de nenhum nó da superfície. A entrada empurra na Esteira ou no Filtro Subterrâneo à frente, e o Divisor
/// Subterrâneo à frente a puxa (<see cref="GetUndergroundOutgoingSlot"/>). Sem bloco que aceite, o item para no fim da entrada.
/// </remarks>
public sealed class TunnelNode : LogisticsNode
{
    public TunnelNode(TilePos position, Direction facing, TunnelRole role)
        : base(position, facing)
    {
        this.Role = role;
    }

    public override NodeKind Kind => NodeKind.Tunnel;

    /// <summary>Definido na colocação (guardado no <c>modData</c>) e fixo depois.</summary>
    public TunnelRole Role { get; }

    /// <summary>Sempre a traseira: o Túnel não faz curva.</summary>
    public Direction InputSide => this.Facing.Opposite();

    /// <summary>O slot do próprio tile.</summary>
    public TransitSlot Slot { get; } = new();

    /// <summary>Tile à frente: o do bloco subterrâneo (entrada) ou o do nó que recebe na superfície (saída).</summary>
    public TilePos FrontPosition => this.Position.Offset(this.Facing);

    /// <summary>O tile do bloco subterrâneo ligado a este Túnel: à frente da entrada, atrás da saída (plano mestre D22).</summary>
    public TilePos UndergroundPosition => this.Role == TunnelRole.Entrance ? this.FrontPosition : this.Position.Offset(this.InputSide);

    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot };

    /// <summary>Só a saída entrega na superfície, no tile à frente (assim a esteira da frente faz curva saindo dela).</summary>
    public override bool ProvidesTo(TilePos neighbor)
    {
        return this.Role == TunnelRole.Exit && neighbor == this.FrontPosition;
    }

    /// <summary>Entrada: o bloco subterrâneo à frente. Saída: o nó da superfície à frente.</summary>
    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        LogisticsNode? next = this.Role == TunnelRole.Entrance ? grid.GetUnderground(this.FrontPosition) : grid.Get(this.FrontPosition);
        return next == null ? Array.Empty<LogisticsNode>() : new[] { next };
    }

    /// <summary>O slot da saída, para quem puxa à frente dela (Inseridor, Divisor).</summary>
    public override TransitSlot? GetOutgoingSlot(TilePos target)
    {
        return this.Role == TunnelRole.Exit && target == this.FrontPosition && this.Slot.Item != null ? this.Slot : null;
    }

    /// <summary>Só a entrada recebe de um vizinho da superfície: por trás, com o <see cref="Slot"/> vazio.</summary>
    public override bool CanReceiveFrom(Direction entrySide)
    {
        return this.Role == TunnelRole.Entrance && entrySide == this.InputSide && this.Slot.IsEmpty;
    }

    public override bool CanAccept(Direction entrySide, ILogisticsItem item)
    {
        return this.CanReceiveFrom(entrySide);
    }

    /// <summary>Guarda a pilha inteira, se pode receber; senão nada. Nunca junta.</summary>
    public override int Accept(Direction entrySide, ILogisticsItem item, TickContext ctx)
    {
        if (!this.CanReceiveFrom(entrySide))
            return 0;

        this.Slot.Put(item, entrySide);
        return item.Count;
    }

    public override void Update(TickContext ctx)
    {
        if (this.Slot.IsEmpty || !this.Slot.Advance(Balance.UndergroundSpeed))
            return;

        if (this.Role == TunnelRole.Exit)
        {
            ctx.PushForward(this, this.Slot, this.Facing);
            return;
        }

        // Entrada: entrega ao bloco subterrâneo à frente, pelo lado virado para o Túnel. Um Divisor não recebe empurrado:
        // o item fica parado no fim até ele puxar.
        LogisticsNode? below = ctx.UndergroundAt(this.FrontPosition);
        ILogisticsItem item = this.Slot.Item!;
        int before = item.Count;
        if (below != null && below.Accept(this.Facing.Opposite(), item, ctx) == before)
            this.Slot.Clear();
        else
            this.Slot.Blocked = true;
    }

    /// <summary>True se a saída pode receber agora a pilha do bloco subterrâneo atrás dela.</summary>
    internal bool CanReceiveFromUnderground()
    {
        return this.Role == TunnelRole.Exit && this.Slot.IsEmpty;
    }

    /// <summary>
    /// O bloco subterrâneo atrás entrega a pilha inteira na saída. Retorna false (e não guarda nada) se este Túnel não
    /// é uma saída ou se o <see cref="Slot"/> está ocupado.
    /// </summary>
    internal bool ReceiveFromUnderground(ILogisticsItem item)
    {
        if (!this.CanReceiveFromUnderground())
            return false;

        this.Slot.Put(item, this.InputSide);
        return true;
    }

    /// <summary>
    /// O slot da entrada, para o Divisor Subterrâneo à frente dela, que puxa. Nunca é oferecido a nós da superfície (só o
    /// <see cref="TickContext.OutgoingSlot"/> de um bloco subterrâneo chega aqui).
    /// </summary>
    internal TransitSlot? GetUndergroundOutgoingSlot()
    {
        return this.Role == TunnelRole.Entrance && this.Slot.Item != null ? this.Slot : null;
    }
}
