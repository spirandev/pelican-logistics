using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Machines;

/// <summary>Os slots virtuais das máquinas de um local, por posição da máquina.</summary>
public sealed class MachineSlotStore
{
    private readonly Dictionary<TilePos, VirtualSlots> slots = new();

    /// <summary>Todas as máquinas com slots.</summary>
    public IEnumerable<KeyValuePair<TilePos, VirtualSlots>> Entries => this.slots;

    public int Count => this.slots.Count;

    public VirtualSlots GetOrCreate(TilePos pos)
    {
        if (!this.slots.TryGetValue(pos, out VirtualSlots? entry))
        {
            entry = new VirtualSlots();
            this.slots[pos] = entry;
        }

        return entry;
    }

    public VirtualSlots? Get(TilePos pos)
    {
        return this.slots.TryGetValue(pos, out VirtualSlots? entry) ? entry : null;
    }

    /// <summary>Remove a entrada da máquina e devolve todos os itens guardados (para o drop).</summary>
    public IReadOnlyList<ILogisticsItem> RemoveMachine(TilePos pos)
    {
        if (!this.slots.Remove(pos, out VirtualSlots? entry))
            return Array.Empty<ILogisticsItem>();

        List<ILogisticsItem> items = entry.AllItems().ToList();
        entry.Clear();
        return items;
    }

    public void Clear()
    {
        this.slots.Clear();
    }

    /// <summary>Remove as entradas sem itens. Chamado no fim de cada tick.</summary>
    internal void RemoveEmpty()
    {
        List<TilePos> empty = this.slots.Where(p => p.Value.IsEmpty).Select(p => p.Key).ToList();
        foreach (TilePos pos in empty)
            this.slots.Remove(pos);
    }
}
