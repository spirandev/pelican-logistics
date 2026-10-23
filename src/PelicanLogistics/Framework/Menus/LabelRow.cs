using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Linha só com texto: título de uma seção (ex.: "Regulador") ou uma observação.</summary>
internal sealed class LabelRow : ConfigRow
{
    private const int RowHeight = 40;

    private readonly string text;

    public LabelRow(string text)
    {
        this.text = text;
    }

    public override int Height => RowHeight;

    public override int MinWidth => (int)Game1.smallFont.MeasureString(this.text).X;

    public override void Draw(SpriteBatch b)
    {
        float textY = this.Bounds.Y + ((this.Bounds.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.text, Game1.smallFont, new Vector2(this.Bounds.X, textY), Game1.textColor);
    }
}
