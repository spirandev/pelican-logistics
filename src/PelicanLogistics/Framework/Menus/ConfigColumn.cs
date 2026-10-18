using System;
using System.Collections.Generic;
using System.Linq;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Uma coluna de linhas do <see cref="ConfigMenu"/>. Interfaces altas usam duas colunas para caber na tela.</summary>
internal sealed class ConfigColumn
{
    /// <param name="rows">Linhas, de cima para baixo.</param>
    /// <param name="minWidth">Largura mínima; a coluna cresce até a linha mais larga.</param>
    public ConfigColumn(IReadOnlyList<ConfigRow> rows, int minWidth)
    {
        this.Rows = rows;
        this.Width = Math.Max(minWidth, rows.Select(r => r.MinWidth).DefaultIfEmpty(0).Max());
    }

    public IReadOnlyList<ConfigRow> Rows { get; }

    public int Width { get; }
}
