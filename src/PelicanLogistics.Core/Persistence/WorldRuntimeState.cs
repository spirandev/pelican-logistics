using System.Collections.Generic;

namespace PelicanLogistics.Core.Persistence;

/// <summary>
/// Estado em tempo de execução de todo o mundo (itens em trânsito, mãos dos Inseridores, slots virtuais), salvo
/// em JSON. Os itens não ficam aqui: cada slot guarda o índice do item numa lista externa (ver
/// <see cref="RuntimeStateSerializer"/>).
/// </summary>
public sealed class WorldRuntimeState
{
    /// <summary>Versão atual do formato.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Estado de cada grade, pela chave do local.</summary>
    public Dictionary<string, GridRuntimeState> Grids { get; set; } = new();
}
