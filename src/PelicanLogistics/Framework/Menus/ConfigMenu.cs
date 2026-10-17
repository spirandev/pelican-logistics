using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Interface base de configuração dos blocos: caixa centralizada com o título no topo, linhas
/// (<see cref="ConfigRow"/>) empilhadas e um botão OK. Fecha também com Esc ou a tecla de menu.
/// Cada linha grava a alteração na hora, pelo seu próprio callback.
/// </summary>
internal class ConfigMenu : IClickableMenu
{
    private const int MenuWidth = 720;
    private const int Padding = 40;
    private const int TitleHeight = 72;
    private const int RowSpacing = 8;
    private const int OkSize = 64;

    /// <summary>Fundo da caixa em <see cref="Game1.menuTexture"/>.</summary>
    private static readonly Rectangle BackgroundSource = new(0, 256, 60, 60);

    private readonly string title;
    private readonly IReadOnlyList<ConfigRow> rows;
    private readonly ClickableTextureComponent okButton;

    public ConfigMenu(string title, string okText, IReadOnlyList<ConfigRow> rows)
        : base(0, 0, MenuWidth, ComputeHeight(rows))
    {
        this.title = title;
        this.rows = rows;
        this.okButton = new ClickableTextureComponent(
            "OK",
            new Rectangle(0, 0, OkSize, OkSize),
            null,
            okText,
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46),
            1f);
        this.Layout();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.okButton.containsPoint(x, y))
        {
            this.exitThisMenu();
            return;
        }

        foreach (ConfigRow row in this.rows)
        {
            if (row.ReceiveLeftClick(x, y))
                return;
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        foreach (ConfigRow row in this.rows)
        {
            if (row.ReceiveKeyPress(key))
                return;
        }

        // Não chama a base de propósito: só Esc e a tecla de menu fecham.
        if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
            this.exitThisMenu();
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
            this.exitThisMenu();
    }

    public override void performHoverAction(int x, int y)
    {
        this.okButton.tryHover(x, y);
        foreach (ConfigRow row in this.rows)
            row.PerformHoverAction(x, y);
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.Layout();
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.4f);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, BackgroundSource, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

        Vector2 titleSize = Game1.dialogueFont.MeasureString(this.title);
        var titlePos = new Vector2(this.xPositionOnScreen + ((this.width - titleSize.X) / 2f), this.yPositionOnScreen + Padding - 8);
        Utility.drawTextWithShadow(b, this.title, Game1.dialogueFont, titlePos, Game1.textColor);

        foreach (ConfigRow row in this.rows)
            row.Draw(b);

        this.okButton.draw(b);
        this.drawMouse(b);
    }

    private static int ComputeHeight(IReadOnlyList<ConfigRow> rows)
    {
        return Padding + TitleHeight + rows.Sum(r => r.Height + RowSpacing) + OkSize + Padding;
    }

    /// <summary>Centraliza a caixa e posiciona as linhas e o botão OK.</summary>
    private void Layout()
    {
        Vector2 topLeft = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height);
        this.xPositionOnScreen = (int)topLeft.X;
        this.yPositionOnScreen = (int)topLeft.Y;

        int y = this.yPositionOnScreen + Padding + TitleHeight;
        foreach (ConfigRow row in this.rows)
        {
            row.SetBounds(this.xPositionOnScreen + Padding, y, this.width - (Padding * 2));
            y += row.Height + RowSpacing;
        }

        this.okButton.bounds = new Rectangle(
            this.xPositionOnScreen + this.width - Padding - OkSize,
            this.yPositionOnScreen + this.height - Padding - OkSize,
            OkSize,
            OkSize);
    }
}
