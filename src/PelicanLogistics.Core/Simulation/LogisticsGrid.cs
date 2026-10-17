using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>Os nós de um local do jogo.</summary>
public sealed class LogisticsGrid
{
    private readonly Dictionary<TilePos, LogisticsNode> nodes = new();
    private IReadOnlyList<LogisticsNode> order = Array.Empty<LogisticsNode>();
    private bool dirty;

    public IEnumerable<LogisticsNode> Nodes => this.nodes.Values;

    public int Count => this.nodes.Count;

    /// <summary>Slots virtuais das máquinas abastecidas pelos Inseridores deste local.</summary>
    public MachineSlotStore MachineSlots { get; } = new();

    /// <summary>Ordem em que os nós são atualizados a cada tick (ver <see cref="Topology"/>).</summary>
    public IReadOnlyList<LogisticsNode> ProcessingOrder
    {
        get
        {
            this.EnsureTopology();
            return this.order;
        }
    }

    /// <summary>Adiciona um nó. Lança exceção se já houver nó na posição.</summary>
    public void Add(LogisticsNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        if (this.nodes.ContainsKey(node.Position))
            throw new InvalidOperationException($"Já existe um nó em {node.Position}.");

        this.nodes[node.Position] = node;
        this.MarkDirty();
    }

    /// <summary>Remove o nó e devolve os itens de todos os seus espaços e o acessório.</summary>
    public RemovalResult Remove(TilePos pos)
    {
        if (!this.nodes.Remove(pos, out LogisticsNode? node))
            return RemovalResult.Empty;

        var items = new List<ILogisticsItem>();
        foreach (TransitSlot slot in node.Slots)
        {
            ILogisticsItem? item = slot.Clear();
            if (item != null)
                items.Add(item);
        }

        this.MarkDirty();
        return new RemovalResult(items, node.Accessory);
    }

    public LogisticsNode? Get(TilePos pos)
    {
        return this.nodes.TryGetValue(pos, out LogisticsNode? node) ? node : null;
    }

    /// <summary>Força o recálculo da topologia no próximo tick. Chame também ao girar um nó.</summary>
    public void MarkDirty()
    {
        this.dirty = true;
    }

    /// <summary>Um tick de lógica: atualiza todos os nós, de jusante para montante, e depois aciona as máquinas.</summary>
    public void Tick(IEndpointProvider endpoints)
    {
        var ctx = new TickContext(this, endpoints);
        foreach (LogisticsNode node in this.ProcessingOrder)
            node.Update(ctx);

        this.StartMachines(endpoints);
        this.MachineSlots.RemoveEmpty();
    }

    /// <summary>
    /// Aciona as máquinas ociosas com os slots completos. Slots completos de uma máquina ocupada esperam o
    /// produto ser retirado. Se a máquina não está mais lá, o adaptador trata a remoção.
    /// </summary>
    private void StartMachines(IEndpointProvider endpoints)
    {
        foreach ((TilePos pos, VirtualSlots slots) in this.MachineSlots.Entries)
        {
            if (endpoints.GetEndpoint(pos) is not IMachineEndpoint machine)
                continue;

            IReadOnlyList<AdditionalRequirement> reqs = machine.AdditionalRequirements;
            if (!slots.IsComplete(reqs) || !machine.IsIdle)
                continue;

            var additional = new List<ILogisticsItem>(reqs.Count);
            foreach (AdditionalRequirement req in reqs)
                additional.Add(slots.Additional[req.Id]);

            // O que a máquina não consumiu continua nos slots: o Core nunca perde itens.
            if (machine.TryStart(slots.Main!, additional))
                slots.RemoveConsumed();
        }
    }

    private void EnsureTopology()
    {
        if (!this.dirty)
            return;

        this.order = Topology.Recompute(this);
        this.dirty = false;
    }
}
