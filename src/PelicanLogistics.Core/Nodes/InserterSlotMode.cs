namespace PelicanLogistics.Core.Nodes;

/// <summary>Qual slot virtual da máquina o Inseridor abastece (README "Slots virtuais").</summary>
public enum InserterSlotMode
{
    /// <summary>O mod descobre o slot certo para cada item.</summary>
    Auto,

    /// <summary>Só a entrada principal.</summary>
    Main,

    /// <summary>Só os itens adicionais.</summary>
    Additional,
}
