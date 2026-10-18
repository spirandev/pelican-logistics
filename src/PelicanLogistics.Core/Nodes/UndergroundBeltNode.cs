using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Esteira Subterrânea (README "Transporte"): par entrada/saída que passa itens por baixo de até
/// <see cref="Balance.UndergroundMaxGap"/> tiles. O que estiver no vão não interfere.
/// </summary>
/// <remarks>
/// <para>
/// Entrada: recebe só por trás (sem curva), quando o <see cref="Slot"/> está vazio, e manda os itens pelo
/// <see cref="Tunnel"/> (um slot por tile do vão) até o <see cref="Slot"/> da saída. Sem par, o item para no fim do
/// <see cref="Slot"/>. Saída: recebe só do par e entrega ao nó à frente como uma esteira.
/// </para>
/// <para>
/// O túnel tem sempre <see cref="Balance.UndergroundMaxGap"/> slots (assim a persistência não depende do pareamento),
/// mas só os <see cref="TunnelLength"/> primeiros são usados. Se o par for refeito mais perto, os itens que estavam além
/// do vão novo seguem direto para a saída, sem se perder. Todos os slots andam com <see cref="Balance.UndergroundSpeed"/>.
/// </para>
/// </remarks>
public sealed class UndergroundBeltNode : LogisticsNode
{
    public UndergroundBeltNode(TilePos position, Direction facing, UndergroundRole role)
        : base(position, facing)
    {
        this.Role = role;
        this.Tunnel = role == UndergroundRole.Entrance
            ? Enumerable.Range(0, Balance.UndergroundMaxGap).Select(_ => new TransitSlot()).ToArray()
            : Array.Empty<TransitSlot>();
    }

    public override NodeKind Kind => NodeKind.UndergroundBelt;

    /// <summary>Definido na colocação (guardado no <c>modData</c>) e fixo depois.</summary>
    public UndergroundRole Role { get; }

    /// <summary>Sempre a traseira: a Subterrânea não faz curva.</summary>
    public Direction InputSide => this.Facing.Opposite();

    /// <summary>O slot do próprio tile.</summary>
    public TransitSlot Slot { get; } = new();

    /// <summary>Só na entrada: um slot por tile do vão, do mais perto da entrada ao mais perto da saída. Vazio na saída.</summary>
    public IReadOnlyList<TransitSlot> Tunnel { get; }

    /// <summary>A outra ponta do par, ou null. Calculado pela <see cref="Topology"/>.</summary>
    public UndergroundBeltNode? Partner { get; internal set; }

    /// <summary>Tiles do vão entre a entrada e a saída pareadas (0..<see cref="Balance.UndergroundMaxGap"/>). Calculado pela <see cref="Topology"/>.</summary>
    public int TunnelLength { get; internal set; }

    /// <summary><see cref="Slot"/> e depois o <see cref="Tunnel"/> (para desenho, persistência e remoção).</summary>
    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot }.Concat(this.Tunnel);

    /// <summary>Só a saída entrega, e só no tile à frente (assim a esteira da frente faz curva saindo dela).</summary>
    public override bool ProvidesTo(TilePos neighbor)
    {
        return this.Role == UndergroundRole.Exit && neighbor == this.Position.Offset(this.Facing);
    }

    /// <summary>Entrada: a saída parceira. Saída: o nó à frente.</summary>
    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        if (this.Role == UndergroundRole.Entrance)
            return this.Partner != null ? new LogisticsNode[] { this.Partner } : Array.Empty<LogisticsNode>();

        LogisticsNode? front = grid.Get(this.Position.Offset(this.Facing));
        return front == null ? Array.Empty<LogisticsNode>() : new[] { front };
    }

    /// <summary>O slot da saída, para quem puxa à frente dela (Inseridor, Divisor).</summary>
    public override TransitSlot? GetOutgoingSlot(TilePos target)
    {
        return this.Role == UndergroundRole.Exit && target == this.Position.Offset(this.Facing) && this.Slot.Item != null ? this.Slot : null;
    }

    /// <summary>Só a entrada recebe de um vizinho: por trás, com o <see cref="Slot"/> vazio.</summary>
    public override bool CanReceiveFrom(Direction entrySide)
    {
        return this.Role == UndergroundRole.Entrance && entrySide == this.InputSide && this.Slot.IsEmpty;
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
        if (this.Role == UndergroundRole.Exit)
        {
            if (!this.Slot.IsEmpty && this.Slot.Advance(Balance.UndergroundSpeed))
                ctx.PushForward(this, this.Slot, this.Facing);
            return;
        }

        // Do fim do túnel para o começo, para a fila andar junta no mesmo tick (como nas esteiras).
        for (int i = this.Tunnel.Count - 1; i >= 0; i--)
            this.AdvanceEntranceSlot(this.Tunnel[i], i + 1);

        this.AdvanceEntranceSlot(this.Slot, 0);
    }

    /// <summary>
    /// Avança um slot da entrada e, no fim, passa a pilha para o próximo: o slot 'next' do túnel ou, a partir do fim
    /// do vão, o <see cref="Slot"/> da saída. Sem par, a pilha para no fim.
    /// </summary>
    private void AdvanceEntranceSlot(TransitSlot slot, int next)
    {
        if (slot.IsEmpty || !slot.Advance(Balance.UndergroundSpeed))
            return;

        TransitSlot? target = this.Partner == null
            ? null
            : next < this.TunnelLength ? this.Tunnel[next] : this.Partner.Slot;
        if (target == null || !target.IsEmpty)
        {
            slot.Blocked = true;
            return;
        }

        target.Put(slot.Clear()!, this.InputSide);
    }
}
