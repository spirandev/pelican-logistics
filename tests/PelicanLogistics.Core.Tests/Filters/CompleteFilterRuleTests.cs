using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Filters;

public class CompleteFilterRuleTests
{
    private const string Wood = "(O)388";
    private const string Stone = "(O)390";
    private const string Parsnip = "(O)24";
    private const string Quartz = "(O)80";
    private const int Vegetable = -75;
    private const int Fruit = -79;
    private const int Mineral = -2;
    private const int Resource = -16;
    private const int Gold = 2;

    [Fact]
    public void Whitelist_ByCategory_OnlyThatCategoryPasses()
    {
        var rule = new CompleteFilterRule { Categories = { Mineral } };

        Assert.True(rule.Matches(new FakeItem(Quartz, 1, category: Mineral)));
        Assert.False(rule.Matches(new FakeItem(Wood, 1, category: Resource)));
    }

    [Fact]
    public void Whitelist_ItemAndQuality_BothMustMatch()
    {
        var rule = new CompleteFilterRule { ItemIds = { Parsnip }, Qualities = { Gold } };

        Assert.True(rule.Matches(new FakeItem(Parsnip, 1, Gold, category: Vegetable)));
        Assert.False(rule.Matches(new FakeItem(Parsnip, 1, 0, category: Vegetable)));
        Assert.False(rule.Matches(new FakeItem(Wood, 1, Gold, category: Resource)));
    }

    [Fact]
    public void Whitelist_TwoCategories_EitherPasses()
    {
        var rule = new CompleteFilterRule { Categories = { Vegetable, Fruit } };

        Assert.True(rule.Matches(new FakeItem(Parsnip, 1, category: Vegetable)));
        Assert.True(rule.Matches(new FakeItem("(O)613", 1, category: Fruit)));
        Assert.False(rule.Matches(new FakeItem(Wood, 1, category: Resource)));
    }

    [Fact]
    public void Whitelist_ContextTag_OnlyItemsWithTheTagPass()
    {
        var rule = new CompleteFilterRule { ContextTags = { "color_red", "color_orange" } };

        Assert.True(rule.Matches(new FakeItem("(O)282", 1, tags: "color_red")));
        Assert.True(rule.Matches(new FakeItem("(O)24", 1, tags: new[] { "season_spring", "color_orange" })));
        Assert.False(rule.Matches(new FakeItem(Wood, 1, tags: "color_brown")));
    }

    [Fact]
    public void Blacklist_InvertsTheMatch()
    {
        var rule = new CompleteFilterRule { Mode = FilterMode.Blacklist, ItemIds = { Wood } };

        Assert.False(rule.Matches(new FakeItem(Wood, 1)));
        Assert.True(rule.Matches(new FakeItem(Stone, 1)));
        Assert.True(rule.Matches(new FakeItem(Parsnip, 1, Gold)));
    }

    [Fact]
    public void Blacklist_Empty_LetsEverythingPass()
    {
        var rule = new CompleteFilterRule { Mode = FilterMode.Blacklist };

        Assert.True(rule.Matches(new FakeItem(Wood, 1)));
        Assert.True(rule.Matches(new FakeItem(Stone, 1, 4)));
    }

    [Fact]
    public void Whitelist_Empty_LetsNothingPass()
    {
        var rule = new CompleteFilterRule();

        Assert.False(rule.Matches(new FakeItem(Wood, 1)));
        Assert.False(rule.Matches(new FakeItem(Stone, 1, 4)));
    }

    [Fact]
    public void Blacklist_TwoGroups_BlocksOnlyItemsMatchingBoth()
    {
        // Lista negra de "vegetais ouro": um vegetal normal passa, uma pedra ouro passa, um vegetal ouro não.
        var rule = new CompleteFilterRule { Mode = FilterMode.Blacklist, Categories = { Vegetable }, Qualities = { Gold } };

        Assert.True(rule.Matches(new FakeItem(Parsnip, 1, 0, category: Vegetable)));
        Assert.True(rule.Matches(new FakeItem(Stone, 1, Gold, category: Resource)));
        Assert.False(rule.Matches(new FakeItem(Parsnip, 1, Gold, category: Vegetable)));
    }
}
