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

/// <summary>Desenha os blocos do mod (placeholder girado) e os itens em trânsito sobre eles.</summary>
internal sealed class NodeRenderer
{
    /// <summary>Escala do sprite 16×16 para o tile de 64 pixels.</summary>
    private const float SpriteScale = 4f;

    /// <summary>Os itens ficam logo acima do bloco.</summary>
    private const float ItemDepthOffset = 0.00001f;

    /// <summary>Escala do ícone do item em trânsito (metade do tamanho no inventário).</summary>
    private const float ItemScale = 0.5f;

    private static readonly Vector2 SpriteOrigin = new(PlaceholderPainter.SpriteSize / 2f, PlaceholderPainter.SpriteSize / 2f);
    private static readonly Vector2 TileCenter = new(Game1.tileSize / 2f, Game1.tileSize / 2f);

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

        if (node == null)
            return;

        foreach (TransitSlot slot in node.Slots)
        {
            if (slot.Item is not GameItem item)
                continue;

            (float px, float py) = PathMath.GetPosition(slot.EntrySide, node.Facing, slot.Progress);
            Vector2 center = topLeft + new Vector2(px * Game1.tileSize, py * Game1.tileSize);

            // drawInMenu desenha o ícone numa caixa de 64×64 a partir do canto indicado.
            item.Item.drawInMenu(b, center - TileCenter, ItemScale, 1f, baseDepth + ItemDepthOffset, StackDrawType.Hide, Color.White, false);
        }
    }
}
