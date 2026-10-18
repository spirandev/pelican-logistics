using System;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Interface do Filtro Comum (README "Filtro"): escolhe o único item que passa, sem gastá-lo.</summary>
internal sealed class CommonFilterMenu : ConfigMenu
{
    /// <param name="currentId">ID qualificado do item configurado, ou null.</param>
    /// <param name="onChanged">Chamado a cada mudança, com o novo ID (null = nada configurado).</param>
    public CommonFilterMenu(ITranslationHelper i18n, string? currentId, Action<string?> onChanged)
        : base(
            i18n.Get("menu.filter-common.title"),
            i18n.Get("menu.ok"),
            new ConfigRow[]
            {
                new ItemPickerRow(i18n.Get("menu.filter.item"), currentId, i18n.Get("menu.filter.clear"), onChanged),
            })
    {
    }
}
