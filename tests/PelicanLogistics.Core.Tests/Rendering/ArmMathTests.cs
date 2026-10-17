using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Core.Tests.Fakes;

namespace PelicanLogistics.Core.Tests.Rendering;

public class ArmMathTests
{
    private const float Precision = 0.0001f;

    [Theory]
    [InlineData(Direction.North, 0)]
    [InlineData(Direction.East, 90)]
    [InlineData(Direction.South, 180)]
    [InlineData(Direction.West, 270)]
    public void AngleOf_ZeroIsNorth_Clockwise(Direction d, float degrees)
    {
        Assert.Equal(Radians(degrees), ArmMath.AngleOf(d), Precision);
    }

    [Theory]
    [InlineData(Direction.West, Direction.East)]
    [InlineData(Direction.North, Direction.South)]
    [InlineData(Direction.East, Direction.West)]
    [InlineData(Direction.South, Direction.North)]
    public void BackToFront_TurnsHalfTurnClockwise(Direction from, Direction to)
    {
        // No meio da meia-volta o braço aponta para a direção 90° no sentido horário a partir da origem.
        float middle = ArmMath.GetArmAngle(from, to, 0.5f);

        Assert.Equal(ArmMath.AngleOf(from.Clockwise()), middle, Precision);
    }

    [Theory]
    [InlineData(Direction.North, Direction.East, 45)]
    [InlineData(Direction.East, Direction.North, 45)]
    [InlineData(Direction.West, Direction.North, 315)]
    [InlineData(Direction.North, Direction.West, 315)]
    public void SideToFront_TurnsQuarterByShortestPath(Direction from, Direction to, float middleDegrees)
    {
        Assert.Equal(Radians(middleDegrees), ArmMath.GetArmAngle(from, to, 0.5f), Precision);
    }

    [Theory]
    [InlineData(Direction.West, Direction.North)]
    [InlineData(Direction.South, Direction.East)]
    [InlineData(Direction.North, Direction.North)]
    public void GetArmAngle_EndpointsAreTheDirections(Direction from, Direction to)
    {
        Assert.Equal(ArmMath.AngleOf(from), ArmMath.GetArmAngle(from, to, 0f), Precision);
        Assert.Equal(ArmMath.AngleOf(to), ArmMath.GetArmAngle(from, to, 1f), Precision);
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void GetHandPosition_AtEachDirection_IsMiddleOfThatEdge(Direction d)
    {
        (float x, float y) = ArmMath.GetHandPosition(ArmMath.AngleOf(d));
        (float ex, float ey) = PathMath.GetEdgeMidpoint(d);

        Assert.Equal(ex, x, Precision);
        Assert.Equal(ey, y, Precision);
    }

    [Fact]
    public void GetHandPosition_StaysAtHandRadiusFromCenter()
    {
        (float x, float y) = ArmMath.GetHandPosition(1.234f);

        double distance = Math.Sqrt(((x - 0.5) * (x - 0.5)) + ((y - 0.5) * (y - 0.5)));
        Assert.Equal(ArmMath.HandRadius, distance, 4);
    }

    [Fact]
    public void Node_Carrying_GoesFromEntrySideToFacing()
    {
        var g = new GridBuilder()
            .Chest(0, 0, new FakeChest(36, new FakeItem("wood", 5)))
            .Inserter(1, 0, Direction.East)
            .Belt(2, 0, Direction.East);
        InserterNode inserter = g.InserterAt(1, 0);

        g.Run(1);
        Assert.Equal(ArmMath.AngleOf(Direction.West), ArmMath.GetArmAngle(inserter), Precision);

        // Meia-volta horária a partir do oeste: passa pelo norte (270° → 360°/0° → 90°).
        g.Run(7);
        float t = inserter.Hand.Progress / (float)Balance.End;
        Assert.Equal(7 * Balance.InserterSpeed, inserter.Hand.Progress);
        Assert.Equal(Radians(270 + (180 * t)), ArmMath.GetArmAngle(inserter), Precision);

        g.Run(1);
        Assert.InRange(ArmMath.GetArmAngle(inserter), Radians(0), Radians(90));
    }

    [Fact]
    public void Node_Returning_GoesFromFacingToLastInput()
    {
        var g = new GridBuilder()
            .Belt(1, -1, Direction.South)
            .Inserter(1, 0, Direction.East)
            .Chest(2, 0, new FakeChest())
            .Item(1, -1, new FakeItem("wood", 1), Balance.End);
        InserterNode inserter = g.InserterAt(1, 0);

        g.Run(1 + (Balance.End / Balance.InserterSpeed));
        Assert.True(inserter.Hand.IsEmpty);
        Assert.Equal(Balance.InserterReturnTicks, inserter.Cooldown);
        Assert.Equal(ArmMath.AngleOf(Direction.East), ArmMath.GetArmAngle(inserter), Precision);

        // Leste → norte pelo caminho curto (anti-horário): o ângulo diminui de 90° até 0°.
        g.Run(7);
        Assert.Equal(Balance.InserterReturnTicks - 7, inserter.Cooldown);
        float t = 1f - (inserter.Cooldown / (float)Balance.InserterReturnTicks);
        Assert.Equal(Radians(90 - (90 * t)), ArmMath.GetArmAngle(inserter), Precision);

        g.Run(100);
        Assert.Equal(0, inserter.Cooldown);
        Assert.Equal(ArmMath.AngleOf(Direction.North), ArmMath.GetArmAngle(inserter), Precision);
    }

    [Fact]
    public void Node_IdleAndNeverPicked_PointsBack()
    {
        var g = new GridBuilder().Inserter(1, 0, Direction.East);
        _ = g.Grid.ProcessingOrder;

        Assert.Equal(ArmMath.AngleOf(Direction.West), ArmMath.GetArmAngle(g.InserterAt(1, 0)), Precision);
    }

    [Fact]
    public void Node_IdleAndNeverPicked_WithExactlyOneSideInput_PointsToIt()
    {
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Belt(1, 1, Direction.North);
        _ = g.Grid.ProcessingOrder;

        Assert.Equal(ArmMath.AngleOf(Direction.South), ArmMath.GetArmAngle(g.InserterAt(1, 0)), Precision);
    }

    [Fact]
    public void Node_IdleAndNeverPicked_WithTwoSideInputs_PointsBack()
    {
        var g = new GridBuilder()
            .Inserter(1, 0, Direction.East)
            .Belt(1, 1, Direction.North)
            .Belt(1, -1, Direction.South);
        _ = g.Grid.ProcessingOrder;

        Assert.Equal(ArmMath.AngleOf(Direction.West), ArmMath.GetArmAngle(g.InserterAt(1, 0)), Precision);
    }

    private static float Radians(float degrees) => (float)(degrees * Math.PI / 180);
}
