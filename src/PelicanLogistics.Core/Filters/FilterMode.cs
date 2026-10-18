namespace PelicanLogistics.Core.Filters;

/// <summary>Modo do Filtro Completo.</summary>
public enum FilterMode
{
    /// <summary>Lista branca: passa o que corresponde.</summary>
    Whitelist,

    /// <summary>Lista negra: passa o que não corresponde.</summary>
    Blacklist,
}
