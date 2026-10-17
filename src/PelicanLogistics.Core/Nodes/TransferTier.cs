namespace PelicanLogistics.Core.Nodes;

/// <summary>Versões do Inseridor (README: básico, de Pilha, Configurável).</summary>
public enum TransferTier
{
    /// <summary>1 item por vez.</summary>
    Single,

    /// <summary>A pilha inteira de uma vez.</summary>
    Stack,

    /// <summary>Uma quantidade configurável por vez.</summary>
    Configurable,
}
