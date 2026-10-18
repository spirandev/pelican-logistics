using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// "Slot fantasma": mostra um item do filtro num quadro de inventário, sem que o item exista de verdade (não sai do
/// inventário do jogador). Os itens são criados pelo ID só para o desenho e a dica.
/// </summary>
internal static class GhostSlot
{
    public const int Size = 64;

    /// <summary>Profundidade dos itens no menu (a mesma do <c>InventoryMenu</c>).</summary>
    private const float ItemDepth = 0.865f;

    /// <summary>Quadro do slot em <see cref="Game1.menuTexture"/> (o mesmo do <c>InventoryMenu</c>).</summary>
    private static readonly Rectangle SlotSource = Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10);

    /// <summary>Cria o item do ID qualificado para mostrar (item de erro do jogo se o ID não existir mais).</summary>
    public static Item Create(string qualifiedItemId)
    {
        return ItemRegistry.Create(qualifiedItemId);
    }

    /// <summary>Desenha o quadro do slot em 'bounds' e, se houver, o item por cima, sem a quantidade.</summary>
    public static void Draw(SpriteBatch b, Rectangle bounds, Item? item)
    {
        b.Draw(Game1.menuTexture, bounds, SlotSource, Color.White);
        item?.drawInMenu(b, new Vector2(bounds.X, bounds.Y), bounds.Width / (float)Size, 1f, ItemDepth, StackDrawType.Hide, Color.White, true);
    }
}
