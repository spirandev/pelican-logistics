using System.Collections.Generic;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Machines;

/// <summary>
/// Slots virtuais de uma máquina (README "Slots virtuais"): o "estoque" do mod para máquinas abastecidas por
/// Inseridores. Quando todos estão completos e a máquina está ociosa, a grade aciona a máquina.
/// </summary>
public sealed class VirtualSlots
{
    /// <summary>Pilha da entrada principal.</summary>
    public ILogisticsItem? Main { get; set; }

    /// <summary>Quantidade exigida para a pilha atual (definida quando o primeiro item entra).</summary>
    public int MainRequired { get; set; }

    /// <summary>Itens adicionais, por <see cref="AdditionalRequirement.Id"/>.</summary>
    public Dictionary<string, ILogisticsItem> Additional { get; } = new();

    /// <summary>Sem itens.</summary>
    public bool IsEmpty => this.Main == null && this.Additional.Count == 0;

    /// <summary>Principal com a quantidade exigida e cada requisito adicional com a sua quantidade.</summary>
    public bool IsComplete(IReadOnlyList<AdditionalRequirement> reqs)
    {
        if (this.Main == null || this.Main.Count < this.MainRequired)
            return false;

        foreach (AdditionalRequirement req in reqs)
        {
            if (this.CountOf(req) < req.RequiredCount)
                return false;
        }

        return true;
    }

    /// <summary>Unidades guardadas para o requisito adicional.</summary>
    public int CountOf(AdditionalRequirement req)
    {
        return this.Additional.TryGetValue(req.Id, out ILogisticsItem? item) ? item.Count : 0;
    }

    /// <summary>Todos os itens guardados (para o drop e a persistência).</summary>
    public IEnumerable<ILogisticsItem> AllItems()
    {
        if (this.Main != null)
            yield return this.Main;
        foreach (ILogisticsItem item in this.Additional.Values)
            yield return item;
    }

    public void Clear()
    {
        this.Main = null;
        this.MainRequired = 0;
        this.Additional.Clear();
    }

    /// <summary>
    /// Guarda até 'n' unidades de 'item' no Principal (1 &lt;= n &lt;= item.Count). Se o slot está vazio,
    /// 'required' passa a ser a quantidade exigida. Retorna quantas entraram.
    /// </summary>
    internal int AddToMain(ILogisticsItem item, int n, int required)
    {
        if (this.Main == null)
        {
            this.Main = TakePart(item, n);
            this.MainRequired = required;
            return n;
        }

        return this.Main.MergeFrom(item, n);
    }

    /// <summary>Guarda até 'n' unidades de 'item' no Adicional do requisito 'id'. Retorna quantas entraram.</summary>
    internal int AddToAdditional(string id, ILogisticsItem item, int n)
    {
        if (!this.Additional.TryGetValue(id, out ILogisticsItem? current))
        {
            this.Additional[id] = TakePart(item, n);
            return n;
        }

        return current.MergeFrom(item, n);
    }

    /// <summary>Retira até 'max' unidades do Principal. Se sair tudo, o slot fica vazio.</summary>
    internal ILogisticsItem? TakeFromMain(int max)
    {
        ILogisticsItem? main = this.Main;
        if (main == null || max <= 0)
            return null;

        if (max < main.Count)
            return main.Split(max);

        this.Main = null;
        this.MainRequired = 0;
        return main;
    }

    /// <summary>Retira até 'max' unidades do Adicional do requisito 'id'. Se sair tudo, a entrada é removida.</summary>
    internal ILogisticsItem? TakeFromAdditional(string id, int max)
    {
        if (max <= 0 || !this.Additional.TryGetValue(id, out ILogisticsItem? item))
            return null;

        if (max < item.Count)
            return item.Split(max);

        this.Additional.Remove(id);
        return item;
    }

    /// <summary>Descarta as pilhas que a máquina consumiu por inteiro (Count chegou a 0).</summary>
    internal void RemoveConsumed()
    {
        if (this.Main != null && this.Main.Count <= 0)
        {
            this.Main = null;
            this.MainRequired = 0;
        }

        var consumed = new List<string>();
        foreach ((string id, ILogisticsItem item) in this.Additional)
        {
            if (item.Count <= 0)
                consumed.Add(id);
        }

        foreach (string id in consumed)
            this.Additional.Remove(id);
    }

    /// <summary>A própria pilha se 'n' é tudo; senão uma parte dela (contrato de <see cref="ILogisticsItem"/>).</summary>
    private static ILogisticsItem TakePart(ILogisticsItem item, int n)
    {
        return n >= item.Count ? item : item.Split(n);
    }
}
