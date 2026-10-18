using System;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Escolha, no inventário, do item a acrescentar à lista do Regulador (modo Por item).</summary>
internal sealed class RegulatorItemPickerMenu : ConfigMenu
{
    /// <param name="onPicked">Chamado com o ID qualificado do item escolhido.</param>
    /// <param name="onCancel">Chamado ao clicar em "Cancelar" (volta para a interface do Inseridor).</param>
    public RegulatorItemPickerMenu(ITranslationHelper i18n, Action<string> onPicked, Action onCancel)
        : base(
            i18n.Get("menu.regulator.title"),
            i18n.Get("menu.ok"),
            new ConfigRow[]
            {
                new RegulatorItemPickerRow(i18n.Get("menu.regulator.pick"), i18n.Get("menu.regulator.cancel"), onPicked, onCancel),
            })
    {
    }
}
