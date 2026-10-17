using System;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Rendering;

/// <summary>Posição de um item em trânsito dentro do tile, para o desenho.</summary>
public static class PathMath
{
    private const double Radius = 0.5;

    /// <summary>
    /// Posição do centro do item dentro do tile, em unidades de tile (0..1, origem no canto superior esquerdo).
    /// Reta: do meio da borda de entrada ao meio da borda de saída.
    /// Curva (entrada e saída em lados adjacentes): arco de quarto de círculo com centro no canto
    /// compartilhado pelos dois lados e raio 0,5.
    /// Entrada e saída no mesmo lado: trate como reta do centro ao lado (não deve acontecer).
    /// </summary>
    public static (float X, float Y) GetPosition(Direction entrySide, Direction exitSide, int progress)
    {
        double t = Math.Clamp(progress / (double)Balance.End, 0.0, 1.0);
        (double ex, double ey) = EdgeMidpoint(exitSide);

        if (entrySide == exitSide)
            return Lerp((0.5, 0.5), (ex, ey), t);

        (double sx, double sy) = EdgeMidpoint(entrySide);
        if (entrySide == exitSide.Opposite())
            return Lerp((sx, sy), (ex, ey), t);

        // Curva: centro no canto comum aos dois lados. As pontas são exatas (sem erro de arredondamento).
        if (t <= 0)
            return ((float)sx, (float)sy);
        if (t >= 1)
            return ((float)ex, (float)ey);

        double cx = entrySide == Direction.East || exitSide == Direction.East ? 1 : 0;
        double cy = entrySide == Direction.South || exitSide == Direction.South ? 1 : 0;
        double startAngle = Math.Atan2(sy - cy, sx - cx);
        double endAngle = Math.Atan2(ey - cy, ex - cx);
        double delta = endAngle - startAngle;
        if (delta > Math.PI)
            delta -= 2 * Math.PI;
        else if (delta < -Math.PI)
            delta += 2 * Math.PI;

        double angle = startAngle + (delta * t);
        return ((float)(cx + (Radius * Math.Cos(angle))), (float)(cy + (Radius * Math.Sin(angle))));
    }

    /// <summary>Meio da borda do tile no lado indicado.</summary>
    public static (float X, float Y) GetEdgeMidpoint(Direction side)
    {
        (double x, double y) = EdgeMidpoint(side);
        return ((float)x, (float)y);
    }

    private static (double X, double Y) EdgeMidpoint(Direction side)
    {
        return side switch
        {
            Direction.North => (0.5, 0),
            Direction.East => (1, 0.5),
            Direction.South => (0.5, 1),
            Direction.West => (0, 0.5),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Direção inválida."),
        };
    }

    private static (float X, float Y) Lerp((double X, double Y) from, (double X, double Y) to, double t)
    {
        return ((float)(from.X + ((to.X - from.X) * t)), (float)(from.Y + ((to.Y - from.Y) * t)));
    }
}
