using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Simulation;

/// <summary>
/// Formas, conexões e ordem de processamento de uma grade. Recalculado sempre que algo é colocado,
/// removido ou girado (<see cref="LogisticsGrid.MarkDirty"/>).
/// </summary>
public static class Topology
{
    /// <summary>
    /// Recalcula, nesta ordem, o lado de entrada das esteiras e das Esteiras Subterrâneas, as portas dos Divisores
    /// (dependem do lado de entrada das esteiras) e as entradas dos Inseridores, e devolve a ordem de processamento dos
    /// nós das duas camadas (o Túnel liga a superfície à camada subterrânea).
    /// </summary>
    public static IReadOnlyList<LogisticsNode> Recompute(LogisticsGrid grid)
    {
        List<LogisticsNode> nodes = grid.Nodes
            .OrderBy(n => n.Position.Y)
            .ThenBy(n => n.Position.X)
            .ToList();
        List<UndergroundSegmentNode> segments = grid.UndergroundNodes
            .OrderBy(n => n.Position.Y)
            .ThenBy(n => n.Position.X)
            .ToList();

        foreach (LogisticsNode node in nodes)
        {
            if (node is BeltNode belt)
                belt.InputSide = ComputeInputSide(belt, side => Provides(grid, belt, side));
        }

        foreach (UndergroundSegmentNode segment in segments)
            segment.InputSide = ComputeInputSide(segment, side => ProvidesUnderground(grid, segment, side));

        foreach (LogisticsNode node in nodes)
        {
            if (node is SplitterNode splitter)
                ComputeSplitterPorts(grid, splitter);
        }

        foreach (LogisticsNode node in nodes)
        {
            if (node is InserterNode inserter)
                inserter.Inputs = ComputeInserterInputs(grid, inserter);
        }

        return ComputeOrder(grid, nodes.Concat(segments).ToList());
    }

    /// <summary>
    /// Lado de entrada de uma esteira (README "Regras de conexão"): reta se algo entrega por trás;
    /// curva se nada entrega por trás e exatamente uma lateral entrega; senão reta. 'provides' diz se o vizinho do
    /// lado indicado entrega nela (na camada da esteira).
    /// </summary>
    private static Direction ComputeInputSide(ConveyorNode belt, Func<Direction, bool> provides)
    {
        Direction back = belt.Facing.Opposite();
        if (provides(back))
            return back;

        bool left = provides(belt.Facing.CounterClockwise());
        bool right = provides(belt.Facing.Clockwise());
        if (left != right)
            return left ? belt.Facing.CounterClockwise() : belt.Facing.Clockwise();

        return back;
    }

    /// <summary>
    /// True se o vizinho do lado indicado entrega na Esteira Subterrânea: outra Esteira Subterrânea cuja frente é este
    /// tile, ou a entrada de um Túnel (na superfície do tile vizinho) cuja frente é este tile.
    /// </summary>
    private static bool ProvidesUnderground(LogisticsGrid grid, UndergroundSegmentNode segment, Direction side)
    {
        TilePos pos = segment.Position.Offset(side);
        if (grid.GetUnderground(pos) is { } neighbor && neighbor.ProvidesTo(segment.Position))
            return true;

        return grid.Get(pos) is TunnelNode { Role: TunnelRole.Entrance } tunnel && tunnel.FrontPosition == segment.Position;
    }

