using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Linha com rótulo à esquerda e um <see cref="NumberField"/> alinhado à direita.</summary>
internal sealed class NumberRow : ConfigRow
{
    private const int RowHeight = 64;
    private const int Gap = 16;

    private readonly string label;
    private readonly NumberField field;

    /// <param name="label">Rótulo à esquerda.</param>
    /// <param name="value">Valor ao abrir.</param>
    /// <param name="min">Menor valor aceito.</param>
    /// <param name="max">Maior valor aceito.</param>
    /// <param name="onChanged">Chamado a cada mudança, com o novo valor.</param>
    public NumberRow(string label, int value, int min, int max, Action<int> onChanged)
    {
        this.label = label;
        this.field = new NumberField(value, min, max, onChanged);
    }

    public override int Height => RowHeight;

    public override int MinWidth => (int)Game1.smallFont.MeasureString(this.label).X + Gap + NumberField.Width;

    public override void Draw(SpriteBatch b)
    {
        float textY = this.Bounds.Y + ((this.Bounds.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.label, Game1.smallFont, new Vector2(this.Bounds.X, textY), Game1.textColor);
        this.field.Draw(b);
    }

    public override bool ReceiveLeftClick(int x, int y)
    {
        return this.field.ReceiveLeftClick(x, y);
    }

    public override bool ReceiveKeyPress(Keys key)
    {
        return this.field.ReceiveKeyPress(key);
    }

    public override void Update()
    {
        this.field.Update();
    }

    public override void OnClose()
    {
        this.field.OnClose();
    }

    protected override void OnBoundsChanged()
    {
        this.field.SetPosition(this.Bounds.Right - NumberField.Width, this.Bounds.Y + ((this.Bounds.Height - NumberField.Height) / 2));
    }
}
