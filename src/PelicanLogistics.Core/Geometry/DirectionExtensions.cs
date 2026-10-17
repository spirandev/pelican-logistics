using System;
using System.Collections.Generic;

namespace PelicanLogistics.Core.Geometry;

/// <summary>Operações sobre <see cref="Direction"/>.</summary>
public static class DirectionExtensions
{
    private const int DirectionCount = 4;

    /// <summary>As 4 direções em ordem horária, começando pelo norte: N, E, S, W.</summary>
    public static readonly IReadOnlyList<Direction> All = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

    /// <summary>Direção oposta (N ↔ S, E ↔ W).</summary>
    public static Direction Opposite(this Direction d)
    {
        return Rotate(d, 2);
    }

    /// <summary>Próxima direção em ordem horária: N → E → S → W → N.</summary>
    public static Direction Clockwise(this Direction d)
    {
        return Rotate(d, 1);
    }

    /// <summary>Próxima direção em ordem anti-horária: N → W → S → E → N.</summary>
    public static Direction CounterClockwise(this Direction d)
    {
        return Rotate(d, DirectionCount - 1);
    }

    /// <summary>Deslocamento de um tile na direção. O eixo Y do jogo cresce para baixo, então o norte é (0, -1).</summary>
    public static (int Dx, int Dy) ToOffset(this Direction d)
    {
        return d switch
        {
            Direction.North => (0, -1),
            Direction.East => (1, 0),
            Direction.South => (0, 1),
            Direction.West => (-1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Direção inválida."),
        };
    }

    /// <summary>As 4 direções em ordem horária, começando por <paramref name="start"/>.</summary>
    public static IReadOnlyList<Direction> ClockwiseFrom(Direction start)
    {
        Validate(start);

        var result = new Direction[DirectionCount];
        for (int i = 0; i < DirectionCount; i++)
            result[i] = Rotate(start, i);
        return result;
    }

    /// <summary>Gira a direção <paramref name="steps"/> passos em sentido horário.</summary>
    private static Direction Rotate(Direction d, int steps)
    {
        Validate(d);
        return (Direction)(((int)d + steps) % DirectionCount);
    }

    private static void Validate(Direction d)
    {
        if ((int)d < 0 || (int)d >= DirectionCount)
            throw new ArgumentOutOfRangeException(nameof(d), d, "Direção inválida.");
    }
}
