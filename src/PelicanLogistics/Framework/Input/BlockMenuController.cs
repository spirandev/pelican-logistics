using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
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
                return new InserterMenu(
                    i18n,
                    NodeFactory.ReadPickupSource(obj),
                    source =>
                    {
                        NodeFactory.WritePickupSource(obj, source);
                        if (this.registry.GetNode(location, pos) is InserterNode inserter)
                            inserter.Source = source;
                    },
                    NodeFactory.ReadInserterSlotMode(obj),
                    mode =>
                    {
                        NodeFactory.WriteInserterSlotMode(obj, mode);
                        if (this.registry.GetNode(location, pos) is InserterNode inserter)
                            inserter.SlotMode = mode;
                    });

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

    /// <summary>Grava a regra no <c>modData</c> e a aplica na hora ao nó do filtro.</summary>
    private void SetFilterRule(GameLocation location, SObject obj, TilePos pos, IFilterRule rule)
    {
        NodeFactory.WriteFilterRule(obj, rule);
        if (this.registry.GetNode(location, pos) is FilterNode filter)
            filter.Rule = rule;
    }
}
