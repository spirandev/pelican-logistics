using System;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Endpoints;

/// <summary>Um item adicional exigido por uma máquina (README "Slots virtuais"; ex.: 1 carvão na fornalha).</summary>
/// <param name="Id">Identifica o requisito (ID qualificado do item). Chave do slot Adicional.</param>
/// <param name="RequiredCount">Quantidade exigida por rodada.</param>
/// <param name="Matches">True se o item serve para este requisito.</param>
public sealed record AdditionalRequirement(string Id, int RequiredCount, Func<ILogisticsItem, bool> Matches);
