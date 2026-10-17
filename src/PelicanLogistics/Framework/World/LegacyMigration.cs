using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>
/// Migra os saves de teste das Fases 1 e 2: o Extrator foi unificado no Inseridor (Fase 3). A direção e o sentido
/// batem: o Extrator tinha a origem atrás e a saída na frente, como o Inseridor.
/// </summary>
/// <remarks>
/// Só os Extratores colocados no mapa são trocados. Os guardados em baús ou no inventário não são migrados e
/// viram itens de erro do jogo (limitação da alpha).
/// </remarks>
internal static class LegacyMigration
{
    /// <summary>
    /// Troca cada Extrator colocado no local por um Inseridor no mesmo tile, com a mesma direção e o mesmo
    /// "Retirar de". Chamado ao carregar o save, antes de registrar os nós. Retorna quantos foram trocados.
    /// </summary>
    public static int MigrateExtractors(GameLocation location)
    {
        List<KeyValuePair<Vector2, SObject>> extractors = location.objects.Pairs
            .Where(p => p.Value != null && p.Value.bigCraftable.Value && p.Value.ItemId == ItemIds.LegacyExtractor)
            .ToList();

        foreach ((Vector2 tile, SObject extractor) in extractors)
        {
            SObject inserter = ItemRegistry.Create<SObject>(ItemIds.Qualified(ItemIds.Inserter));
            inserter.TileLocation = tile;
            CopyModData(extractor, inserter, ModDataKeys.Facing, ModDataKeys.Facing);
            CopyModData(extractor, inserter, ModDataKeys.LegacyExtractorSource, ModDataKeys.PickupSource);

            location.objects.Remove(tile);
            location.objects.Add(tile, inserter);
        }

        return extractors.Count;
    }

    private static void CopyModData(SObject from, SObject to, string fromKey, string toKey)
    {
        if (from.modData.TryGetValue(fromKey, out string? value))
            to.modData[toKey] = value;
    }
}
