using PelicanLogistics.Core.Regulators;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Regulators;

public class RegulatorRuleTests
{
    private const string Wood = "(O)388";
    private const string Stone = "(O)390";
    private const string Clay = "(O)330";

    [Fact]
    public void Default_IsGeneralWithLimit100()
    {
        var rule = new RegulatorRule();

        Assert.Equal(RegulatorMode.General, rule.Mode);
        Assert.Equal(100, rule.GeneralLimit);
        Assert.Empty(rule.PerItemLimits);
    }

    [Fact]
    public void General_LimitMinusTotalInChest_NeverNegative()
    {
        var rule = new RegulatorRule { GeneralLimit = 50 };

        Assert.Equal(5, rule.AllowedAmount(new FakeItem(Wood, 20), new FakeChest(36, new FakeItem(Stone, 30), new FakeItem(Clay, 15))));
        Assert.Equal(50, rule.AllowedAmount(new FakeItem(Wood, 20), new FakeChest()));
        Assert.Equal(0, rule.AllowedAmount(new FakeItem(Wood, 20), new FakeChest(36, new FakeItem(Stone, 80))));
    }

    [Fact]
    public void PerItem_LimitMinusSameIdInChest_AnyQuality_UnlistedIsUnlimited()
    {
        var rule = new RegulatorRule
        {
            Mode = RegulatorMode.PerItem,
            PerItemLimits = { [Wood] = 50, [Stone] = 100 },
        };
        var chest = new FakeChest(36, new FakeItem(Wood, 30), new FakeItem(Wood, 15, quality: 2), new FakeItem(Stone, 120));

        Assert.Equal(5, rule.AllowedAmount(new FakeItem(Wood, 10), chest));
        Assert.Equal(0, rule.AllowedAmount(new FakeItem(Stone, 10), chest));
        Assert.Equal(int.MaxValue, rule.AllowedAmount(new FakeItem(Clay, 10), chest));
    }
}
