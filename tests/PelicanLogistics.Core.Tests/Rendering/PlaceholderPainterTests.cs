using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;

namespace PelicanLogistics.Core.Tests.Rendering;

public class PlaceholderPainterTests
{
    [Theory]
    [InlineData(BeltTier.Basic)]
    [InlineData(BeltTier.Fast)]
    [InlineData(BeltTier.Express)]
    public void PaintBelt_Is16x16AndOpaque(BeltTier tier)
    {
        PixelBuffer belt = PlaceholderPainter.PaintBelt(tier);

        Assert.Equal(16, belt.Width);
        Assert.Equal(16, belt.Height);
        Assert.Equal(16 * 16, belt.CountOpaque());
    }

    [Fact]
    public void PaintBelt_TiersHaveOneTwoThreeArrowsAndDifferentGrays()
    {
        int MarkPixels(BeltTier tier) => PlaceholderPainter.PaintBelt(tier).Pixels.Count(p => p == PlaceholderColors.Mark);

        Assert.Equal(9, MarkPixels(BeltTier.Basic));
        Assert.Equal(18, MarkPixels(BeltTier.Fast));
        Assert.Equal(27, MarkPixels(BeltTier.Express));
        Assert.Equal(PlaceholderColors.BeltBasic, PlaceholderPainter.PaintBelt(BeltTier.Basic).Get(3, 3));
        Assert.Equal(PlaceholderColors.BeltExpress, PlaceholderPainter.PaintBelt(BeltTier.Express).Get(3, 5));
    }

    [Fact]
    public void PaintBelt_ArrowPointsNorth()
    {
        PixelBuffer belt = PlaceholderPainter.PaintBelt(BeltTier.Basic);

        // Ponta (1 pixel) em cima, base (5 pixels) embaixo.
        Assert.Equal(PlaceholderColors.Mark, belt.Get(8, 7));
        Assert.NotEqual(PlaceholderColors.Mark, belt.Get(7, 7));
        Assert.Equal(PlaceholderColors.Mark, belt.Get(6, 9));
        Assert.Equal(PlaceholderColors.Mark, belt.Get(10, 9));
    }

    [Theory]
    [InlineData(TransferTier.Single, 0)]
    [InlineData(TransferTier.Stack, 8)]
    [InlineData(TransferTier.Configurable, 12)]
    public void PaintExtractorAndInserter_HaveColorAndDots(TransferTier tier, int markPixels)
    {
        PixelBuffer extractor = PlaceholderPainter.PaintExtractor(tier);
        PixelBuffer inserter = PlaceholderPainter.PaintInserter(tier);

        Assert.Equal(16, extractor.Width);
        Assert.Equal(16, extractor.Height);
        Assert.Contains(PlaceholderColors.Extractor, extractor.Pixels);
        Assert.Contains(PlaceholderColors.Inserter, inserter.Pixels);
        Assert.True(extractor.Get(0, 15).IsTransparent || extractor.Get(15, 0).IsTransparent);
        Assert.True(extractor.Get(15, 0).IsTransparent);
        Assert.Equal(markPixels, extractor.Pixels.Count(p => p == PlaceholderColors.Mark));
        Assert.Equal(markPixels, inserter.Pixels.Count(p => p == PlaceholderColors.Mark));
    }

    [Fact]
    public void PaintExtractor_TrianglePointsNorth()
    {
        PixelBuffer extractor = PlaceholderPainter.PaintExtractor(TransferTier.Single);

        int RowWidth(int y) => Enumerable.Range(0, 16).Count(x => extractor.Get(x, y) == PlaceholderColors.Extractor);

        Assert.True(RowWidth(4) < RowWidth(12));
    }

    [Fact]
    public void PaintIcon_Is16x32_WithSpriteInBottomHalf()
    {
        PixelBuffer sprite = PlaceholderPainter.PaintBelt(BeltTier.Basic);

        PixelBuffer icon = PlaceholderPainter.PaintIcon(sprite);

        Assert.Equal(16, icon.Width);
        Assert.Equal(32, icon.Height);
        Assert.Equal(sprite.CountOpaque(), icon.CountOpaque());
        Assert.True(icon.Get(5, 5).IsTransparent);
        Assert.Equal(sprite.Get(5, 5), icon.Get(5, 21));
    }

    [Fact]
    public void PaintGhostArrow_HasOpaqueAndTransparentPixels()
    {
        PixelBuffer arrow = PlaceholderPainter.PaintGhostArrow();

        Assert.Equal(16, arrow.Width);
        Assert.InRange(arrow.CountOpaque(), 1, (16 * 16) - 1);
        Assert.False(arrow.Get(8, 2).IsTransparent);
        Assert.True(arrow.Get(0, 0).IsTransparent);
    }
}
