using System;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>Cria os nós do Core a partir dos objetos do jogo e lê/grava a configuração no <c>modData</c>.</summary>
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
            ItemIds.Inserter => new InserterNode(pos, facing, TransferTier.Single)
            {
                Source = ReadPickupSource(obj),
                SlotMode = ReadInserterSlotMode(obj),
            },
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

    /// <summary>
    /// Apaga a configuração do bloco. Ao colocar, o jogo copia o <c>modData</c> do item segurado (<c>getOne</c>),
    /// então um bloco recolhido e colocado de novo traria a configuração antiga.
    /// </summary>
    public static void ResetConfig(SObject obj)
    {
        foreach (string key in ModDataKeys.Config)
            obj.modData.Remove(key);
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
