using PelicanLogistics.Core.Geometry;

namespace PelicanLogistics.Core.Persistence;

/// <summary>Estado de um <see cref="Nodes.TransitSlot"/>.</summary>
public sealed class SlotState
{
    /// <summary>Índice do item na lista externa, ou -1 se o slot está vazio.</summary>
    public int ItemRef { get; set; } = -1;

    public int Progress { get; set; }

    public Direction EntrySide { get; set; }

    public bool Blocked { get; set; }
}
