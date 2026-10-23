using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Persistence;

/// <summary>
/// Converte o estado em tempo de execução do <see cref="LogisticsWorld"/> em <see cref="WorldRuntimeState"/> e
/// vice-versa. Os itens ficam numa lista externa (o adaptador usa um inventário do jogo); o estado guarda só os
/// índices.
/// </summary>
/// <remarks>
/// Nenhum item se perde nem se duplica: cada índice é resolvido no máximo uma vez, e todo item resolvido vai para
/// um slot ou para 'orphans'.
/// </remarks>
public static class RuntimeStateSerializer
{
    /// <summary>'register' recebe um item e devolve o índice dele na lista externa.</summary>
    public static WorldRuntimeState Export(LogisticsWorld world, Func<ILogisticsItem, int> register)
    {
        if (world == null)
            throw new ArgumentNullException(nameof(world));
        if (register == null)
            throw new ArgumentNullException(nameof(register));

        var state = new WorldRuntimeState();
        foreach ((string key, LogisticsGrid grid) in world.Grids)
        {
            if (grid.TotalCount == 0 && grid.MachineSlots.Count == 0)
                continue;

            var gridState = new GridRuntimeState();
            foreach (LogisticsNode node in grid.Nodes.Concat(grid.UndergroundNodes))
                gridState.Nodes.Add(ExportNode(node, register));

            foreach ((TilePos pos, VirtualSlots slots) in grid.MachineSlots.Entries)
            {
                if (!slots.IsEmpty)
                    gridState.Machines.Add(ExportMachine(pos, slots, register));
            }

            state.Grids[key] = gridState;
        }

        return state;
    }

    /// <summary>
    /// Restaura o estado em um mundo cujos nós JÁ foram recriados (a partir dos objetos do jogo).
    /// 'resolve' devolve o item de um índice, ou null se não existir.
    /// Itens que não encontram destino (nó removido, tipo diferente, número de slots diferente)
    /// vão para 'orphans', com o local e a posição, para o adaptador soltar no chão.
    /// </summary>
    /// <remarks>
    /// Slots virtuais são restaurados sem verificar se a máquina ainda existe (o Core não sabe); o adaptador
    /// remove os que sobrarem. <see cref="WorldRuntimeState.Version"/> desconhecida → nada é importado e todos os
    /// itens viram órfãos.
    /// </remarks>
    public static void Import(
        LogisticsWorld world,
        WorldRuntimeState state,
        Func<int, ILogisticsItem?> resolve,
        ICollection<OrphanItem> orphans)
    {
        if (world == null)
            throw new ArgumentNullException(nameof(world));
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (resolve == null)
            throw new ArgumentNullException(nameof(resolve));
        if (orphans == null)
            throw new ArgumentNullException(nameof(orphans));

        var refs = new ItemRefs(resolve);
        bool compatible = state.Version == WorldRuntimeState.CurrentVersion;
        foreach ((string key, GridRuntimeState? gridState) in state.Grids ?? new Dictionary<string, GridRuntimeState>())
        {
            if (gridState == null)
                continue;

            foreach (NodeRuntimeState? nodeState in gridState.Nodes ?? new List<NodeRuntimeState>())
            {
                if (nodeState == null)
                    continue;

                var pos = new TilePos(nodeState.X, nodeState.Y);
                LogisticsNode? node = compatible ? FindNode(world.Get(key), pos, nodeState.Layer) : null;
                if (node != null && node.Kind == nodeState.Kind)
                    ImportNode(key, node, nodeState, refs, orphans);
                else
                    OrphanNode(key, pos, nodeState, refs, orphans);
            }

            foreach (MachineSlotsState? machineState in gridState.Machines ?? new List<MachineSlotsState>())
            {
                if (machineState == null)
                    continue;

                if (compatible)
                    ImportMachine(key, world.GetOrCreate(key), machineState, refs, orphans);
                else
                    OrphanMachine(key, machineState, refs, orphans);
            }
        }
    }

    /// <summary>O nó salvo na camada indicada; camada desconhecida → nenhum (os itens viram órfãos).</summary>
    private static LogisticsNode? FindNode(LogisticsGrid? grid, TilePos pos, int layer)
    {
        return layer switch
        {
            NodeRuntimeState.SurfaceLayer => grid?.Get(pos),
            NodeRuntimeState.UndergroundLayer => grid?.GetUnderground(pos),
            _ => null,
        };
    }

    private static NodeRuntimeState ExportNode(LogisticsNode node, Func<ILogisticsItem, int> register)
    {
        (int input, int output) = node.ExportPointers();
        var nodeState = new NodeRuntimeState
        {
            X = node.Position.X,
            Y = node.Position.Y,
            Kind = node.Kind,
            Layer = (int)node.Layer,
            Cooldown = node.ExportCooldown(),
            InputPointer = input,
            OutputPointer = output,
        };

        foreach (TransitSlot slot in node.Slots)
        {
            nodeState.Slots.Add(new SlotState
            {
                ItemRef = slot.Item != null ? register(slot.Item) : -1,
                Progress = slot.Progress,
                EntrySide = slot.EntrySide,
                Blocked = slot.Blocked,
            });
        }

        return nodeState;
    }

