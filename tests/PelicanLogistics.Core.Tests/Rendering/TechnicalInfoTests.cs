using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;

namespace PelicanLogistics.Core.Tests.Rendering;

public class TechnicalInfoTests
{
    [Theory]
    [InlineData(BeltTier.Basic, 1.0)]
    [InlineData(BeltTier.Fast, 2.0)]
    [InlineData(BeltTier.Express, 4.0)]
    public void Belts_OneTwoFourTilesPerSecond(BeltTier tier, double expected)
    {
        Assert.Equal(expected, TechnicalInfo.BeltTilesPerSecond(tier), 6);
    }

    [Fact]
    public void Underground_TwoTilesPerSecond_FilterAndSplitterFour_InserterHalfASecond()
    {
        Assert.Equal(2.0, TechnicalInfo.UndergroundTilesPerSecond, 6);
        Assert.Equal(4.0, TechnicalInfo.FilterTilesPerSecond, 6);
        Assert.Equal(4.0, TechnicalInfo.SplitterTilesPerSecond, 6);
        Assert.Equal(0.5, TechnicalInfo.InserterCycleSeconds, 6);
    }
}
