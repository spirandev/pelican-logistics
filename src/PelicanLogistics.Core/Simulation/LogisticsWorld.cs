using System;
using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;

namespace PelicanLogistics.Core.Simulation;

/// <summary>Todas as grades do jogo, uma por local.</summary>
public sealed class LogisticsWorld
{
    private readonly Dictionary<string, LogisticsGrid> grids = new(StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<string, LogisticsGrid>> Grids => this.grids;

    public LogisticsGrid GetOrCreate(string locationKey)
    {
        if (!this.grids.TryGetValue(locationKey, out LogisticsGrid? grid))
        {
            grid = new LogisticsGrid();
            this.grids[locationKey] = grid;
        }

        return grid;
    }

    public LogisticsGrid? Get(string locationKey)
    {
        return this.grids.TryGetValue(locationKey, out LogisticsGrid? grid) ? grid : null;
    }

    public void Clear()
    {
        this.grids.Clear();
    }

    /// <summary>
    /// Um tick em todas as grades com nós ou com itens nos slots virtuais (a máquina ainda pode ser acionada
    /// depois que o Inseridor for removido). 'endpointsFor' devolve os endpoints do local, ou null se o
    /// local estiver indisponível (a grade é pulada nesse tick).
    /// </summary>
    public void Tick(Func<string, IEndpointProvider?> endpointsFor)
    {
        foreach ((string key, LogisticsGrid grid) in this.grids)
        {
            if (grid.Count == 0 && grid.MachineSlots.Count == 0)
                continue;

            IEndpointProvider? endpoints = endpointsFor(key);
            if (endpoints != null)
                grid.Tick(endpoints);
        }
    }
}
