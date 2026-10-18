using PelicanLogistics.Core.Filters;

namespace PelicanLogistics.Core.Tests.Filters;

public class FilterRuleSerializerTests
{
    [Fact]
    public void Common_RoundTrip()
    {
        var rule = new CommonFilterRule { QualifiedItemId = "(O)388" };

        CommonFilterRule back = FilterRuleSerializer.DeserializeCommon(FilterRuleSerializer.Serialize(rule));

        Assert.Equal("(O)388", back.QualifiedItemId);
    }

    [Fact]
    public void Common_EmptyRule_RoundTripsAsEmpty()
    {
        CommonFilterRule back = FilterRuleSerializer.DeserializeCommon(FilterRuleSerializer.Serialize(new CommonFilterRule()));

        Assert.Null(back.QualifiedItemId);
    }

    [Fact]
    public void Complete_RoundTrip_KeepsEveryGroupAndTheMode()
    {
        var rule = new CompleteFilterRule
        {
            Mode = FilterMode.Blacklist,
            ItemIds = { "(O)388", "(O)390" },
            Categories = { -75, -79 },
            Qualities = { 0, 4 },
            ContextTags = { "color_red" },
        };

        string json = FilterRuleSerializer.Serialize(rule);
        CompleteFilterRule back = FilterRuleSerializer.DeserializeComplete(json);

        Assert.Contains("Blacklist", json); // modo gravado pelo nome
        Assert.Equal(FilterMode.Blacklist, back.Mode);
        Assert.Equal(rule.ItemIds, back.ItemIds);
        Assert.Equal(rule.Categories, back.Categories);
        Assert.Equal(rule.Qualities, back.Qualities);
        Assert.Equal(rule.ContextTags, back.ContextTags);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ isto não é json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"texto\"")]
    [InlineData("{\"QualifiedItemId\": 42}")]
    [InlineData("{\"Mode\": \"Rainbow\"}")]
    [InlineData("{\"Mode\": 7, \"ItemIds\": [\"(O)388\"]}")]
    [InlineData("{\"ItemIds\": {\"a\": 1}}")]
    public void CorruptedJson_GivesEmptyRules_WithoutThrowing(string? json)
    {
        CommonFilterRule common = FilterRuleSerializer.DeserializeCommon(json);
        CompleteFilterRule complete = FilterRuleSerializer.DeserializeComplete(json);

        Assert.Null(common.QualifiedItemId);
        Assert.Equal(FilterMode.Whitelist, complete.Mode);
        Assert.Empty(complete.ItemIds);
        Assert.Empty(complete.Categories);
        Assert.Empty(complete.Qualities);
        Assert.Empty(complete.ContextTags);
    }

    [Fact]
    public void Complete_InvalidValuesInsideTheLists_AreDropped()
    {
        const string json = "{\"Mode\":\"Whitelist\",\"ItemIds\":[\"(O)388\",null,\"  \"],\"Categories\":null,\"Qualities\":[0,3,4,-1],\"ContextTags\":[\" color_red \",\"\"]}";

        CompleteFilterRule rule = FilterRuleSerializer.DeserializeComplete(json);

        Assert.Equal(new[] { "(O)388" }, rule.ItemIds);
        Assert.Empty(rule.Categories);
        Assert.Equal(new[] { 0, 4 }, rule.Qualities);
        Assert.Equal(new[] { "color_red" }, rule.ContextTags);
    }
}
