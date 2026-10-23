using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Framework.Content;
using PelicanLogistics.Framework.Rendering;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Input;

/// <summary>
/// Rotação dos blocos e seta fantasma (plano mestre D1). Segurando um bloco do mod, a tecla gira a direção
/// de colocação; com a mão livre e o cursor sobre um bloco direcional, gira o bloco. A Esteira Subterrânea (objeto)
/// também usa a direção de colocação, e com o Capacete de Engenheiro na cabeça e a mão livre, `R` gira a Esteira
/// Subterrânea sob o cursor quando não há bloco na superfície.
/// </summary>
internal sealed class PlacementController
{
    private const float GhostAlpha = 0.5f;
    private const float GhostScale = 4f;
    private const float GhostDepth = 1f;

    private static readonly Vector2 SpriteOrigin = new(PlaceholderPainter.SpriteSize / 2f, PlaceholderPainter.SpriteSize / 2f);
    private static readonly Vector2 TileCenter = new(Game1.tileSize / 2f, Game1.tileSize / 2f);

    private readonly IModHelper helper;
    private readonly ModConfig config;
    private readonly WorldRegistry registry;

    public PlacementController(IModHelper helper, ModConfig config, WorldRegistry registry)
    {
        this.helper = helper;
        this.config = config;
        this.registry = registry;
    }

    /// <summary>Direção em que o próximo bloco será colocado.</summary>
    public Direction CurrentDirection { get; private set; } = NodeFactory.DefaultFacing;

    public void OnButtonsChanged(ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || !this.config.RotateKey.JustPressed())
            return;

        if (IsHoldingDirectionalBlock())
        {
            this.CurrentDirection = this.CurrentDirection.Clockwise();
            this.helper.Input.SuppressActiveKeybinds(this.config.RotateKey);
            return;
        }

        GameLocation? location = Game1.currentLocation;
        if (location == null)
            return;

        TilePos pos = WorldRegistry.ToTilePos(this.helper.Input.GetCursorPosition().Tile);
        LogisticsNode? node = this.registry.GetNode(location, pos);
        bool rotated = node != null
            ? IsDirectional(node) && this.registry.Rotate(location, pos)
            : EngineerHelmet.IsWorn && this.registry.RotateSegment(location, pos);
        if (rotated)
            this.helper.Input.SuppressActiveKeybinds(this.config.RotateKey);
    }

    /// <summary>Desenha a seta fantasma no tile do cursor enquanto o jogador segura um bloco direcional.</summary>
    public void OnRenderedWorld(RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || !IsHoldingDirectionalBlock())
            return;


        Vector2 tile = this.helper.Input.GetCursorPosition().Tile;
        Vector2 topLeft = Game1.GlobalToLocal(Game1.viewport, tile * Game1.tileSize);
        e.SpriteBatch.Draw(
            PlaceholderTextures.GhostArrow,
            topLeft + TileCenter,
            null,
            Color.White * GhostAlpha,
            NodeRenderer.RotationOf(this.CurrentDirection),
            SpriteOrigin,
            GhostScale,
            SpriteEffects.None,
            GhostDepth);
    }

    /// <summary>Segurando um bloco direcional do mod ou uma Esteira Subterrânea.</summary>
    private static bool IsHoldingDirectionalBlock()
    {
        return Game1.player?.ActiveObject is SObject obj
            && ((ItemIds.IsOurBlock(obj) && ItemIds.IsDirectional(obj.ItemId)) || ItemIds.IsUndergroundBelt(obj));
    }

    /// <summary>Nós sem direção (Divisor) não giram.</summary>
    private static bool IsDirectional(LogisticsNode node)
    {
        return node.Kind != NodeKind.Splitter;
    }
}
