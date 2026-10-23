using HarmonyLib;
using PelicanLogistics.Core.Nodes;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Patches;

/// <summary>
/// Postfix em <c>Object.isPassable()</c>: o jogador passa por cima de uma esteira com Ponte (README "Ponte"). A colisão
/// do jogador com objetos (<c>GameLocation.isCollidingPosition</c>) pula os objetos para os quais <c>isPassable()</c>
/// devolve true. As ferramentas não consultam <c>isPassable</c>, então a esteira com ponte continua podendo ser quebrada.
/// </summary>
internal static class PassablePatch
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(SObject), nameof(SObject.isPassable)),
            postfix: new HarmonyMethod(typeof(PassablePatch), nameof(Postfix)));
    }

    /// <summary>
    /// Lê a ponte do <c>modData</c> do próprio objeto (gravado ao colocar a ponte), sem consultar a grade: o método é
    /// chamado muitas vezes por quadro, e o objeto pode estar em qualquer local.
    /// </summary>
    public static void Postfix(SObject __instance, ref bool __result)
    {
        if (__result || !__instance.bigCraftable.Value || !ItemIds.IsBelt(__instance.ItemId))
            return;

        if (__instance.modData.TryGetValue(ModDataKeys.Accessory, out string? accessory) && accessory == nameof(AccessoryKind.Bridge))
            __result = true;
    }
}
