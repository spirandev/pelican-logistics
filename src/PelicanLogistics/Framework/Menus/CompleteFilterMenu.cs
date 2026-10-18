using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Filters;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Interface do Filtro Completo (README "Filtros e roteamento"): modo, lista de itens, qualidades, categorias e context
/// tags. Cada alteração muda a <see cref="CompleteFilterRule"/> recebida e chama 'onChanged' para gravar.
/// </summary>
/// <remarks>
/// Duas colunas, para caber em telas de 1280×720 e 1366×768: à esquerda modo, qualidade e context tags; à direita a
/// lista de itens com o inventário e as categorias.
/// </remarks>
internal sealed class CompleteFilterMenu : ConfigMenu
{
    /// <summary>Separador das context tags no campo de texto.</summary>
    private const char TagSeparator = ',';

    /// <summary>Largura da coluna da esquerda.</summary>
    private const int LeftColumnWidth = 360;

    public CompleteFilterMenu(ITranslationHelper i18n, CompleteFilterRule rule, Action onChanged)
        : base(i18n.Get("menu.filter-complete.title"), i18n.Get("menu.ok"), CreateColumns(i18n, rule, onChanged))
    {
    }

    /// <summary>Context tags digitadas: separadas por vírgula, sem espaços nas pontas, em minúsculas (como as do jogo), sem repetir.</summary>
    public static List<string> ParseTags(string text)
    {
        return text
            .Split(TagSeparator)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();
    }

    private static ConfigColumn[] CreateColumns(ITranslationHelper i18n, CompleteFilterRule rule, Action onChanged)
    {
        var qualities = new (int, string)[]
        {
            (0, i18n.Get("quality.normal")),
            (1, i18n.Get("quality.silver")),
            (2, i18n.Get("quality.gold")),
            (4, i18n.Get("quality.iridium")),
        };

        var mode = new CycleRow<FilterMode>(
            i18n.Get("menu.filter.mode"),
            new (FilterMode, string)[]
            {
                (FilterMode.Whitelist, i18n.Get("filter-mode.whitelist")),
                (FilterMode.Blacklist, i18n.Get("filter-mode.blacklist")),
            },
            rule.Mode,
            value =>
            {
                rule.Mode = value;
                onChanged();
            });

        var items = new ItemListRow(
            i18n.Get("menu.filter.items"),
            rule.ItemIds,
            ids =>
            {
                rule.ItemIds = ids.ToList();
                onChanged();
            });

        var quality = new ToggleGroupRow<int>(
            i18n.Get("menu.filter.quality"),
            qualities,
            rule.Qualities,
            selected =>
            {
                rule.Qualities = selected.ToList();
                onChanged();
            });

        var categories = new ToggleGroupRow<int>(
            i18n.Get("menu.filter.categories"),
            FilterCategories.All.Select(c => (c.Category, i18n.Get(c.TranslationKey).ToString())).ToList(),
            rule.Categories,
            selected =>
            {
                rule.Categories = selected.ToList();
                onChanged();
            });

        var tags = new TextRow(
            i18n.Get("menu.filter.tags"),
            string.Join(TagSeparator + " ", rule.ContextTags),
            text =>
            {
                rule.ContextTags = ParseTags(text);
                onChanged();
            });

        return new[]
        {
            new ConfigColumn(new ConfigRow[] { mode, quality, tags }, LeftColumnWidth),
            new ConfigColumn(new ConfigRow[] { items, categories }, 0),
        };
    }
}
