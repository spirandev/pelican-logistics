using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Interface base de configuração dos blocos: caixa centralizada com o título e o botão OK no topo e as linhas
/// (<see cref="ConfigRow"/>) empilhadas em uma ou mais colunas (<see cref="ConfigColumn"/>). Fecha também com Esc ou
/// a tecla de menu. Cada linha grava a alteração na hora, pelo seu próprio callback.
/// </summary>
internal class ConfigMenu : IClickableMenu
{
    /// <summary>Largura da coluna de uma interface de coluna única.</summary>
    private const int SingleColumnWidth = 656;

    private const int Padding = 32;
    private const int TitleHeight = 72;
    private const int RowSpacing = 8;
    private const int ColumnGap = 32;
    private const int OkSize = 64;

    /// <summary>Fundo da caixa em <see cref="Game1.menuTexture"/>.</summary>
    private static readonly Rectangle BackgroundSource = new(0, 256, 60, 60);

    private readonly string title;
    private readonly IReadOnlyList<ConfigColumn> columns;
    private readonly IReadOnlyList<ConfigRow> rows;
    private readonly ClickableTextureComponent okButton;

    /// <summary>Interface de uma coluna.</summary>
    public ConfigMenu(string title, string okText, IReadOnlyList<ConfigRow> rows)
        : this(title, okText, new[] { new ConfigColumn(rows, SingleColumnWidth) })
    {
    }

    public ConfigMenu(string title, string okText, IReadOnlyList<ConfigColumn> columns)
        : base(0, 0, ComputeWidth(columns), ComputeHeight(columns))
    {
        this.title = title;
        this.columns = columns;
        this.rows = columns.SelectMany(c => c.Rows).ToList();
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

        // Todas as linhas recebem o clique: um campo de texto precisa saber quando o clique foi fora dele.
        foreach (ConfigRow row in this.rows)
            row.ReceiveLeftClick(x, y);
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

    public override void update(GameTime time)
    {
        base.update(time);
        foreach (ConfigRow row in this.rows)
            row.Update();
    }

    public override void emergencyShutDown()
    {
        base.emergencyShutDown();
        this.CloseRows();
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
        foreach (ConfigRow row in this.rows)
            row.DrawTooltip(b);
        this.drawMouse(b);
    }

    protected override void cleanupBeforeExit()
    {
        base.cleanupBeforeExit();
        this.CloseRows();
    }

    private void CloseRows()
    {
        foreach (ConfigRow row in this.rows)
            row.OnClose();
    }

    private static int ComputeWidth(IReadOnlyList<ConfigColumn> columns)
    {
        return (Padding * 2) + columns.Sum(c => c.Width) + (ColumnGap * Math.Max(0, columns.Count - 1));
    }

    /// <summary>Altura da coluna mais alta, mais título e margens. Informa antes a largura a cada linha.</summary>
    private static int ComputeHeight(IReadOnlyList<ConfigColumn> columns)
    {
        int tallest = 0;
        foreach (ConfigColumn column in columns)
        {
            foreach (ConfigRow row in column.Rows)
                row.Measure(column.Width);

            tallest = Math.Max(tallest, column.Rows.Sum(r => r.Height + RowSpacing) - RowSpacing);
        }

        return Padding + TitleHeight + tallest + Padding;
    }

    /// <summary>
    /// Centraliza a caixa e posiciona as colunas e o botão OK (no canto de cima, na altura do título). Se a caixa não
    /// couber na tela, ela fica encostada no canto de cima, para o título e o OK continuarem visíveis.
    /// </summary>
    private void Layout()
    {
        Vector2 topLeft = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height);
        this.xPositionOnScreen = Math.Max(0, (int)topLeft.X);
        this.yPositionOnScreen = Math.Max(0, (int)topLeft.Y);

        int x = this.xPositionOnScreen + Padding;
        foreach (ConfigColumn column in this.columns)
        {
            int y = this.yPositionOnScreen + Padding + TitleHeight;
            foreach (ConfigRow row in column.Rows)
            {
                row.SetBounds(x, y, column.Width);
                y += row.Height + RowSpacing;
            }

            x += column.Width + ColumnGap;
        }

        this.okButton.bounds = new Rectangle(
            this.xPositionOnScreen + this.width - Padding - OkSize,
            this.yPositionOnScreen + Padding - 8,
            OkSize,
            OkSize);
    }
}
