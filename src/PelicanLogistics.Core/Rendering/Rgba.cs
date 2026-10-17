namespace PelicanLogistics.Core.Rendering;

/// <summary>Uma cor RGBA de 8 bits por canal (sem alfa pré-multiplicado).</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    public bool IsTransparent => this.A == 0;
}
