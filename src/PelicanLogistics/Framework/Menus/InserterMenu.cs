using System;
using PelicanLogistics.Core.Nodes;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Interface do Inseridor: qual slot virtual abastecer em máquinas (README "Slots virtuais").</summary>
internal sealed class InserterMenu : ConfigMenu
{
    public InserterMenu(ITranslationHelper i18n, InserterSlotMode current, Action<InserterSlotMode> onChanged)
        : base(
            i18n.Get("menu.inserter.title"),
            i18n.Get("menu.ok"),
            new ConfigRow[]
            {
                new CycleRow<InserterSlotMode>(
                    i18n.Get("menu.inserter.slot"),
                    new (InserterSlotMode, string)[]
                    {
                        (InserterSlotMode.Auto, i18n.Get("slot.auto")),
                        (InserterSlotMode.Main, i18n.Get("slot.main")),
                        (InserterSlotMode.Additional, i18n.Get("slot.additional")),
                    },
                    current,
                    onChanged),
            })
    {
    }
}
