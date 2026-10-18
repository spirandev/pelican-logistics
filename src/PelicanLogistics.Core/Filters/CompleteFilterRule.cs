using System.Collections.Generic;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Filters;

/// <summary>
/// Regra do Filtro Completo (README "Filtros e roteamento"): lista de itens, categorias, qualidades e context tags,
/// em lista branca ou lista negra.
/// </summary>
/// <remarks>
/// <para>
/// Cada grupo <b>não vazio</b> é um critério. Dentro de um grupo vale OU (o item está na lista de itens, ou é de uma
/// das categorias...). Entre grupos vale E (o item precisa satisfazer todos os grupos preenchidos). Nenhum grupo
/// preenchido = o item "não corresponde".
/// </para>
/// <para>
/// <see cref="FilterMode.Whitelist"/>: passa se corresponde. <see cref="FilterMode.Blacklist"/>: passa se <b>não</b>
/// corresponde. Por isso a lista branca vazia não deixa passar nada, e a lista negra vazia deixa passar tudo.
/// </para>
/// </remarks>
public sealed class CompleteFilterRule : IFilterRule
{
    /// <summary>Qualidades do jogo, na ordem da interface: normal, prata, ouro, irídio.</summary>
    public static readonly IReadOnlyList<int> ValidQualities = new[] { 0, 1, 2, 4 };

    public FilterMode Mode { get; set; } = FilterMode.Whitelist;

    /// <summary>IDs qualificados, ex.: "(O)388".</summary>
    public List<string> ItemIds { get; set; } = new();

    /// <summary>Categorias do jogo, ex.: -75 (vegetais).</summary>
    public List<int> Categories { get; set; } = new();

    /// <summary>Qualidades: 0 normal, 1 prata, 2 ouro, 4 irídio.</summary>
    public List<int> Qualities { get; set; } = new();

    public List<string> ContextTags { get; set; } = new();

    public bool Matches(ILogisticsItem item)
    {
        bool corresponds = this.Corresponds(item);
        return this.Mode == FilterMode.Blacklist ? !corresponds : corresponds;
    }

    /// <summary>True se o item satisfaz todos os grupos preenchidos, e há pelo menos um grupo preenchido.</summary>
    private bool Corresponds(ILogisticsItem item)
    {
        bool anyGroup = false;

        if (this.ItemIds.Count > 0)
        {
            anyGroup = true;
            if (!this.ItemIds.Contains(item.QualifiedItemId))
                return false;
        }

        if (this.Categories.Count > 0)
        {
            anyGroup = true;
            if (!this.Categories.Contains(item.Category))
                return false;
        }

        if (this.Qualities.Count > 0)
        {
            anyGroup = true;
            if (!this.Qualities.Contains(item.Quality))
                return false;
        }

        if (this.ContextTags.Count > 0)
        {
            anyGroup = true;
            if (!this.ContextTags.Exists(item.HasContextTag))
                return false;
        }

        return anyGroup;
    }
}
