using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Tests.Fakes;

/// <summary>Baú para testes: lista de pilhas com um número máximo de slots.</summary>
public sealed class FakeChest : IChestEndpoint
{
    public FakeChest(int capacity = 36, params FakeItem[] items)
    {
        this.Capacity = capacity;
        this.Items = items.Cast<ILogisticsItem>().ToList();
    }

    public int Capacity { get; }

    public List<ILogisticsItem> Items { get; }

    public ILogisticsItem? Extract(Func<ILogisticsItem, bool> predicate, ExtractAmount mode, int amount)
    {
        int index = this.Items.FindIndex(i => predicate(i));
        if (index < 0)
            return null;

        ILogisticsItem first = this.Items[index];
        switch (mode)
        {
            case ExtractAmount.One:
                return this.Take(index, 1);

            case ExtractAmount.Stack:
                this.Items.RemoveAt(index);
                return first;

            case ExtractAmount.Exactly:
            {
                if (amount <= 0)
                    return null;

                int target = Math.Min(amount, first.MaxStack);
                ILogisticsItem? result = null;
                for (int i = index; i < this.Items.Count && (result?.Count ?? 0) < target;)
                {
                    ILogisticsItem stack = this.Items[i];
                    if (!first.CanStackWith(stack))
                    {
                        i++;
                        continue;
                    }

                    int need = target - (result?.Count ?? 0);
                    bool takesAll = stack.Count <= need;
                    ILogisticsItem part = this.Take(i, Math.Min(need, stack.Count));
                    if (result == null)
                        result = part;
                    else
                        result.MergeFrom(part, part.Count);

                    if (!takesAll)
                        i++;
                }

                return result;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    public int Insert(ILogisticsItem item, int max)
    {
        int toInsert = Math.Min(max, item.Count);
        if (toInsert <= 0)
            return 0;

        int inserted = 0;
        foreach (ILogisticsItem stack in this.Items)
        {
            if (inserted == toInsert)
                break;
            if (stack.CanStackWith(item))
                inserted += stack.MergeFrom(item, toInsert - inserted);
        }

        if (inserted < toInsert && this.Items.Count < this.Capacity)
        {
            int rest = toInsert - inserted;
            this.Items.Add(rest == item.Count ? item : item.Split(rest));
            inserted = toInsert;
        }

        return inserted;
    }

    public int CountById(string qualifiedItemId)
    {
        return this.Items.Where(i => i.QualifiedItemId == qualifiedItemId).Sum(i => i.Count);
    }

    public int CountTotal()
    {
        return this.Items.Sum(i => i.Count);
    }

    /// <summary>Retira 'amount' da pilha no índice; se for a pilha toda, remove o slot e devolve a instância.</summary>
    private ILogisticsItem Take(int index, int amount)
    {
        ILogisticsItem stack = this.Items[index];
        if (amount >= stack.Count)
        {
            this.Items.RemoveAt(index);
            return stack;
        }

        return stack.Split(amount);
    }
}
