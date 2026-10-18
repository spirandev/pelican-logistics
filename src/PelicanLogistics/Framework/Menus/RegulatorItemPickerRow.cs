using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Escolha do item a limitar no Regulador: instrução e botão "Cancelar" em cima, o inventário embaixo. Clicar num item
/// do inventário escolhe o item sem tirá-lo do inventário.
/// </summary>
internal sealed class RegulatorItemPickerRow : InventoryRow
{
    private readonly string instruction;
    private readonly TextButton cancelButton;
    private readonly Action<string> onPicked;
    private readonly Action onCancel;

    /// <param name="instruction">Texto à esquerda, em cima do inventário.</param>
    /// <param name="cancelText">Texto do botão que volta sem escolher.</param>
    /// <param name="onPicked">Chamado com o ID qualificado do item clicado.</param>
    /// <param name="onCancel">Chamado ao clicar em "Cancelar".</param>
    public RegulatorItemPickerRow(string instruction, string cancelText, Action<string> onPicked, Action onCancel)
    {
        this.instruction = instruction;
        this.cancelButton = new TextButton(cancelText);
        this.onPicked = onPicked;
        this.onCancel = onCancel;
    }

    protected override int TopHeight => TextButton.ButtonHeight;

    protected override void DrawTop(SpriteBatch b)
    {
        Rectangle top = this.TopBounds;
        float textY = top.Y + ((top.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.instruction, Game1.smallFont, new Vector2(top.X, textY), Game1.textColor);
        this.cancelButton.Draw(b);
    }

    protected override bool ReceiveTopClick(int x, int y)
    {
        if (!this.cancelButton.Contains(x, y))
            return false;

        Game1.playSound("smallSelect");
        this.onCancel();
        return true;
    }

    protected override Item? GetTopHoveredItem(int x, int y)
    {
        return null;
    }

    protected override void OnInventoryItemClicked(Item item)
    {
        this.onPicked(item.QualifiedItemId);
    }

    /// <summary>Instrução à esquerda e o botão "Cancelar" alinhado à direita.</summary>
    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();

        Rectangle top = this.TopBounds;
        int buttonWidth = this.cancelButton.PreferredWidth;
        this.cancelButton.Bounds = new Rectangle(top.Right - buttonWidth, top.Y, buttonWidth, TextButton.ButtonHeight);
    }
}
