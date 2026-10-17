using System.Collections.Generic;

namespace PelicanLogistics.Core.Persistence;

/// <summary>Estado em tempo de execução de uma grade (um local do jogo).</summary>
public sealed class GridRuntimeState
{
    public List<NodeRuntimeState> Nodes { get; set; } = new();

    public List<MachineSlotsState> Machines { get; set; } = new();
}
