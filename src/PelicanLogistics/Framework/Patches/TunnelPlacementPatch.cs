using HarmonyLib;
using Microsoft.Xna.Framework;
using PelicanLogistics.Framework.World;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Patches;

/// <summary>
/// Postfix em <c>Object.canBePlacedHere(GameLocation, Vector2, CollisionMask, bool)</c>: o Túnel só pode ser colocado com
/// o Capacete de Engenheiro na cabeça e num tile sem Esteira Subterrânea (plano mestre D18, D21). O jogo consulta esse
/// método ao colocar um objeto (botão de ação ou de usar ferramenta, via <c>Utility.playerCanPlaceItemHere</c>) e na
/// prévia de colocação. A mensagem para o jogador é dada pelo <c>UndergroundController</c>, porque o jogo chama o método
/// sem pedir erro.
/// </summary>
internal static class TunnelPlacementPatch
{
    private static WorldRegistry? registry;

    public static void Apply(Harmony harmony, WorldRegistry worldRegistry)
    {
        registry = worldRegistry;
        harmony.Patch(
            original: AccessTools.Method(typeof(SObject), nameof(SObject.canBePlacedHere)),
            postfix: new HarmonyMethod(typeof(TunnelPlacementPatch), nameof(Postfix)));
    }

    /// <summary>Argumentos pela posição (__0 = local, __1 = tile), para não depender dos nomes dos parâmetros no jogo.</summary>
    public static void Postfix(SObject __instance, GameLocation __0, Vector2 __1, ref bool __result)
    {
        if (!__result || !__instance.bigCraftable.Value || __instance.ItemId != ItemIds.Tunnel)
            return;

        if (!EngineerHelmet.IsWorn || (registry != null && !registry.CanPlaceTunnel(__0, WorldRegistry.ToTilePos(__1))))
            __result = false;
    }
}
