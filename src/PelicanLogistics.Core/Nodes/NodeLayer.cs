namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Camada da grade em que um nó está (plano mestre D19, D22). Os valores são os mesmos de
/// <see cref="Persistence.NodeRuntimeState.Layer"/>.
/// </summary>
public enum NodeLayer
{
    /// <summary>Blocos colocados no mapa (esteiras, Inseridores, filtros, Divisores, Túneis).</summary>
    Surface = 0,

    /// <summary>Blocos subterrâneos: Esteiras, Filtros e Divisores Subterrâneos.</summary>
    Underground = 1,
}
