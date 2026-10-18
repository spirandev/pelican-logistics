using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Escolha de um item (Filtro Comum): rótulo, slot fantasma com o item atual e botão "Limpar"; embaixo, o inventário.
/// Clicar num item do inventário define o item sem tirá-lo do inventário.
/// </summary>
internal sealed class ItemPickerRow : InventoryRow
{
    private const int Gap = 16;

    private readonly string label;
    private readonly Action<string?> onChanged;
    private readonly TextButton clearButton;
    private Item? current;
    private Rectangle slotBounds;

    /// <param name="label">Rótulo à esquerda do slot.</param>
    /// <param name="currentId">ID qualificado do item atual, ou null.</param>
    /// <param name="clearText">Texto do botão que limpa o item.</param>
    /// <param name="onChanged">Chamado a cada mudança, com o novo ID (null = limpo).</param>
    public ItemPickerRow(string label, string? currentId, string clearText, Action<string?> onChanged)
    {
        this.label = label;
        this.onChanged = onChanged;
        this.current = currentId != null ? GhostSlot.Create(currentId) : null;
        this.clearButton = new TextButton(clearText);
    }

    protected override int TopHeight => GhostSlot.Size;

    protected override void DrawTop(SpriteBatch b)
    {
        Rectangle top = this.TopBounds;
        float textY = top.Y + ((top.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.label, Game1.smallFont, new Vector2(top.X, textY), Game1.textColor);

        GhostSlot.Draw(b, this.slotBounds, this.current);
        if (this.current != null)
        {
            var namePos = new Vector2(this.slotBounds.Right + Gap, textY);
            Utility.drawTextWithShadow(b, this.current.DisplayName, Game1.smallFont, namePos, Game1.textColor);
        }

        this.clearButton.Draw(b);
    }

    protected override bool ReceiveTopClick(int x, int y)
    {
        if (!this.clearButton.Contains(x, y) || this.current == null)
            return false;

        this.current = null;
        Game1.playSound("trashcan");
        this.onChanged(null);
        return true;
    }

    protected override Item? GetTopHoveredItem(int x, int y)
    {
        return this.slotBounds.Contains(x, y) ? this.current : null;
    }

    protected override void OnInventoryItemClicked(Item item)
    {
        this.current = GhostSlot.Create(item.QualifiedItemId);
        Game1.playSound("smallSelect");
        this.onChanged(item.QualifiedItemId);
    }

    /// <summary>Rótulo à esquerda, slot logo depois e o botão "Limpar" alinhado à direita.</summary>
    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();

        Rectangle top = this.TopBounds;
        int labelWidth = (int)Game1.smallFont.MeasureString(this.label).X;
        this.slotBounds = new Rectangle(top.X + labelWidth + Gap, top.Y, GhostSlot.Size, GhostSlot.Size);

        int buttonWidth = this.clearButton.PreferredWidth;
        this.clearButton.Bounds = new Rectangle(top.Right - buttonWidth, top.Y + ((top.Height - TextButton.ButtonHeight) / 2), buttonWidth, TextButton.ButtonHeight);
    }
}
