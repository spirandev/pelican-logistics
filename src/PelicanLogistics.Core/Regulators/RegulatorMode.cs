namespace PelicanLogistics.Core.Regulators;

/// <summary>Tipo de limite do Regulador (README "Regulador").</summary>
public enum RegulatorMode
{
    /// <summary>Um limite separado para cada item.</summary>
    PerItem,

    /// <summary>Um limite para o total de itens no destino.</summary>
    General,
}
