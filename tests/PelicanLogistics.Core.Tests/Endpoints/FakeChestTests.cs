using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Endpoints;

/// <summary>As consultas sem efeito colateral do baú (Find e SpaceFor), que o Inseridor usa antes de pegar.</summary>
public class FakeChestTests
{
    [Fact]
    public void Find_ReturnsFirstMatch_WithoutTaking()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 3), new FakeItem("stone", 2), new FakeItem("stone", 4));

        var found = chest.Find(i => i.QualifiedItemId == "stone");

        Assert.Same(chest.Items[1], found);
        Assert.Equal(9, chest.CountTotal());
        Assert.Equal(3, chest.Items.Count);
    }

    [Fact]
    public void Find_NoMatch_ReturnsNull()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 3));

        Assert.Null(chest.Find(i => i.QualifiedItemId == "stone"));
    }

    [Fact]
    public void SpaceFor_FullChest_IsZero()
    {
        var chest = new FakeChest(2, new FakeItem("stone", 999), new FakeItem("wood", 999));

        Assert.Equal(0, chest.SpaceFor(new FakeItem("wood", 1)));
    }

    [Fact]
    public void SpaceFor_IncompleteStack_CountsWhatIsMissing()
    {
        var chest = new FakeChest(1, new FakeItem("wood", 990));

        Assert.Equal(9, chest.SpaceFor(new FakeItem("wood", 1)));
    }

    [Fact]
    public void SpaceFor_FreeSlots_CountMaxStackEach()
    {
        var chest = new FakeChest(3, new FakeItem("stone", 10));

        Assert.Equal(2 * 999, chest.SpaceFor(new FakeItem("wood", 1)));
    }

    [Fact]
    public void SpaceFor_DifferentQuality_DoesNotUseThatStack()
    {
        var chest = new FakeChest(2, new FakeItem("wood", 10, quality: 2));

        Assert.Equal(999, chest.SpaceFor(new FakeItem("wood", 1)));
        Assert.Equal(989 + 999, chest.SpaceFor(new FakeItem("wood", 1, quality: 2)));
    }

    [Fact]
    public void SpaceFor_HasNoSideEffect()
    {
        var chest = new FakeChest(2, new FakeItem("wood", 10));
        var item = new FakeItem("wood", 5);

        chest.SpaceFor(item);

        Assert.Equal(5, item.Count);
        Assert.Equal(10, chest.CountTotal());
        Assert.Single(chest.Items);
    }
}
