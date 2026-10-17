using System;
using System.Collections.Generic;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework;

/// <summary>IDs dos itens do mod.</summary>
internal static class ItemIds
{
    public const string Prefix = "spirandev.PelicanLogistics_";
    public const string Belt = Prefix + "Belt";
    public const string Extractor = Prefix + "Extractor";
    public const string Inserter = Prefix + "Inserter";

    /// <summary>Prefixo do tipo Big Craftable nos IDs qualificados.</summary>
    private const string BigCraftableTypePrefix = "(BC)";

    /// <summary>Todos os blocos (Big Craftables) do mod, na ordem da loja e do <c>pl_give</c>.</summary>
    public static readonly IReadOnlyList<string> AllBlocks = new[] { Belt, Extractor, Inserter };

    /// <summary>Chave do item nos textos do i18n (ex.: "belt" para <c>item.belt.name</c>).</summary>
    private static readonly IReadOnlyDictionary<string, string> TranslationKeys = new Dictionary<string, string>
    {
        [Belt] = "belt",
        [Extractor] = "extractor",
        [Inserter] = "inserter",
    };

    public static string Qualified(string id) => BigCraftableTypePrefix + id;

    /// <summary>True se o ID qualificado é de um Big Craftable do mod.</summary>
    public static bool IsOurBigCraftable(string? qualifiedItemId)
    {
        return qualifiedItemId != null && qualifiedItemId.StartsWith(BigCraftableTypePrefix + Prefix, StringComparison.Ordinal);
    }

    /// <summary>True se o item é um bloco do mod.</summary>
    public static bool IsOurBlock(Item? item)
    {
        return item is SObject obj && obj.bigCraftable.Value && IsOurBigCraftable(obj.QualifiedItemId);
    }

    /// <summary>True se o bloco (ID não qualificado) tem direção e pode ser girado.</summary>
    public static bool IsDirectional(string itemId)
    {
        return itemId is Belt or Extractor or Inserter;
    }

    /// <summary>Chave do item no i18n, ex.: "belt".</summary>
    public static string TranslationKeyOf(string id)
    {
        return TranslationKeys[id];
    }
}
