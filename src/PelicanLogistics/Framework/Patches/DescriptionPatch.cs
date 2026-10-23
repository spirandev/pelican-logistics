using System;
using HarmonyLib;
using PelicanLogistics.Framework.Content;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Patches;

/// <summary>
/// Postfix em <c>Object.getDescription()</c>: com o Capacete de Engenheiro ativo, a descrição dos itens do mod (blocos,
/// acessórios e itens subterrâneos) ganha as linhas técnicas (README "Capacete de Engenheiro"). O jogo não guarda a
/// descrição de objetos em cache (ela é montada a cada chamada), então pôr ou tirar o capacete muda a dica na hora.
/// </summary>
internal static class DescriptionPatch
{
    /// <summary>Largura mínima do texto da descrição (a mesma de <c>Item.getDescriptionWidth</c> em pt-BR).</summary>
    private const int MinDescriptionWidth = 272;

    private static ITranslationHelper? i18n;
    private static IMonitor? monitor;
    private static bool loggedError;

    public static void Apply(Harmony harmony, ITranslationHelper translation, IMonitor log)
    {
        i18n = translation;
        monitor = log;
        harmony.Patch(
            original: AccessTools.Method(typeof(SObject), nameof(SObject.getDescription)),
            postfix: new HarmonyMethod(typeof(DescriptionPatch), nameof(Postfix)));
    }

    public static void Postfix(SObject __instance, ref string __result)
    {
        if (i18n == null || !EngineerHelmet.IsActive || !(ItemIds.IsOurBlock(__instance) || ItemIds.IsOurObject(__instance)))
            return;

        try
        {
            string? technical = TechnicalDescriptions.For(__instance.ItemId, i18n);
            if (technical == null)
                return;

            // A mesma largura que o jogo usa para quebrar a descrição (Item.getDescriptionWidth, que é protegido).
            int width = Math.Max(MinDescriptionWidth, (int)Game1.dialogueFont.MeasureString(__instance.DisplayName ?? "").X);
            __result += "\n\n" + Game1.parseText(technical, Game1.smallFont, width);
        }
        catch (Exception ex)
        {
            if (!loggedError)
            {
                loggedError = true;
                monitor?.Log($"Erro ao montar a descrição técnica de um item do mod.\n{ex}", LogLevel.Error);
            }
        }
    }
}
