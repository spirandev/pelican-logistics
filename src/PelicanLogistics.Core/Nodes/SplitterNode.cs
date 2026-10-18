using System;
using System.Collections.Generic;
using System.Linq;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Nodes;

/// <summary>
/// Divisor (README "Divisor"): bloco sem direção com 4 portas. Cada esteira, filtro, Inseridor ou saída de Esteira
/// Subterrânea que aponta para ele é uma entrada, e cada esteira ou filtro que sai dele, ou Inseridor ou entrada de
/// Subterrânea com a traseira virada para ele, é uma saída (portas calculadas pela <see cref="Topology"/>, plano mestre D4).
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
/// <para>
/// Inseridores como portas: o Divisor puxa o item da mão de um Inseridor de entrada, e entrega na mão de um Inseridor
/// de saída só o que ele pegaria agora (braço parado, quantidade do tier, o que o destino dele aceita). O que o
/// Inseridor não pegou fica no Divisor e é distribuído de novo no próximo tick.
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

    /// <summary>Lados com uma esteira, filtro, Inseridor ou saída de Subterrânea apontando para o Divisor, em ordem horária. Calculado pela <see cref="Topology"/>.</summary>
    public IReadOnlyList<Direction> Inputs { get; internal set; } = Array.Empty<Direction>();

    /// <summary>Lados com uma esteira, filtro, Inseridor ou entrada de Subterrânea que recebe do Divisor, em ordem horária. Calculado pela <see cref="Topology"/>.</summary>
    public IReadOnlyList<Direction> Outputs { get; internal set; } = Array.Empty<Direction>();

    public override IEnumerable<TransitSlot> Slots => new[] { this.Slot };

    /// <summary>O Divisor pode entregar para qualquer lado; quem decide é o vizinho (assim a esteira faz curva saindo dele).</summary>
    public override bool ProvidesTo(TilePos neighbor)
    {
        return this.Position.DirectionTo(neighbor) != null;
    }

    /// <summary>
    /// True se o Divisor puxaria agora um item desse lado: é uma entrada e ele está vazio. Usado pelo Inseridor de
    /// entrada para saber se pode pegar; o Divisor nunca recebe empurrado (<see cref="LogisticsNode.Accept"/> = 0).
    /// </summary>
    public override bool CanAccept(Direction entrySide, ILogisticsItem item)
    {
        return this.Slot.IsEmpty && this.Inputs.Contains(entrySide);
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

    /// <summary>
    /// Regras 1 a 5: escolhe as saídas e divide a pilha igualmente entre elas. O que uma saída não recebeu (um
    /// Inseridor pega só a quantidade do tier) fica no Divisor, bloqueado, e é distribuído de novo no próximo tick.
    /// </summary>
    private void Distribute(TickContext ctx)
    {
        ILogisticsItem item = this.Slot.Item!;
        List<(Direction Side, LogisticsNode Node)> candidates = this.ChooseOutputs(ctx, item);
        int k = Math.Min(candidates.Count, item.Count);
        if (k == 0)
        {
            this.Slot.Blocked = true;
            return;
        }

        int share = item.Count / k;
        int remainder = item.Count % k;
        for (int i = 0; i < k; i++)
        {
            (Direction side, LogisticsNode node) = candidates[i];
            int amount = Math.Min(item.Count, share + (i < remainder ? 1 : 0));

            // A pilha inteira vai como a própria instância; uma parte, separada dela.
            bool whole = amount == item.Count;
            ILogisticsItem part = whole ? item : item.Split(amount);
            int taken = Give(node, side, part, ctx);
            if (taken > 0)
                this.LastOutput = side;

            if (whole)
            {
                if (taken == amount)
                {
                    this.Slot.Clear();
                    return;
                }

                break;
            }

            // O que não entrou volta para a pilha.
            if (taken < amount)
                item.MergeFrom(part, part.Count);
        }

        this.Slot.Blocked = true;
    }

    /// <summary>Entrega numa saída: empurra numa esteira ou filtro; na mão de um Inseridor, só o que ele pegaria.</summary>
    private static int Give(LogisticsNode node, Direction side, ILogisticsItem part, TickContext ctx)
    {
        return node is InserterNode inserter
            ? inserter.Offer(side.Opposite(), part, ctx)
            : node.Accept(side.Opposite(), part, ctx);
    }

    /// <summary>
    /// Saídas livres que recebem o item, em ordem horária depois de <see cref="LastOutput"/>: as com filtro que
    /// aceita o item; se nenhuma estiver livre, as sem filtro (esteiras e Inseridores que pegariam o item agora). Uma
    /// saída com filtro que não aceita nunca é candidata.
    /// </summary>
    private List<(Direction Side, LogisticsNode Node)> ChooseOutputs(TickContext ctx, ILogisticsItem item)
    {
        var filtered = new List<(Direction, LogisticsNode)>();
        var unfiltered = new List<(Direction, LogisticsNode)>();
        foreach (Direction side in ClockwiseAfter(this.LastOutput))
        {
            if (!this.Outputs.Contains(side) || ctx.NodeAt(this.Position.Offset(side)) is not { } node)
                continue;

            bool free = node is InserterNode inserter
                ? inserter.AmountToTake(side.Opposite(), item, ctx) > 0
                : node.CanAccept(side.Opposite(), item);
            if (!free)
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
