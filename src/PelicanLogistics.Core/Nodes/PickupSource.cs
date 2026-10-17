namespace PelicanLogistics.Core.Nodes;

/// <summary>De onde o Inseridor retira quando a origem atrás dele é uma máquina (README "Retirada (Inseridor)").</summary>
public enum PickupSource
{
    /// <summary>O que a máquina fabricou (padrão).</summary>
    Product,

    /// <summary>O que está guardado no slot virtual Principal.</summary>
    Main,

    /// <summary>O que está guardado nos slots virtuais Adicionais.</summary>
    Additional,
}
