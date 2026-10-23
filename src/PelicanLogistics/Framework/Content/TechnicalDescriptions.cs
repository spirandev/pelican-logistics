using System.Collections.Generic;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Core.Simulation;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Content;

/// <summary>
/// Linhas técnicas acrescentadas à descrição dos itens do mod quando o Capacete de Engenheiro está ativo (README "Capacete
/// de Engenheiro"). Os números vêm do <see cref="TechnicalInfo"/> e os textos do i18n.
/// </summary>
internal static class TechnicalDescriptions
{
    /// <summary>As linhas técnicas do item (ID não qualificado), ou null se ele não tem.</summary>
    public static string? For(string itemId, ITranslationHelper i18n)
    {
        var lines = new List<string>();
        switch (itemId)
        {
            case ItemIds.Belt:
                lines.Add(Speed(i18n, TechnicalInfo.BeltTilesPerSecond(BeltTier.Basic)));
                break;
            case ItemIds.BeltFast:
                lines.Add(Speed(i18n, TechnicalInfo.BeltTilesPerSecond(BeltTier.Fast)));
                break;
            case ItemIds.BeltExpress:
                lines.Add(Speed(i18n, TechnicalInfo.BeltTilesPerSecond(BeltTier.Express)));
                break;
            case ItemIds.FilterCommon:
            case ItemIds.FilterComplete:
                lines.Add(Speed(i18n, TechnicalInfo.FilterTilesPerSecond));
                lines.Add(i18n.Get("tech.one-stack"));
                break;
            case ItemIds.Splitter:
                lines.Add(Speed(i18n, TechnicalInfo.SplitterTilesPerSecond));
                lines.Add(i18n.Get("tech.one-stack"));
                break;
            case ItemIds.Inserter:
            case ItemIds.InserterStack:
            case ItemIds.InserterConfigurable:
                lines.Add(i18n.Get("tech.inserter-cycle", new { seconds = Number(i18n, TechnicalInfo.InserterCycleSeconds) }));
                lines.Add(itemId switch
                {
                    ItemIds.Inserter => i18n.Get("tech.inserter-single"),
                    ItemIds.InserterStack => i18n.Get("tech.inserter-stack"),
                    _ => i18n.Get("tech.inserter-configurable", new { max = Balance.MaxConfigurableAmount }),
                });
                break;
            case ItemIds.Tunnel:
                lines.Add(Speed(i18n, TechnicalInfo.UndergroundTilesPerSecond));
                lines.Add(i18n.Get("tech.tunnel"));
                break;
            case ItemIds.UndergroundBelt:
                lines.Add(Speed(i18n, TechnicalInfo.UndergroundTilesPerSecond));
                lines.Add(i18n.Get("tech.underground-belt"));
                break;
            case ItemIds.Bridge:
                lines.Add(i18n.Get("tech.bridge"));
                break;
            case ItemIds.Regulator:
                lines.Add(i18n.Get("tech.regulator", new { max = Balance.MaxRegulatorLimit.ToString("N0", Culture(i18n)) }));
                break;
            default:
                return null;
        }

        return string.Join("\n", lines);
    }

    private static string Speed(ITranslationHelper i18n, double tilesPerSecond)
    {
        return i18n.Get("tech.speed", new { speed = Number(i18n, tilesPerSecond) });
    }

    /// <summary>Número com até 2 casas decimais, na cultura do idioma do i18n (ex.: "0,5" em pt-BR).</summary>
    private static string Number(ITranslationHelper i18n, double value)
    {
        return value.ToString("0.##", Culture(i18n));
    }

    private static System.Globalization.CultureInfo Culture(ITranslationHelper i18n)
    {
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(i18n.Get("tech.culture"));
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return System.Globalization.CultureInfo.InvariantCulture;
        }
    }
}
