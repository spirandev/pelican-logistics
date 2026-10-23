using System.Collections.Generic;
using System.Linq;
using System.Text;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewValley;

namespace PelicanLogistics.Framework;

/// <summary>Comandos de console do mod (prefixo <c>pl_</c>).</summary>
internal sealed class ConsoleCommands
{
    /// <summary>Quantas unidades de cada item o <c>pl_give</c> entrega.</summary>
    private const int GiveAmount = 10;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly WorldRegistry registry;

    public ConsoleCommands(IModHelper helper, IMonitor monitor, WorldRegistry registry)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.registry = registry;
    }

    public void Register()
    {
        this.helper.ConsoleCommands.Add("pl_give", this.helper.Translation.Get("command.give.description", new { amount = GiveAmount }), this.Give);
        this.helper.ConsoleCommands.Add("pl_dump", this.helper.Translation.Get("command.dump.description"), this.Dump);
        this.helper.ConsoleCommands.Add("pl_inspect", this.helper.Translation.Get("command.inspect.description"), this.Inspect);
    }

    /// <summary>pl_give: coloca no inventário 10 de cada item do mod (blocos e acessórios).</summary>
    private void Give(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log(this.helper.Translation.Get("command.world-not-ready"), LogLevel.Warn);
            return;
        }

        IEnumerable<string> qualifiedIds = ItemIds.AllBlocks.Select(ItemIds.Qualified)
            .Concat(ItemIds.AllObjects.Select(ItemIds.QualifiedObject))
            .Append(ItemIds.QualifiedHelmet);
        foreach (string qualifiedId in qualifiedIds)
        {
            Item item = ItemRegistry.Create(qualifiedId, GiveAmount);
            Game1.player.addItemByMenuIfNecessary(item);
        }

        this.monitor.Log(this.helper.Translation.Get("command.give.done", new { amount = GiveAmount }), LogLevel.Info);
    }

    /// <summary>pl_dump: lista os nós do local atual (posição, tipo, direção, entrada, item e progresso).</summary>
    private void Dump(string command, string[] args)
    {
        GameLocation? location = Game1.currentLocation;
        if (!Context.IsWorldReady || location == null)
        {
            this.monitor.Log(this.helper.Translation.Get("command.world-not-ready"), LogLevel.Warn);
            return;
        }

        string key = WorldRegistry.KeyOf(location);
        LogisticsGrid? grid = this.registry.World.Get(key);
        if (grid == null || grid.TotalCount == 0)
        {
            this.monitor.Log(this.helper.Translation.Get("command.dump.empty", new { location = key }), LogLevel.Info);
            return;
        }

        var report = new StringBuilder();
        report.AppendLine(this.helper.Translation.Get("command.dump.header", new { location = key, count = grid.TotalCount }).ToString());
        foreach (LogisticsNode node in grid.Nodes.OrderBy(n => n.Position.Y).ThenBy(n => n.Position.X))
            report.AppendLine("  " + Describe(node));
        foreach (LogisticsNode node in grid.UndergroundNodes.OrderBy(n => n.Position.Y).ThenBy(n => n.Position.X))
            report.AppendLine("  " + Describe(node));

        this.monitor.Log(report.ToString().TrimEnd(), LogLevel.Info);
    }

    /// <summary>pl_inspect: o nó sob o cursor (tipo, direção, configuração, itens) e, se for uma máquina, os slots virtuais.</summary>
    private void Inspect(string command, string[] args)
    {
        GameLocation? location = Game1.currentLocation;
        if (!Context.IsWorldReady || location == null)
        {
            this.monitor.Log(this.helper.Translation.Get("command.world-not-ready"), LogLevel.Warn);
            return;
        }

        TilePos pos = WorldRegistry.ToTilePos(this.helper.Input.GetCursorPosition().Tile);
        LogisticsNode? node = this.registry.GetNode(location, pos);
        VirtualSlots? slots = this.registry.GetMachineSlots(location, pos);
        IEndpoint? endpoint = new GameEndpointProvider(location).GetEndpoint(pos);

        var report = new StringBuilder();
        if (node != null)
            report.AppendLine(this.helper.Translation.Get("command.inspect.node", new { node = Describe(node) }).ToString());
        if (this.registry.GetUnderground(location, pos) is { } below)
            report.AppendLine(this.helper.Translation.Get("command.inspect.node", new { node = Describe(below) }).ToString());

        if (endpoint is MachineEndpoint machine)
        {
            report.AppendLine(this.helper.Translation.Get("command.inspect.machine", new { name = machine.DisplayName, tile = pos }).ToString());
            this.DescribeSlots(report, machine, slots);
        }
        else if (slots != null)
        {
            // A máquina não está mais lá (ou não é suportada), mas ainda há itens guardados.
            report.AppendLine(this.helper.Translation.Get("command.inspect.orphan-slots", new { tile = pos }).ToString());
            foreach (ILogisticsItem item in slots.AllItems())
                report.AppendLine($"  {item.DisplayName} x{item.Count}");
        }

        if (report.Length == 0)
            report.Append(this.helper.Translation.Get("command.inspect.nothing", new { tile = pos }).ToString());

        this.monitor.Log(report.ToString().TrimEnd(), LogLevel.Info);
    }

    /// <summary>Linhas dos slots virtuais, ex.: "Principal: 3/5 Minério de cobre" (README "Slots virtuais").</summary>
    private void DescribeSlots(StringBuilder report, MachineEndpoint machine, VirtualSlots? slots)
    {
        if (slots?.Main != null)
            report.AppendLine(this.helper.Translation.Get("command.inspect.main", new { count = slots.Main.Count, required = slots.MainRequired, item = slots.Main.DisplayName }).ToString());
        else
            report.AppendLine(this.helper.Translation.Get("command.inspect.main-empty").ToString());

        foreach (AdditionalRequirement req in machine.AdditionalRequirements)
        {
            string name = ItemRegistry.GetDataOrErrorItem(req.Id).DisplayName;
            int count = slots?.CountOf(req) ?? 0;
            report.AppendLine(this.helper.Translation.Get("command.inspect.additional", new { count, required = req.RequiredCount, item = name }).ToString());
        }

        report.AppendLine(this.helper.Translation.Get(
            "command.inspect.state",
            new { idle = machine.IsIdle, ready = machine.PeekOutput()?.DisplayName ?? "-", repeating = machine.IsRepeating }).ToString());
    }

    private static string Describe(LogisticsNode node)
    {
        var line = new StringBuilder($"{node.Position} {node.Kind} facing={node.Facing}");
        switch (node)
        {
            case BeltNode belt:
                line.Append($" tier={belt.Tier} input={belt.InputSide}");
                break;
            case InserterNode inserter:
                line.Append($" tier={inserter.Tier} source={inserter.Source} slot={inserter.SlotMode}");
                if (inserter.Tier == TransferTier.Configurable)
                    line.Append($" amount={inserter.ConfiguredAmount}");
                line.Append($" cooldown={inserter.Cooldown} lastInput={inserter.LastInput?.ToString() ?? "-"}");
                line.Append($" inputs={string.Join(",", inserter.Inputs)}");
                if (inserter.Regulator != null)
                    line.Append($" regulator={DescribeRegulator(inserter.Regulator)}");
                break;
            case TunnelNode tunnel:
                line.Append($" role={tunnel.Role}");
                break;
            case UndergroundSegmentNode segment:
                line.Append($" input={segment.InputSide}");
                break;
            case FilterNode filter:
                line.Append($" rule={DescribeRule(filter.Rule)}");
                break;
            case SplitterNode splitter:
                line.Append($" inputs={DescribeSides(splitter.Inputs)} outputs={DescribeSides(splitter.Outputs)}");
                line.Append($" lastInput={splitter.LastInput?.ToString() ?? "-"} lastOutput={splitter.LastOutput?.ToString() ?? "-"}");
                break;
        }

        if (node.Accessory != AccessoryKind.None)
            line.Append($" accessory={node.Accessory}");
        if (node.Layer == NodeLayer.Underground)
            line.Append(" (subterrâneo)");

        // A mão do Inseridor aparece como "hand=[...]"; os espaços das esteiras, só como "[...]".
        string prefix = node is InserterNode ? " hand=" : " ";
        foreach (TransitSlot slot in node.Slots)
            line.Append(prefix).Append(DescribeSlot(slot));

        return line.ToString();
    }

    /// <summary>Regra do filtro em uma linha, ex.: "Common((O)388)" ou "Complete(Whitelist items=(O)388 categories=-75)".</summary>
    private static string DescribeRule(IFilterRule rule)
    {
        switch (rule)
        {
            case CommonFilterRule common:
                return $"Common({common.QualifiedItemId ?? "-"})";

            case CompleteFilterRule complete:
            {
                var text = new StringBuilder($"Complete({complete.Mode}");
                if (complete.ItemIds.Count > 0)
                    text.Append($" items={string.Join(",", complete.ItemIds)}");
                if (complete.Categories.Count > 0)
                    text.Append($" categories={string.Join(",", complete.Categories)}");
                if (complete.Qualities.Count > 0)
                    text.Append($" qualities={string.Join(",", complete.Qualities)}");
                if (complete.ContextTags.Count > 0)
                    text.Append($" tags={string.Join(",", complete.ContextTags)}");
                return text.Append(')').ToString();
            }

            default:
                return rule.GetType().Name;
        }
    }

    /// <summary>Regra do Regulador em uma linha, ex.: "General(50)" ou "PerItem((O)388:50,(O)390:100)".</summary>
    private static string DescribeRegulator(RegulatorRule rule)
    {
        return rule.Mode == RegulatorMode.General
            ? $"General({rule.GeneralLimit})"
            : $"PerItem({string.Join(",", rule.PerItemLimits.Select(p => $"{p.Key}:{p.Value}"))})";
    }

    private static string DescribeSides(IReadOnlyList<Direction> sides)
    {
        return sides.Count > 0 ? string.Join(",", sides) : "-";
    }

    /// <summary>Item, quantidade, progresso (0..600), lado de entrada e se está bloqueado; ou "[vazio]".</summary>
    private static string DescribeSlot(TransitSlot slot)
    {
        return slot.Item == null
            ? "[vazio]"
            : $"[{slot.Item.DisplayName} x{slot.Item.Count} {slot.Progress}/{Balance.End} entry={slot.EntrySide}{(slot.Blocked ? " blocked" : "")}]";
    }
}
