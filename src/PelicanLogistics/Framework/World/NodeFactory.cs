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
            ItemIds.Extractor => new ExtractorNode(pos, facing, TransferTier.Single),
            ItemIds.Inserter => new InserterNode(pos, facing, TransferTier.Single),
            _ => null,
        };
    }

    public static bool HasFacing(SObject obj)
    {
        return obj.modData.ContainsKey(ModDataKeys.Facing);
    }

    public static Direction ReadFacing(SObject obj)
    {
        return obj.modData.TryGetValue(ModDataKeys.Facing, out string? raw)
            && Enum.TryParse(raw, out Direction facing)
            && Enum.IsDefined(facing)
                ? facing
                : DefaultFacing;
    }

    public static void WriteFacing(SObject obj, Direction facing)
    {
        obj.modData[ModDataKeys.Facing] = facing.ToString();
    }
}
