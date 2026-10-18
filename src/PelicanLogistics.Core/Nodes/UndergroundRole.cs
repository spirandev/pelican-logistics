namespace PelicanLogistics.Core.Nodes;

/// <summary>Papel de uma Esteira Subterrânea, decidido ao colocar (<see cref="Simulation.UndergroundPairing.DecideRole"/>).</summary>
public enum UndergroundRole
{
    /// <summary>Recebe por trás e manda os itens pelo túnel até a saída.</summary>
    Entrance,

    /// <summary>Recebe só da entrada parceira e entrega à frente, como uma esteira.</summary>
    Exit,
}
