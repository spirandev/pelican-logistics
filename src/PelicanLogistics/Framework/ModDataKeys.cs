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

    /// <summary>Quantidade por operação do Inseridor Configurável: número de 1 a 999.</summary>
    public const string ConfiguredAmount = Prefix + "ConfiguredAmount";

    /// <summary>Papel da Esteira Subterrânea, decidido ao colocar: "Entrance" | "Exit".</summary>
    public const string UndergroundRole = Prefix + "UndergroundRole";

    /// <summary>Acessório sobre o bloco: "Bridge" | "Regulator" (ausente = nenhum).</summary>
    public const string Accessory = Prefix + "Accessory";

    /// <summary>Regra do Regulador, em JSON (<c>RegulatorRuleSerializer</c>).</summary>
    public const string RegulatorRule = Prefix + "RegulatorRule";

    /// <summary>Chaves de configuração (tudo menos a direção), apagadas quando o bloco é colocado.</summary>
    public static readonly string[] Config = { PickupSource, InserterSlotMode, FilterRule, ConfiguredAmount, UndergroundRole, Accessory, RegulatorRule };
}
