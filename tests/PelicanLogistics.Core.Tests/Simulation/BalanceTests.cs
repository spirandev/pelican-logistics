using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Simulation;

public class BalanceTests
{
    [Theory]
    [InlineData(BeltTier.Basic, Balance.BeltSpeedBasic)]
    [InlineData(BeltTier.Fast, Balance.BeltSpeedFast)]
    [InlineData(BeltTier.Express, Balance.BeltSpeedExpress)]
    public void SpeedOf_ReturnsSpeedOfTier(BeltTier tier, int expected)
    {
        Assert.Equal(expected, Balance.SpeedOf(tier));
    }

    [Fact]
    public void BasicBelt_CrossesOneTilePerSecond()
    {
        Assert.Equal(Balance.TicksPerSecond, Balance.ProgressPerTile / Balance.BeltSpeedBasic);
        Assert.Equal(Balance.ProgressPerTile, Balance.End);
    }
}
