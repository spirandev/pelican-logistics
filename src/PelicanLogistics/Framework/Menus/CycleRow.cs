using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Linha com rótulo, valor atual e setas ◀ ▶ que alternam entre as opções.</summary>
internal sealed class CycleRow<TValue> : ConfigRow
{
    private const int RowHeight = 64;

    /// <summary>Espaço entre as setas e o texto do valor.</summary>
    private const int ValueGap = 16;

    /// <summary>Setas em <see cref="Game1.mouseCursors"/> (as mesmas usadas pelos mods do Pathoschild).</summary>
    private static readonly Rectangle LeftArrowSource = new(8, 268, 44, 40);

    private static readonly Rectangle RightArrowSource = new(12, 204, 44, 40);

    private readonly string label;
    private readonly IReadOnlyList<(TValue Value, string Text)> options;
    private readonly Action<TValue> onChanged;
    private readonly ClickableTextureComponent leftArrow;
    private readonly ClickableTextureComponent rightArrow;
    private readonly int valueWidth;
    private int index;

    /// <param name="label">Rótulo à esquerda.</param>
    /// <param name="options">Valores e seus textos, na ordem das setas.</param>
    /// <param name="current">Valor selecionado ao abrir.</param>
    /// <param name="onChanged">Chamado a cada mudança, com o novo valor.</param>
    public CycleRow(string label, IReadOnlyList<(TValue Value, string Text)> options, TValue current, Action<TValue> onChanged)
    {
        if (options.Count == 0)
            throw new ArgumentException("A linha precisa de pelo menos uma opção.", nameof(options));

        this.label = label;
        this.options = options;
        this.onChanged = onChanged;
        this.index = Math.Max(0, options.ToList().FindIndex(o => EqualityComparer<TValue>.Default.Equals(o.Value, current)));
        this.valueWidth = (int)Math.Ceiling(options.Max(o => Game1.smallFont.MeasureString(o.Text).X));
        this.leftArrow = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, LeftArrowSource, 1f);
        this.rightArrow = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, RightArrowSource, 1f);
    }

    public override int Height => RowHeight;

    /// <summary>Valor selecionado.</summary>
    public TValue Value => this.options[this.index].Value;

    public override void Draw(SpriteBatch b)
    {
        Rectangle bounds = this.Bounds;
        float textY = bounds.Y + ((bounds.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.label, Game1.smallFont, new Vector2(bounds.X, textY), Game1.textColor);

        string text = this.options[this.index].Text;
        float textWidth = Game1.smallFont.MeasureString(text).X;
        int valueLeft = this.leftArrow.bounds.Right + ValueGap;
        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(valueLeft + ((this.valueWidth - textWidth) / 2f), textY), Game1.textColor);

        this.leftArrow.draw(b);
        this.rightArrow.draw(b);
    }

    public override bool ReceiveLeftClick(int x, int y)
    {
        if (this.leftArrow.containsPoint(x, y))
        {
            this.Cycle(-1);
            return true;
        }

        if (this.rightArrow.containsPoint(x, y))
        {
            this.Cycle(1);
            return true;
        }

        return false;
    }

    public override void PerformHoverAction(int x, int y)
    {
        this.leftArrow.tryHover(x, y);
        this.rightArrow.tryHover(x, y);
    }

    /// <summary>Setas alinhadas à direita: ◀ valor ▶.</summary>
    protected override void OnBoundsChanged()
    {
        Rectangle bounds = this.Bounds;
        int arrowY = bounds.Y + ((bounds.Height - RightArrowSource.Height) / 2);
        int rightX = bounds.Right - RightArrowSource.Width;
        int leftX = rightX - ValueGap - this.valueWidth - ValueGap - LeftArrowSource.Width;
        this.leftArrow.bounds = new Rectangle(leftX, arrowY, LeftArrowSource.Width, LeftArrowSource.Height);
        this.rightArrow.bounds = new Rectangle(rightX, arrowY, RightArrowSource.Width, RightArrowSource.Height);
    }

    private void Cycle(int delta)
    {
        this.index = (this.index + delta + this.options.Count) % this.options.Count;
        Game1.playSound("drumkit6");
        this.onChanged(this.Value);
    }
}
