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
    public const string Tunnel = Prefix + "Tunnel";

    /// <summary>Acessórios: objetos comuns (<c>Data/Objects</c>), não colocáveis pelo jogo.</summary>
    public const string Bridge = Prefix + "Bridge";

    public const string Regulator = Prefix + "Regulator";

    /// <summary>Esteira Subterrânea: objeto comum não colocável pelo jogo; o mod cria um segmento na camada subterrânea (plano mestre D19).</summary>
    public const string UndergroundBelt = Prefix + "UndergroundBelt";

    /// <summary>Divisor e Filtros Subterrâneos: objetos comuns não colocáveis pelo jogo, como a Esteira Subterrânea (plano mestre D22).</summary>
    public const string UndergroundSplitter = Prefix + "UndergroundSplitter";

    public const string UndergroundFilterCommon = Prefix + "UndergroundFilterCommon";

    public const string UndergroundFilterComplete = Prefix + "UndergroundFilterComplete";

    /// <summary>Capacete de Engenheiro: chapéu (<c>Data/Hats</c>), plano mestre D18.</summary>
    public const string EngineerHelmet = Prefix + "EngineerHelmet";

    /// <summary>Prefixo do tipo Big Craftable nos IDs qualificados.</summary>
    private const string BigCraftableTypePrefix = "(BC)";

    /// <summary>Prefixo do tipo objeto comum nos IDs qualificados.</summary>
    private const string ObjectTypePrefix = "(O)";

    /// <summary>Prefixo do tipo chapéu nos IDs qualificados.</summary>
    private const string HatTypePrefix = "(H)";

    /// <summary>Todos os blocos (Big Craftables) do mod, na ordem da loja e do <c>pl_give</c>.</summary>
    public static readonly IReadOnlyList<string> AllBlocks = new[]
    {
        Belt, BeltFast, BeltExpress, Inserter, InserterStack, InserterConfigurable, FilterCommon, FilterComplete, Splitter, Tunnel,
    };

    /// <summary>Os acessórios (Ponte e Regulador).</summary>
    public static readonly IReadOnlyList<string> AllAccessories = new[] { Bridge, Regulator };

    /// <summary>Todos os objetos comuns do mod (<c>Data/Objects</c>), na ordem da loja e do <c>pl_give</c>.</summary>
    public static readonly IReadOnlyList<string> AllObjects = new[]
    {
        Bridge, Regulator, UndergroundBelt, UndergroundSplitter, UndergroundFilterCommon, UndergroundFilterComplete,
    };

    /// <summary>Os itens subterrâneos (plano mestre D22): viram blocos da camada subterrânea pelo clique.</summary>
    public static readonly IReadOnlyList<string> AllUnderground = new[]
    {
        UndergroundBelt, UndergroundSplitter, UndergroundFilterCommon, UndergroundFilterComplete,
    };

    /// <summary><see cref="AllBlocks"/> para consulta rápida (usado no desenho, a cada quadro).</summary>
    private static readonly HashSet<string> Blocks = new(AllBlocks, StringComparer.Ordinal);

    /// <summary>IDs qualificados de <see cref="AllBlocks"/>.</summary>
    private static readonly HashSet<string> QualifiedBlocks = new(AllBlocks.Select(Qualified), StringComparer.Ordinal);

    /// <summary>IDs qualificados de <see cref="AllObjects"/>.</summary>
    private static readonly HashSet<string> QualifiedObjects = new(AllObjects.Select(QualifiedObject), StringComparer.Ordinal);

    /// <summary>IDs qualificados de <see cref="AllUnderground"/>.</summary>
    private static readonly HashSet<string> QualifiedUnderground = new(AllUnderground.Select(QualifiedObject), StringComparer.Ordinal);

    /// <summary>IDs qualificados de <see cref="AllAccessories"/>.</summary>
    private static readonly HashSet<string> QualifiedAccessories = new(AllAccessories.Select(QualifiedObject), StringComparer.Ordinal);

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
        [Tunnel] = "tunnel",
        [Bridge] = "bridge",
        [Regulator] = "regulator",
        [UndergroundBelt] = "underground-belt",
        [UndergroundSplitter] = "underground-splitter",
        [UndergroundFilterCommon] = "underground-filter-common",
        [UndergroundFilterComplete] = "underground-filter-complete",
        [EngineerHelmet] = "engineer-helmet",
    };

    /// <summary>ID qualificado de um bloco (Big Craftable).</summary>
    public static string Qualified(string id) => BigCraftableTypePrefix + id;

    /// <summary>ID qualificado de um objeto comum do mod (acessórios e itens subterrâneos).</summary>
    public static string QualifiedObject(string id) => ObjectTypePrefix + id;

    /// <summary>ID qualificado do Capacete de Engenheiro.</summary>
    public static string QualifiedHelmet => HatTypePrefix + EngineerHelmet;

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

    /// <summary>True se o item é um objeto comum do mod (Ponte, Regulador ou item subterrâneo).</summary>
    public static bool IsOurObject(Item? item)
    {
        return item is SObject obj && !obj.bigCraftable.Value && QualifiedObjects.Contains(obj.QualifiedItemId);
    }

    /// <summary>
    /// True se o item é um item subterrâneo: Esteira, Divisor, Filtro Comum ou Filtro Completo Subterrâneo (o objeto que o
    /// mod transforma em bloco da camada subterrânea).
    /// </summary>
    public static bool IsUndergroundItem(Item? item)
    {
        return item is SObject obj && !obj.bigCraftable.Value && QualifiedUnderground.Contains(obj.QualifiedItemId);
    }

    /// <summary>True se o item subterrâneo (ID não qualificado) tem direção: todos menos o Divisor Subterrâneo.</summary>
    public static bool IsUndergroundDirectional(string itemId)
    {
        return itemId is UndergroundBelt or UndergroundFilterCommon or UndergroundFilterComplete;
    }

    /// <summary>O item subterrâneo (ID não qualificado) que corresponde ao bloco subterrâneo.</summary>
    public static string UndergroundItemOf(LogisticsNode node)
    {
        return node switch
        {
            UndergroundSegmentNode => UndergroundBelt,
            SplitterNode => UndergroundSplitter,
            FilterNode { IsComplete: true } => UndergroundFilterComplete,
            FilterNode => UndergroundFilterCommon,
            _ => throw new ArgumentException($"{node.Kind} não é um bloco subterrâneo.", nameof(node)),
        };
    }

    /// <summary>True se o item é o Capacete de Engenheiro.</summary>
    public static bool IsEngineerHelmet(Item? item)
    {
        return item?.QualifiedItemId == QualifiedHelmet;
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

        return ItemRegistry.Create(QualifiedObject(id));
    }

    /// <summary>Chave do item no i18n, ex.: "belt".</summary>
    public static string TranslationKeyOf(string id)
    {
        return TranslationKeys[id];
    }
}
