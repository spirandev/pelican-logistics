using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Regulators;

/// <summary>
/// Regra do Regulador (README "Regulador"): o Inseridor para de inserir quando o baú de destino atinge o limite. Só tem
/// efeito com destino baú (plano mestre D8).
/// </summary>
public sealed class RegulatorRule
{
    public RegulatorMode Mode { get; set; } = RegulatorMode.General;

    /// <summary>Limite do total de itens no baú (modo <see cref="RegulatorMode.General"/>).</summary>
    public int GeneralLimit { get; set; } = Balance.RegulatorDefaultLimit;

    /// <summary>QualifiedItemId → limite (modo <see cref="RegulatorMode.PerItem"/>). Itens fora da lista não são limitados (plano mestre D7).</summary>
    public Dictionary<string, int> PerItemLimits { get; set; } = new();

    /// <summary>Quantas unidades de 'item' ainda podem entrar no baú. <see cref="int.MaxValue"/> = sem limite.</summary>
    public int AllowedAmount(ILogisticsItem item, IChestEndpoint chest)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (chest == null)
            throw new ArgumentNullException(nameof(chest));

        return this.Mode switch
        {
            RegulatorMode.General => Math.Max(0, this.GeneralLimit - chest.CountTotal()),
            RegulatorMode.PerItem => this.PerItemLimits.TryGetValue(item.QualifiedItemId, out int limit)
                ? Math.Max(0, limit - chest.CountById(item.QualifiedItemId))
                : int.MaxValue,
            _ => throw new InvalidOperationException($"Modo de regulador inválido: {this.Mode}."),
        };
    }
}
