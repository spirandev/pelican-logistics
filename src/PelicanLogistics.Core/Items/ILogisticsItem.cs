namespace PelicanLogistics.Core.Items;

/// <summary>
/// Abstração de uma pilha de itens do jogo. O Core nunca cria itens do nada: só divide e junta os que recebe.
/// </summary>
/// <remarks>
/// Contrato de transferência (vale para todo o Core):
/// <list type="bullet">
/// <item><see cref="Split"/> só é chamado com <c>1 &lt;= amount &lt; Count</c>. Para mover a pilha inteira, passe a própria instância.</item>
/// <item>Quem recebe uma pilha inteira guarda a própria instância; quem entregou descarta sua referência.</item>
/// <item>Quem recebe só parte de uma pilha retira as unidades dela (com <see cref="Split"/> ou <see cref="MergeFrom"/>),
/// de modo que <see cref="Count"/> da pilha original diminui na mesma quantidade aceita.</item>
/// </list>
/// </remarks>
public interface ILogisticsItem
{
    /// <summary>ID qualificado do jogo, ex.: "(O)388" (madeira).</summary>
    string QualifiedItemId { get; }

    /// <summary>Qualidade: 0 normal, 1 prata, 2 ouro, 4 irídio.</summary>
    int Quality { get; }

    /// <summary>Categoria do jogo (ex.: -75 vegetais).</summary>
    int Category { get; }

    string DisplayName { get; }

    /// <summary>Quantidade na pilha. Sempre >= 1 enquanto o item estiver em uso.</summary>
    int Count { get; }

    int MaxStack { get; }

    bool HasContextTag(string tag);

    /// <summary>True se os dois podem ocupar a mesma pilha (mesmo item, mesma qualidade, mesmo sabor etc.).</summary>
    bool CanStackWith(ILogisticsItem other);

    /// <summary>Retira 'amount' (1..Count-1) desta pilha e devolve uma nova pilha com essa quantidade.</summary>
    ILogisticsItem Split(int amount);

    /// <summary>Move até 'max' unidades de 'other' para esta pilha, respeitando MaxStack. Retorna quantas moveu.</summary>
    int MergeFrom(ILogisticsItem other, int max);
}
