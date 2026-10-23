using System.Linq;
using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Framework.Menus;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Input;

/// <summary>
/// Coloca, retira e configura os blocos subterrâneos (Esteiras, Divisores e Filtros Subterrâneos) com o botão de ação ou o
/// de usar ferramenta (README "Túnel e Esteira Subterrânea" e "Roteamento subterrâneo"; plano mestre D18 a D23), sempre
/// com o Capacete de Engenheiro na cabeça. Avisa também quando o jogador tenta colocar um Túnel sem o capacete ou sobre um
/// bloco subterrâneo (a colocação em si é barrada pelo <c>TunnelPlacementPatch</c>).
/// </summary>
internal sealed class UndergroundController
{
    private readonly IModHelper helper;
    private readonly WorldRegistry registry;
    private readonly PlacementController placement;

    public UndergroundController(IModHelper helper, WorldRegistry registry, PlacementController placement)
    {
        this.helper = helper;
        this.registry = registry;
        this.placement = placement;
    }

    /// <summary>
    /// True se dá para cavar no tile: dentro do mapa, fora da água e sem parede ou penhasco do mapa (camada
    /// <c>Buildings</c>). Construções do jogador, objetos e plantações por cima não impedem.
    /// </summary>
    public static bool IsDiggable(GameLocation location, TilePos pos)
    {
        return location.isTileOnMap(pos.X, pos.Y)
            && !location.isWaterTile(pos.X, pos.Y)
            && location.map.GetLayer("Back")?.Tiles[pos.X, pos.Y] != null
            && location.map.GetLayer("Buildings")?.Tiles[pos.X, pos.Y] == null;
    }

    /// <summary>Trata o clique. Retorna true se tratou (o botão é suprimido).</summary>
    public bool OnButtonPressed(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree)
            return false;

        GameLocation? location = Game1.currentLocation;
        if (location == null)
            return false;

        bool action = e.Button.IsActionButton();
        bool useTool = e.Button.IsUseToolButton();
        if (!action && !useTool)
            return false;

        Vector2 tile = e.Cursor.GrabTile;
        TilePos pos = WorldRegistry.ToTilePos(tile);
        this.WarnAboutTunnel(location, tile, pos);

        // Segurando qualquer item subterrâneo, os dois botões constroem, como o jogo faz ao colocar objetos (D23): sobre
        // um bloco subterrâneo, retira (e devolve o item daquele bloco); num tile vazio, coloca o item da mão.
        Item? held = Game1.player.CurrentItem;
        if (ItemIds.IsUndergroundItem(held))
        {
            this.helper.Input.Suppress(e.Button);
            if (!EngineerHelmet.IsWorn)
                Game1.showRedMessage(this.helper.Translation.Get("message.helmet-required"));
            else if (this.registry.GetUnderground(location, pos) != null)
                this.Remove(location, pos);
            else if (IsDiggable(location, pos) && this.registry.TryPlaceUnderground(location, pos, held!.ItemId, this.placement.CurrentDirection))
            {
                Game1.player.reduceActiveItemByOne();
                Game1.playSound("dirtyHit");
            }
            else
                Game1.showRedMessage(this.helper.Translation.Get("message.cannot-dig"));

            return true;
        }

        // Mão livre: só o botão de ação, e só se nada mais no tile responderia ao clique (D20). O Filtro Subterrâneo abre
        // a interface; a Esteira e o Divisor Subterrâneos saem (D23).
        if (action && held == null && EngineerHelmet.IsWorn && this.registry.GetUnderground(location, pos) is { } node && IsBare(location, tile))
        {
            this.helper.Input.Suppress(e.Button);
            if (node is FilterNode filter)
                Game1.activeClickableMenu = this.CreateFilterMenu(location, pos, filter);
            else
                this.Remove(location, pos);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True se no tile não há nada com que o jogo interaja: objeto, móvel, construção, arbusto, toco ou pedra grande, nem
    /// terrain feature além de caminho ou grama (plano mestre D20).
    /// </summary>
    private static bool IsBare(GameLocation location, Vector2 tile)
    {
        int x = (int)tile.X;
        int y = (int)tile.Y;
        if (location.getObjectAtTile(x, y) != null || location.getBuildingAt(tile) != null || location.getLargeTerrainFeatureAt(x, y) != null)
            return false;

        if (location.resourceClumps.Any(clump => clump.occupiesTile(x, y)))
            return false;

        return !location.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature) || feature is Flooring or Grass;
    }

    /// <summary>Retira o bloco subterrâneo, devolve o item daquele bloco ao jogador e solta no chão o que estava dentro dele.</summary>
    private void Remove(GameLocation location, TilePos pos)
    {
        if (this.registry.RemoveUnderground(location, pos) is not { } itemId)
            return;

        AccessoryController.GiveToPlayer(ItemRegistry.Create(ItemIds.QualifiedObject(itemId)));
        Game1.playSound("dirtyHit");
    }

    /// <summary>
    /// A interface do Filtro Comum ou Completo (as mesmas da superfície), editando uma cópia da regra do nó. Cada alteração
    /// vai na hora para o nó e para o layout do local.
    /// </summary>
    private ConfigMenu CreateFilterMenu(GameLocation location, TilePos pos, FilterNode filter)
    {
        ITranslationHelper i18n = this.helper.Translation;
        if (filter.Rule is CommonFilterRule common)
        {
            return new CommonFilterMenu(
                i18n,
                common.QualifiedItemId,
                id => this.registry.SetUndergroundFilterRule(location, pos, new CommonFilterRule { QualifiedItemId = id }));
        }

        CompleteFilterRule rule = FilterRuleSerializer.DeserializeComplete(FilterRuleSerializer.Serialize(filter.Rule));
        return new CompleteFilterMenu(i18n, rule, () => this.registry.SetUndergroundFilterRule(location, pos, rule));
    }

    /// <summary>Segurando um Túnel sobre um tile livre: avisa por que ele não pode ser colocado (sem capacete, ou sobre um bloco subterrâneo).</summary>
    private void WarnAboutTunnel(GameLocation location, Vector2 tile, TilePos pos)
    {
        if (Game1.player.ActiveObject is not SObject held || !ItemIds.IsOurBlock(held) || held.ItemId != ItemIds.Tunnel || location.objects.ContainsKey(tile))
            return;

        if (!EngineerHelmet.IsWorn)
            Game1.showRedMessage(this.helper.Translation.Get("message.helmet-required"));
        else if (!this.registry.CanPlaceTunnel(location, pos))
            Game1.showRedMessage(this.helper.Translation.Get("message.tunnel-over-segment"));
    }
}
