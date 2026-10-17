using System;
using PelicanLogistics.Core.Nodes;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Interface do Inseridor (README "Inseridores"): "Retirar de" vale quando atrás há uma máquina (README "Retirada
/// (Inseridor)"); "Colocar em" vale quando na frente há uma máquina (README "Slots virtuais").
/// </summary>
internal sealed class InserterMenu : ConfigMenu
{
    public InserterMenu(
        ITranslationHelper i18n,
        PickupSource currentSource,
        Action<PickupSource> onSourceChanged,
        InserterSlotMode currentSlot,
        Action<InserterSlotMode> onSlotChanged)
        : base(
            i18n.Get("menu.inserter.title"),
            i18n.Get("menu.ok"),
            new ConfigRow[]
            {
                new CycleRow<PickupSource>(
                    i18n.Get("menu.inserter.source"),
                    new (PickupSource, string)[]
                    {
                        (PickupSource.Product, i18n.Get("slot.product")),
                        (PickupSource.Main, i18n.Get("slot.main")),
                        (PickupSource.Additional, i18n.Get("slot.additional")),
                    },
                    currentSource,
                    onSourceChanged),
                new CycleRow<InserterSlotMode>(
                    i18n.Get("menu.inserter.slot"),
                    new (InserterSlotMode, string)[]
                    {
                        (InserterSlotMode.Auto, i18n.Get("slot.auto")),
                        (InserterSlotMode.Main, i18n.Get("slot.main")),
                        (InserterSlotMode.Additional, i18n.Get("slot.additional")),
                    },
                    currentSlot,
                    onSlotChanged),
            })
    {
    }
}
