using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;
using PelicanLogistics.Core.Simulation;
using StardewModdingAPI;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Interface do Inseridor (README "Inseridores"): "Retirar de" vale quando atrás há uma máquina (README "Retirada
/// (Inseridor)"); "Colocar em" vale quando na frente há uma máquina (README "Slots virtuais"). O Inseridor Configurável
/// tem "Quantidade por vez", e com um Regulador aparece a seção dele (plano mestre D13): modo, limite geral ou a lista de
/// limites por item (numa segunda coluna) e "Remover regulador".
/// </summary>
internal sealed class InserterMenu : ConfigMenu
{
    /// <summary>Itens na lista do Regulador (modo Por item).</summary>
    public const int MaxRegulatorItems = 8;

    /// <summary>Largura mínima de cada coluna quando há a lista do Regulador.</summary>
    private const int ColumnWidth = 560;

    public InserterMenu(ITranslationHelper i18n, InserterMenuOptions options)
        : base(i18n.Get("menu.inserter.title"), i18n.Get("menu.ok"), CreateColumns(i18n, options))
    {
    }

    private static ConfigColumn[] CreateColumns(ITranslationHelper i18n, InserterMenuOptions options)
    {
        var left = new List<ConfigRow>
        {
            new CycleRow<PickupSource>(
                i18n.Get("menu.inserter.source"),
                new (PickupSource, string)[]
                {
                    (PickupSource.Product, i18n.Get("slot.product")),
                    (PickupSource.Main, i18n.Get("slot.main")),
                    (PickupSource.Additional, i18n.Get("slot.additional")),
                },
                options.Source,
                options.OnSourceChanged),
            new CycleRow<InserterSlotMode>(
                i18n.Get("menu.inserter.slot"),
                new (InserterSlotMode, string)[]
                {
                    (InserterSlotMode.Auto, i18n.Get("slot.auto")),
                    (InserterSlotMode.Main, i18n.Get("slot.main")),
                    (InserterSlotMode.Additional, i18n.Get("slot.additional")),
                },
                options.SlotMode,
                options.OnSlotModeChanged),
        };

        if (options.ConfiguredAmount is { } amount)
            left.Add(new NumberRow(i18n.Get("menu.inserter.amount"), amount, 1, Balance.MaxConfigurableAmount, options.OnAmountChanged));

        RegulatorRule? rule = options.Regulator;
        if (rule == null)
            return new[] { new ConfigColumn(left, SingleColumnWidth) };

        left.Add(new LabelRow(i18n.Get("menu.regulator.title")));
        left.Add(new CycleRow<RegulatorMode>(
            i18n.Get("menu.regulator.mode"),
            new (RegulatorMode, string)[]
            {
                (RegulatorMode.General, i18n.Get("regulator-mode.general")),
                (RegulatorMode.PerItem, i18n.Get("regulator-mode.per-item")),
            },
            rule.Mode,
            mode =>
            {
                rule.Mode = mode;
                options.OnRegulatorChanged();
                options.Reopen();
            }));

        if (rule.Mode == RegulatorMode.General)
        {
            left.Add(new NumberRow(
                i18n.Get("menu.regulator.limit"),
                rule.GeneralLimit,
                0,
                Balance.MaxRegulatorLimit,
                limit =>
                {
                    rule.GeneralLimit = limit;
                    options.OnRegulatorChanged();
                }));
        }

        left.Add(new ButtonRow(i18n.Get("menu.regulator.remove"), options.OnRemoveRegulator));
        if (rule.Mode == RegulatorMode.General)
            return new[] { new ConfigColumn(left, SingleColumnWidth) };

        return new[] { new ConfigColumn(left, ColumnWidth), new ConfigColumn(CreateItemRows(i18n, options, rule), ColumnWidth) };
    }

    /// <summary>Coluna da lista do Regulador: título, um <see cref="RegulatorItemRow"/> por item e "Adicionar item".</summary>
    private static List<ConfigRow> CreateItemRows(ITranslationHelper i18n, InserterMenuOptions options, RegulatorRule rule)
    {
        var rows = new List<ConfigRow> { new LabelRow(i18n.Get("menu.regulator.items")) };
        foreach ((string id, int limit) in rule.PerItemLimits.Take(MaxRegulatorItems).ToList())
        {
            rows.Add(new RegulatorItemRow(
                id,
                limit,
                Balance.MaxRegulatorLimit,
                i18n.Get("menu.regulator.remove-item"),
                newLimit =>
                {
                    rule.PerItemLimits[id] = newLimit;
                    options.OnRegulatorChanged();
                },
                () =>
                {
                    rule.PerItemLimits.Remove(id);
                    options.OnRegulatorChanged();
                    options.Reopen();
                }));
        }

        if (rule.PerItemLimits.Count == 0)
            rows.Add(new LabelRow(i18n.Get("menu.regulator.no-items")));
        if (rule.PerItemLimits.Count < MaxRegulatorItems)
            rows.Add(new ButtonRow(i18n.Get("menu.regulator.add"), options.OnAddRegulatorItem));

        return rows;
    }
}
