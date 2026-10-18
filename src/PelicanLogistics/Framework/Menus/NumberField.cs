using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Campo numérico com botões <c>-10</c>, <c>-1</c>, <c>+1</c>, <c>+10</c> e um campo digitável no meio (só dígitos). O
/// valor fica sempre no intervalo do campo. Usado pelo <see cref="NumberRow"/> e pelo <see cref="RegulatorItemRow"/>.
/// </summary>
internal sealed class NumberField
{
    public const int Height = TextButton.ButtonHeight;

    private const int ButtonWidth = 56;
    private const int FieldWidth = 96;
    private const int Gap = 8;

    /// <summary>Passos dos botões, da esquerda para a direita (o campo fica entre o segundo e o terceiro).</summary>
    private static readonly int[] Steps = { -10, -1, 1, 10 };

    private readonly int min;
    private readonly int max;
    private readonly Action<int> onChanged;
    private readonly TextButton[] buttons;
    private readonly TextBox textBox;
    private int value;

    /// <param name="value">Valor ao abrir (limitado ao intervalo).</param>
    /// <param name="min">Menor valor aceito.</param>
    /// <param name="max">Maior valor aceito.</param>
    /// <param name="onChanged">Chamado a cada mudança, com o novo valor.</param>
    public NumberField(int value, int min, int max, Action<int> onChanged)
    {
        this.min = min;
        this.max = max;
        this.onChanged = onChanged;
        this.value = Math.Clamp(value, min, max);
        this.buttons = Array.ConvertAll(Steps, step => new TextButton(step > 0 ? $"+{step}" : step.ToString()));
        this.textBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
        {
            numbersOnly = true,
            textLimit = max.ToString().Length,
            limitWidth = false,
            Text = this.value.ToString(),
        };
        this.textBox.OnEnterPressed += _ => this.Deselect();
    }

    /// <summary>Largura total: os quatro botões e o campo.</summary>
    public static int Width => (Steps.Length * ButtonWidth) + FieldWidth + (Steps.Length * Gap);

    public int Value => this.value;

    /// <summary>Posiciona o campo com o canto superior esquerdo em (x, y).</summary>
    public void SetPosition(int x, int y)
    {
        int left = x;
        for (int i = 0; i < this.buttons.Length; i++)
        {
            if (i == Steps.Length / 2)
            {
                this.textBox.X = left;
                this.textBox.Y = y + ((Height - this.textBox.Height) / 2);
                this.textBox.Width = FieldWidth;
                left += FieldWidth + Gap;
            }

            this.buttons[i].Bounds = new Rectangle(left, y, ButtonWidth, Height);
            left += ButtonWidth + Gap;
        }
    }

    public void Draw(SpriteBatch b)
    {
        foreach (TextButton button in this.buttons)
            button.Draw(b);
        this.textBox.Draw(b);
    }

    /// <summary>Botão: soma o passo. Campo: ativa a digitação. Clique fora: solta o teclado. Retorna true se tratou.</summary>
    public bool ReceiveLeftClick(int x, int y)
    {
        for (int i = 0; i < this.buttons.Length; i++)
        {
            if (this.buttons[i].Contains(x, y))
            {
                this.Deselect();
                this.Set(this.value + Steps[i]);
                Game1.playSound("smallSelect");
                return true;
            }
        }

        bool inside = new Rectangle(this.textBox.X, this.textBox.Y, this.textBox.Width, this.textBox.Height).Contains(x, y);
        if (inside)
            this.textBox.Selected = true;
        else
            this.Deselect();
        return inside;
    }

    /// <summary>Enquanto o campo está ativo, as teclas são dele: só Esc fecha o menu.</summary>
    public bool ReceiveKeyPress(Keys key)
    {
        return this.textBox.Selected && key != Keys.Escape;
    }

    /// <summary>Aplica o que foi digitado (limitado ao intervalo). Campo vazio não muda o valor.</summary>
    public void Update()
    {
        if (int.TryParse(this.textBox.Text, out int typed) && Math.Clamp(typed, this.min, this.max) != this.value)
            this.Set(typed, rewriteText: false);
    }

    public void OnClose()
    {
        this.Deselect();
    }

    /// <summary>Solta o teclado e mostra o valor atual no campo (ex.: depois de apagar tudo ou passar do limite).</summary>
    private void Deselect()
    {
        this.textBox.Selected = false;
        this.Update();
        this.textBox.Text = this.value.ToString();
    }

    private void Set(int newValue, bool rewriteText = true)
    {
        newValue = Math.Clamp(newValue, this.min, this.max);
        if (rewriteText)
            this.textBox.Text = newValue.ToString();
        if (newValue == this.value)
            return;

        this.value = newValue;
        this.onChanged(newValue);
    }
}
