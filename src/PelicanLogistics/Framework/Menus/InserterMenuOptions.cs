using System;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Regulators;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Estado atual e ações da interface do Inseridor (<see cref="InserterMenu"/>).</summary>
internal sealed class InserterMenuOptions
{
    public PickupSource Source { get; init; }

    public Action<PickupSource> OnSourceChanged { get; init; } = _ => { };

    public InserterSlotMode SlotMode { get; init; }

    public Action<InserterSlotMode> OnSlotModeChanged { get; init; } = _ => { };

    /// <summary>Quantidade por vez (só o Inseridor Configurável); null esconde a linha.</summary>
    public int? ConfiguredAmount { get; init; }

    public Action<int> OnAmountChanged { get; init; } = _ => { };

    /// <summary>Regra do Regulador, alterada no lugar pela interface; null (sem Regulador) esconde a seção (plano mestre D13).</summary>
    public RegulatorRule? Regulator { get; init; }

    /// <summary>A regra do Regulador mudou: gravar.</summary>
    public Action OnRegulatorChanged { get; init; } = () => { };

    /// <summary>A forma da interface mudou (modo do Regulador, item removido): abrir de novo com as linhas certas.</summary>
    public Action Reopen { get; init; } = () => { };

    /// <summary>"Adicionar item" do Regulador: escolher o item no inventário.</summary>
    public Action OnAddRegulatorItem { get; init; } = () => { };

    /// <summary>"Remover regulador": devolver o Regulador ao jogador e apagar a regra.</summary>
    public Action OnRemoveRegulator { get; init; } = () => { };
}
