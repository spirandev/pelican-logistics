namespace PelicanLogistics.Framework;

/// <summary>Chaves do <c>modData</c> dos objetos do mod. São salvas automaticamente pelo jogo.</summary>
internal static class ModDataKeys
{
    public const string Prefix = "spirandev.PelicanLogistics/";

    /// <summary>Direção do bloco: "North" | "East" | "South" | "West".</summary>
    public const string Facing = Prefix + "Facing";
}
