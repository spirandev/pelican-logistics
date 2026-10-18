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
internal sealed class CompleteFilterMenu : ConfigMenu
{
    /// <summary>Separador das context tags no campo de texto.</summary>
    private const char TagSeparator = ',';

    public CompleteFilterMenu(ITranslationHelper i18n, CompleteFilterRule rule, Action onChanged)
        : base(i18n.Get("menu.filter-complete.title"), i18n.Get("menu.ok"), CreateRows(i18n, rule, onChanged))
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

    private static ConfigRow[] CreateRows(ITranslationHelper i18n, CompleteFilterRule rule, Action onChanged)
    {
        var qualities = new (int, string)[]
        {
            (0, i18n.Get("quality.normal")),
            (1, i18n.Get("quality.silver")),
            (2, i18n.Get("quality.gold")),
            (4, i18n.Get("quality.iridium")),
        };

        return new ConfigRow[]
        {
            new CycleRow<FilterMode>(
                i18n.Get("menu.filter.mode"),
                new (FilterMode, string)[]
                {
                    (FilterMode.Whitelist, i18n.Get("filter-mode.whitelist")),
                    (FilterMode.Blacklist, i18n.Get("filter-mode.blacklist")),
                },
                rule.Mode,
                mode =>
                {
                    rule.Mode = mode;
                    onChanged();
                }),
            new ItemListRow(
                i18n.Get("menu.filter.items"),
                rule.ItemIds,
                ids =>
                {
                    rule.ItemIds = ids.ToList();
                    onChanged();
                }),
            new ToggleGroupRow<int>(
                i18n.Get("menu.filter.quality"),
                qualities,
                rule.Qualities,
                selected =>
                {
                    rule.Qualities = selected.ToList();
                    onChanged();
                }),
            new ToggleGroupRow<int>(
                i18n.Get("menu.filter.categories"),
                FilterCategories.All.Select(c => (c.Category, i18n.Get(c.TranslationKey).ToString())).ToList(),
                rule.Categories,
                selected =>
                {
                    rule.Categories = selected.ToList();
                    onChanged();
                }),
            new TextRow(
                i18n.Get("menu.filter.tags"),
                string.Join(TagSeparator + " ", rule.ContextTags),
                text =>
                {
                    rule.ContextTags = ParseTags(text);
                    onChanged();
                }),
        };
    }
}
