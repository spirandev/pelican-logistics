namespace PelicanLogistics.Core.Nodes;

/// <summary>Papel de um Túnel, decidido ao colocar (<see cref="Simulation.TunnelRules.DecideRole"/>) e fixo depois (plano mestre D21).</summary>
public enum TunnelRole
{
    /// <summary>Recebe por trás, na superfície, e entrega à Esteira Subterrânea à frente.</summary>
    Entrance,

    /// <summary>Recebe da Esteira Subterrânea atrás e entrega à frente, na superfície, como uma esteira.</summary>
    Exit,
}
