namespace PelicanLogistics.Core.Rendering;

/// <summary>Cores dos placeholders da alpha (README "Placeholders visuais").</summary>
public static class PlaceholderColors
{
    public static readonly Rgba BeltBasic = new(200, 200, 200);
    public static readonly Rgba BeltFast = new(150, 150, 150);
    public static readonly Rgba BeltExpress = new(95, 95, 95);
    public static readonly Rgba Bridge = new(139, 90, 43);
    public static readonly Rgba Underground = new(70, 70, 70);
    public static readonly Rgba Inserter = new(40, 110, 230);
    public static readonly Rgba FilterCommon = new(60, 180, 75);
    public static readonly Rgba FilterComplete = new(25, 110, 45);
    public static readonly Rgba Splitter = new(140, 70, 190);
    public static readonly Rgba Regulator = new(240, 210, 40);

    /// <summary>Setas e pontos.</summary>
    public static readonly Rgba Mark = new(255, 255, 255);

    /// <summary>Contornos.</summary>
    public static readonly Rgba Outline = new(40, 40, 40);
}
