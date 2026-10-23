namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Tipos de bloco do mod. Os valores são gravados no estado salvo (persistência), então nunca são renumerados: o 4 era
/// a Esteira Subterrânea de vão fixo da Fase 6, removida na Fase 6b.
/// </summary>
public enum NodeKind
{
    Belt = 0,
    Inserter = 1,
    Filter = 2,
    Splitter = 3,
    Tunnel = 5,
    UndergroundSegment = 6,
}
