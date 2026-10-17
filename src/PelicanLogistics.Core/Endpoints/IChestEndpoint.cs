using System;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Endpoints;

/// <summary>Um baú com que Extratores e Inseridores interagem.</summary>
public interface IChestEndpoint : IEndpoint
{
    /// <summary>
    /// Retira itens do baú. Procura a primeira pilha (na ordem do inventário) que satisfaz 'predicate'.
    /// One: 1 unidade. Stack: a pilha inteira daquele slot. Exactly: até 'amount' unidades
    /// de itens empilháveis com o primeiro encontrado, podendo juntar de vários slots, limitado a MaxStack.
    /// Retorna null se não encontrar nada.
    /// </summary>
    ILogisticsItem? Extract(Func<ILogisticsItem, bool> predicate, ExtractAmount mode, int amount);

    /// <summary>Insere até 'max' unidades de 'item'. Retorna quantas entraram. Se entrar tudo, o baú pode ficar com a instância.</summary>
    int Insert(ILogisticsItem item, int max);

    /// <summary>Quantas unidades com este QualifiedItemId existem no baú (qualquer qualidade). Usado na Fase 5.</summary>
    int CountById(string qualifiedItemId);

    /// <summary>Total de unidades no baú. Usado na Fase 5.</summary>
    int CountTotal();
}
