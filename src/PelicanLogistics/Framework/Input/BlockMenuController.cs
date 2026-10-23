using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Framework.Menus;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Input;

/// <summary>Abre a interface do bloco quando o jogador usa o botão de ação sobre ele (README "Interface").</summary>
internal sealed class BlockMenuController
{
    private readonly IModHelper helper;
    private readonly WorldRegistry registry;

    public BlockMenuController(IModHelper helper, WorldRegistry registry)
    {
        this.helper = helper;
        this.registry = registry;
    }

    public void OnButtonPressed(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || !e.Button.IsActionButton())
            return;

        GameLocation? location = Game1.currentLocation;
        Vector2 tile = e.Cursor.GrabTile;
        if (location == null || !location.objects.TryGetValue(tile, out SObject? obj) || obj == null || !ItemIds.IsOurBlock(obj))
            return;

        IClickableMenu? menu = this.CreateMenu(location, obj, WorldRegistry.ToTilePos(tile));
        if (menu == null)
            return;

        this.helper.Input.Suppress(e.Button);
        Game1.activeClickableMenu = menu;
    }

    /// <summary>Interface do bloco, ou null se ele não tem nada configurável. Cada alteração vai na hora para o <c>modData</c> e o nó.</summary>
    private IClickableMenu? CreateMenu(GameLocation location, SObject obj, TilePos pos)
    {
        ITranslationHelper i18n = this.helper.Translation;
        switch (obj.ItemId)
        {
            case ItemIds.Inserter:
            case ItemIds.InserterStack:
            case ItemIds.InserterConfigurable:
                return this.CreateInserterMenu(location, obj, pos);

            case ItemIds.FilterCommon:
                return new CommonFilterMenu(
                    i18n,
                    NodeFactory.ReadCommonFilterRule(obj).QualifiedItemId,
                    id => this.SetFilterRule(location, obj, pos, new CommonFilterRule { QualifiedItemId = id }));

            case ItemIds.FilterComplete:
            {
                CompleteFilterRule rule = NodeFactory.ReadCompleteFilterRule(obj);
                return new CompleteFilterMenu(i18n, rule, () => this.SetFilterRule(location, obj, pos, rule));
            }

            default:
                return null;
        }
    }

    /// <summary>Interface do Inseridor: "Retirar de", "Colocar em", a quantidade (Configurável) e a seção do Regulador, se houver.</summary>
    private InserterMenu CreateInserterMenu(GameLocation location, SObject obj, TilePos pos)
    {
        InserterNode? node = this.registry.GetNode(location, pos) as InserterNode;
        return new InserterMenu(this.helper.Translation, new InserterMenuOptions
        {
            Source = NodeFactory.ReadPickupSource(obj),
            OnSourceChanged = source =>
            {
                NodeFactory.WritePickupSource(obj, source);
                if (this.registry.GetNode(location, pos) is InserterNode inserter)
                    inserter.Source = source;
            },
            SlotMode = NodeFactory.ReadInserterSlotMode(obj),
            OnSlotModeChanged = mode =>
            {
                NodeFactory.WriteInserterSlotMode(obj, mode);
                if (this.registry.GetNode(location, pos) is InserterNode inserter)
                    inserter.SlotMode = mode;
            },
            ConfiguredAmount = obj.ItemId == ItemIds.InserterConfigurable ? node?.ConfiguredAmount ?? NodeFactory.ReadConfiguredAmount(obj) : null,
            OnAmountChanged = amount =>
            {
                NodeFactory.WriteConfiguredAmount(obj, amount);
                if (this.registry.GetNode(location, pos) is InserterNode inserter)
                    inserter.ConfiguredAmount = amount;
            },
            Regulator = node?.Regulator,
            OnRegulatorChanged = () => this.registry.SaveRegulatorRule(location, pos),
            Reopen = () => this.Show(this.CreateInserterMenu(location, obj, pos)),
            OnAddRegulatorItem = () => this.Show(this.CreateRegulatorItemPicker(location, obj, pos)),
            OnRemoveRegulator = () =>
            {
                if (this.registry.RemoveAccessory(location, pos) == AccessoryKind.Regulator)
                    AccessoryController.GiveToPlayer(ItemIds.CreateAccessory(AccessoryKind.Regulator));
                this.Show(this.CreateInserterMenu(location, obj, pos));
            },
        });
    }

    /// <summary>Escolha do item a limitar: o item entra na lista com o limite padrão, e a interface do Inseridor volta.</summary>
    private RegulatorItemPickerMenu CreateRegulatorItemPicker(GameLocation location, SObject obj, TilePos pos)
    {
        return new RegulatorItemPickerMenu(
            this.helper.Translation,
            id =>
            {
                if (this.registry.GetNode(location, pos) is InserterNode { Regulator: { } rule }
                    && !rule.PerItemLimits.ContainsKey(id)
                    && rule.PerItemLimits.Count < InserterMenu.MaxRegulatorItems)
                {
                    rule.PerItemLimits[id] = Balance.RegulatorDefaultLimit;
                    this.registry.SaveRegulatorRule(location, pos);
                    Game1.playSound("smallSelect");
                }
                else
                {
                    Game1.playSound("cancel");
                }

                this.Show(this.CreateInserterMenu(location, obj, pos));
            },
            () => this.Show(this.CreateInserterMenu(location, obj, pos)));
    }

    /// <summary>Troca a interface aberta por outra (fecha a atual antes, para ela gravar o que estiver pendente).</summary>
    private void Show(IClickableMenu menu)
    {
        Game1.activeClickableMenu?.exitThisMenuNoSound();
        Game1.activeClickableMenu = menu;
    }

    /// <summary>Grava a regra no <c>modData</c> e a aplica na hora ao nó do filtro.</summary>
    private void SetFilterRule(GameLocation location, SObject obj, TilePos pos, IFilterRule rule)
    {
        NodeFactory.WriteFilterRule(obj, rule);
        if (this.registry.GetNode(location, pos) is FilterNode filter)
            filter.Rule = rule;
    }
}
