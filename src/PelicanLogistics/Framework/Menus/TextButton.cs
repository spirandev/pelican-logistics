using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Botão com texto, no estilo dos botões das opções do jogo.</summary>
internal sealed class TextButton
{
    public const int ButtonHeight = 56;

    /// <summary>Caixa do botão em <see cref="Game1.mouseCursors"/> (a mesma do <c>OptionsButton</c>).</summary>
    private static readonly Rectangle BoxSource = new(432, 439, 9, 9);

    private const int TextPadding = 24;

    public TextButton(string text)
    {
        this.Text = text;
    }

    public string Text { get; }

    public Rectangle Bounds { get; set; }

    /// <summary>Largura que cabe o texto com folga.</summary>
    public int PreferredWidth => (int)Game1.smallFont.MeasureString(this.Text).X + (TextPadding * 2);

    public bool Contains(int x, int y)
    {
        return this.Bounds.Contains(x, y);
    }

    public void Draw(SpriteBatch b)
    {
        Rectangle box = this.Bounds;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, BoxSource, box.X, box.Y, box.Width, box.Height, Color.White, 4f, false);

        Vector2 size = Game1.smallFont.MeasureString(this.Text);
        var pos = new Vector2(box.X + ((box.Width - size.X) / 2f), box.Y + ((box.Height - size.Y) / 2f) + 2);
        Utility.drawTextWithShadow(b, this.Text, Game1.smallFont, pos, Game1.textColor);
    }
}
