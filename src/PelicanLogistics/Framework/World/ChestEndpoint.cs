using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;
using StardewValley;
using StardewValley.Objects;

namespace PelicanLogistics.Framework.World;

/// <summary>Implementa <see cref="IChestEndpoint"/> para um baú do jogo.</summary>
internal sealed class ChestEndpoint : IChestEndpoint
{
    private readonly Chest chest;

    public ChestEndpoint(Chest chest)
    {
        this.chest = chest;
    }

    /// <summary>Inventário do baú. Pode conter null (slots vazios).</summary>
    private IList<Item?> Items => this.chest.GetItemsForPlayer();

    /// <summary>
    /// Baús suportados na alpha (plano mestre D10): baús de jogador comuns e grandes (inclui os de pedra).
    /// Ficam de fora: Junimo, auto-alimentador, mini caixa de envio e geladeiras.
    /// </summary>
    public static bool IsSupported(Chest chest)
    {
        return chest.playerChest.Value
            && !chest.fridge.Value
            && (chest.SpecialChestType == Chest.SpecialChestTypes.None || chest.SpecialChestType == Chest.SpecialChestTypes.BigChest);
    }

    public ILogisticsItem? Extract(Func<ILogisticsItem, bool> predicate, ExtractAmount mode, int amount)
    {
        IList<Item?> items = this.Items;
        for (int i = 0; i < items.Count; i++)
        {
            Item? stack = items[i];
            if (stack == null || stack.Stack <= 0 || !predicate(new GameItem(stack)))
                continue;

            return mode switch
            {
                ExtractAmount.One => Take(items, i, 1),
                ExtractAmount.Stack => Take(items, i, stack.Stack),
                ExtractAmount.Exactly => TakeExactly(items, i, amount),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Modo inválido."),
            };
        }

        return null;
    }

    public ILogisticsItem? Find(Func<ILogisticsItem, bool> predicate)
    {
        foreach (Item? stack in this.Items)
        {
            if (stack == null || stack.Stack <= 0)
                continue;

            var item = new GameItem(stack);
            if (predicate(item))
                return item;
        }

        return null;
    }

    /// <summary>Pilhas que empilham: o que falta para o máximo; slots vazios dentro da capacidade: uma pilha cheia cada.</summary>
    public int SpaceFor(ILogisticsItem item)
    {
        Item source = ((GameItem)item).Item;
        int perSlot = source.maximumStackSize();
        int capacity = this.chest.GetActualCapacity();
        IList<Item?> items = this.Items;

        int space = 0;
        for (int i = 0; i < items.Count; i++)
        {
            Item? slot = items[i];
            if (slot == null)
            {
                if (i < capacity)
                    space += perSlot;
            }
            else if (source.canStackWith(slot))
            {
                space += Math.Max(0, slot.maximumStackSize() - slot.Stack);
            }
        }

        if (items.Count < capacity)
            space += (capacity - items.Count) * perSlot;
        return space;
    }

    public int Insert(ILogisticsItem item, int max)
    {
        var source = (GameItem)item;
        int toInsert = Math.Min(max, source.Count);
        if (toInsert <= 0)
            return 0;

        IList<Item?> items = this.Items;

        // Primeiro junta em pilhas existentes.
        int remaining = toInsert;
        foreach (Item? slot in items)
        {
            if (remaining <= 0)
                break;
            if (slot == null || !source.Item.canStackWith(slot))
                continue;

            Item sample = source.Item.getOne();
            sample.Stack = remaining;
            remaining = slot.addToStack(sample);
        }

        // Depois usa um slot livre. Se nada foi juntado e vai a pilha inteira, o baú fica com a própria instância.
        bool keptInstance = false;
        if (remaining > 0)
        {
            int index = this.FindFreeSlot(items);
            if (index >= 0)
            {
                Item piece;
                if (remaining == source.Count)
                {
                    piece = source.Item;
                    keptInstance = true;
                }
                else
                {
                    piece = source.Item.getOne();
                    piece.Stack = remaining;
                }

                if (index == items.Count)
                    items.Add(piece);
                else
                    items[index] = piece;
                remaining = 0;
            }
        }

        int inserted = toInsert - remaining;
        if (!keptInstance)
            source.Item.Stack -= inserted;
        return inserted;
    }

    public int CountById(string qualifiedItemId)
    {
        int total = 0;
        foreach (Item? item in this.Items)
        {
            if (item != null && item.QualifiedItemId == qualifiedItemId)
                total += item.Stack;
        }

        return total;
    }

    public int CountTotal()
    {
        int total = 0;
        foreach (Item? item in this.Items)
            total += item?.Stack ?? 0;
        return total;
    }

    /// <summary>Retira 'amount' da pilha no índice; se for a pilha toda, tira o item do baú e devolve a instância.</summary>
    private static GameItem Take(IList<Item?> items, int index, int amount)
    {
        Item stack = items[index]!;
        if (amount >= stack.Stack)
        {
            items.RemoveAt(index);
            return new GameItem(stack);
        }

        return (GameItem)new GameItem(stack).Split(amount);
    }

    /// <summary>Até 'amount' unidades empilháveis com a pilha no índice, juntando de vários slots, limitado a MaxStack.</summary>
    private static GameItem? TakeExactly(IList<Item?> items, int index, int amount)
    {
        if (amount <= 0)
            return null;

        Item first = items[index]!;
        int target = Math.Min(amount, first.maximumStackSize());
        GameItem? result = null;
        int i = index;
        while (i < items.Count && (result?.Count ?? 0) < target)
        {
            Item? stack = items[i];
            if (stack == null || stack.Stack <= 0 || !first.canStackWith(stack))
            {
                i++;
                continue;
            }

            int need = target - (result?.Count ?? 0);
            bool takesAll = stack.Stack <= need;
            GameItem part = Take(items, i, Math.Min(need, stack.Stack));
            if (result == null)
                result = part;
            else
                result.MergeFrom(part, part.Count);

            // Se a pilha inteira saiu, o próximo item já está no mesmo índice.
            if (!takesAll)
                i++;
        }

        return result;
    }

    /// <summary>Índice de um slot vazio dentro da capacidade, items.Count para acrescentar um slot, ou -1 se o baú está cheio.</summary>
    private int FindFreeSlot(IList<Item?> items)
    {
        int capacity = this.chest.GetActualCapacity();
        for (int i = 0; i < capacity && i < items.Count; i++)
        {
            if (items[i] == null)
                return i;
        }

        return items.Count < capacity ? items.Count : -1;
    }
}
