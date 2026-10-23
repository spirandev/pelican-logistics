using System;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>Cria os nós do Core a partir dos objetos do jogo e lê/grava a configuração no <c>modData</c>.</summary>
/// <remarks>
/// O acessório (Ponte, Regulador) não é aplicado aqui: o <see cref="WorldRegistry"/> o coloca pela grade
/// (<c>LogisticsGrid.TrySetAccessory</c>), que valida o tipo do bloco.
/// </remarks>
internal static class NodeFactory
{
    /// <summary>Direção usada quando o objeto não tem direção gravada.</summary>
    public const Direction DefaultFacing = Direction.East;

    /// <summary>Cria o nó do bloco, ou null se o objeto não é um bloco do mod.</summary>
    public static LogisticsNode? Create(SObject obj, TilePos pos)
    {
        Direction facing = ReadFacing(obj);
        return obj.ItemId switch
        {
            ItemIds.Belt => new BeltNode(pos, facing, BeltTier.Basic),
            ItemIds.BeltFast => new BeltNode(pos, facing, BeltTier.Fast),
            ItemIds.BeltExpress => new BeltNode(pos, facing, BeltTier.Express),
            ItemIds.Inserter => CreateInserter(obj, pos, facing, TransferTier.Single),
            ItemIds.InserterStack => CreateInserter(obj, pos, facing, TransferTier.Stack),
            ItemIds.InserterConfigurable => CreateInserter(obj, pos, facing, TransferTier.Configurable),
            ItemIds.FilterCommon => new FilterNode(pos, facing, ReadCommonFilterRule(obj)),
            ItemIds.FilterComplete => new FilterNode(pos, facing, ReadCompleteFilterRule(obj)),
            ItemIds.Splitter => new SplitterNode(pos),
            ItemIds.Tunnel => new TunnelNode(pos, facing, ReadTunnelRole(obj)),
            _ => null,
        };
    }

    public static Direction ReadFacing(SObject obj)
    {
        return ReadEnum(obj, ModDataKeys.Facing, DefaultFacing);
    }

    public static void WriteFacing(SObject obj, Direction facing)
    {
        obj.modData[ModDataKeys.Facing] = facing.ToString();
    }

    public static PickupSource ReadPickupSource(SObject obj)
    {
        return ReadEnum(obj, ModDataKeys.PickupSource, PickupSource.Product);
    }

    public static void WritePickupSource(SObject obj, PickupSource source)
    {
        obj.modData[ModDataKeys.PickupSource] = source.ToString();
    }

    public static InserterSlotMode ReadInserterSlotMode(SObject obj)
    {
        return ReadEnum(obj, ModDataKeys.InserterSlotMode, InserterSlotMode.Auto);
    }

    public static void WriteInserterSlotMode(SObject obj, InserterSlotMode mode)
    {
        obj.modData[ModDataKeys.InserterSlotMode] = mode.ToString();
    }

    /// <summary>Quantidade por operação do Inseridor Configurável; ausente ou inválida → 1. O nó limita a 1..999.</summary>
    public static int ReadConfiguredAmount(SObject obj)
    {
        return int.TryParse(ReadRaw(obj, ModDataKeys.ConfiguredAmount), out int amount) ? amount : 1;
    }

    public static void WriteConfiguredAmount(SObject obj, int amount)
    {
        obj.modData[ModDataKeys.ConfiguredAmount] = amount.ToString();
    }

    /// <summary>Papel do Túnel; ausente ou inválido → entrada.</summary>
    public static TunnelRole ReadTunnelRole(SObject obj)
    {
        return ReadEnum(obj, ModDataKeys.TunnelRole, TunnelRole.Entrance);
    }

    public static void WriteTunnelRole(SObject obj, TunnelRole role)
    {
        obj.modData[ModDataKeys.TunnelRole] = role.ToString();
    }

    /// <summary>Acessório gravado no bloco; ausente ou inválido → nenhum.</summary>
    public static AccessoryKind ReadAccessory(SObject obj)
    {
        return ReadEnum(obj, ModDataKeys.Accessory, AccessoryKind.None);
    }

    /// <summary>Grava o acessório e, para o Regulador, a regra.</summary>
    public static void WriteAccessory(SObject obj, AccessoryKind kind, RegulatorRule? regulator)
    {
        ClearAccessory(obj);
        if (kind == AccessoryKind.None)
            return;

        obj.modData[ModDataKeys.Accessory] = kind.ToString();
        if (regulator != null)
            WriteRegulatorRule(obj, regulator);
    }

    /// <summary>Apaga o acessório e a regra do Regulador.</summary>
    public static void ClearAccessory(SObject obj)
    {
        obj.modData.Remove(ModDataKeys.Accessory);
        obj.modData.Remove(ModDataKeys.RegulatorRule);
    }

    /// <summary>Regra do Regulador; ausente ou inválida → regra padrão (Geral, 100).</summary>
    public static RegulatorRule ReadRegulatorRule(SObject obj)
    {
        return RegulatorRuleSerializer.Deserialize(ReadRaw(obj, ModDataKeys.RegulatorRule));
    }

    public static void WriteRegulatorRule(SObject obj, RegulatorRule rule)
    {
        obj.modData[ModDataKeys.RegulatorRule] = RegulatorRuleSerializer.Serialize(rule);
    }

    /// <summary>Regra do Filtro Comum; ausente ou inválida → filtro vazio (não deixa passar nada).</summary>
    public static CommonFilterRule ReadCommonFilterRule(SObject obj)
    {
        return FilterRuleSerializer.DeserializeCommon(ReadRaw(obj, ModDataKeys.FilterRule));
    }

    /// <summary>Regra do Filtro Completo; ausente ou inválida → lista branca vazia (não deixa passar nada).</summary>
    public static CompleteFilterRule ReadCompleteFilterRule(SObject obj)
    {
        return FilterRuleSerializer.DeserializeComplete(ReadRaw(obj, ModDataKeys.FilterRule));
    }

    public static void WriteFilterRule(SObject obj, IFilterRule rule)
    {
        obj.modData[ModDataKeys.FilterRule] = FilterRuleSerializer.Serialize(rule);
    }

    /// <summary>
    /// Apaga a configuração do bloco. Ao colocar, o jogo copia o <c>modData</c> do item segurado (<c>getOne</c>),
    /// então um bloco recolhido e colocado de novo traria a configuração antiga.
    /// </summary>
    public static void ResetConfig(SObject obj)
    {
        foreach (string key in ModDataKeys.Config)
            obj.modData.Remove(key);
    }

    private static InserterNode CreateInserter(SObject obj, TilePos pos, Direction facing, TransferTier tier)
    {
        return new InserterNode(pos, facing, tier)
        {
            Source = ReadPickupSource(obj),
            SlotMode = ReadInserterSlotMode(obj),
            ConfiguredAmount = ReadConfiguredAmount(obj),
        };
    }

    private static string? ReadRaw(SObject obj, string key)
    {
        return obj.modData.TryGetValue(key, out string? raw) ? raw : null;
    }

    /// <summary>Lê um enum gravado pelo nome; valor ausente ou inválido → 'fallback'.</summary>
    private static TEnum ReadEnum<TEnum>(SObject obj, string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        return obj.modData.TryGetValue(key, out string? raw)
            && Enum.TryParse(raw, out TEnum value)
            && Enum.IsDefined(value)
                ? value
                : fallback;
    }
}
