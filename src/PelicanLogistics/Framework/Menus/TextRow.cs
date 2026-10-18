using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Campo de texto com rótulo (context tags do Filtro Completo). O texto é gravado quando muda; clicar no campo
/// ativa a digitação e clicar fora (ou Enter) desativa.
/// </summary>
internal sealed class TextRow : ConfigRow
{
    private const int RowHeight = 64;
    private const int Gap = 16;

    private readonly string label;
    private readonly Action<string> onChanged;
    private readonly TextBox textBox;
    private string lastText;

    /// <param name="label">Rótulo à esquerda.</param>
    /// <param name="text">Texto ao abrir.</param>
    /// <param name="onChanged">Chamado quando o texto muda, com o texto novo.</param>
    public TextRow(string label, string text, Action<string> onChanged)
    {
        this.label = label;
        this.onChanged = onChanged;
        // Sem limite de largura: com ele, o texto inicial seria cortado na largura da textura, antes do layout.
        // Um texto maior que o campo é desenhado só com o final visível.
        this.textBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
        {
            limitWidth = false,
            Text = text,
        };
        this.textBox.OnEnterPressed += _ => this.textBox.Selected = false;
        this.lastText = this.textBox.Text;
    }

    public override int Height => RowHeight;

    public override void Draw(SpriteBatch b)
    {
        float textY = this.Bounds.Y + ((this.Bounds.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.label, Game1.smallFont, new Vector2(this.Bounds.X, textY), Game1.textColor);
        this.textBox.Draw(b);
    }

    /// <summary>Seleciona o campo se o clique foi nele; senão, solta o teclado.</summary>
    public override bool ReceiveLeftClick(int x, int y)
    {
        bool inside = new Rectangle(this.textBox.X, this.textBox.Y, this.textBox.Width, this.textBox.Height).Contains(x, y);
        this.textBox.Selected = inside;
        return inside;
    }

    /// <summary>Enquanto o campo está ativo, as teclas são do texto: só Esc fecha o menu.</summary>
    public override bool ReceiveKeyPress(Keys key)
    {
        return this.textBox.Selected && key != Keys.Escape;
    }

    public override void Update()
    {
        this.Commit();
    }

    public override void OnClose()
    {
        this.textBox.Selected = false;
        this.Commit();
    }

    /// <summary>Campo à direita do rótulo, ocupando o resto da linha.</summary>
    protected override void OnBoundsChanged()
    {
        int labelWidth = (int)Game1.smallFont.MeasureString(this.label).X;
        this.textBox.X = this.Bounds.X + labelWidth + Gap;
        this.textBox.Y = this.Bounds.Y + ((this.Bounds.Height - this.textBox.Height) / 2);
        this.textBox.Width = this.Bounds.Right - this.textBox.X;
    }

    private void Commit()
    {
        if (this.textBox.Text == this.lastText)
            return;

        this.lastText = this.textBox.Text;
        this.onChanged(this.lastText);
    }
}
