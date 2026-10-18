using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Filters;

/// <summary>Regra de um filtro (README "Filtro"): decide se um item passa.</summary>
public interface IFilterRule
{
    /// <summary>True se o item passa pelo filtro. Sem efeito colateral.</summary>
    bool Matches(ILogisticsItem item);
}
