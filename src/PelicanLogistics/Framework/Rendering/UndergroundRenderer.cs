using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
/// Desenha a camada subterrânea do local atual, só com o Capacete de Engenheiro ativo (plano mestre D18): cada Esteira
/// Subterrânea semitransparente (reta ou curva, girada) e o item dentro dela. É uma "visão de raio X": aparece por cima
/// do chão, das plantas e dos objetos.
/// </summary>
internal sealed class UndergroundRenderer
{
    /// <summary>Escala do sprite 16×16 para o tile de 64 pixels.</summary>
    private const float SpriteScale = 4f;

    /// <summary>Opacidade das Esteiras Subterrâneas.</summary>
    private const float SegmentAlpha = 0.55f;

    /// <summary>Escala do ícone do item em trânsito (metade do tamanho no inventário, como na superfície).</summary>
    private const float ItemScale = 0.5f;

    /// <summary>Profundidades: o segmento e, por cima, o item (no lote do <c>RenderedWorld</c>, acima do mundo).</summary>
    private const float SegmentDepth = 0.98f;

    private const float ItemDepth = 0.99f;

    private static readonly Vector2 SpriteOrigin = new(PlaceholderPainter.SpriteSize / 2f, PlaceholderPainter.SpriteSize / 2f);
    private static readonly Vector2 TileCenter = new(Game1.tileSize / 2f, Game1.tileSize / 2f);

    private readonly WorldRegistry registry;

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

        foreach (UndergroundSegmentNode segment in grid.UndergroundNodes)
            this.Draw(e.SpriteBatch, segment);
    }

    private void Draw(SpriteBatch b, UndergroundSegmentNode segment)
    {
        Vector2 topLeft = Game1.GlobalToLocal(Game1.viewport, new Vector2(segment.Position.X * Game1.tileSize, segment.Position.Y * Game1.tileSize));
        Texture2D sprite = PlaceholderTextures.GetSegmentSprite(segment.IsCurve, segment.InputSide == segment.Facing.CounterClockwise());
        b.Draw(sprite, topLeft + TileCenter, null, Color.White * SegmentAlpha, NodeRenderer.RotationOf(segment.Facing), SpriteOrigin, SpriteScale, SpriteEffects.None, SegmentDepth);

        if (segment.Slot.Item is not GameItem item)
            return;

        (float px, float py) = PathMath.GetPosition(segment.Slot.EntrySide, segment.Facing, segment.Slot.Progress);
        Vector2 center = topLeft + new Vector2(px * Game1.tileSize, py * Game1.tileSize);
        item.Item.drawInMenu(b, center - TileCenter, ItemScale, 1f, ItemDepth, StackDrawType.Hide, Color.White, false);
    }
}
