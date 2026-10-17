using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class FullFlowTests
{
    [Fact]
    public void ChestToChest_WithCurve_MovesEverythingAndConservesItems()
    {
        // Baú A → Extrator → 2 esteiras para o leste → curva para o sul → esteira → Inseridor → baú B.
        var chestA = new FakeChest(36, new FakeItem("wood", 10), new FakeItem("stone", 3));
        var chestB = new FakeChest();
        var g = new GridBuilder()
            .Chest(0, 0, chestA)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(4, 0, Direction.South)
            .Belt(4, 1, Direction.South)
            .Inserter(4, 2, Direction.South)
            .Chest(4, 3, chestB);

        int total = chestA.CountTotal();
        for (int tick = 0; tick < 3000; tick++)
        {
            g.Run(1);
            Assert.Equal(total, chestA.CountTotal() + g.CountInTransit() + chestB.CountTotal());
        }

        Assert.True(g.BeltAt(4, 0).IsCurve);
        Assert.Equal(0, chestA.CountTotal());
        Assert.Equal(0, g.CountInTransit());
        Assert.Equal(10, chestB.CountById("wood"));
        Assert.Equal(3, chestB.CountById("stone"));
        Assert.Equal(13, chestB.CountTotal());
    }

    [Fact]
    public void WithoutInserter_ItemsAccumulate_SameItemsMerge_OtherItemWaitsBehind()
    {
        var chest = new FakeChest(36, new FakeItem("wood", 4), new FakeItem("stone", 1));
        var g = new GridBuilder()
            .Chest(0, 0, chest)
            .Extractor(1, 0, Direction.East)
            .Belt(2, 0, Direction.East)
            .Belt(3, 0, Direction.East)
            .Belt(4, 0, Direction.East);

        g.Run(2000);

        Assert.Equal(0, chest.CountTotal());
        Assert.Equal("wood", g.BeltAt(4, 0).Slot.Item!.QualifiedItemId);
        Assert.Equal(4, g.BeltAt(4, 0).Slot.Item!.Count);
        Assert.Equal("stone", g.BeltAt(3, 0).Slot.Item!.QualifiedItemId);
        Assert.True(g.BeltAt(2, 0).Slot.IsEmpty);
    }
}
