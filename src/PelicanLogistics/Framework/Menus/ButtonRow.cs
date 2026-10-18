using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Linha com um botão de texto alinhado à esquerda (ex.: "Remover regulador").</summary>
internal sealed class ButtonRow : ConfigRow
{
    private readonly TextButton button;
    private readonly Action onClick;

    public ButtonRow(string text, Action onClick)
    {
        this.button = new TextButton(text);
        this.onClick = onClick;
    }

    public override int Height => TextButton.ButtonHeight;

    public override int MinWidth => this.button.PreferredWidth;

    public override void Draw(SpriteBatch b)
    {
        this.button.Draw(b);
    }

    public override bool ReceiveLeftClick(int x, int y)
    {
        if (!this.button.Contains(x, y))
            return false;

        Game1.playSound("smallSelect");
        this.onClick();
        return true;
    }

    protected override void OnBoundsChanged()
    {
        this.button.Bounds = new Rectangle(this.Bounds.X, this.Bounds.Y, this.button.PreferredWidth, TextButton.ButtonHeight);
    }
}
