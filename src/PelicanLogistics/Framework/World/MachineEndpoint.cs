using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>
/// Implementa <see cref="IMachineEndpoint"/> para uma máquina de <c>Data/Machines</c>. Segue a abordagem do
/// Automate (<c>DataBasedObjectMachine</c>), mas nunca usa o inventário do jogador: a entrada e os adicionais
/// vêm dos slots virtuais e são consumidos aqui, só se a máquina iniciar.
/// </summary>
internal sealed class MachineEndpoint : IMachineEndpoint
{
    private readonly SObject machine;
    private readonly GameLocation location;
    private readonly MachineData data;
    private IReadOnlyList<AdditionalRequirement>? additionalRequirements;

    public MachineEndpoint(SObject machine, GameLocation location, MachineData data)
    {
        this.machine = machine;
        this.location = location;
        this.data = data;
    }

    /// <summary>Nome da máquina (para o <c>pl_inspect</c>).</summary>
    public string DisplayName => this.machine.DisplayName;

    public IReadOnlyList<AdditionalRequirement> AdditionalRequirements => this.additionalRequirements ??= this.BuildAdditionalRequirements();

    public bool IsIdle => this.machine.heldObject.Value == null;

    /// <summary>Repete sozinha depois de carregada: alguma regra é disparada pela coleta do produto (ex.: Cristalário).</summary>
    public bool IsRepeating =>
        this.data.OutputRules?.Any(rule => rule?.Triggers?.Any(t => t != null && (t.Trigger & MachineOutputTrigger.OutputCollected) != 0) == true) == true;

    public bool HasLoadedInput => this.machine.heldObject.Value != null || this.machine.lastInputItem.Value != null;

    /// <summary>
    /// Máquinas suportadas na alpha (README "Máquinas suportadas"): objetos com <c>Data/Machines</c>, exceto os
    /// nossos, o Covo e o Barril de carvalho (lógica própria).
    /// </summary>
    public static bool IsSupported(SObject obj)
    {
        return !ItemIds.IsOurBlock(obj) && obj is not CrabPot && obj is not Cask && obj.GetMachineData() != null;
    }

    public int? GetMainRequiredCount(ILogisticsItem item)
    {
        if (item is not GameItem game)
            return null;

        bool found = MachineDataUtility.TryGetMachineOutputRule(
            this.machine,
            this.data,
            MachineOutputTrigger.ItemPlacedInMachine,
            game.Item,
            Game1.player,
            this.location,
            out MachineOutputRule? rule,
            out MachineOutputTriggerRule? trigger,
            out MachineOutputRule? ruleIgnoringCount,
            out MachineOutputTriggerRule? triggerIgnoringCount);

        // A pilha na esteira pode ser menor que a exigida: vale a regra que aceita o item "ignorando a quantidade".
        if (found && rule != null && trigger != null)
            return Math.Max(1, trigger.RequiredCount);
        if (ruleIgnoringCount != null && triggerIgnoringCount != null)
            return Math.Max(1, triggerIgnoringCount.RequiredCount);
        return null;
    }

    public ILogisticsItem? PeekOutput()
    {
        SObject? held = this.machine.heldObject.Value;
        return held != null && this.machine.readyForHarvest.Value ? new GameItem(held) : null;
    }

    public ILogisticsItem? TakeOutput(int max)
    {
        SObject? held = this.machine.heldObject.Value;
        if (held == null || !this.machine.readyForHarvest.Value || max <= 0)
            return null;

        if (max < held.Stack)
        {
            var part = (GameItem)new GameItem(held).Split(max);
            MachineDataUtility.UpdateStats(this.data.StatsToIncrementWhenHarvested, part.Item, part.Count);
            return part;
        }

        this.Collect(held);
        return new GameItem(held);
    }

