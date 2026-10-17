using System;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Rendering;

/// <summary>
/// Ângulo e posição do braço do Inseridor, para o desenho. Ângulos em radianos, com 0 = norte e sentido horário
/// positivo (a mesma convenção da rotação dos sprites), normalizados para 0..2π.
/// </summary>
public static class ArmMath
{
    /// <summary>Distância da mão ao centro, em tiles: a mão chega ao meio da borda, onde as esteiras entregam.</summary>
    public const float HandRadius = 0.5f;

    private const double FullTurn = 2 * Math.PI;

    /// <summary>Ângulo que aponta para o lado indicado.</summary>
    public static float AngleOf(Direction d)
    {
        return (float)AngleOfDouble(d);
    }

    /// <summary>Gira de 'from' para 'to' pelo caminho mais curto; meia-volta (180°) gira no sentido horário. t em 0..1.</summary>
    public static float GetArmAngle(Direction from, Direction to, float t)
    {
        double start = AngleOfDouble(from);
        double delta = AngleOfDouble(to) - start;
        if (delta > Math.PI)
            delta -= FullTurn;
        else if (delta <= -Math.PI)
            delta += FullTurn;

        return Normalize(start + (delta * Math.Clamp(t, 0f, 1f)));
    }

    /// <summary>Posição da mão no tile (0..1, origem no canto superior esquerdo).</summary>
    public static (float X, float Y) GetHandPosition(float angle)
    {
        return ((float)(0.5 + (HandRadius * Math.Sin(angle))), (float)(0.5 - (HandRadius * Math.Cos(angle))));
    }

    /// <summary>
    /// Ângulo atual do braço de um Inseridor. Carregando: da origem até a frente. Voltando: da frente até a última
    /// entrada usada. Parado: apontando para a última entrada; se nunca pegou, para a única lateral em
    /// <see cref="InserterNode.Inputs"/> quando há exatamente uma, senão para trás.
    /// </summary>
    public static float GetArmAngle(InserterNode node)
    {
        if (node.Hand.Item != null)
            return GetArmAngle(node.Hand.EntrySide, node.Facing, node.Hand.Progress / (float)Balance.End);

        Direction rest = node.LastInput ?? DefaultRest(node);
        if (node.Cooldown > 0)
            return GetArmAngle(node.Facing, rest, 1f - (node.Cooldown / (float)Balance.InserterReturnTicks));

        return AngleOf(rest);
    }

    /// <summary>Direção do braço parado de um Inseridor que nunca pegou.</summary>
    private static Direction DefaultRest(InserterNode node)
    {
        Direction back = node.Facing.Opposite();
        Direction? lateral = null;
        int laterals = 0;
        foreach (Direction input in node.Inputs)
        {
            if (input == back)
                continue;

            lateral = input;
            laterals++;
        }

        return laterals == 1 ? lateral!.Value : back;
    }

    private static double AngleOfDouble(Direction d)
    {
        return d switch
        {
            Direction.North => 0,
            Direction.East => Math.PI / 2,
            Direction.South => Math.PI,
            Direction.West => 3 * Math.PI / 2,
            _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Direção inválida."),
        };
    }

    private static float Normalize(double angle)
    {
        angle %= FullTurn;
        if (angle < 0)
            angle += FullTurn;
        return (float)angle;
    }
}
