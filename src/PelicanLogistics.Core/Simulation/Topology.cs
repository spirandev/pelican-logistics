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
    /// <summary>Recalcula o lado de entrada das esteiras e devolve a ordem de processamento.</summary>
    public static IReadOnlyList<LogisticsNode> Recompute(LogisticsGrid grid)
    {
        List<LogisticsNode> nodes = grid.Nodes
            .OrderBy(n => n.Position.Y)
            .ThenBy(n => n.Position.X)
            .ToList();

        foreach (LogisticsNode node in nodes)
        {
            if (node is BeltNode belt)
                belt.InputSide = ComputeInputSide(grid, belt);
        }

        return ComputeOrder(grid, nodes);
    }

    /// <summary>
    /// Lado de entrada de uma esteira (README "Regras de conexão"): reta se algo entrega por trás;
    /// curva se nada entrega por trás e exatamente uma lateral entrega; senão reta.
    /// </summary>
    private static Direction ComputeInputSide(LogisticsGrid grid, BeltNode belt)
    {
        Direction back = belt.Facing.Opposite();
        if (Provides(grid, belt, back))
            return back;

        bool left = Provides(grid, belt, belt.Facing.CounterClockwise());
        bool right = Provides(grid, belt, belt.Facing.Clockwise());
        if (left != right)
            return left ? belt.Facing.CounterClockwise() : belt.Facing.Clockwise();

        return back;
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
