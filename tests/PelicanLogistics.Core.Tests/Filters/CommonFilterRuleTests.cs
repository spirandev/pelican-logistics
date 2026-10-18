using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Filters;

public class CommonFilterRuleTests
{
    private const string Wood = "(O)388";
    private const string Stone = "(O)390";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Common_WoodOfAnyQuality_Passes(int quality)
    {
        var rule = new CommonFilterRule { QualifiedItemId = Wood };

        Assert.True(rule.Matches(new FakeItem(Wood, 1, quality)));
    }

    [Fact]
    public void Common_OtherItem_DoesNotPass()
    {
        var rule = new CommonFilterRule { QualifiedItemId = Wood };

        Assert.False(rule.Matches(new FakeItem(Stone, 1)));
    }

    [Fact]
    public void Common_Empty_LetsNothingPass()
    {
        var rule = new CommonFilterRule();

        Assert.False(rule.Matches(new FakeItem(Wood, 1)));
        Assert.False(rule.Matches(new FakeItem(Stone, 1)));
    }
}
