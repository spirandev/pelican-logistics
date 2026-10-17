using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Rendering;

namespace PelicanLogistics.Core.Tests.Rendering;

public class PixelBufferTests
{
    private static readonly Rgba Red = new(255, 0, 0);

    [Fact]
    public void Set_OutsideBounds_IsIgnored()
    {
        var buffer = new PixelBuffer(4, 4);

        buffer.Set(-1, 0, Red);
        buffer.Set(4, 0, Red);
        buffer.Set(0, 4, Red);

        Assert.Equal(0, buffer.CountOpaque());
        Assert.True(buffer.Get(10, 10).IsTransparent);
    }

    [Fact]
    public void FillRect_And_DrawRect()
    {
        var filled = new PixelBuffer(8, 8);
        filled.FillRect(1, 1, 3, 2, Red);
        Assert.Equal(6, filled.CountOpaque());

        var outline = new PixelBuffer(8, 8);
        outline.DrawRect(0, 0, 4, 4, Red);
        Assert.Equal(12, outline.CountOpaque());
        Assert.True(outline.Get(1, 1).IsTransparent);
    }

    [Fact]
    public void FillDiamond_And_FillCircle()
    {
        var diamond = new PixelBuffer(9, 9);
        diamond.FillDiamond(4, 4, 2, Red);
        Assert.Equal(13, diamond.CountOpaque());
        Assert.True(diamond.Get(2, 2).IsTransparent);

        var circle = new PixelBuffer(16, 16);
        circle.FillCircle(8, 8, 4, Red);
        Assert.False(circle.Get(8, 8).IsTransparent);
        Assert.True(circle.Get(0, 0).IsTransparent);
        Assert.InRange(circle.CountOpaque(), 40, 60);
    }

    [Theory]
    [InlineData(Direction.North, 5, 4)]
    [InlineData(Direction.East, 6, 5)]
    [InlineData(Direction.South, 5, 6)]
    [InlineData(Direction.West, 4, 5)]
    public void DrawArrow_TipPointsInDirection(Direction dir, int tipX, int tipY)
    {
        var buffer = new PixelBuffer(11, 11);

        buffer.DrawArrow(5, 5, dir, Red);

        Assert.Equal(9, buffer.CountOpaque());
        Assert.Equal(Red, buffer.Get(tipX, tipY));
        // Ao lado da ponta não há pixel (a seta afina na ponta).
        (int dx, int dy) = dir.Clockwise().ToOffset();
        Assert.True(buffer.Get(tipX + dx, tipY + dy).IsTransparent);
    }

    [Fact]
    public void Blit_SkipsTransparentPixels()
    {
        var dest = new PixelBuffer(4, 4);
        dest.FillRect(0, 0, 4, 4, Red);
        var src = new PixelBuffer(2, 2);
        var blue = new Rgba(0, 0, 255);
        src.Set(0, 0, blue);

        dest.Blit(src, 1, 1);

        Assert.Equal(blue, dest.Get(1, 1));
        Assert.Equal(Red, dest.Get(2, 2));
    }
}
