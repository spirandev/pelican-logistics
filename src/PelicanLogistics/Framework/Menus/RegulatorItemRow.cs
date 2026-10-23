using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Um item da lista do Regulador (modo Por item): slot fantasma com o item, limite (<see cref="NumberField"/>) e botão "Remover".</summary>
internal sealed class RegulatorItemRow : ConfigRow
{
    private const int SlotSize = NumberField.Height;
    private const int Gap = 12;

    private readonly Item item;
    private readonly NumberField field;
    private readonly TextButton removeButton;
    private readonly Action onRemove;
    private Rectangle slotBounds;
    private bool hovered;

    /// <param name="qualifiedItemId">Item limitado.</param>
    /// <param name="limit">Limite ao abrir.</param>
    /// <param name="maxLimit">Maior limite aceito.</param>
    /// <param name="removeText">Texto do botão que tira o item da lista.</param>
    /// <param name="onLimitChanged">Chamado a cada mudança do limite.</param>
    /// <param name="onRemove">Chamado ao clicar em "Remover".</param>
    public RegulatorItemRow(string qualifiedItemId, int limit, int maxLimit, string removeText, Action<int> onLimitChanged, Action onRemove)
    {
        this.item = GhostSlot.Create(qualifiedItemId);
        this.field = new NumberField(limit, 0, maxLimit, onLimitChanged);
        this.removeButton = new TextButton(removeText);
        this.onRemove = onRemove;
    }

    public override int Height => SlotSize;

    public override int MinWidth => SlotSize + Gap + NumberField.Width + Gap + this.removeButton.PreferredWidth;

    public override void Draw(SpriteBatch b)
    {
        GhostSlot.Draw(b, this.slotBounds, this.item);
        this.field.Draw(b);
        this.removeButton.Draw(b);
    }

    public override void DrawTooltip(SpriteBatch b)
    {
        if (this.hovered)
            IClickableMenu.drawToolTip(b, this.item.getDescription(), this.item.DisplayName, this.item);
    }

    public override bool ReceiveLeftClick(int x, int y)
    {
        if (this.removeButton.Contains(x, y))
        {
            this.field.OnClose();
            Game1.playSound("trashcan");
            this.onRemove();
            return true;
        }

        return this.field.ReceiveLeftClick(x, y);
    }

    public override bool ReceiveKeyPress(Keys key)
    {
        return this.field.ReceiveKeyPress(key);
    }

    public override void PerformHoverAction(int x, int y)
    {
        this.hovered = this.slotBounds.Contains(x, y);
    }

    public override void Update()
    {
        this.field.Update();
    }

    public override void OnClose()
    {
        this.field.OnClose();
    }

    /// <summary>Slot à esquerda, o campo logo depois e o botão "Remover" alinhado à direita.</summary>
    protected override void OnBoundsChanged()
    {
        Rectangle bounds = this.Bounds;
        this.slotBounds = new Rectangle(bounds.X, bounds.Y, SlotSize, SlotSize);
        this.field.SetPosition(this.slotBounds.Right + Gap, bounds.Y);

        int buttonWidth = this.removeButton.PreferredWidth;
        this.removeButton.Bounds = new Rectangle(bounds.Right - buttonWidth, bounds.Y, buttonWidth, TextButton.ButtonHeight);
    }
}
