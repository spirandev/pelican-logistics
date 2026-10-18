using System.Collections.Generic;

namespace PelicanLogistics.Core.Persistence;

/// <summary>Slots virtuais de uma máquina (<see cref="Machines.VirtualSlots"/>).</summary>
public sealed class MachineSlotsState
{
    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>Índice do item do Principal na lista externa, ou -1 se vazio.</summary>
    public int MainRef { get; set; } = -1;

    public int MainRequired { get; set; }

    /// <summary>Índice do item de cada requisito adicional, por <see cref="Endpoints.AdditionalRequirement.Id"/>.</summary>
    public Dictionary<string, int> AdditionalRefs { get; set; } = new();
}
