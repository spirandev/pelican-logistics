namespace PelicanLogistics.Core.Nodes;

/// <summary>De onde o Extrator retira quando a origem é uma máquina (README "Retirada (Extrator)").</summary>
public enum ExtractorSource
{
    /// <summary>O que a máquina fabricou (padrão).</summary>
    Product,

    /// <summary>O que está guardado no slot virtual Principal.</summary>
    Main,

    /// <summary>O que está guardado nos slots virtuais Adicionais.</summary>
    Additional,
}
