using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Persistence;

/// <summary>
/// Converte os blocos subterrâneos de uma grade (tipo, posição, direção e a regra dos filtros) para JSON e de volta. Os
/// blocos subterrâneos não são objetos do jogo, então o adaptador guarda esse JSON no <c>modData</c> do local (plano
/// mestre D19, D22). Ler nunca lança exceção: JSON ausente ou corrompido dá a lista vazia; entradas sem tipo (gravadas
/// antes da Fase 6c) são Esteiras Subterrâneas; entradas com tipo ou direção desconhecidos, ou com posição repetida, são
/// descartadas; a regra de filtro corrompida vira a regra vazia (como na superfície).
/// </summary>
public static class UndergroundLayoutSerializer
{
    public static string Serialize(LogisticsGrid grid)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));

        List<BlockEntry> entries = grid.UndergroundNodes
            .OrderBy(n => n.Position.Y)
            .ThenBy(n => n.Position.X)
            .Select(ToEntry)
            .ToList();
        return JsonSerializer.Serialize(entries);
    }

    public static IReadOnlyList<UndergroundBlockLayout> Deserialize(string? json)
    {
        var result = new List<UndergroundBlockLayout>();
        List<BlockEntry?>? entries = TryDeserialize(json);
        if (entries == null)
            return result;

        var seen = new HashSet<TilePos>();
        foreach (BlockEntry? entry in entries)
        {
            UndergroundBlockLayout? block = entry != null ? FromEntry(entry) : null;
            if (block != null && seen.Add(block.Position))
                result.Add(block);
        }

        return result;
    }

    private static BlockEntry ToEntry(LogisticsNode node)
    {
        var entry = new BlockEntry
        {
            Kind = node.Kind.ToString(),
            X = node.Position.X,
            Y = node.Position.Y,
            Facing = node.Facing.ToString(),
        };

        if (node is FilterNode filter)
        {
            entry.Complete = filter.IsComplete;
            entry.Rule = FilterRuleSerializer.Serialize(filter.Rule);
        }

        return entry;
    }

    /// <summary>O bloco descrito pela entrada, ou null se ela é inválida.</summary>
    private static UndergroundBlockLayout? FromEntry(BlockEntry entry)
    {
        NodeKind kind = NodeKind.UndergroundSegment;
        if (entry.Kind != null && (!Enum.TryParse(entry.Kind, out kind) || !IsUndergroundKind(kind)))
            return null;

        var pos = new TilePos(entry.X, entry.Y);
        if (kind == NodeKind.Splitter)
            return new UndergroundBlockLayout(pos, kind, Direction.North);

        if (!Enum.TryParse(entry.Facing, out Direction facing) || !Enum.IsDefined(facing))
            return null;

        if (kind == NodeKind.Filter)
        {
            IFilterRule rule = entry.Complete
                ? FilterRuleSerializer.DeserializeComplete(entry.Rule)
                : FilterRuleSerializer.DeserializeCommon(entry.Rule);
            return new UndergroundBlockLayout(pos, kind, facing, rule);
        }

        return new UndergroundBlockLayout(pos, kind, facing);
    }

    private static bool IsUndergroundKind(NodeKind kind)
    {
        return kind is NodeKind.UndergroundSegment or NodeKind.Filter or NodeKind.Splitter;
    }

    private static List<BlockEntry?>? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<BlockEntry?>>(json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Um bloco subterrâneo no JSON. O tipo e a direção vão pelo nome e são lidos à mão, para um valor inválido descartar só
    /// a entrada. No filtro, <see cref="Rule"/> é o JSON do <see cref="FilterRuleSerializer"/>.
    /// </summary>
    private sealed class BlockEntry
    {
        public string? Kind { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public string? Facing { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Complete { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Rule { get; set; }
    }
}
