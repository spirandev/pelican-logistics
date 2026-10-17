using PelicanLogistics.Core.Geometry;

namespace PelicanLogistics.Core.Tests.Geometry;

public class TilePosTests
{
    [Theory]
    [InlineData(Direction.North, 5, 4)]
    [InlineData(Direction.East, 6, 5)]
    [InlineData(Direction.South, 5, 6)]
    [InlineData(Direction.West, 4, 5)]
    public void Offset_ReturnsNeighborInDirection(Direction direction, int expectedX, int expectedY)
    {
        var pos = new TilePos(5, 5);

        Assert.Equal(new TilePos(expectedX, expectedY), pos.Offset(direction));
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void DirectionTo_ReturnsDirection_ForEachAdjacentNeighbor(Direction direction)
    {
        var pos = new TilePos(5, 5);

        Assert.Equal(direction, pos.DirectionTo(pos.Offset(direction)));
    }

    [Fact]
    public void DirectionTo_ReturnsNull_ForSamePosition()
    {
        var pos = new TilePos(5, 5);

        Assert.Null(pos.DirectionTo(pos));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(6, 4)]
    [InlineData(4, 6)]
    [InlineData(6, 6)]
    public void DirectionTo_ReturnsNull_ForDiagonals(int x, int y)
    {
        var pos = new TilePos(5, 5);

        Assert.Null(pos.DirectionTo(new TilePos(x, y)));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(7, 5)]
    [InlineData(5, 15)]
    [InlineData(-5, 5)]
    public void DirectionTo_ReturnsNull_ForDistantTiles(int x, int y)
    {
        var pos = new TilePos(5, 5);

        Assert.Null(pos.DirectionTo(new TilePos(x, y)));
    }

    [Fact]
    public void ToString_FormatsCoordinates()
    {
        Assert.Equal("(3, -2)", new TilePos(3, -2).ToString());
    }
}
