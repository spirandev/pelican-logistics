using Microsoft.Xna.Framework;
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
            case ItemIds.Extractor:
                return new ExtractorMenu(i18n, NodeFactory.ReadExtractorSource(obj), source =>
                {
                    NodeFactory.WriteExtractorSource(obj, source);
                    if (this.registry.GetNode(location, pos) is ExtractorNode extractor)
                        extractor.Source = source;
                });

            case ItemIds.Inserter:
                return new InserterMenu(i18n, NodeFactory.ReadInserterSlotMode(obj), mode =>
                {
                    NodeFactory.WriteInserterSlotMode(obj, mode);
                    if (this.registry.GetNode(location, pos) is InserterNode inserter)
                        inserter.SlotMode = mode;
                });

            default:
                return null;
        }
    }
}
