using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Persistence;

/// <summary>
/// Converte as Esteiras Subterrâneas de uma grade (posição e direção) para JSON e de volta. As Esteiras Subterrâneas não
/// são objetos do jogo, então o adaptador guarda esse JSON no <c>modData</c> do local (plano mestre D19). Ler nunca
/// lança exceção: JSON ausente ou corrompido dá a lista vazia; entradas com direção desconhecida ou posição repetida são
/// descartadas.
/// </summary>
public static class UndergroundLayoutSerializer
{
    public static string Serialize(LogisticsGrid grid)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));

        List<SegmentEntry> entries = grid.UndergroundNodes
            .OrderBy(n => n.Position.Y)
            .ThenBy(n => n.Position.X)
            .Select(n => new SegmentEntry { X = n.Position.X, Y = n.Position.Y, Facing = n.Facing.ToString() })
            .ToList();
        return JsonSerializer.Serialize(entries);
    }

    public static IReadOnlyList<(TilePos Position, Direction Facing)> Deserialize(string? json)
    {
        var result = new List<(TilePos, Direction)>();
        List<SegmentEntry?>? entries = TryDeserialize(json);
        if (entries == null)
            return result;

        var seen = new HashSet<TilePos>();
        foreach (SegmentEntry? entry in entries)
        {
            if (entry == null || !Enum.TryParse(entry.Facing, out Direction facing) || !Enum.IsDefined(facing))
                continue;

            var pos = new TilePos(entry.X, entry.Y);
            if (seen.Add(pos))
                result.Add((pos, facing));
        }

        return result;
    }

    private static List<SegmentEntry?>? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<SegmentEntry?>>(json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Uma Esteira Subterrânea no JSON. A direção vai pelo nome e é lida à mão, para uma direção inválida descartar só a entrada.</summary>
    private sealed class SegmentEntry
    {
        public int X { get; set; }

        public int Y { get; set; }

        public string? Facing { get; set; }
    }
}
