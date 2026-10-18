using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Vários liga/desliga (caixas de seleção do jogo) com um rótulo acima, quebrando a linha quando não cabem
/// (qualidades e categorias do Filtro Completo).
/// </summary>
internal sealed class ToggleGroupRow<TValue> : ConfigRow
    where TValue : notnull
{
    private const int LabelHeight = 36;
    private const int LineHeight = 44;
    private const int BoxSize = 36;
    private const int BoxTextGap = 8;
    private const int ToggleGap = 28;
    private const float BoxScale = 4f;

    private readonly string label;
    private readonly IReadOnlyList<(TValue Value, string Text)> options;
    private readonly HashSet<TValue> selected;
    private readonly Action<IReadOnlyList<TValue>> onChanged;
    private readonly Rectangle[] toggles;

    /// <param name="label">Rótulo acima dos toggles.</param>
    /// <param name="options">Valores e seus textos, na ordem de exibição.</param>
    /// <param name="selected">Valores ligados ao abrir.</param>
    /// <param name="onChanged">Chamado a cada mudança, com os valores ligados na ordem de <paramref name="options"/>.</param>
    public ToggleGroupRow(string label, IReadOnlyList<(TValue Value, string Text)> options, IEnumerable<TValue> selected, Action<IReadOnlyList<TValue>> onChanged)
    {
        this.label = label;
        this.options = options;
        this.selected = new HashSet<TValue>(selected);
        this.onChanged = onChanged;
        this.toggles = new Rectangle[options.Count];
    }

    public override int Height => LabelHeight + (this.CountLines(this.Width) * LineHeight);

    public override void Draw(SpriteBatch b)
    {
        Utility.drawTextWithShadow(b, this.label, Game1.smallFont, new Vector2(this.Bounds.X, this.Bounds.Y), Game1.textColor);

        for (int i = 0; i < this.options.Count; i++)
        {
            Rectangle toggle = this.toggles[i];
            Rectangle source = this.selected.Contains(this.options[i].Value) ? OptionsCheckbox.sourceRectChecked : OptionsCheckbox.sourceRectUnchecked;
            b.Draw(Game1.mouseCursors, new Vector2(toggle.X, toggle.Y), source, Color.White, 0f, Vector2.Zero, BoxScale, SpriteEffects.None, 0.4f);

            float textY = toggle.Y + ((BoxSize - Game1.smallFont.LineSpacing) / 2f) + 2;
            Utility.drawTextWithShadow(b, this.options[i].Text, Game1.smallFont, new Vector2(toggle.X + BoxSize + BoxTextGap, textY), Game1.textColor);
        }
    }

    public override bool ReceiveLeftClick(int x, int y)
    {
        int index = Array.FindIndex(this.toggles, t => t.Contains(x, y));
        if (index < 0)
            return false;

        TValue value = this.options[index].Value;
        if (!this.selected.Remove(value))
            this.selected.Add(value);

        Game1.playSound("drumkit6");
        this.onChanged(this.options.Select(o => o.Value).Where(this.selected.Contains).ToList());
        return true;
    }

    /// <summary>Toggles da esquerda para a direita, quebrando a linha quando o próximo não cabe.</summary>
    protected override void OnBoundsChanged()
    {
        int x = this.Bounds.X;
        int y = this.Bounds.Y + LabelHeight;
        for (int i = 0; i < this.options.Count; i++)
        {
            int width = this.ToggleWidth(i);
            if (x > this.Bounds.X && x + width > this.Bounds.Right)
            {
                x = this.Bounds.X;
                y += LineHeight;
            }

            this.toggles[i] = new Rectangle(x, y, width, BoxSize);
            x += width + ToggleGap;
        }
    }

    /// <summary>Quantas linhas de toggles cabem na largura (a mesma quebra de <see cref="OnBoundsChanged"/>).</summary>
    private int CountLines(int width)
    {
        if (this.options.Count == 0)
            return 0;

        int lines = 1;
        int x = 0;
        for (int i = 0; i < this.options.Count; i++)
        {
            int toggleWidth = this.ToggleWidth(i);
            if (x > 0 && x + toggleWidth > width)
            {
                x = 0;
                lines++;
            }

            x += toggleWidth + ToggleGap;
        }

        return lines;
    }

    /// <summary>Caixa + texto: a área clicável do toggle.</summary>
    private int ToggleWidth(int index)
    {
        return BoxSize + BoxTextGap + (int)Math.Ceiling(Game1.smallFont.MeasureString(this.options[index].Text).X);
    }
}
