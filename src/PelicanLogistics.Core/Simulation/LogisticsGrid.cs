using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;

namespace PelicanLogistics.Core.Simulation;

/// <summary>
/// Os nós de um local do jogo, em duas camadas: a <b>superfície</b> (um bloco por tile) e a <b>subterrânea</b> (no máximo
/// uma <see cref="UndergroundSegmentNode"/> por tile). Um tile pode ter um nó em cada camada; só o <see cref="TunnelNode"/>
/// liga as duas (plano mestre D19).
/// </summary>
public sealed class LogisticsGrid
{
    private readonly Dictionary<TilePos, LogisticsNode> nodes = new();
    private readonly Dictionary<TilePos, UndergroundSegmentNode> underground = new();
    private IReadOnlyList<LogisticsNode> order = Array.Empty<LogisticsNode>();
    private bool dirty;

    /// <summary>Os nós da superfície.</summary>
    public IEnumerable<LogisticsNode> Nodes => this.nodes.Values;

    /// <summary>Quantos nós há na superfície.</summary>
    public int Count => this.nodes.Count;

    /// <summary>As Esteiras Subterrâneas.</summary>
    public IEnumerable<UndergroundSegmentNode> UndergroundNodes => this.underground.Values;

    /// <summary>Quantos nós há nas duas camadas.</summary>
    public int TotalCount => this.nodes.Count + this.underground.Count;

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

    /// <summary>Adiciona um nó da superfície. Lança exceção se já houver nó na posição ou se for uma Esteira Subterrânea.</summary>
    public void Add(LogisticsNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        if (node is UndergroundSegmentNode)
            throw new ArgumentException("Esteiras Subterrâneas vão na camada subterrânea (AddUnderground).", nameof(node));
        if (this.nodes.ContainsKey(node.Position))
            throw new InvalidOperationException($"Já existe um nó em {node.Position}.");

        this.nodes[node.Position] = node;
        this.MarkDirty();
    }

    /// <summary>Remove o nó da superfície e devolve os itens de todos os seus espaços e o acessório.</summary>
    public RemovalResult Remove(TilePos pos)
    {
        if (!this.nodes.Remove(pos, out LogisticsNode? node))
            return RemovalResult.Empty;

        this.MarkDirty();
        return new RemovalResult(ClearSlots(node), node.Accessory);
    }

    /// <summary>Adiciona uma Esteira Subterrânea. Lança exceção se o tile já tem uma.</summary>
    public void AddUnderground(UndergroundSegmentNode segment)
    {
        if (segment == null)
            throw new ArgumentNullException(nameof(segment));
        if (this.underground.ContainsKey(segment.Position))
            throw new InvalidOperationException($"Já existe uma Esteira Subterrânea em {segment.Position}.");

        this.underground[segment.Position] = segment;
        this.MarkDirty();
    }

    public UndergroundSegmentNode? GetUnderground(TilePos pos)
    {
        return this.underground.TryGetValue(pos, out UndergroundSegmentNode? segment) ? segment : null;
    }

    /// <summary>Remove a Esteira Subterrânea do tile e devolve o item dela (nunca há acessório).</summary>
    public RemovalResult RemoveUnderground(TilePos pos)
    {
        if (!this.underground.Remove(pos, out UndergroundSegmentNode? segment))
            return RemovalResult.Empty;

        this.MarkDirty();
        return new RemovalResult(ClearSlots(segment), AccessoryKind.None);
    }

    /// <summary>
    /// True se dá para colocar uma Esteira Subterrânea no tile: sem outra Esteira Subterrânea e sem Túnel na superfície
    /// (plano mestre D21). Outros blocos da superfície não impedem; as regras do mapa (água, paredes) ficam no adaptador.
    /// </summary>
    public bool CanPlaceSegment(TilePos pos)
    {
        return !this.underground.ContainsKey(pos) && this.Get(pos) is not TunnelNode;
    }

    /// <summary>
    /// Coloca um acessório sobre o bloco (README "Acessórios"): a Ponte só numa esteira (qualquer velocidade), o
    /// Regulador só num Inseridor (qualquer versão), e no máximo um acessório por tile. O Regulador recebe 'regulatorRule'
    /// (ou a regra padrão); para a Ponte, 'regulatorRule' é ignorada. Retorna false se não colocou.
    /// </summary>
    public bool TrySetAccessory(TilePos pos, AccessoryKind kind, RegulatorRule? regulatorRule = null)
    {
        LogisticsNode? node = this.Get(pos);
        if (node == null || node.Accessory != AccessoryKind.None)
            return false;

        switch (kind)
        {
            case AccessoryKind.Bridge when node is BeltNode:
                node.Accessory = AccessoryKind.Bridge;
                return true;

            case AccessoryKind.Regulator when node is InserterNode inserter:
                inserter.Accessory = AccessoryKind.Regulator;
                inserter.Regulator = regulatorRule ?? new RegulatorRule();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Tira o acessório do bloco e o devolve (<see cref="AccessoryKind.None"/> se não havia bloco ou acessório).</summary>
    public AccessoryKind RemoveAccessory(TilePos pos)
    {
        LogisticsNode? node = this.Get(pos);
        if (node == null)
            return AccessoryKind.None;

        AccessoryKind removed = node.Accessory;
        node.Accessory = AccessoryKind.None;
        if (node is InserterNode inserter)
            inserter.Regulator = null;
        return removed;
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

    /// <summary>Um tick de lógica: atualiza todos os nós (das duas camadas), de jusante para montante, e depois aciona as máquinas.</summary>
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

    /// <summary>Esvazia todos os espaços do nó e devolve os itens.</summary>
    private static List<ILogisticsItem> ClearSlots(LogisticsNode node)
    {
        var items = new List<ILogisticsItem>();
        foreach (TransitSlot slot in node.Slots)
        {
            ILogisticsItem? item = slot.Clear();
            if (item != null)
                items.Add(item);
        }

        return items;
    }

    private void EnsureTopology()
    {
        if (!this.dirty)
            return;

        this.order = Topology.Recompute(this);
        this.dirty = false;
    }
}