    public bool TryStart(ILogisticsItem main, IReadOnlyList<ILogisticsItem> additional)
    {
        if (!this.IsIdle || main is not GameItem mainItem)
            return false;

        // Confere os adicionais contra a lista recebida (nunca contra o inventário do jogador).
        IReadOnlyList<AdditionalRequirement> reqs = this.AdditionalRequirements;
        if (additional.Count != reqs.Count)
            return false;
        for (int i = 0; i < reqs.Count; i++)
        {
            if (additional[i] is not GameItem || !reqs[i].Matches(additional[i]) || additional[i].Count < reqs[i].RequiredCount)
                return false;
        }

        if (!MachineDataUtility.TryGetMachineOutputRule(this.machine, this.data, MachineOutputTrigger.ItemPlacedInMachine, mainItem.Item, Game1.player, this.location, out MachineOutputRule? rule, out MachineOutputTriggerRule? trigger, out _, out _)
            || rule == null
            || trigger == null)
            return false;

        int required = Math.Max(1, trigger.RequiredCount);
        if (mainItem.Count < required)
            return false;

        // Aciona a máquina sem mensagens nem sons de erro (eles ficam em PlaceInMachine, que não usamos).
        if (!this.machine.OutputMachine(this.data, rule, mainItem.Item, Game1.player, this.location, false))
            return false;

        // Entrada usada nas coletas seguintes (ex.: o Cristalário recomeça com a mesma gema).
        this.machine.lastInputItem.Value = mainItem.Item.getOne();
        MachineDataUtility.UpdateStats(this.data.StatsToIncrementWhenLoaded, mainItem.Item, required);

        // Consome manualmente; o que sobrar continua nos slots virtuais.
        mainItem.Item.Stack -= required;
        for (int i = 0; i < reqs.Count; i++)
            ((GameItem)additional[i]).Item.Stack -= reqs[i].RequiredCount;

        return true;
    }

    /// <summary>
    /// Coleta o produto inteiro como o jogo faz ao clicar na máquina (abordagem do Automate,
    /// <c>DataBasedObjectMachine.OnOutputCollected</c>): reinicia a máquina e aplica a regra <c>OutputCollected</c>.
    /// </summary>
    private void Collect(SObject item)
    {
        MachineDataUtility.UpdateStats(this.data.StatsToIncrementWhenHarvested, item, item.Stack);

        // Precisa vir antes da regra OutputCollected, que pode iniciar um novo produto.
        this.machine.heldObject.Value = null;
        this.machine.readyForHarvest.Value = false;
        this.machine.showNextIndex.Value = false;
        this.machine.ResetParentSheetIndex();

        if (MachineDataUtility.TryGetMachineOutputRule(this.machine, this.data, MachineOutputTrigger.OutputCollected, item.getOne(), null, this.location, out MachineOutputRule? collectedRule, out _, out _, out _))
            this.machine.OutputMachine(this.data, collectedRule, this.machine.lastInputItem.Value, null, this.location, false);

        if (this.machine.IsTapper()
            && this.location.terrainFeatures.TryGetValue(this.machine.TileLocation, out TerrainFeature? feature)
            && feature is Tree tree)
            tree.UpdateTapperProduct(this.machine, item);

        this.GrantHarvestExperience();
    }

    /// <summary>Experiência da coleta (<c>ExperienceGainOnHarvest</c>, ex.: "Farming 5"), como no jogo.</summary>
    private void GrantHarvestExperience()
    {
        if (string.IsNullOrWhiteSpace(this.data.ExperienceGainOnHarvest))
            return;

        string[] parts = this.data.ExperienceGainOnHarvest.Split(' ');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            int skill = Farmer.getSkillNumberFromName(parts[i]);
            if (skill != -1 && int.TryParse(parts[i + 1], out int amount))
                Game1.player.gainExperience(skill, amount);
        }
    }

    /// <summary>Um requisito por item de <c>AdditionalConsumedItems</c> (IDs repetidos são somados).</summary>
    private IReadOnlyList<AdditionalRequirement> BuildAdditionalRequirements()
    {
        if (this.data.AdditionalConsumedItems == null)
            return Array.Empty<AdditionalRequirement>();

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var consumed in this.data.AdditionalConsumedItems)
        {
            if (consumed == null || string.IsNullOrWhiteSpace(consumed.ItemId) || consumed.RequiredCount <= 0)
                continue;

            string id = ItemRegistry.QualifyItemId(consumed.ItemId) ?? consumed.ItemId;
            if (!counts.ContainsKey(id))
            {
                counts[id] = 0;
                order.Add(id);
            }

            counts[id] += consumed.RequiredCount;
        }

        return order
            .Select(id => new AdditionalRequirement(id, counts[id], item => item.QualifiedItemId == id))
            .ToList();
    }
}
