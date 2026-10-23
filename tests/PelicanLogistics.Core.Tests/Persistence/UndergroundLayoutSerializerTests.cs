using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Persistence;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Persistence;

public class UndergroundLayoutSerializerTests
{
    [Fact]
    public void RoundTrip_PositionsAndDirections_DirectionWrittenByName()
    {
        var g = new GridBuilder()
            .Segment(3, 1, Direction.South)
            .Segment(0, 0, Direction.East)
            .Segment(-2, 7, Direction.West);

        string json = UndergroundLayoutSerializer.Serialize(g.Grid);
        IReadOnlyList<(TilePos Position, Direction Facing)> back = UndergroundLayoutSerializer.Deserialize(json);

        Assert.Contains("\"South\"", json);
        Assert.Equal(
            new[] { (new TilePos(0, 0), Direction.East), (new TilePos(3, 1), Direction.South), (new TilePos(-2, 7), Direction.West) },
            back);
    }

    [Fact]
    public void Serialize_EmptyGrid_IsAnEmptyList()
    {
        string json = UndergroundLayoutSerializer.Serialize(new GridBuilder().Belt(0, 0, Direction.East).Grid);

        Assert.Equal("[]", json);
        Assert.Empty(UndergroundLayoutSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ isto não é json")]
    [InlineData("{\"X\":1}")]
    [InlineData("[1, 2]")]
    public void Deserialize_MissingOrCorrupted_GivesAnEmptyList(string? json)
    {
        Assert.Empty(UndergroundLayoutSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_UnknownDirectionAndRepeatedPosition_AreDropped()
    {
        IReadOnlyList<(TilePos Position, Direction Facing)> result = UndergroundLayoutSerializer.Deserialize(
            "[{\"X\":0,\"Y\":0,\"Facing\":\"East\"},{\"X\":1,\"Y\":0,\"Facing\":\"Up\"},{\"X\":2,\"Y\":0,\"Facing\":\"7\"},"
            + "{\"X\":3,\"Y\":0},null,{\"X\":0,\"Y\":0,\"Facing\":\"North\"},{\"X\":4,\"Y\":0,\"Facing\":\"North\"}]");

        Assert.Equal(new[] { (new TilePos(0, 0), Direction.East), (new TilePos(4, 0), Direction.North) }, result);
    }
}
