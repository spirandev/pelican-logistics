using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Base de todos os blocos do mod.
/// </summary>
/// <remarks>
/// Convenção de lados: 'entrySide' é o lado DESTE nó por onde o item entra. Se o item vem do vizinho
/// a oeste, entrySide = West. Para entregar a um vizinho na direção d, o nó chama
/// <c>vizinho.Accept(d.Opposite(), item, ctx)</c>.
/// </remarks>
public abstract class LogisticsNode
{
    protected LogisticsNode(TilePos position, Direction facing)
    {
        this.Position = position;
        this.Facing = facing;
    }

    public TilePos Position { get; }

    /// <summary>Direção do bloco. Para nós sem direção (Divisor), é ignorada. Quem altera deve chamar <see cref="LogisticsGrid.MarkDirty"/>.</summary>
    public Direction Facing { get; set; }

    public abstract NodeKind Kind { get; }

    /// <summary>Acessório sobre o bloco (usado a partir da Fase 5).</summary>
    public AccessoryKind Accessory { get; set; } = AccessoryKind.None;

    /// <summary>Todos os espaços do nó. Usado no desenho, na persistência e na remoção.</summary>
    public virtual IEnumerable<TransitSlot> Slots => Array.Empty<TransitSlot>();

    /// <summary>True se este nó entrega itens no tile vizinho indicado.</summary>
    public virtual bool ProvidesTo(TilePos neighbor)
    {
        return false;
    }

    /// <summary>Nós para onde este nó entrega itens. Usado para calcular a ordem de processamento.</summary>
    public virtual IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        return Array.Empty<LogisticsNode>();
    }

    /// <summary>True se o nó pode receber agora uma pilha qualquer pelo lado indicado. Sem efeito colateral.</summary>
    public virtual bool CanReceiveFrom(Direction entrySide)
    {
        return false;
    }

    /// <summary>True se o nó aceitaria agora este item pelo lado indicado. Sem efeito colateral.</summary>
    public virtual bool CanAccept(Direction entrySide, ILogisticsItem item)
    {
        return false;
    }

    /// <summary>
    /// Aceita até <c>item.Count</c> unidades pelo lado indicado e retorna quantas aceitou
    /// (ver o contrato de transferência em <see cref="ILogisticsItem"/>).
    /// </summary>
    public virtual int Accept(Direction entrySide, ILogisticsItem item, TickContext ctx)
    {
        return 0;
    }

    /// <summary>Um tick de lógica.</summary>
    public abstract void Update(TickContext ctx);
}
