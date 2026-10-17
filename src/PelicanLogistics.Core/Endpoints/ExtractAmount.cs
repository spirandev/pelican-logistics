namespace PelicanLogistics.Core.Endpoints;

/// <summary>Quanto retirar de um baú em uma operação (ver <see cref="IChestEndpoint.Extract"/>).</summary>
public enum ExtractAmount
{
    /// <summary>Uma unidade.</summary>
    One,

    /// <summary>A pilha inteira de um slot.</summary>
    Stack,

    /// <summary>Até uma quantidade exata, podendo juntar de vários slots.</summary>
    Exactly,
}
