using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>O que sobra ao remover um nó: os itens que ele carregava e o acessório.</summary>
public sealed record RemovalResult(IReadOnlyList<ILogisticsItem> Items, AccessoryKind Accessory)
{
    public static readonly RemovalResult Empty = new(Array.Empty<ILogisticsItem>(), AccessoryKind.None);
}
