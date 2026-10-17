using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Uma linha de configuração do <see cref="ConfigMenu"/>. Esta fase tem <see cref="CycleRow{TValue}"/>; as Fases 4
/// e 5 acrescentam ItemPickerRow, ToggleRow, NumberRow, TextRow e ButtonRow.
/// </summary>
internal abstract class ConfigRow
{
    /// <summary>Área da linha na tela, definida pelo menu em <see cref="SetBounds"/>.</summary>
    protected Rectangle Bounds { get; private set; }

    /// <summary>Altura da linha em pixels.</summary>
    public abstract int Height { get; }

    /// <summary>Posiciona a linha. Chamado pelo menu ao abrir e quando a janela muda de tamanho.</summary>
    public void SetBounds(int x, int y, int width)
    {
        this.Bounds = new Rectangle(x, y, width, this.Height);
        this.OnBoundsChanged();
    }

    public abstract void Draw(SpriteBatch b);

    /// <summary>Clique esquerdo. Retorna true se a linha tratou o clique.</summary>
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

    /// <summary>Reposiciona os componentes internos depois de <see cref="SetBounds"/>.</summary>
    protected virtual void OnBoundsChanged()
    {
    }
}
