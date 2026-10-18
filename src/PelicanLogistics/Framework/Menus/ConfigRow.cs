using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Uma linha de configuração do <see cref="ConfigMenu"/>: <see cref="CycleRow{TValue}"/>, <see cref="ItemPickerRow"/>,
/// <see cref="ItemListRow"/>, <see cref="ToggleGroupRow{TValue}"/> e <see cref="TextRow"/>. A Fase 6 acrescenta NumberRow e ButtonRow.
/// </summary>
internal abstract class ConfigRow
{
    /// <summary>Área da linha na tela, definida pelo menu em <see cref="SetBounds"/>.</summary>
    protected Rectangle Bounds { get; private set; }

    /// <summary>Largura disponível para a linha, definida pelo menu em <see cref="Measure"/> antes de pedir a altura.</summary>
    protected int Width { get; private set; }

    /// <summary>Altura da linha em pixels. Pode depender de <see cref="Width"/>.</summary>
    public abstract int Height { get; }

    /// <summary>Largura mínima da linha (o menu fica pelo menos com essa largura, mais as margens).</summary>
    public virtual int MinWidth => 0;

    /// <summary>Informa a largura da linha. Chamado pelo menu antes de <see cref="Height"/>.</summary>
    public void Measure(int width)
    {
        this.Width = width;
    }

    /// <summary>Posiciona a linha. Chamado pelo menu ao abrir e quando a janela muda de tamanho.</summary>
    public void SetBounds(int x, int y, int width)
    {
        this.Width = width;
        this.Bounds = new Rectangle(x, y, width, this.Height);
        this.OnBoundsChanged();
    }

    public abstract void Draw(SpriteBatch b);

    /// <summary>Desenha por cima de tudo (dicas de itens). Chamado depois de todas as linhas.</summary>
    public virtual void DrawTooltip(SpriteBatch b)
    {
    }

    /// <summary>Clique esquerdo. O menu repassa o clique a todas as linhas. Retorna true se a linha tratou o clique.</summary>
    public virtual bool ReceiveLeftClick(int x, int y)
    {
        return false;
    }

    /// <summary>Tecla pressionada. Retorna true se a linha tratou a tecla (o menu não a usa para fechar).</summary>
    public virtual bool ReceiveKeyPress(Keys key)
    {
        return false;
    }

    public virtual void PerformHoverAction(int x, int y)
    {
    }

    /// <summary>Uma vez por tick, enquanto o menu está aberto.</summary>
    public virtual void Update()
    {
    }

    /// <summary>O menu vai fechar: grava o que estiver pendente e solta o teclado.</summary>
    public virtual void OnClose()
    {
    }

    /// <summary>Reposiciona os componentes internos depois de <see cref="SetBounds"/>.</summary>
    protected virtual void OnBoundsChanged()
    {
    }
}
