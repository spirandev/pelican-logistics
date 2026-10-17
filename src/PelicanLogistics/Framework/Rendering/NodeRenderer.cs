using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using PelicanLogistics.Framework.Content;
using PelicanLogistics.Framework.Input;
using PelicanLogistics.Framework.World;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Rendering;

/// <summary>Desenha os blocos do mod (placeholder girado), o braço do Inseridor e os itens em trânsito sobre eles.</summary>
internal sealed class NodeRenderer
{
    /// <summary>Escala do sprite 16×16 para o tile de 64 pixels.</summary>
    private const float SpriteScale = 4f;

    /// <summary>Profundidades acima da base: itens na esteira e braço, mão, item na mão (nessa ordem).</summary>
    private const float ItemDepthOffset = 0.00001f;

    private const float ArmDepthOffset = 0.00001f;
    private const float HandDepthOffset = 0.00002f;
    private const float HandItemDepthOffset = 0.00003f;

    /// <summary>Escala do ícone do item em trânsito (metade do tamanho no inventário).</summary>
    private const float ItemScale = 0.5f;

    /// <summary>Largura do traço do braço, em pixels de tela.</summary>
    private const float ArmWidth = 8f;

    /// <summary>Lado do quadradinho da mão, em pixels de tela.</summary>
    private const float HandSize = 12f;

    private static readonly Vector2 SpriteOrigin = new(PlaceholderPainter.SpriteSize / 2f, PlaceholderPainter.SpriteSize / 2f);
    private static readonly Vector2 TileCenter = new(Game1.tileSize / 2f, Game1.tileSize / 2f);

    /// <summary>Origem do pixel do braço: a base do traço (meio da borda de baixo), para ele crescer a partir do centro do tile.</summary>
    private static readonly Vector2 ArmOrigin = new(0.5f, 1f);

    private static readonly Vector2 PixelCenter = new(0.5f, 0.5f);

    private static readonly Color ArmColor = new(PlaceholderColors.Outline.R, PlaceholderColors.Outline.G, PlaceholderColors.Outline.B);

    private readonly WorldRegistry registry;
    private readonly PlacementController placement;

    public NodeRenderer(WorldRegistry registry, PlacementController placement)
    {
        this.registry = registry;
        this.placement = placement;
    }

    /// <summary>Rotação do sprite (desenhado apontando para o norte) para a direção indicada.</summary>
    public static float RotationOf(Direction facing)
    {
        return (int)facing * MathHelper.PiOver2;
    }

    /// <summary>Desenha o bloco no tile (x, y). Chamado pelo patch de <c>Object.draw</c>.</summary>
    public void Draw(SpriteBatch b, SObject obj, int x, int y, float alpha)
    {
        GameLocation? location = Game1.currentLocation;
        var pos = new TilePos(x, y);

        // O jogo também desenha o item segurado no tile do cursor como prévia de colocação. Nesse caso o
        // objeto não é o que está no mapa, e a prévia usa a direção de colocação.
        LogisticsNode? node = null;
        Direction facing;
        if (location != null && location.objects.TryGetValue(new Vector2(x, y), out SObject? placed) && ReferenceEquals(placed, obj))
        {
            node = this.registry.GetNode(location, pos);
            facing = node?.Facing ?? NodeFactory.ReadFacing(obj);
        }
        else
            facing = this.placement.CurrentDirection;

        Vector2 topLeft = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * Game1.tileSize, y * Game1.tileSize));

        // Profundidade: abaixo do jogador quando ele está ao sul do bloco. Valor a ajustar no teste local.
        float baseDepth = Math.Max(0f, y * Game1.tileSize / 10000f) + 0.0001f;

        b.Draw(
            PlaceholderTextures.GetWorldSprite(obj.ItemId),
            topLeft + TileCenter,
            null,
            Color.White * alpha,
            RotationOf(facing),
            SpriteOrigin,
            SpriteScale,
            SpriteEffects.None,
            baseDepth);

        if (node is InserterNode inserter)
            DrawArm(b, topLeft, ArmMath.GetArmAngle(inserter), inserter.Hand.Item as GameItem, alpha, baseDepth);
        else if (node != null)
            DrawTransitItems(b, node, topLeft, baseDepth);
        else if (obj.ItemId == ItemIds.Inserter)
            DrawArm(b, topLeft, ArmMath.AngleOf(facing.Opposite()), null, alpha, baseDepth); // prévia: braço parado apontando para trás
    }

    /// <summary>Itens em trânsito de uma esteira, ao longo do caminho da entrada até a saída.</summary>
    private static void DrawTransitItems(SpriteBatch b, LogisticsNode node, Vector2 topLeft, float baseDepth)
    {
        foreach (TransitSlot slot in node.Slots)
        {
            if (slot.Item is not GameItem item)
                continue;

            (float px, float py) = PathMath.GetPosition(slot.EntrySide, node.Facing, slot.Progress);
            DrawItem(b, item, topLeft + new Vector2(px * Game1.tileSize, py * Game1.tileSize), baseDepth + ItemDepthOffset);
        }
    }

    /// <summary>Braço do Inseridor: traço escuro do centro até a mão, a mão e o item que ela carrega (README "Placeholders visuais").</summary>
    private static void DrawArm(SpriteBatch b, Vector2 topLeft, float angle, GameItem? item, float alpha, float baseDepth)
    {
        Texture2D pixel = PlaceholderTextures.Pixel;
        Color color = ArmColor * alpha;

        b.Draw(
            pixel,
            topLeft + TileCenter,
            null,
            color,
            angle,
            ArmOrigin,
            new Vector2(ArmWidth, ArmMath.HandRadius * Game1.tileSize),
            SpriteEffects.None,
            baseDepth + ArmDepthOffset);

        (float hx, float hy) = ArmMath.GetHandPosition(angle);
        Vector2 hand = topLeft + new Vector2(hx * Game1.tileSize, hy * Game1.tileSize);
        b.Draw(pixel, hand, null, color, 0f, PixelCenter, HandSize, SpriteEffects.None, baseDepth + HandDepthOffset);

        if (item != null)
            DrawItem(b, item, hand, baseDepth + HandItemDepthOffset);
    }

    /// <summary>Ícone do item centralizado em 'center'. <c>drawInMenu</c> desenha numa caixa de 64×64 a partir do canto indicado.</summary>
    private static void DrawItem(SpriteBatch b, GameItem item, Vector2 center, float depth)
    {
        item.Item.drawInMenu(b, center - TileCenter, ItemScale, 1f, depth, StackDrawType.Hide, Color.White, false);
    }
}
