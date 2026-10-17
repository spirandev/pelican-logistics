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

    [Fact]
    public void Inserter_TravelPlusReturn_IsOneOperationEveryHalfSecond()
    {
        int travel = Balance.End / Balance.InserterSpeed;

        Assert.Equal(0, Balance.End % Balance.InserterSpeed);
        Assert.Equal(15, travel);
        Assert.Equal(15, Balance.InserterReturnTicks);
        Assert.Equal(Balance.TicksPerSecond / 2, travel + Balance.InserterReturnTicks);
    }
}
