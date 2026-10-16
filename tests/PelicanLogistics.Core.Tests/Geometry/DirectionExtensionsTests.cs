using PelicanLogistics.Core.Geometry;

namespace PelicanLogistics.Core.Tests.Geometry;

public class DirectionExtensionsTests
{
    [Theory]
    [InlineData(Direction.North, Direction.South)]
    [InlineData(Direction.East, Direction.West)]
    [InlineData(Direction.South, Direction.North)]
    [InlineData(Direction.West, Direction.East)]
    public void Opposite_ReturnsOppositeDirection(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.Opposite());
    }

    [Theory]
    [InlineData(Direction.North, Direction.East)]
    [InlineData(Direction.East, Direction.South)]
    [InlineData(Direction.South, Direction.West)]
    [InlineData(Direction.West, Direction.North)]
    public void Clockwise_ReturnsNextDirectionClockwise(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.Clockwise());
    }

    [Theory]
    [InlineData(Direction.North, Direction.West)]
    [InlineData(Direction.East, Direction.North)]
    [InlineData(Direction.South, Direction.East)]
    [InlineData(Direction.West, Direction.South)]
    public void CounterClockwise_ReturnsNextDirectionCounterClockwise(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.CounterClockwise());
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void Clockwise_AppliedFourTimes_ReturnsToStart(Direction direction)
    {
        Assert.Equal(direction, direction.Clockwise().Clockwise().Clockwise().Clockwise());
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void CounterClockwise_UndoesClockwise(Direction direction)
    {
        Assert.Equal(direction, direction.Clockwise().CounterClockwise());
    }

    [Fact]
    public void ClockwiseFrom_North_ReturnsNorthEastSouthWest()
    {
        Assert.Equal(
            new[] { Direction.North, Direction.East, Direction.South, Direction.West },
            DirectionExtensions.ClockwiseFrom(Direction.North));
    }

    [Fact]
    public void ClockwiseFrom_West_ReturnsWestNorthEastSouth()
    {
        Assert.Equal(
            new[] { Direction.West, Direction.North, Direction.East, Direction.South },
            DirectionExtensions.ClockwiseFrom(Direction.West));
    }

    [Fact]
    public void All_ReturnsNorthEastSouthWest()
    {
        Assert.Equal(
            new[] { Direction.North, Direction.East, Direction.South, Direction.West },
            DirectionExtensions.All);
    }

    [Theory]
    [InlineData(Direction.North, 0, -1)]
    [InlineData(Direction.East, 1, 0)]
    [InlineData(Direction.South, 0, 1)]
    [InlineData(Direction.West, -1, 0)]
    public void ToOffset_ReturnsTileOffset_WithYGrowingDown(Direction direction, int expectedDx, int expectedDy)
    {
        Assert.Equal((expectedDx, expectedDy), direction.ToOffset());
    }

    [Fact]
    public void ToOffset_Throws_ForInvalidDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Direction)4).ToOffset());
    }
}
