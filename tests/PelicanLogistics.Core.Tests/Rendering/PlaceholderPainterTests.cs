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
    public void PaintInserter_IsOpaqueBlueSquare_WithTierDots(TransferTier tier, int markPixels)
    {
        PixelBuffer inserter = PlaceholderPainter.PaintInserter(tier);

        Assert.Equal(16, inserter.Width);
        Assert.Equal(16, inserter.Height);
        Assert.Equal(16 * 16, inserter.CountOpaque());
        Assert.Equal(PlaceholderColors.Inserter, inserter.Get(8, 12));
        Assert.Equal(markPixels, inserter.Pixels.Count(p => p == PlaceholderColors.Mark));
    }

    [Fact]
    public void PaintInserter_HasSmallTriangleAtNorthEdge()
    {
        PixelBuffer inserter = PlaceholderPainter.PaintInserter(TransferTier.Single);

        // O contorno fica na borda; dentro do quadrado, só o triângulo usa a cor escura, e só no topo.
        int RowWidth(int y) => Enumerable.Range(1, 14).Count(x => inserter.Get(x, y) == PlaceholderColors.Outline);

        Assert.True(RowWidth(2) > 0);
        Assert.True(RowWidth(2) < RowWidth(4));
        Assert.All(Enumerable.Range(7, 8), y => Assert.Equal(0, RowWidth(y)));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaintFilter_IsOpaque_GreenDiamondWithArrowPointingNorth(bool complete)
    {
        PixelBuffer filter = PlaceholderPainter.PaintFilter(complete);
        Rgba green = complete ? PlaceholderColors.FilterComplete : PlaceholderColors.FilterCommon;

        Assert.Equal(16 * 16, filter.CountOpaque());
        Assert.Equal(PlaceholderColors.BeltBasic, filter.Get(2, 2)); // canto fora do losango
        Assert.Equal(green, filter.Get(8, 14));                       // ponta de baixo do losango
        Assert.Equal(green, filter.Get(2, 8));                        // ponta da esquerda
        Assert.Equal(PlaceholderColors.Outline, filter.Get(15, 8));   // o losango não invade o contorno
        Assert.Equal(PlaceholderColors.Mark, filter.Get(8, 3));       // ponta da seta, em cima
        Assert.Equal(PlaceholderColors.Mark, filter.Get(6, 5));
        Assert.NotEqual(PlaceholderColors.Mark, filter.Get(7, 3));
    }

    [Fact]
    public void PaintFilter_CompleteHasADoubleDiamond_CommonDoesNot()
    {
        // O losango de dentro é um contorno escuro a 4 pixels do centro.
        Assert.Equal(PlaceholderColors.Outline, PlaceholderPainter.PaintFilter(true).Get(8, 12));
        Assert.Equal(PlaceholderColors.FilterComplete, PlaceholderPainter.PaintFilter(true).Get(8, 10));
        Assert.Equal(PlaceholderColors.FilterComplete, PlaceholderPainter.PaintFilter(true).Get(8, 13));
        Assert.Equal(PlaceholderColors.FilterCommon, PlaceholderPainter.PaintFilter(false).Get(8, 12));
    }

    [Fact]
    public void PaintSplitter_IsOpaque_PurpleCircleWithFourExits()
    {
        PixelBuffer splitter = PlaceholderPainter.PaintSplitter();

        Assert.Equal(16 * 16, splitter.CountOpaque());
        Assert.Equal(PlaceholderColors.Splitter, splitter.Get(8, 4));
        Assert.Equal(PlaceholderColors.BeltBasic, splitter.Get(2, 2));

        // Uma saída no meio de cada borda, logo dentro do contorno.
        Assert.Equal(PlaceholderColors.Splitter, splitter.Get(7, 1));
        Assert.Equal(PlaceholderColors.Splitter, splitter.Get(14, 7));
        Assert.Equal(PlaceholderColors.Splitter, splitter.Get(8, 14));
        Assert.Equal(PlaceholderColors.Splitter, splitter.Get(1, 8));
    }

    [Theory]
    [InlineData(BeltTier.Basic, 9)]
    [InlineData(BeltTier.Fast, 18)]
    [InlineData(BeltTier.Express, 27)]
    public void PaintBeltCurve_IsOpaque_WithOneTwoThreeArrows(BeltTier tier, int markPixels)
    {
        foreach (bool fromLeft in new[] { true, false })
        {
            PixelBuffer curve = PlaceholderPainter.PaintBeltCurve(tier, fromLeft);

            Assert.Equal(16 * 16, curve.CountOpaque());
            Assert.Equal(markPixels, curve.Pixels.Count(p => p == PlaceholderColors.Mark));
        }
    }

    [Theory]
    [InlineData(true, 1, 14)]
    [InlineData(false, 14, 1)]
    public void PaintBeltCurve_PathGoesFromTheInputSideToTheTop_LikeTheStraightArrowColumn(bool fromLeft, int inputX, int otherX)
    {
        PixelBuffer curve = PlaceholderPainter.PaintBeltCurve(BeltTier.Basic, fromLeft);

        Assert.Equal(PlaceholderColors.Outline, curve.Get(inputX, 8));                 // perna horizontal na borda da entrada
        Assert.Equal(PlaceholderColors.BeltBasic, curve.Get(otherX, 8));               // o outro lado fica livre
        Assert.Equal(PlaceholderColors.Outline, curve.Get(8, 1));                      // perna vertical até a borda de cima
        Assert.Equal(PlaceholderColors.Mark, curve.Get(8, 3));                         // ponta da seta para cima, na coluna da reta
        Assert.Equal(PlaceholderColors.BeltBasic, curve.Get(8, 12));                   // nada embaixo: a traseira não recebe
    }

    [Theory]
    [InlineData(UndergroundRole.Entrance)]
    [InlineData(UndergroundRole.Exit)]
    public void PaintUnderground_IsOpaqueDarkGray_WithWhiteOrBlackCircle_AndArrowPointingNorth(UndergroundRole role)
    {
        PixelBuffer underground = PlaceholderPainter.PaintUnderground(role);
        Rgba circle = role == UndergroundRole.Entrance ? PlaceholderColors.Mark : PlaceholderColors.UndergroundExit;

        Assert.Equal(16 * 16, underground.CountOpaque());
        Assert.Equal(PlaceholderColors.Underground, underground.Get(2, 13));
        Assert.Equal(circle, underground.Get(8, 9));
        Assert.Equal(PlaceholderColors.Outline, underground.Get(8, 4));  // contorno do círculo
        Assert.Equal(PlaceholderColors.Mark, underground.Get(8, 1));     // ponta da seta, em cima
        Assert.Equal(PlaceholderColors.Mark, underground.Get(6, 3));
    }

    [Fact]
    public void PaintUnderground_EntranceAndExitDiffer()
    {
        Assert.NotEqual(
            PlaceholderPainter.PaintUnderground(UndergroundRole.Entrance).Get(8, 9),
            PlaceholderPainter.PaintUnderground(UndergroundRole.Exit).Get(8, 9));
    }

    [Fact]
    public void PaintBridge_IsABrownHorizontalBand_TransparentAboveAndBelow()
    {
        PixelBuffer bridge = PlaceholderPainter.PaintBridge();

        Assert.Equal(16 * 8, bridge.CountOpaque());
        Assert.All(Enumerable.Range(0, 16), x => Assert.False(bridge.Get(x, 7).IsTransparent));
        Assert.True(bridge.Get(8, 2).IsTransparent);
        Assert.True(bridge.Get(8, 13).IsTransparent);
        Assert.Equal(PlaceholderColors.Bridge, bridge.Get(1, 7));
    }

    [Fact]
    public void PaintRegulator_IsASmallYellowSquareInTheCorner()
    {
        PixelBuffer regulator = PlaceholderPainter.PaintRegulator();

        Assert.Equal(36, regulator.CountOpaque());
        Assert.Equal(PlaceholderColors.Regulator, regulator.Get(11, 11));
        Assert.True(regulator.Get(8, 8).IsTransparent);
    }

    [Theory]
    [InlineData(AccessoryKind.Bridge)]
    [InlineData(AccessoryKind.Regulator)]
    public void PaintAccessoryIcon_Is16x16_WithSomeOpaquePixels(AccessoryKind kind)
    {
        PixelBuffer icon = PlaceholderPainter.PaintAccessoryIcon(kind);

        Assert.Equal(16, icon.Width);
        Assert.Equal(16, icon.Height);
        Assert.InRange(icon.CountOpaque(), 1, (16 * 16) - 1);
    }

    [Fact]
    public void PaintAccessoryIcon_None_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlaceholderPainter.PaintAccessoryIcon(AccessoryKind.None));
    }
}
