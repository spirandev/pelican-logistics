using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Tests.Fakes;

/// <summary>Pilha de itens para testes. Empilha com outra se tiver o mesmo id e a mesma qualidade.</summary>
public sealed class FakeItem : ILogisticsItem
{
    private readonly HashSet<string> tags;

    public FakeItem(string id, int count, int quality = 0, int maxStack = 999, int category = 0, params string[] tags)
    {
        this.QualifiedItemId = id;
        this.Count = count;
        this.Quality = quality;
        this.MaxStack = maxStack;
        this.Category = category;
        this.tags = new HashSet<string>(tags);
    }

    public string QualifiedItemId { get; }

    public int Quality { get; }

    public int Category { get; }

    public string DisplayName => this.QualifiedItemId;

    public int Count { get; set; }

    public int MaxStack { get; }

    public bool HasContextTag(string tag) => this.tags.Contains(tag);

    public bool CanStackWith(ILogisticsItem other)
    {
        return other.QualifiedItemId == this.QualifiedItemId && other.Quality == this.Quality;
    }

    public ILogisticsItem Split(int amount)
    {
        if (amount < 1 || amount >= this.Count)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Split precisa de 1 <= amount < {this.Count}.");

        this.Count -= amount;
        return new FakeItem(this.QualifiedItemId, amount, this.Quality, this.MaxStack, this.Category, this.tags.ToArray());
    }

    public int MergeFrom(ILogisticsItem other, int max)
    {
        if (!this.CanStackWith(other))
            return 0;

        var source = (FakeItem)other;
        int n = Math.Min(max, Math.Min(source.Count, this.MaxStack - this.Count));
        if (n <= 0)
            return 0;

        this.Count += n;
        source.Count -= n;
        return n;
    }

    public override string ToString() => $"{this.QualifiedItemId} q{this.Quality} x{this.Count}";
}
