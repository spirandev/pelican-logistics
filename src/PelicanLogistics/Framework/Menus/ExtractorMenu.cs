using System;
using PelicanLogistics.Core.Nodes;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Interface do Extrator: de onde retirar quando a origem é uma máquina (README "Retirada (Extrator)").</summary>
internal sealed class ExtractorMenu : ConfigMenu
{
    public ExtractorMenu(ITranslationHelper i18n, ExtractorSource current, Action<ExtractorSource> onChanged)
        : base(
            i18n.Get("menu.extractor.title"),
            i18n.Get("menu.ok"),
            new ConfigRow[]
            {
                new CycleRow<ExtractorSource>(
                    i18n.Get("menu.extractor.source"),
                    new (ExtractorSource, string)[]
                    {
                        (ExtractorSource.Product, i18n.Get("slot.product")),
                        (ExtractorSource.Main, i18n.Get("slot.main")),
                        (ExtractorSource.Additional, i18n.Get("slot.additional")),
                    },
                    current,
                    onChanged),
            })
    {
    }
}
