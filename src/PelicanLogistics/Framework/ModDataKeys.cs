namespace PelicanLogistics.Framework;

/// <summary>Chaves do <c>modData</c> dos objetos do mod. São salvas automaticamente pelo jogo.</summary>
internal static class ModDataKeys
{
    public const string Prefix = "spirandev.PelicanLogistics/";

    /// <summary>Direção do bloco: "North" | "East" | "South" | "West".</summary>
    public const string Facing = Prefix + "Facing";

    /// <summary>De onde o Inseridor retira quando atrás há uma máquina: "Product" | "Main" | "Additional".</summary>
    public const string PickupSource = Prefix + "PickupSource";

    /// <summary>Qual slot o Inseridor abastece em máquinas: "Auto" | "Main" | "Additional".</summary>
    public const string InserterSlotMode = Prefix + "InserterSlotMode";

    /// <summary>Regra do Filtro Comum ou do Filtro Completo, em JSON (<c>FilterRuleSerializer</c>).</summary>
    public const string FilterRule = Prefix + "FilterRule";

    /// <summary>Chaves de configuração (tudo menos a direção), apagadas quando o bloco é colocado.</summary>
    public static readonly string[] Config = { PickupSource, InserterSlotMode, FilterRule };
}
