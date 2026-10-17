using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Simulation;

public class LogisticsWorldTests
{
    [Fact]
    public void GetOrCreate_ReturnsSameGridForSameKey()
    {
        var world = new LogisticsWorld();

        LogisticsGrid farm = world.GetOrCreate("Farm");

        Assert.Same(farm, world.GetOrCreate("Farm"));
        Assert.Same(farm, world.Get("Farm"));
        Assert.Null(world.Get("Town"));
        Assert.NotSame(farm, world.GetOrCreate("Town"));
    }

    [Fact]
    public void Tick_SkipsLocationsWithoutProvider_AndGridsWithoutNodes()
    {
        var world = new LogisticsWorld();
        var farmBelt = new BeltNode(new TilePos(0, 0), Direction.East);
        var caveBelt = new BeltNode(new TilePos(0, 0), Direction.East);
        world.GetOrCreate("Farm").Add(farmBelt);
        world.GetOrCreate("Cave").Add(caveBelt);
        world.GetOrCreate("Empty");
        farmBelt.Slot.Put(new FakeItem("wood", 1), Direction.West);
        caveBelt.Slot.Put(new FakeItem("wood", 1), Direction.West);

        var asked = new List<string>();
        world.Tick(key =>
        {
            asked.Add(key);
            return key == "Farm" ? new FakeEndpoints() : null;
        });

        Assert.Equal(Balance.BeltSpeedBasic, farmBelt.Slot.Progress);
        Assert.Equal(0, caveBelt.Slot.Progress);
        Assert.DoesNotContain("Empty", asked);
    }

    [Fact]
    public void Clear_RemovesAllGrids()
    {
        var world = new LogisticsWorld();
        world.GetOrCreate("Farm");

        world.Clear();

        Assert.Empty(world.Grids);
    }

    [Fact]
    public void Tick_GridWithoutNodesButWithMachineSlots_StillStartsMachine()
    {
        var world = new LogisticsWorld();
        LogisticsGrid grid = world.GetOrCreate("Farm");
        var endpoints = new FakeEndpoints();
        var keg = FakeMachine.Keg();
        endpoints.Endpoints[new TilePos(0, 0)] = keg;
        VirtualSlots slots = grid.MachineSlots.GetOrCreate(new TilePos(0, 0));
        slots.Main = new FakeItem("(O)613", 1, category: FakeMachine.Ids.FruitCategory);
        slots.MainRequired = 1;

        world.Tick(_ => endpoints);

        Assert.Single(keg.Starts);
        Assert.Equal(0, grid.MachineSlots.Count);
    }
}
