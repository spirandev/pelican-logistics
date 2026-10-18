using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Nodes;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework;

/// <summary>IDs dos itens do mod.</summary>
internal static class ItemIds
{
    public const string Prefix = "spirandev.PelicanLogistics_";
    public const string Belt = Prefix + "Belt";
    public const string BeltFast = Prefix + "BeltFast";
    public const string BeltExpress = Prefix + "BeltExpress";
    public const string Inserter = Prefix + "Inserter";
    public const string InserterStack = Prefix + "InserterStack";
    public const string InserterConfigurable = Prefix + "InserterConfigurable";
    public const string FilterCommon = Prefix + "FilterCommon";
    public const string FilterComplete = Prefix + "FilterComplete";
    public const string Splitter = Prefix + "Splitter";
    public const string UndergroundBelt = Prefix + "UndergroundBelt";

    /// <summary>Acessórios: objetos comuns (<c>Data/Objects</c>), não colocáveis pelo jogo.</summary>
    public const string Bridge = Prefix + "Bridge";

    public const string Regulator = Prefix + "Regulator";

    /// <summary>Prefixo do tipo Big Craftable nos IDs qualificados.</summary>
    private const string BigCraftableTypePrefix = "(BC)";

    /// <summary>Prefixo do tipo objeto comum nos IDs qualificados.</summary>
    private const string ObjectTypePrefix = "(O)";

    /// <summary>Todos os blocos (Big Craftables) do mod, na ordem da loja e do <c>pl_give</c>.</summary>
    public static readonly IReadOnlyList<string> AllBlocks = new[]
    {
        Belt, BeltFast, BeltExpress, Inserter, InserterStack, InserterConfigurable, FilterCommon, FilterComplete, Splitter, UndergroundBelt,
    };

    /// <summary>Todos os acessórios (objetos comuns) do mod, na ordem da loja e do <c>pl_give</c>.</summary>
    public static readonly IReadOnlyList<string> AllAccessories = new[] { Bridge, Regulator };

    /// <summary><see cref="AllBlocks"/> para consulta rápida (usado no desenho, a cada quadro).</summary>
    private static readonly HashSet<string> Blocks = new(AllBlocks, StringComparer.Ordinal);

    /// <summary>IDs qualificados de <see cref="AllBlocks"/>.</summary>
    private static readonly HashSet<string> QualifiedBlocks = new(AllBlocks.Select(Qualified), StringComparer.Ordinal);

    /// <summary>IDs qualificados de <see cref="AllAccessories"/>.</summary>
    private static readonly HashSet<string> QualifiedAccessories = new(AllAccessories.Select(QualifiedAccessory), StringComparer.Ordinal);

    /// <summary>Chave do item nos textos do i18n (ex.: "belt" para <c>item.belt.name</c>).</summary>
    private static readonly IReadOnlyDictionary<string, string> TranslationKeys = new Dictionary<string, string>
    {
        [Belt] = "belt",
        [BeltFast] = "belt-fast",
        [BeltExpress] = "belt-express",
        [Inserter] = "inserter",
        [InserterStack] = "inserter-stack",
        [InserterConfigurable] = "inserter-configurable",
        [FilterCommon] = "filter-common",
        [FilterComplete] = "filter-complete",
        [Splitter] = "splitter",
        [UndergroundBelt] = "underground-belt",
        [Bridge] = "bridge",
        [Regulator] = "regulator",
    };

    /// <summary>ID qualificado de um bloco (Big Craftable).</summary>
    public static string Qualified(string id) => BigCraftableTypePrefix + id;

    /// <summary>ID qualificado de um acessório (objeto comum).</summary>
    public static string QualifiedAccessory(string id) => ObjectTypePrefix + id;

    /// <summary>True se o ID qualificado é de um Big Craftable registrado pelo mod.</summary>
    public static bool IsOurBigCraftable(string? qualifiedItemId)
    {
        return qualifiedItemId != null && QualifiedBlocks.Contains(qualifiedItemId);
    }

    /// <summary>True se o item é um bloco do mod.</summary>
    public static bool IsOurBlock(Item? item)
    {
        return item is SObject obj && obj.bigCraftable.Value && IsOurBigCraftable(obj.QualifiedItemId);
    }

    /// <summary>True se o item é um acessório do mod (Ponte ou Regulador).</summary>
    public static bool IsOurAccessory(Item? item)
    {
        return item is SObject obj && !obj.bigCraftable.Value && QualifiedAccessories.Contains(obj.QualifiedItemId);
    }

    /// <summary>True se o bloco (ID não qualificado) tem direção e pode ser girado. O Divisor não tem.</summary>
    public static bool IsDirectional(string itemId)
    {
        return itemId != Splitter && Blocks.Contains(itemId);
    }

    /// <summary>True se o bloco (ID não qualificado) é uma esteira de qualquer velocidade.</summary>
    public static bool IsBelt(string itemId)
    {
        return itemId is Belt or BeltFast or BeltExpress;
    }

    /// <summary>True se o bloco (ID não qualificado) é um Inseridor de qualquer versão.</summary>
    public static bool IsInserter(string itemId)
    {
        return itemId is Inserter or InserterStack or InserterConfigurable;
    }

    /// <summary>Acessório do item (ID não qualificado), ou <see cref="AccessoryKind.None"/>.</summary>
    public static AccessoryKind AccessoryKindOf(string itemId)
    {
        return itemId switch
        {
            Bridge => AccessoryKind.Bridge,
            Regulator => AccessoryKind.Regulator,
            _ => AccessoryKind.None,
        };
    }

    /// <summary>Cria um item do acessório (ex.: para devolver ao jogador ou soltar no chão).</summary>
    public static Item CreateAccessory(AccessoryKind kind)
    {
        string id = kind switch
        {
            AccessoryKind.Bridge => Bridge,
            AccessoryKind.Regulator => Regulator,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Acessório inválido."),
        };

        return ItemRegistry.Create(QualifiedAccessory(id));
    }

    /// <summary>Chave do item no i18n, ex.: "belt".</summary>
    public static string TranslationKeyOf(string id)
    {
        return TranslationKeys[id];
    }
}
