using System.Collections.Generic;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Endpoints;

/// <summary>Uma máquina com que os Inseridores interagem, por meio dos slots virtuais (README "Interação com máquinas").</summary>
public interface IMachineEndpoint : IEndpoint
{
    /// <summary>Itens adicionais exigidos pela máquina (ex.: 1 carvão). Lista vazia se não houver.</summary>
    IReadOnlyList<AdditionalRequirement> AdditionalRequirements { get; }

    /// <summary>Sem produto guardado e sem processamento em andamento: pode começar uma nova rodada.</summary>
    bool IsIdle { get; }

    /// <summary>Máquina que repete sozinha depois de carregada (ex.: Cristalário).</summary>
    bool IsRepeating { get; }

    /// <summary>Para máquinas repetidoras: já está carregada com uma entrada.</summary>
    bool HasLoadedInput { get; }

    /// <summary>Quantidade exigida se 'item' é aceito como entrada principal; null se não é aceito.</summary>
    int? GetMainRequiredCount(ILogisticsItem item);

    /// <summary>Produto pronto, se houver (sem retirar).</summary>
    ILogisticsItem? PeekOutput();

    /// <summary>Retira até 'max' unidades do produto pronto. Retirar tudo conta como "coletar" (dispara a lógica de coleta do jogo).</summary>
    ILogisticsItem? TakeOutput(int max);

    /// <summary>
    /// Tenta iniciar a máquina com a entrada principal e os adicionais (na ordem de <see cref="AdditionalRequirements"/>).
    /// Consome os itens só se iniciar, diminuindo o <see cref="ILogisticsItem.Count"/> das pilhas recebidas na
    /// quantidade usada. O que sobrar nas pilhas continua nos slots virtuais.
    /// </summary>
    bool TryStart(ILogisticsItem main, IReadOnlyList<ILogisticsItem> additional);
}
