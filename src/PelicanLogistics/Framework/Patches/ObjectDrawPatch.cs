using System;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Framework.Rendering;
using StardewModdingAPI;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Patches;

/// <summary>
/// Prefix em <c>Object.draw(SpriteBatch, int x, int y, float alpha)</c>: os blocos do mod não são
/// desenhados pelo jogo, e sim pelo <see cref="NodeRenderer"/>.
/// </summary>
internal static class ObjectDrawPatch
{
    private static NodeRenderer? renderer;
    private static IMonitor? monitor;
    private static bool loggedError;

    public static void Apply(Harmony harmony, NodeRenderer nodeRenderer, IMonitor log)
    {
        renderer = nodeRenderer;
        monitor = log;

        harmony.Patch(
            original: AccessTools.Method(typeof(SObject), nameof(SObject.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            prefix: new HarmonyMethod(typeof(ObjectDrawPatch), nameof(Prefix)));
    }

    /// <summary>
    /// Os argumentos são pegos pela posição (__0 = spriteBatch, __1 = x, __2 = y, __3 = alpha), para não
    /// depender dos nomes dos parâmetros no jogo.
    /// </summary>
    public static bool Prefix(SObject __instance, SpriteBatch __0, int __1, int __2, float __3)
    {
        if (renderer == null || !__instance.bigCraftable.Value || !ItemIds.IsOurBigCraftable(__instance.QualifiedItemId))
            return true;

        try
        {
            renderer.Draw(__0, __instance, __1, __2, __3);
            return false;
        }
        catch (Exception ex)
        {
            if (!loggedError)
            {
                loggedError = true;
                monitor?.Log($"Erro ao desenhar um bloco do mod; usando o desenho do jogo.\n{ex}", LogLevel.Error);
            }

            return true;
        }
    }
}
