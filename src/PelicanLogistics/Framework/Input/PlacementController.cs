using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Core.Simulation;
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
/// de colocação; com a mão livre e o cursor sobre um bloco direcional, gira o bloco. Segurando uma Esteira
/// Subterrânea, uma linha tracejada fraca liga cada entrada à sua saída (ajuda a montar).
/// </summary>
internal sealed class PlacementController
{
    private const float GhostAlpha = 0.5f;
    private const float GhostScale = 4f;
    private const float GhostDepth = 1f;

    /// <summary>Linha entre Subterrâneas pareadas: traço e espaço (em pixels de tela), espessura, opacidade.</summary>
    private const int DashLength = 8;

    private const int DashGap = 8;
    private const float DashThickness = 4f;
    private const float DashAlpha = 0.35f;

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
        if (node != null && IsDirectional(node) && this.registry.Rotate(location, pos))
            this.helper.Input.SuppressActiveKeybinds(this.config.RotateKey);
    }

    /// <summary>Desenha a seta fantasma no tile do cursor enquanto o jogador segura um bloco direcional.</summary>
    public void OnRenderedWorld(RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || !IsHoldingDirectionalBlock())
            return;

        if (Game1.player.ActiveObject?.ItemId == ItemIds.UndergroundBelt && Game1.currentLocation != null)
            this.DrawUndergroundLinks(e.SpriteBatch, Game1.currentLocation);

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

    /// <summary>Linha tracejada do centro de cada entrada pareada ao centro da sua saída, no local atual.</summary>
    private void DrawUndergroundLinks(SpriteBatch b, GameLocation location)
    {
        LogisticsGrid? grid = this.registry.World.Get(WorldRegistry.KeyOf(location));
        if (grid == null)
            return;

        foreach (LogisticsNode node in grid.Nodes)
        {
            if (node is not UndergroundBeltNode { Role: UndergroundRole.Entrance, Partner: { } exit } entrance)
                continue;

            (int dx, int dy) = entrance.Facing.ToOffset();
            var step = new Vector2(dx, dy);
            Vector2 start = Game1.GlobalToLocal(Game1.viewport, new Vector2(entrance.Position.X, entrance.Position.Y) * Game1.tileSize) + TileCenter;
            float length = (entrance.TunnelLength + 1) * Game1.tileSize;
            for (float offset = 0; offset < length; offset += DashLength + DashGap)
            {
                float dash = Math.Min(DashLength, length - offset);
                Vector2 from = start + (step * offset);
                Vector2 size = dx != 0 ? new Vector2(dash, DashThickness) : new Vector2(DashThickness, dash);
                Vector2 topLeft = from - (dx != 0 ? new Vector2(dx < 0 ? dash : 0, DashThickness / 2) : new Vector2(DashThickness / 2, dy < 0 ? dash : 0));
                b.Draw(PlaceholderTextures.Pixel, topLeft, null, Color.White * DashAlpha, 0f, Vector2.Zero, size, SpriteEffects.None, GhostDepth);
            }
        }
    }

    private static bool IsHoldingDirectionalBlock()
    {
        return Game1.player?.ActiveObject is SObject obj && ItemIds.IsOurBlock(obj) && ItemIds.IsDirectional(obj.ItemId);
    }

    /// <summary>Nós sem direção (Divisor) não giram.</summary>
    private static bool IsDirectional(LogisticsNode node)
    {
        return node.Kind != NodeKind.Splitter;
    }
}