    /// <summary>
    /// Portas de um Divisor (plano mestre D4), em ordem horária N → E → S → W. Entrada se o vizinho (esteira, filtro,
    /// Inseridor ou saída de Túnel) entrega no Divisor; saída se o vizinho recebe pelo lado virado para o Divisor
    /// (uma esteira com o Divisor atrás, ou sem nada atrás e com o Divisor na lateral, que vira curva; um filtro,
    /// Inseridor ou entrada de Túnel com a traseira virada para ele). Dois Divisores encostados não se conectam,
    /// nem um Inseridor com o Divisor na lateral.
    /// </summary>
    private static void ComputeSplitterPorts(LogisticsGrid grid, SplitterNode splitter)
    {
        var inputs = new List<Direction>(4);
        var outputs = new List<Direction>(4);
        foreach (Direction side in DirectionExtensions.All)
        {
            LogisticsNode? neighbor = grid.Get(splitter.Position.Offset(side));
            if (neighbor is null or SplitterNode)
                continue;

            // Lado por onde o vizinho recebe; null se ele não recebe de vizinhos da superfície (saída de Túnel).
            Direction? neighborInput = neighbor switch
            {
                BeltNode belt => belt.InputSide,
                FilterNode filter => filter.InputSide,
                InserterNode inserter => inserter.Facing.Opposite(),
                TunnelNode { Role: TunnelRole.Entrance } entrance => entrance.InputSide,
                _ => null,
            };

            if (neighbor.ProvidesTo(splitter.Position))
                inputs.Add(side);
            else if (neighborInput == side.Opposite())
                outputs.Add(side);
        }

        splitter.Inputs = inputs;
        splitter.Outputs = outputs;
    }

    /// <summary>
    /// Entradas de um Inseridor (README "Inseridores"), em ordem horária N → E → S → W: a traseira sempre (pode haver
    /// baú ou máquina ali; o Core não sabe) e cada lateral com um vizinho que entrega nele (uma esteira, filtro ou saída
    /// de Túnel que termina nele; outro Inseridor ou um Divisor não contam). A frente nunca.
    /// </summary>
    private static IReadOnlyList<Direction> ComputeInserterInputs(LogisticsGrid grid, InserterNode inserter)
    {
        Direction back = inserter.Facing.Opposite();
        var inputs = new List<Direction>(3);
        foreach (Direction side in DirectionExtensions.All)
        {
            if (side == back || (side != inserter.Facing && Provides(grid, inserter, side) && !IsUnconnectableAt(grid, inserter, side)))
                inputs.Add(side);
        }

        return inputs;
    }

    /// <summary>Inseridores encostados não se conectam (README "Inseridores"), nem Divisor e Inseridor (plano mestre D4).</summary>
    private static bool IsUnconnectableAt(LogisticsGrid grid, LogisticsNode node, Direction side)
    {
        return grid.Get(node.Position.Offset(side)) is InserterNode or SplitterNode;
    }

    private static bool Provides(LogisticsGrid grid, LogisticsNode node, Direction side)
    {
        LogisticsNode? neighbor = grid.Get(node.Position.Offset(side));
        return neighbor != null && neighbor.ProvidesTo(node.Position);
    }

    /// <summary>
    /// Ordem "de jusante para montante": DFS em pós-ordem seguindo <see cref="LogisticsNode.Downstream"/>,
    /// então todo nó aparece depois dos nós para onde entrega. Ciclos são cortados pela marcação de
    /// visitado (plano mestre D12). Iterativa para não estourar a pilha em linhas longas.
    /// </summary>
    private static IReadOnlyList<LogisticsNode> ComputeOrder(LogisticsGrid grid, IReadOnlyList<LogisticsNode> nodes)
    {
        var order = new List<LogisticsNode>(nodes.Count);
        var visited = new HashSet<LogisticsNode>();
        var stack = new Stack<(LogisticsNode Node, IEnumerator<LogisticsNode> Next)>();

        foreach (LogisticsNode root in nodes)
        {
            if (!visited.Add(root))
                continue;

            stack.Push((root, root.Downstream(grid).GetEnumerator()));
            while (stack.Count > 0)
            {
                (LogisticsNode node, IEnumerator<LogisticsNode> next) = stack.Peek();
                if (next.MoveNext())
                {
                    LogisticsNode child = next.Current;
                    if (visited.Add(child))
                        stack.Push((child, child.Downstream(grid).GetEnumerator()));
                }
                else
                {
                    stack.Pop();
                    next.Dispose();
                    order.Add(node);
                }
            }
        }

        return order;
    }
}