    private static MachineSlotsState ExportMachine(TilePos pos, VirtualSlots slots, Func<ILogisticsItem, int> register)
    {
        var machineState = new MachineSlotsState
        {
            X = pos.X,
            Y = pos.Y,
            MainRef = slots.Main != null ? register(slots.Main) : -1,
            MainRequired = slots.MainRequired,
        };

        foreach ((string id, ILogisticsItem item) in slots.Additional)
            machineState.AdditionalRefs[id] = register(item);

        return machineState;
    }

    /// <summary>Restaura os slots na ordem de <see cref="LogisticsNode.Slots"/>; os que sobram viram órfãos.</summary>
    private static void ImportNode(string key, LogisticsNode node, NodeRuntimeState nodeState, ItemRefs refs, ICollection<OrphanItem> orphans)
    {
        List<TransitSlot> slots = node.Slots.ToList();
        List<SlotState> saved = nodeState.Slots ?? new List<SlotState>();
        for (int i = 0; i < saved.Count; i++)
        {
            SlotState? slotState = saved[i];
            ILogisticsItem? item = slotState != null ? refs.Take(slotState.ItemRef) : null;
            if (item == null)
                continue;

            // Um slot já ocupado (não deveria acontecer: os nós acabaram de ser recriados) não é sobrescrito.
            if (i < slots.Count && slots[i].IsEmpty)
            {
                Direction entrySide = Enum.IsDefined(slotState!.EntrySide) ? slotState.EntrySide : node.Facing.Opposite();
                slots[i].Restore(item, slotState.Progress, entrySide, slotState.Blocked);
            }
            else
            {
                orphans.Add(new OrphanItem(key, node.Position, item));
            }
        }

        node.ImportCooldown(nodeState.Cooldown);
        node.ImportPointers(nodeState.InputPointer, nodeState.OutputPointer);
    }

    private static void OrphanNode(string key, TilePos pos, NodeRuntimeState nodeState, ItemRefs refs, ICollection<OrphanItem> orphans)
    {
        foreach (SlotState? slotState in nodeState.Slots ?? new List<SlotState>())
        {
            if (slotState != null && refs.Take(slotState.ItemRef) is { } item)
                orphans.Add(new OrphanItem(key, pos, item));
        }
    }

    private static void ImportMachine(string key, LogisticsGrid grid, MachineSlotsState machineState, ItemRefs refs, ICollection<OrphanItem> orphans)
    {
        var pos = new TilePos(machineState.X, machineState.Y);
        ILogisticsItem? main = refs.Take(machineState.MainRef);
        var additional = new List<KeyValuePair<string, ILogisticsItem>>();
        foreach ((string id, int itemRef) in machineState.AdditionalRefs ?? new Dictionary<string, int>())
        {
            if (refs.Take(itemRef) is { } item)
                additional.Add(new KeyValuePair<string, ILogisticsItem>(id, item));
        }

        if (main == null && additional.Count == 0)
            return;

        // Slots já ocupados (não deveria acontecer) não são misturados com os salvos.
        if (grid.MachineSlots.Get(pos) is { IsEmpty: false })
        {
            if (main != null)
                orphans.Add(new OrphanItem(key, pos, main));
            foreach (KeyValuePair<string, ILogisticsItem> pair in additional)
                orphans.Add(new OrphanItem(key, pos, pair.Value));
            return;
        }

        VirtualSlots slots = grid.MachineSlots.GetOrCreate(pos);
        if (main != null)
        {
            slots.Main = main;
            slots.MainRequired = Math.Max(1, machineState.MainRequired);
        }

        foreach ((string id, ILogisticsItem item) in additional)
            slots.Additional[id] = item;
    }

    private static void OrphanMachine(string key, MachineSlotsState machineState, ItemRefs refs, ICollection<OrphanItem> orphans)
    {
        var pos = new TilePos(machineState.X, machineState.Y);
        if (refs.Take(machineState.MainRef) is { } main)
            orphans.Add(new OrphanItem(key, pos, main));

        foreach (int itemRef in (machineState.AdditionalRefs ?? new Dictionary<string, int>()).Values)
        {
            if (refs.Take(itemRef) is { } item)
                orphans.Add(new OrphanItem(key, pos, item));
        }
    }

    /// <summary>Resolve os índices da lista externa, cada um no máximo uma vez (um estado corrompido não duplica itens).</summary>
    private sealed class ItemRefs
    {
        private readonly Func<int, ILogisticsItem?> resolve;
        private readonly HashSet<int> taken = new();

        public ItemRefs(Func<int, ILogisticsItem?> resolve)
        {
            this.resolve = resolve;
        }

        /// <summary>O item do índice, ou null se o índice é -1, já foi usado ou não existe.</summary>
        public ILogisticsItem? Take(int itemRef)
        {
            if (itemRef < 0 || !this.taken.Add(itemRef))
                return null;

            return this.resolve(itemRef);
        }
    }
}
