using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Input;

/// <summary>
/// Aplica e remove os acessórios com o botão de ação (README "Acessórios"; plano mestre D9): segurando uma Ponte ou um
/// Regulador sobre um bloco, coloca o acessório e gasta 1; com a mão livre sobre uma esteira com ponte, devolve a ponte.
/// O Regulador é removido pela interface do Inseridor.
/// </summary>
internal sealed class AccessoryController
{
    private readonly IModHelper helper;
    private readonly WorldRegistry registry;

    public AccessoryController(IModHelper helper, WorldRegistry registry)
    {
        this.helper = helper;
        this.registry = registry;
    }

    /// <summary>Devolve o item ao inventário do jogador; se não couber, solta no chão perto dele.</summary>
    public static void GiveToPlayer(Item item)
    {
        if (!Game1.player.addItemToInventoryBool(item))
            Game1.createItemDebris(item, Game1.player.getStandingPosition(), -1, Game1.player.currentLocation);
    }

    /// <summary>Trata o botão de ação sobre um bloco do mod. Retorna true se tratou (o botão é suprimido).</summary>
    public bool OnButtonPressed(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || !e.Button.IsActionButton())
            return false;

        GameLocation? location = Game1.currentLocation;
        Vector2 tile = e.Cursor.GrabTile;
        if (location == null || !location.objects.TryGetValue(tile, out SObject? obj) || obj == null || !ItemIds.IsOurBlock(obj))
            return false;

        TilePos pos = WorldRegistry.ToTilePos(tile);
        LogisticsNode? node = this.registry.GetNode(location, pos);
        if (node == null)
            return false;

        Item? held = Game1.player.CurrentItem;
        if (ItemIds.IsOurAccessory(held))
        {
            this.Apply(location, pos, node, ItemIds.AccessoryKindOf(held!.ItemId));
            this.helper.Input.Suppress(e.Button);
            return true;
        }

        if (held == null && node.Accessory == AccessoryKind.Bridge && this.registry.RemoveAccessory(location, pos) == AccessoryKind.Bridge)
        {
            GiveToPlayer(ItemIds.CreateAccessory(AccessoryKind.Bridge));
            Game1.playSound("pickUpItem");
            this.helper.Input.Suppress(e.Button);
            return true;
        }

        return false;
    }

    /// <summary>Coloca o acessório segurado e gasta 1; se não servir para o bloco, mostra o motivo e não gasta nada.</summary>
    private void Apply(GameLocation location, TilePos pos, LogisticsNode node, AccessoryKind kind)
    {
        if (this.registry.TrySetAccessory(location, pos, kind))
        {
            Game1.player.reduceActiveItemByOne();
            Game1.playSound("hammer");
            return;
        }

        // Bloco errado tem prioridade sobre "já tem acessório": a mensagem diz onde o acessório pode ir.
        string key = kind switch
        {
            AccessoryKind.Bridge when node is not BeltNode => "message.bridge-only-belts",
            AccessoryKind.Regulator when node is not InserterNode => "message.regulator-only-inserters",
            _ => "message.accessory-occupied",
        };
        Game1.showRedMessage(this.helper.Translation.Get(key));
    }
}
