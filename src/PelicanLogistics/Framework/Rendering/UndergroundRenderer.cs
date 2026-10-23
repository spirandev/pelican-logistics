using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Core.Filters;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Framework.Content;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PelicanLogistics.Framework.Rendering;

/// <summary>
/// Desenha a camada subterrânea do local atual, só com o Capacete de Engenheiro ativo (plano mestre D18, D22): cada bloco
/// subterrâneo semitransparente (Esteira reta ou curva, girada; Filtro girado, com o ícone do item no Filtro Comum; Divisor
/// sem girar) e o item dentro dele. É uma "visão de raio X": aparece por cima do chão, das plantas e dos objetos.
/// </summary>
internal sealed class UndergroundRenderer
{
    /// <summary>Escala do sprite 16×16 para o tile de 64 pixels.</summary>
    private const float SpriteScale = 4f;

    /// <summary>Opacidade dos blocos subterrâneos.</summary>
    private const float SegmentAlpha = 0.55f;

    /// <summary>Escala do ícone do item em trânsito (metade do tamanho no inventário, como na superfície).</summary>
    private const float ItemScale = 0.5f;

    /// <summary>Escala do ícone do item configurado no Filtro Comum Subterrâneo (a mesma da superfície).</summary>
    private const float FilterIconScale = 0.35f;

    /// <summary>Profundidades: o bloco, o ícone do filtro e, por cima, o item (no lote do <c>RenderedWorld</c>, acima do mundo).</summary>
    private const float SegmentDepth = 0.98f;

    private const float FilterIconDepth = 0.985f;

    private const float ItemDepth = 0.99f;

    private static readonly Vector2 SpriteOrigin = new(PlaceholderPainter.SpriteSize / 2f, PlaceholderPainter.SpriteSize / 2f);
    private static readonly Vector2 TileCenter = new(Game1.tileSize / 2f, Game1.tileSize / 2f);

    private readonly WorldRegistry registry;

    /// <summary>Ícones dos itens configurados nos Filtros Comuns Subterrâneos, por ID qualificado (só para o desenho).</summary>
    private readonly Dictionary<string, Item> filterIcons = new(StringComparer.Ordinal);

    public UndergroundRenderer(WorldRegistry registry)
    {
        this.registry = registry;
    }

    public void OnRenderedWorld(RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null || !EngineerHelmet.IsActive)
            return;

        LogisticsGrid? grid = this.registry.World.Get(WorldRegistry.KeyOf(Game1.currentLocation));
        if (grid == null)
            return;

        foreach (LogisticsNode node in grid.UndergroundNodes)
            this.Draw(e.SpriteBatch, node);
    }

    private void Draw(SpriteBatch b, LogisticsNode node)
    {
        Vector2 topLeft = Game1.GlobalToLocal(Game1.viewport, new Vector2(node.Position.X * Game1.tileSize, node.Position.Y * Game1.tileSize));
        (Texture2D sprite, float rotation) = node switch
        {
            UndergroundSegmentNode segment => (PlaceholderTextures.GetSegmentSprite(segment.IsCurve, segment.InputSide == segment.Facing.CounterClockwise()), NodeRenderer.RotationOf(segment.Facing)),
            SplitterNode => (PlaceholderTextures.GetUndergroundSprite(ItemIds.UndergroundSplitter), 0f),
            _ => (PlaceholderTextures.GetUndergroundSprite(ItemIds.UndergroundItemOf(node)), NodeRenderer.RotationOf(node.Facing)),
        };
        b.Draw(sprite, topLeft + TileCenter, null, Color.White * SegmentAlpha, rotation, SpriteOrigin, SpriteScale, SpriteEffects.None, SegmentDepth);

        if (node is FilterNode { Rule: CommonFilterRule { QualifiedItemId: { } id } })
            DrawItem(b, this.GetFilterIcon(id), topLeft + TileCenter, FilterIconScale, FilterIconDepth);

        foreach (TransitSlot slot in node.Slots)
        {
            if (slot.Item is not GameItem item)
                continue;

            // No Divisor, a saída só é conhecida ao distribuir: o item vai da entrada ao centro.
            (float px, float py) = node is SplitterNode
                ? PathMath.GetHubPosition(slot.EntrySide, slot.Progress)
                : PathMath.GetPosition(slot.EntrySide, node.Facing, slot.Progress);
            DrawItem(b, item.Item, topLeft + new Vector2(px * Game1.tileSize, py * Game1.tileSize), ItemScale, ItemDepth);
        }
    }

    /// <summary>Ícone de um item pelo ID, criado no primeiro uso.</summary>
    private Item GetFilterIcon(string qualifiedItemId)
    {
        if (!this.filterIcons.TryGetValue(qualifiedItemId, out Item? icon))
        {
            icon = ItemRegistry.Create(qualifiedItemId);
            this.filterIcons[qualifiedItemId] = icon;
        }

        return icon;
    }

    /// <summary>Ícone do item centralizado em 'center'. <c>drawInMenu</c> desenha numa caixa de 64×64 a partir do canto indicado.</summary>
    private static void DrawItem(SpriteBatch b, Item item, Vector2 center, float scale, float depth)
    {
        item.drawInMenu(b, center - TileCenter, scale, 1f, depth, StackDrawType.Hide, Color.White, false);
    }
}
