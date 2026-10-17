using System;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>Um espaço que segura uma pilha em movimento dentro de um nó.</summary>
public sealed class TransitSlot
{
    /// <summary>Pilha atual, ou null se o espaço está vazio.</summary>
    public ILogisticsItem? Item { get; private set; }

    /// <summary>Progresso da pilha no tile, de 0 a <see cref="Balance.End"/>.</summary>
    public int Progress { get; private set; }

    /// <summary>Lado do tile por onde a pilha entrou (usado no desenho).</summary>
    public Direction EntrySide { get; private set; }

    /// <summary>True se a pilha chegou ao fim e não conseguiu sair (total ou parcialmente) no último tick.</summary>
    public bool Blocked { get; internal set; }

    public bool IsEmpty => this.Item == null;

    /// <summary>Coloca uma pilha no espaço, no início do tile.</summary>
    public void Put(ILogisticsItem item, Direction entrySide)
    {
        this.Item = item ?? throw new ArgumentNullException(nameof(item));
        this.EntrySide = entrySide;
        this.Progress = 0;
        this.Blocked = false;
    }

    /// <summary>Esvazia o espaço e devolve a pilha que estava nele.</summary>
    public ILogisticsItem? Clear()
    {
        ILogisticsItem? item = this.Item;
        this.Item = null;
        this.Progress = 0;
        this.Blocked = false;
        return item;
    }

    /// <summary>Avança a pilha 'speed' unidades, sem passar do fim. Retorna true se ela está no fim.</summary>
    public bool Advance(int speed)
    {
        this.Progress = Math.Min(Balance.End, this.Progress + speed);
        return this.Progress >= Balance.End;
    }
}
