using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Filters;

/// <summary>
/// Regra do Filtro Comum (README "Filtros e roteamento"): um único item. Compara só o ID, então qualquer qualidade
/// passa (plano mestre D6); qualidade é critério do Filtro Completo.
/// </summary>
public sealed class CommonFilterRule : IFilterRule
{
    /// <summary>ID qualificado do item, ex.: "(O)388". Null = nada configurado = não deixa passar nada.</summary>
    public string? QualifiedItemId { get; set; }

    public bool Matches(ILogisticsItem item)
    {
        return this.QualifiedItemId != null && item.QualifiedItemId == this.QualifiedItemId;
    }
}
