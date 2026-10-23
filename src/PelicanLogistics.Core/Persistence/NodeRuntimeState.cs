using System.Collections.Generic;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Persistence;

/// <summary>Estado em tempo de execução de um nó. A configuração (direção, slot etc.) fica no <c>modData</c> do jogo.</summary>
public sealed class NodeRuntimeState
{
    /// <summary>Nó da superfície (<see cref="NodeLayer.Surface"/>).</summary>
    public const int SurfaceLayer = (int)NodeLayer.Surface;

    /// <summary>Bloco subterrâneo: Esteira, Filtro ou Divisor Subterrâneo (<see cref="NodeLayer.Underground"/>).</summary>
    public const int UndergroundLayer = (int)NodeLayer.Underground;

    public int X { get; set; }

    public int Y { get; set; }

    public NodeKind Kind { get; set; }

    /// <summary>Camada da grade: <see cref="SurfaceLayer"/> (padrão, inclusive em estados salvos antes da Fase 6b) ou <see cref="UndergroundLayer"/>.</summary>
    public int Layer { get; set; } = SurfaceLayer;

    /// <summary>Um por TransitSlot, na mesma ordem de LogisticsNode.Slots. Slots vazios são gravados com ItemRef = -1.</summary>
    public List<SlotState> Slots { get; set; } = new();

    /// <summary>Ticks de espera (Inseridor: volta do braço). 0 = nenhum.</summary>
    public int Cooldown { get; set; }

    /// <summary>Ponteiros de rodízio (Inseridor: última entrada; Divisor: última entrada e última saída). -1 = não usado.</summary>
    public int InputPointer { get; set; } = -1;

    public int OutputPointer { get; set; } = -1;
}
