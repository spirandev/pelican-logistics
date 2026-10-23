using PelicanLogistics.Core.Regulators;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Regulators;

public class RegulatorRuleSerializerTests
{
    [Fact]
    public void RoundTrip_General()
    {
        var rule = new RegulatorRule { Mode = RegulatorMode.General, GeneralLimit = 50 };

        RegulatorRule back = RegulatorRuleSerializer.Deserialize(RegulatorRuleSerializer.Serialize(rule));

        Assert.Equal(RegulatorMode.General, back.Mode);
        Assert.Equal(50, back.GeneralLimit);
        Assert.Empty(back.PerItemLimits);
    }

    [Fact]
    public void RoundTrip_PerItem_ModeIsWrittenByName()
    {
        var rule = new RegulatorRule
        {
            Mode = RegulatorMode.PerItem,
            GeneralLimit = 10,
            PerItemLimits = { ["(O)388"] = 50, ["(O)390"] = 0 },
        };

        string json = RegulatorRuleSerializer.Serialize(rule);
        RegulatorRule back = RegulatorRuleSerializer.Deserialize(json);

        Assert.Contains("\"PerItem\"", json);
        Assert.Equal(RegulatorMode.PerItem, back.Mode);
        Assert.Equal(10, back.GeneralLimit);
        Assert.Equal(new Dictionary<string, int> { ["(O)388"] = 50, ["(O)390"] = 0 }, back.PerItemLimits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ isto não é json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"Mode\":\"Turbo\"}")]
    [InlineData("{\"Mode\":7}")]
    [InlineData("{\"GeneralLimit\":\"muito\"}")]
    public void Deserialize_MissingOrCorrupted_GivesDefaultRule(string? json)
    {
        RegulatorRule rule = RegulatorRuleSerializer.Deserialize(json);

        Assert.Equal(RegulatorMode.General, rule.Mode);
        Assert.Equal(Balance.RegulatorDefaultLimit, rule.GeneralLimit);
        Assert.Empty(rule.PerItemLimits);
    }

    [Fact]
    public void Deserialize_LimitsOutOfRangeAreClamped_BlankIdsDropped_NullListBecomesEmpty()
    {
        RegulatorRule rule = RegulatorRuleSerializer.Deserialize(
            "{\"Mode\":\"PerItem\",\"GeneralLimit\":-5,\"PerItemLimits\":{\"(O)388\":123456,\" \":5,\" (O)390 \":-1}}");

        Assert.Equal(0, rule.GeneralLimit);
        Assert.Equal(new Dictionary<string, int> { ["(O)388"] = Balance.MaxRegulatorLimit, ["(O)390"] = 0 }, rule.PerItemLimits);

        RegulatorRule nullList = RegulatorRuleSerializer.Deserialize("{\"Mode\":\"PerItem\",\"PerItemLimits\":null}");
        Assert.Empty(nullList.PerItemLimits);
    }
}
