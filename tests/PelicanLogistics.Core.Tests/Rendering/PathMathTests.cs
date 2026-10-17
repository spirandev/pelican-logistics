using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Tests.Rendering;

public class PathMathTests
{
    private const float Tolerance = 0.0001f;

    [Theory]
    [InlineData(0, 0f, 0.5f)]
    [InlineData(Balance.End / 2, 0.5f, 0.5f)]
    [InlineData(Balance.End, 1f, 0.5f)]
    public void Straight_WestToEast(int progress, float x, float y)
    {
        (float px, float py) = PathMath.GetPosition(Direction.West, Direction.East, progress);

        Assert.Equal(x, px, Tolerance);
        Assert.Equal(y, py, Tolerance);
    }

    [Fact]
    public void Straight_SouthToNorth_GoesUp()
    {
        Assert.Equal((0.5f, 1f), PathMath.GetPosition(Direction.South, Direction.North, 0));
        Assert.Equal((0.5f, 0f), PathMath.GetPosition(Direction.South, Direction.North, Balance.End));
    }

    [Fact]
    public void Curve_WestToNorth_FollowsQuarterCircleAroundCorner()
    {
        (float sx, float sy) = PathMath.GetPosition(Direction.West, Direction.North, 0);
        (float ex, float ey) = PathMath.GetPosition(Direction.West, Direction.North, Balance.End);
        (float mx, float my) = PathMath.GetPosition(Direction.West, Direction.North, Balance.End / 2);

        Assert.Equal(0f, sx, Tolerance);
        Assert.Equal(0.5f, sy, Tolerance);
        Assert.Equal(0.5f, ex, Tolerance);
        Assert.Equal(0f, ey, Tolerance);
        Assert.Equal(0.5f, MathF.Sqrt((mx * mx) + (my * my)), Tolerance);
        Assert.True(mx > 0 && my > 0);
    }

    [Theory]
    [InlineData(Direction.North, Direction.East, 1f, 0f)]
    [InlineData(Direction.East, Direction.South, 1f, 1f)]
    [InlineData(Direction.South, Direction.West, 0f, 1f)]
    [InlineData(Direction.West, Direction.South, 0f, 1f)]
    public void Curve_StaysAtRadiusFromSharedCorner(Direction entry, Direction exit, float cornerX, float cornerY)
    {
        for (int progress = 0; progress <= Balance.End; progress += 60)
        {
            (float x, float y) = PathMath.GetPosition(entry, exit, progress);
            float dx = x - cornerX;
            float dy = y - cornerY;
            Assert.Equal(0.5f, MathF.Sqrt((dx * dx) + (dy * dy)), Tolerance);
        }

        Assert.Equal(PathMath.GetEdgeMidpoint(entry), PathMath.GetPosition(entry, exit, 0));
        (float endX, float endY) = PathMath.GetPosition(entry, exit, Balance.End);
        Assert.Equal(PathMath.GetEdgeMidpoint(exit).X, endX, Tolerance);
        Assert.Equal(PathMath.GetEdgeMidpoint(exit).Y, endY, Tolerance);
    }

    [Fact]
    public void SameSide_GoesFromCenterToSide()
    {
        Assert.Equal((0.5f, 0.5f), PathMath.GetPosition(Direction.East, Direction.East, 0));
        Assert.Equal((1f, 0.5f), PathMath.GetPosition(Direction.East, Direction.East, Balance.End));
    }

    [Fact]
    public void Progress_IsClamped()
    {
        Assert.Equal((1f, 0.5f), PathMath.GetPosition(Direction.West, Direction.East, Balance.End * 2));
        Assert.Equal((0f, 0.5f), PathMath.GetPosition(Direction.West, Direction.East, -10));
    }
}
