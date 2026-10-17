using System;
using PelicanLogistics.Core.Items;
using StardewValley;

namespace PelicanLogistics.Framework.World;

/// <summary>Implementa <see cref="ILogisticsItem"/> envolvendo um item do jogo.</summary>
internal sealed class GameItem : ILogisticsItem
{
    public GameItem(Item item)
    {
        this.Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>O item do jogo.</summary>
    public Item Item { get; }

    public string QualifiedItemId => this.Item.QualifiedItemId;

    public int Quality => this.Item.Quality;

    public int Category => this.Item.Category;

    public string DisplayName => this.Item.DisplayName;

    public int Count => this.Item.Stack;

    public int MaxStack => this.Item.maximumStackSize();

    public bool HasContextTag(string tag)
    {
        return this.Item.HasContextTag(tag);
    }

    public bool CanStackWith(ILogisticsItem other)
    {
        return other is GameItem game && this.Item.canStackWith(game.Item);
    }

    public ILogisticsItem Split(int amount)
    {
        if (amount < 1 || amount >= this.Count)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Split precisa de 1 <= amount < {this.Count}.");

        Item part = this.Item.getOne();
        part.Stack = amount;
        this.Item.Stack -= amount;
        return new GameItem(part);
    }

    public int MergeFrom(ILogisticsItem other, int max)
    {
        if (other is not GameItem source || !this.CanStackWith(source))
            return 0;

        int n = Math.Min(max, Math.Min(source.Count, this.MaxStack - this.Count));
        if (n <= 0)
            return 0;

        this.Item.Stack += n;
        source.Item.Stack -= n;
        return n;
    }
}
