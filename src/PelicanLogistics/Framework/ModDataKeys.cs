namespace PelicanLogistics.Framework;

/// <summary>Chaves do <c>modData</c> dos objetos do mod. São salvas automaticamente pelo jogo.</summary>
internal static class ModDataKeys
{
    public const string Prefix = "spirandev.PelicanLogistics/";

    /// <summary>Direção do bloco: "North" | "East" | "South" | "West".</summary>
    public const string Facing = Prefix + "Facing";

    /// <summary>De onde o Extrator retira em máquinas: "Product" | "Main" | "Additional".</summary>
    public const string ExtractorSource = Prefix + "ExtractorSource";

    /// <summary>Qual slot o Inseridor abastece em máquinas: "Auto" | "Main" | "Additional".</summary>
    public const string InserterSlotMode = Prefix + "InserterSlotMode";

    /// <summary>Chaves de configuração (tudo menos a direção), apagadas quando o bloco é colocado.</summary>
    public static readonly string[] Config = { ExtractorSource, InserterSlotMode };
}
