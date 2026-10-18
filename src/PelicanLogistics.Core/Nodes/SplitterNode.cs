using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Divisor (README "Divisor"): bloco sem direção com 4 portas. Cada esteira (ou filtro) que aponta para ele é uma
/// entrada, e cada uma que sai dele é uma saída (portas calculadas pela <see cref="Topology"/>, plano mestre D4).
/// </summary>
/// <remarks>
/// <para>
/// Puxa as entradas (plano mestre D5), alternando em ordem horária, e distribui cada pilha segundo as regras do
/// README, nesta ordem: (1) saídas com filtro que aceita o item; (2) senão, divisão igual entre as saídas livres
/// sem filtro; (3) pilha menor que o número de saídas: um item por saída, em ordem horária; (4) saída ocupada é
/// pulada, e uma saída com filtro ocupada manda o excedente para as sem filtro; (5) sem saída livre que aceite, trava.
/// </para>
/// <para>
/// O rodízio de entradas e de saídas segue a ordem horária começando pelo norte e continua depois da última porta
/// usada. Segura uma pilha por vez e nunca junta itens.
/// </para>
/// </remarks>
public sealed class SplitterNode : LogisticsNode
{
    /// <summary>O Divisor não tem direção; <see cref="LogisticsNode.Facing"/> é ignorada.</summary>
    public SplitterNode(TilePos position)
        : base(position, Direction.North)
    {
    }

    public override NodeKind Kind => NodeKind.Splitter;

    /// <summary>A pilha que está atravessando o Divisor. <see cref="TransitSlot.EntrySide"/> é a entrada de onde veio.</summary>
    public TransitSlot Slot { get; } = new();

    /// <summary>Última entrada usada no rodízio. Null = nunca puxou (o rodízio começa pelo norte).</summary>
    public Direction? LastInput { get; internal set; }

    /// <summary>Última saída usada no rodízio. Null = nunca entregou (o rodízio começa pelo norte).</summary>
    public Direction? LastOutput { get; internal set; }

    /// <summary>Lados com uma esteira ou filtro apontando para o Divisor, em ordem horária. Calculado pela <see cref="Topology"/>.</summary>
    public IReadOnlyList<Direction> Inputs { get; internal set; } = Array.Empty<Direction>();

    /// <summary>Lados com uma esteira ou filtro que recebe do Divisor, em ordem horária. Calculado pela <see cref="Topology"/>.</summary>
    public IReadOnlyList<Direction> Outputs { get; internal set; } = Array.Empty<Direction>();

    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot };

    /// <summary>O Divisor pode entregar para qualquer lado; quem decide é o vizinho (assim a esteira faz curva saindo dele).</summary>
    public override bool ProvidesTo(TilePos neighbor)
    {
        return this.Position.DirectionTo(neighbor) != null;
    }

    public override IEnumerable<LogisticsNode> Downstream(LogisticsGrid grid)
    {
        foreach (Direction output in this.Outputs)
        {
            if (grid.Get(this.Position.Offset(output)) is { } node)
                yield return node;
        }
    }

    public override void Update(TickContext ctx)
    {
        if (!this.Slot.IsEmpty && this.Slot.Advance(Balance.SplitterSpeed))
            this.Distribute(ctx);

        // Puxa no mesmo tick em que esvaziou, como uma esteira que recebe logo depois de entregar.
        if (this.Slot.IsEmpty)
            this.Pull(ctx);
    }

    // Persistência.

    /// <summary>Entrada = <see cref="LastInput"/>, saída = <see cref="LastOutput"/> (-1 = null, senão o valor de <see cref="Direction"/>).</summary>
    internal override (int Input, int Output) ExportPointers()
    {
        return (this.LastInput is { } input ? (int)input : -1, this.LastOutput is { } output ? (int)output : -1);
    }

    internal override void ImportPointers(int input, int output)
    {
        this.LastInput = Enum.IsDefined(typeof(Direction), input) ? (Direction)input : null;
        this.LastOutput = Enum.IsDefined(typeof(Direction), output) ? (Direction)output : null;
    }

    /// <summary>Regra 6: puxa a pilha inteira da próxima entrada com uma pilha parada no fim, em ordem horária depois de <see cref="LastInput"/>.</summary>
    private void Pull(TickContext ctx)
    {
        foreach (Direction side in ClockwiseAfter(this.LastInput))
        {
            if (!this.Inputs.Contains(side))
                continue;

            TransitSlot? source = ctx.NodeAt(this.Position.Offset(side))?.GetOutgoingSlot(this.Position);
            if (source?.Item == null || source.Progress != Balance.End)
                continue;

            this.Slot.Put(source.Clear()!, side);
            this.LastInput = side;
            return;
        }
    }

    /// <summary>Regras 1 a 5: escolhe as saídas e divide a pilha igualmente entre elas.</summary>
    private void Distribute(TickContext ctx)
    {
        ILogisticsItem item = this.Slot.Item!;
        List<(Direction Side, LogisticsNode Node)> candidates = this.ChooseOutputs(ctx, item);
        if (candidates.Count == 0)
        {
            this.Slot.Blocked = true;
            return;
        }

        int k = Math.Min(candidates.Count, item.Count);
        int share = item.Count / k;
        int remainder = item.Count % k;
        for (int i = 0; i < k; i++)
        {
            (Direction side, LogisticsNode node) = candidates[i];
            if (i == k - 1)
            {
                // A última saída recebe a própria pilha, com o que sobrou.
                int count = item.Count;
                if (node.Accept(side.Opposite(), item, ctx) == count)
                    this.Slot.Clear();
                else
                    this.Slot.Blocked = true;
            }
            else
            {
                ILogisticsItem part = item.Split(share + (i < remainder ? 1 : 0));
                int count = part.Count;

                // Não deveria acontecer (a saída estava livre): o que não entrou volta para a pilha.
                if (node.Accept(side.Opposite(), part, ctx) < count)
                    item.MergeFrom(part, part.Count);
            }

            this.LastOutput = side;
        }
    }

    /// <summary>
    /// Saídas livres que recebem o item, em ordem horária depois de <see cref="LastOutput"/>: as com filtro que
    /// aceita o item; se nenhuma estiver livre, as sem filtro. Uma saída com filtro que não aceita nunca é candidata.
    /// </summary>
    private List<(Direction Side, LogisticsNode Node)> ChooseOutputs(TickContext ctx, ILogisticsItem item)
    {
        var filtered = new List<(Direction, LogisticsNode)>();
        var unfiltered = new List<(Direction, LogisticsNode)>();
        foreach (Direction side in ClockwiseAfter(this.LastOutput))
        {
            if (!this.Outputs.Contains(side) || ctx.NodeAt(this.Position.Offset(side)) is not { } node)
                continue;

            if (!node.CanAccept(side.Opposite(), item))
                continue;

            if (node is FilterNode)
                filtered.Add((side, node));
            else
                unfiltered.Add((side, node));
        }

        return filtered.Count > 0 ? filtered : unfiltered;
    }

    /// <summary>As 4 direções em ordem horária, começando depois de 'last' (null → começa pelo norte).</summary>
    private static IReadOnlyList<Direction> ClockwiseAfter(Direction? last)
    {
        return DirectionExtensions.ClockwiseFrom(last?.Clockwise() ?? Direction.North);
    }
}
