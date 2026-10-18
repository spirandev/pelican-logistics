using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Core.Filters;
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

    /// <summary>Ícone do item configurado no Filtro Comum: abaixo do item em trânsito.</summary>
    private const float FilterIconDepthOffset = 0.000005f;

    /// <summary>Escala do ícone do item em trânsito (metade do tamanho no inventário).</summary>
    private const float ItemScale = 0.5f;

    /// <summary>Escala do ícone do item configurado no Filtro Comum.</summary>
    private const float FilterIconScale = 0.35f;

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

    /// <summary>Ícones dos itens configurados nos Filtros Comuns, por ID qualificado (só para o desenho).</summary>
    private readonly Dictionary<string, Item> filterIcons = new(StringComparer.Ordinal);

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

        // O Divisor não tem direção: o sprite nunca gira.
        float rotation = ItemIds.IsDirectional(obj.ItemId) ? RotationOf(facing) : 0f;
        b.Draw(
            PlaceholderTextures.GetWorldSprite(obj.ItemId),
            topLeft + TileCenter,
            null,
            Color.White * alpha,
            rotation,
            SpriteOrigin,
            SpriteScale,
            SpriteEffects.None,
            baseDepth);

        switch (node)
        {
            case InserterNode inserter:
                DrawArm(b, topLeft, ArmMath.GetArmAngle(inserter), inserter.Hand.Item as GameItem, alpha, baseDepth);
                break;

            case SplitterNode splitter:
                DrawSplitterItem(b, splitter, topLeft, baseDepth);
                break;

            case FilterNode filter:
                if (filter.Rule is CommonFilterRule { QualifiedItemId: { } id })
                    DrawItem(b, this.GetFilterIcon(id), topLeft + TileCenter, FilterIconScale, baseDepth + FilterIconDepthOffset);
                DrawTransitItems(b, node, topLeft, baseDepth);
                break;

            case not null:
                DrawTransitItems(b, node, topLeft, baseDepth);
                break;

            default:
                if (obj.ItemId == ItemIds.Inserter)
                    DrawArm(b, topLeft, ArmMath.AngleOf(facing.Opposite()), null, alpha, baseDepth); // prévia: braço parado apontando para trás
                break;
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

    /// <summary>Item no Divisor: da entrada ao centro; a saída só é conhecida ao distribuir (<see cref="PathMath.GetHubPosition"/>).</summary>
    private static void DrawSplitterItem(SpriteBatch b, SplitterNode splitter, Vector2 topLeft, float baseDepth)
    {
        if (splitter.Slot.Item is not GameItem item)
            return;

        (float px, float py) = PathMath.GetHubPosition(splitter.Slot.EntrySide, splitter.Slot.Progress);
        DrawItem(b, item.Item, topLeft + new Vector2(px * Game1.tileSize, py * Game1.tileSize), ItemScale, baseDepth + ItemDepthOffset);
    }

    /// <summary>Itens em trânsito de uma esteira, ao longo do caminho da entrada até a saída.</summary>
    private static void DrawTransitItems(SpriteBatch b, LogisticsNode node, Vector2 topLeft, float baseDepth)
    {
        foreach (TransitSlot slot in node.Slots)
        {
            if (slot.Item is not GameItem item)
                continue;

            (float px, float py) = PathMath.GetPosition(slot.EntrySide, node.Facing, slot.Progress);
            DrawItem(b, item.Item, topLeft + new Vector2(px * Game1.tileSize, py * Game1.tileSize), ItemScale, baseDepth + ItemDepthOffset);
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
            DrawItem(b, item.Item, hand, ItemScale, baseDepth + HandItemDepthOffset);
    }

    /// <summary>Ícone do item centralizado em 'center'. <c>drawInMenu</c> desenha numa caixa de 64×64 a partir do canto indicado.</summary>
    private static void DrawItem(SpriteBatch b, Item item, Vector2 center, float scale, float depth)
    {
        item.drawInMenu(b, center - TileCenter, scale, 1f, depth, StackDrawType.Hide, Color.White, false);
    }
}
