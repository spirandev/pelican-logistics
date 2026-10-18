using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Linha com uma parte de cima própria (<see cref="TopHeight"/>) e, embaixo, o inventário do jogador. Clicar num
/// item do inventário chama <see cref="OnInventoryItemClicked"/> <b>sem</b> tirar o item do inventário (o item usado
/// para configurar o filtro não é gasto, README "Filtro").
/// </summary>
internal abstract class InventoryRow : ConfigRow
{
    /// <summary>Espaço entre a parte de cima e o inventário (inclui os números da barra de atalhos, desenhados acima dos slots).</summary>
    private const int InventoryGap = 24;

    /// <summary>Altura das 3 linhas de slots como o <c>InventoryMenu</c> as desenha (a primeira sobe 16 pixels).</summary>
    private const int InventorySlotsHeight = 196;

    /// <summary>Quanto a primeira linha de slots sobe em relação ao Y passado ao <c>InventoryMenu</c>.</summary>
    private const int InventoryFirstRowOffset = 16;

    /// <summary>Largura do inventário de 12 colunas.</summary>
    private const int InventoryWidth = 12 * 64;

    private InventoryMenu? inventory;
    private Item? hoveredItem;

    public override int Height => this.TopHeight + InventoryGap + InventorySlotsHeight;

    public override int MinWidth => InventoryWidth;

    /// <summary>Altura da parte de cima da linha (rótulo, slots fantasma, botões).</summary>
    protected abstract int TopHeight { get; }

    public override void Draw(SpriteBatch b)
    {
        this.DrawTop(b);
        this.inventory?.draw(b);
    }

    public override void DrawTooltip(SpriteBatch b)
    {
        if (this.hoveredItem != null)
            IClickableMenu.drawToolTip(b, this.hoveredItem.getDescription(), this.hoveredItem.DisplayName, this.hoveredItem);
    }

    public override bool ReceiveLeftClick(int x, int y)
    {
        Item? item = this.inventory?.getItemAt(x, y);
        if (item != null)
        {
            this.OnInventoryItemClicked(item);
            return true;
        }

        return this.ReceiveTopClick(x, y);
    }

    public override void PerformHoverAction(int x, int y)
    {
        this.hoveredItem = this.inventory?.hover(x, y, null) ?? this.GetTopHoveredItem(x, y);
    }

    protected abstract void DrawTop(SpriteBatch b);

    /// <summary>Clique na parte de cima. Retorna true se tratou.</summary>
    protected abstract bool ReceiveTopClick(int x, int y);

    /// <summary>Item sob o cursor na parte de cima (para a dica), ou null.</summary>
    protected abstract Item? GetTopHoveredItem(int x, int y);

    protected abstract void OnInventoryItemClicked(Item item);

    /// <summary>Área da parte de cima.</summary>
    protected Rectangle TopBounds => new(this.Bounds.X, this.Bounds.Y, this.Bounds.Width, this.TopHeight);

    /// <summary>Recria o inventário na nova posição (o <c>InventoryMenu</c> calcula os slots no construtor).</summary>
    protected override void OnBoundsChanged()
    {
        int x = this.Bounds.X + ((this.Bounds.Width - InventoryWidth) / 2);
        int y = this.Bounds.Y + this.TopHeight + InventoryGap + InventoryFirstRowOffset;
        this.inventory = new InventoryMenu(x, y, true);
    }
}
