namespace PelicanLogistics.Core.Geometry;

/// <summary>Posição de um tile no mapa de um local do jogo.</summary>
public readonly record struct TilePos(int X, int Y)
{
    /// <summary>Tile vizinho na direção indicada.</summary>
    public TilePos Offset(Direction direction)
    {
        (int dx, int dy) = direction.ToOffset();
        return new TilePos(this.X + dx, this.Y + dy);
    }

    /// <summary>Direção de 'this' para um vizinho adjacente, ou null se não for adjacente.</summary>
    public Direction? DirectionTo(TilePos neighbor)
    {
        foreach (Direction direction in DirectionExtensions.All)
        {
            if (this.Offset(direction) == neighbor)
                return direction;
        }

        return null;
    }

    public override string ToString() => $"({this.X}, {this.Y})";
}
