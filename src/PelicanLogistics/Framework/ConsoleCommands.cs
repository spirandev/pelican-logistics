using System.Linq;
using System.Text;
using PelicanLogistics.Core.Nodes;
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
    }

    /// <summary>pl_give: coloca no inventário 10 de cada item do mod.</summary>
    private void Give(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log(this.helper.Translation.Get("command.world-not-ready"), LogLevel.Warn);
            return;
        }

        foreach (string id in ItemIds.AllBlocks)
        {
            Item item = ItemRegistry.Create(ItemIds.Qualified(id), GiveAmount);
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
        if (grid == null || grid.Count == 0)
        {
            this.monitor.Log(this.helper.Translation.Get("command.dump.empty", new { location = key }), LogLevel.Info);
            return;
        }

        var report = new StringBuilder();
        report.AppendLine(this.helper.Translation.Get("command.dump.header", new { location = key, count = grid.Count }).ToString());
        foreach (LogisticsNode node in grid.Nodes.OrderBy(n => n.Position.Y).ThenBy(n => n.Position.X))
            report.AppendLine("  " + Describe(node));

        this.monitor.Log(report.ToString().TrimEnd(), LogLevel.Info);
    }

    private static string Describe(LogisticsNode node)
    {
        var line = new StringBuilder($"{node.Position} {node.Kind} facing={node.Facing}");
        switch (node)
        {
            case BeltNode belt:
                line.Append($" tier={belt.Tier} input={belt.InputSide}");
                break;
            case ExtractorNode extractor:
                line.Append($" tier={extractor.Tier} cooldown={extractor.Cooldown}");
                break;
            case InserterNode inserter:
                line.Append($" tier={inserter.Tier} cooldown={inserter.Cooldown}");
                break;
        }

        if (node.Accessory != AccessoryKind.None)
            line.Append($" accessory={node.Accessory}");

        foreach (TransitSlot slot in node.Slots)
        {
            line.Append(slot.Item == null
                ? " [vazio]"
                : $" [{slot.Item.DisplayName} x{slot.Item.Count} {slot.Progress}/{Balance.End} entry={slot.EntrySide}{(slot.Blocked ? " blocked" : "")}]");
        }

        return line.ToString();
    }
}
