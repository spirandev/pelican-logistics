using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Nodes;

public class ExtractorNodeTests
{
    [Fact]
    public void Extractor_TakesOneItemEvery30Ticks()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        // A esteira é esvaziada depois de cada tick, então só o cooldown limita o Extrator.
        var taken = new List<int>();
        for (int tick = 1; tick <= 120; tick++)
        {
            g.Run(1);
            var item = g.BeltAt(2, 0).Slot.Clear();
            if (item != null)
            {
                Assert.Equal(1, item.Count);
                taken.Add(tick);
            }
        }

        Assert.Equal(new[] { 1, 31, 61, 91 }, taken);
        Assert.Equal(1, chest.CountTotal());
    }

    [Fact]
    public void Extractor_PutsItemOnBelt_WithEntryFromBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        TransitSlot slot = g.BeltAt(2, 0).Slot;
        Assert.Equal("wood", slot.Item!.QualifiedItemId);
        Assert.Equal(1, slot.Item.Count);
        Assert.Equal(Direction.West, slot.EntrySide);
        Assert.Equal(4, chest.CountTotal());
    }

    [Fact]
    public void Extractor_DoesNotTake_WhenFrontBeltIsOccupied()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Item(2, 0, new FakeItem("stone", 1));

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.Equal("stone", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
    }

    [Fact]
    public void Extractor_DoesNotTake_WhenFrontBeltDoesNotReceiveFromThatSide()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.North)  // de lado para o Extrator
            .Belt(2, 1, Direction.North); // atrás dela: ela fica reta e ignora a lateral

        g.Run(200);

        Assert.Equal(Direction.South, g.BeltAt(2, 0).InputSide);
        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Extractor_DoesNotTake_WhenFrontBeltFacesIt()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.West);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
    }

    [Fact]
    public void Extractor_TurnsSideBeltWithNothingBehindIntoCurve()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.North);

        g.Run(1);

        Assert.Equal(Direction.West, g.BeltAt(2, 0).InputSide);
        Assert.Equal(4, chest.CountTotal());
    }

    [Fact]
    public void Extractor_DoesNotTake_WithoutChestBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(1, 1, chest) // ao lado, não atrás
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }

    [Fact]
    public void Extractor_DoesNothing_WithEmptyChest()
    {
        var chest = new FakeChest(36);
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(200);

        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
        Assert.Equal(0, g.NodeAt<ExtractorNode>(1, 0).Cooldown);
    }

    [Fact]
    public void Extractor_DoesNotTake_WithoutBeltInFront()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 5));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Inserter(2, 0, Direction.East);

        g.Run(200);

        Assert.Equal(5, chest.CountTotal());
    }

    [Fact]
    public void Extractor_TakesItemsInInventoryOrder()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 1), new FakeItem("stone", 1));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal("wood", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal(new[] { "stone" }, chest.Items.Select(i => i.QualifiedItemId));
    }

    [Fact]
    public void Extractor_StackTier_TakesWholeStack()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 50), new FakeItem("wood", 20));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East, TransferTier.Stack)
            .Belt(2, 0, Direction.East);

        g.Run(1);

        Assert.Equal(50, g.BeltAt(2, 0).Slot.Item!.Count);
        Assert.Equal(20, chest.CountTotal());
    }

    [Fact]
    public void Extractor_ConfigurableTier_TakesConfiguredAmountAcrossSlots()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 3), new FakeItem("stone", 9), new FakeItem("wood", 10));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East, TransferTier.Configurable)
            .Belt(2, 0, Direction.East);
        g.NodeAt<ExtractorNode>(1, 0).ConfiguredAmount = 7;

        g.Run(1);

        Assert.Equal("wood", g.BeltAt(2, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal(7, g.BeltAt(2, 0).Slot.Item!.Count);
        Assert.Equal(6, chest.CountById("wood"));
        Assert.Equal(9, chest.CountById("stone"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5000, Balance.MaxConfigurableAmount)]
    public void Extractor_ConfiguredAmount_IsClamped(int value, int expected)
    {
        var extractor = new ExtractorNode(new TilePos(0, 0), Direction.East, TransferTier.Configurable)
        {
            ConfiguredAmount = value,
        };

        Assert.Equal(expected, extractor.ConfiguredAmount);
    }

    [Fact]
    public void Extractor_ProvidesOnlyToTileInFront()
    {
        var extractor = new ExtractorNode(new TilePos(0, 0), Direction.South);

        Assert.True(extractor.ProvidesTo(new TilePos(0, 1)));
        Assert.False(extractor.ProvidesTo(new TilePos(0, -1)));
        Assert.Equal(new TilePos(0, -1), extractor.SourcePosition);
    }
}
