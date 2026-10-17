using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Persistence;

/// <summary>Item salvo que não encontrou destino ao carregar. O adaptador o solta no chão do local, na posição indicada.</summary>
public sealed record OrphanItem(string LocationKey, TilePos Position, ILogisticsItem Item);
