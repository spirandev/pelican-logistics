using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
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
        IReadOnlyList<UndergroundBlockLayout> back = UndergroundLayoutSerializer.Deserialize(json);

        Assert.Contains("\"South\"", json);
        Assert.Equal(
            new[] { (new TilePos(0, 0), Direction.East), (new TilePos(3, 1), Direction.South), (new TilePos(-2, 7), Direction.West) },
            back.Select(b => (b.Position, b.Facing)));
        Assert.All(back, b => Assert.Equal(NodeKind.UndergroundSegment, b.Kind));
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
        IReadOnlyList<UndergroundBlockLayout> result = UndergroundLayoutSerializer.Deserialize(
            "[{\"X\":0,\"Y\":0,\"Facing\":\"East\"},{\"X\":1,\"Y\":0,\"Facing\":\"Up\"},{\"X\":2,\"Y\":0,\"Facing\":\"7\"},"
            + "{\"X\":3,\"Y\":0},null,{\"X\":0,\"Y\":0,\"Facing\":\"North\"},{\"X\":4,\"Y\":0,\"Facing\":\"North\"}]");

        Assert.Equal(new[] { (new TilePos(0, 0), Direction.East), (new TilePos(4, 0), Direction.North) }, result.Select(b => (b.Position, b.Facing)));
    }

    // Filtros e Divisores Subterrâneos (Fase 6c).

    [Fact]
    public void RoundTrip_ThreeKindsAndTheFilterRules()
    {
        var complete = new CompleteFilterRule { Mode = FilterMode.Blacklist, ItemIds = { "(O)388" }, Categories = { -2 }, Qualities = { 2 } };
        var g = new GridBuilder()
            .Segment(0, 0, Direction.East)
            .UndergroundFilter(1, 0, Direction.South, "(O)390")
            .UndergroundFilter(2, 0, Direction.West, complete)
            .UndergroundSplitter(3, 0);

        string json = UndergroundLayoutSerializer.Serialize(g.Grid);
        IReadOnlyList<UndergroundBlockLayout> back = UndergroundLayoutSerializer.Deserialize(json);

        Assert.Equal(
            new[]
            {
                (new TilePos(0, 0), NodeKind.UndergroundSegment, Direction.East),
                (new TilePos(1, 0), NodeKind.Filter, Direction.South),
                (new TilePos(2, 0), NodeKind.Filter, Direction.West),
                (new TilePos(3, 0), NodeKind.Splitter, Direction.North),
            },
            back.Select(b => (b.Position, b.Kind, b.Facing)));
        Assert.Null(back[0].Rule);
        Assert.Equal("(O)390", Assert.IsType<CommonFilterRule>(back[1].Rule).QualifiedItemId);
        var rule = Assert.IsType<CompleteFilterRule>(back[2].Rule);
        Assert.Equal(FilterMode.Blacklist, rule.Mode);
        Assert.Equal(new[] { "(O)388" }, rule.ItemIds);
        Assert.Equal(new[] { -2 }, rule.Categories);
        Assert.Equal(new[] { 2 }, rule.Qualities);
        Assert.Null(back[3].Rule);

        // CreateNode recria os nós com o mesmo tipo e a mesma regra.
        Assert.IsType<UndergroundSegmentNode>(back[0].CreateNode());
        Assert.True(Assert.IsType<FilterNode>(back[2].CreateNode()).IsComplete);
        Assert.IsType<SplitterNode>(back[3].CreateNode());
    }

    [Fact]
    public void Deserialize_EntryWithoutKind_IsASegment_UnknownKindIsDropped_CorruptedRuleIsEmpty()
    {
        IReadOnlyList<UndergroundBlockLayout> result = UndergroundLayoutSerializer.Deserialize(
            "[{\"X\":0,\"Y\":0,\"Facing\":\"East\"},"
            + "{\"Kind\":\"Belt\",\"X\":1,\"Y\":0,\"Facing\":\"East\"},"
            + "{\"Kind\":\"Inserter\",\"X\":2,\"Y\":0,\"Facing\":\"East\"},"
            + "{\"Kind\":\"Mystery\",\"X\":3,\"Y\":0,\"Facing\":\"East\"},"
            + "{\"Kind\":\"Filter\",\"X\":4,\"Y\":0,\"Facing\":\"North\",\"Rule\":\"{ corrompida\"},"
            + "{\"Kind\":\"Filter\",\"X\":5,\"Y\":0,\"Facing\":\"North\",\"Complete\":true,\"Rule\":\"[1]\"},"
            + "{\"Kind\":\"Filter\",\"X\":6,\"Y\":0,\"Facing\":\"Up\"},"
            + "{\"Kind\":\"Splitter\",\"X\":7,\"Y\":0}]");

        Assert.Equal(
            new[]
            {
                (new TilePos(0, 0), NodeKind.UndergroundSegment),
                (new TilePos(4, 0), NodeKind.Filter),
                (new TilePos(5, 0), NodeKind.Filter),
                (new TilePos(7, 0), NodeKind.Splitter),
            },
            result.Select(b => (b.Position, b.Kind)));
        Assert.Null(Assert.IsType<CommonFilterRule>(result[1].Rule).QualifiedItemId);
        var complete = Assert.IsType<CompleteFilterRule>(result[2].Rule);
        Assert.Empty(complete.ItemIds);
        Assert.Equal(FilterMode.Whitelist, complete.Mode);
    }
}
