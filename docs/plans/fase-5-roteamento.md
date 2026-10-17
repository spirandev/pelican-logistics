# Fase 4: Roteamento (Filtros e Divisor)

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md) (seções "Filtro", "Divisor", "Regras de conexão", "Ordem horária"), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-4-roteamento`
- **PR:** `Fase 4: Roteamento`
- **Depende de:** Fase 3 mesclada.

## Objetivo

Adicionar o **Filtro Comum**, o **Filtro Completo**, o **filtro acoplado** ao Extrator/Inseridor e o **Divisor**, com suas interfaces. Ao final, uma esteira com itens misturados entra num Divisor com filtros nas saídas e cada item vai para o baú certo.

## Fora de escopo

Ponte, tiers de esteira, Esteira Subterrânea, Regulador (Fase 5); dicas ao passar o mouse (Fase 6).

---

## Regras do README que esta fase implementa

**Filtro**
- É um **bloco de esteira**, não um acessório. Move os itens sozinho.
- **Sempre reto**: entra por trás, sai pela frente. Sem curvas.
- **Sempre rápido** (`Balance.FilterSpeed`). Se o tile à frente estiver ocupado, espera.
- Segura no máximo **uma pilha** e **nunca junta itens**.
- O item que **não bate** **não entra**: fica parado na esteira anterior. Sem saída lateral.
- Clicar abre a interface; o item usado para configurar **não é gasto**.
- **Filtro Comum:** um único item (plano mestre D6: compara só o ID, qualquer qualidade).
- **Filtro Completo:** lista de itens, categoria, qualidade, context tags, lista branca ou lista negra.

**Filtro acoplado**
- Extrator com um filtro **imediatamente à frente** (recebendo dele): o Extrator só retira o que o filtro aceita.
- Filtro **imediatamente atrás** de um Inseridor (entregando a ele): o Inseridor só insere o que o filtro aceita (isso já acontece naturalmente, porque o filtro só deixa passar o que aceita).

**Divisor** (bloco com 4 portas, sem modos, sem direção)
1. Se alguma saída tem um filtro que aceita o item → o item vai **todo** para ela; várias saídas com filtro que aceitam → divide igualmente entre elas.
2. Se nenhum filtro aceita → divide igualmente entre as saídas livres **sem filtro** (7 → 3 + 2 + 2).
3. Pilha menor que o número de saídas → um item por vez em cada saída, em **ordem horária**.
4. Saída bloqueada → próxima saída livre que aceite; uma saída com filtro bloqueada manda o excedente para as saídas sem filtro.
5. Sem saída livre que aceite → **trava**.
6. Várias entradas → alterna entre elas em ordem horária.
- Ordem horária **sempre começando pelo norte** (N → E → S → W); o bloco lembra a última porta usada e continua da seguinte.

---

## Parte A: Core

### A1. `Filters/`

```csharp
public interface IFilterRule
{
    bool Matches(ILogisticsItem item);
}

public sealed class CommonFilterRule : IFilterRule
{
    /// <summary>null = nada configurado = não deixa passar nada.</summary>
    public string? QualifiedItemId { get; set; }
}

public enum FilterMode { Whitelist, Blacklist }

public sealed class CompleteFilterRule : IFilterRule
{
    public FilterMode Mode { get; set; } = FilterMode.Whitelist;
    public List<string> ItemIds { get; set; } = new();     // QualifiedItemId
    public List<int> Categories { get; set; } = new();
    public List<int> Qualities { get; set; } = new();      // 0, 1, 2, 4
    public List<string> ContextTags { get; set; } = new();
}
```

**Semântica do Filtro Completo** (registre como comentário na classe):
- Cada grupo **não vazio** é um critério. Dentro de um grupo vale **OU** (o item está na lista de itens, ou é de uma das categorias...). Entre grupos vale **E** (precisa satisfazer todos os grupos preenchidos).
- Nenhum grupo preenchido = o item "não corresponde".
- `Whitelist`: passa se corresponde. `Blacklist`: passa se **não** corresponde.
- Consequências: lista branca vazia não deixa passar nada; lista negra vazia deixa passar tudo.

`FilterRuleSerializer`: converte as regras para string JSON e de volta (`System.Text.Json`), para guardar no `modData`. Valores inválidos ou JSON corrompido → regra vazia (nunca lança exceção).

### A2. `Nodes/FilterNode.cs`

| Membro | Descrição |
|---|---|
| `IFilterRule Rule` | `CommonFilterRule` ou `CompleteFilterRule` |
| `bool IsComplete` | Tipo do filtro |
| `TransitSlot Slot` | |
| `Direction InputSide` | Sempre `Facing.Opposite()` |
| `ProvidesTo(n)` | `n == Position.Offset(Facing)` |
| `Downstream` | O nó à frente |

- `CanReceiveFrom(side)`: `side == InputSide && Slot.IsEmpty`.
- `CanAccept(side, item)`: `CanReceiveFrom(side) && Rule.Matches(item)`.
- `Accept`: se `CanAccept` → guarda a pilha inteira; senão 0. **Nunca** junta.
- `Update`: avança com `Balance.FilterSpeed`; no fim → `ctx.PushForward(this, Slot, Facing)`.
- O Filtro **não** entra no cálculo de curvas como receptor (é sempre reto), mas **é** provedor para a esteira à frente dele.

### A3. Extrator: saídas válidas e acoplamento

- `ExtractorNode.IsValidOutput`: passa a aceitar `FilterNode` (além de `BeltNode`).
- `GetExtractionPredicate(ctx)`: se o nó à frente é `FilterNode f` e `f.InputSide == Facing.Opposite()` (o filtro recebe do Extrator) → `f.Rule.Matches`; senão → aceita tudo.
- O "pode receber" do Extrator agora deve usar `CanReceiveFrom` do nó à frente.

### A4. `Nodes/SplitterNode.cs`

| Membro | Descrição |
|---|---|
| `TransitSlot Slot` | Uma pilha por vez |
| `Direction? LastInput`, `Direction? LastOutput` | Para o rodízio. `null` = começar pelo Norte |
| `IReadOnlyList<Direction> Inputs`, `Outputs` | Calculados pela `Topology` |
| `ProvidesTo(n)` | True para **qualquer** vizinho adjacente (o Divisor pode entregar para qualquer lado; quem decide é o vizinho) |
| `Downstream` | Os nós nas `Outputs` |

**Portas** (calculadas em `Topology.Recompute`, **depois** do `InputSide` das esteiras, plano mestre D4):
- Para cada direção `d` com vizinho `n` em `Position.Offset(d)`:
  - **Entrada** se `n` é `BeltNode` ou `FilterNode` (Fase 5: saída de Subterrânea) e `n.ProvidesTo(Position)`;
  - **Saída** se `n` é `BeltNode` ou `FilterNode` (Fase 5: entrada de Subterrânea) e o lado de entrada de `n` é `d.Opposite()` (ou seja, `n` recebe pelo lado virado para o Divisor);
  - senão, nada. Divisor, Extrator e Inseridor vizinhos não se conectam.
- Uma esteira encostada no Divisor com uma esteira atrás dela continua reta e **não** vira saída (lateral ignorada). Uma esteira sem nada atrás e com o Divisor na lateral vira **curva** saindo do Divisor. Isso já sai do algoritmo de `InputSide` porque o Divisor é provedor para todos os vizinhos.

**`Accept`**: sempre 0. O Divisor **puxa** (plano mestre D5).

**`Update(ctx)`**:
1. Se `Slot` tem item: avança com `Balance.SplitterSpeed`; ao chegar ao fim → `Distribute(ctx)`.
2. Se `Slot` está vazio: **puxa** a próxima entrada. Percorre `Inputs` em ordem horária começando **depois** de `LastInput` (se `LastInput == null`, começa pelo Norte). Para a primeira entrada cujo nó tem um slot com item no fim (`Progress == End`) e cujo destino é o Divisor: tira a pilha inteira daquele slot, coloca no próprio `Slot` com `EntrySide = d`, `LastInput = d`, e para.
   - Para isso, crie em `LogisticsNode` um método `TransitSlot? GetOutgoingSlot()` que devolve o slot que está entregando pela frente (esteira e filtro: o próprio slot; padrão: null).

**`Distribute(ctx)`**:
```
ordem = Outputs em ordem horária começando depois de LastOutput (null → começa pelo Norte)
livre(d) = nó(d).CanAccept(d.Opposite(), item)
comFiltroQueAceita = ordem.Where(d => nó(d) is FilterNode f && f.Rule.Matches(item))
candidatos = comFiltroQueAceita.Where(livre)
se candidatos vazio:
    candidatos = ordem.Where(d => nó(d) is not FilterNode && livre(d))
se candidatos vazio:
    Slot.Blocked = true; return            // regra 5: trava
k = Min(candidatos.Count, item.Count)
base = item.Count / k; resto = item.Count % k
para i em 0..k-1:
    quantidade = base + (i < resto ? 1 : 0)
    parte = (i == k - 1) ? item : item.Split(quantidade)
    nó(candidatos[i]).Accept(candidatos[i].Opposite(), parte, ctx)
LastOutput = candidatos[k - 1]
Slot.Clear()
```
Observações:
- "Saída com filtro que **não** aceita o item" nunca é candidata.
- Saídas com filtro que aceitam mas estão ocupadas são puladas e o item vai como excedente para as sem filtro (regra 4).
- O Divisor **não** junta itens.

**Persistência:** implemente `ExportPointers`/`ImportPointers` (Fase 3) para `LastInput`/`LastOutput` (`-1` = null, senão `(int)Direction`).

### A5. Topologia

- `Downstream` de `FilterNode` e `SplitterNode` (acima).
- Ordem de cálculo em `Recompute`: (1) `InputSide` das esteiras; (2) portas dos Divisores; (3) ordem de processamento.

### A6. Testes do Core

**Regras de filtro**
1. Comum com `(O)388`: madeira de qualquer qualidade passa; pedra não; filtro vazio não deixa passar nada.
2. Completo, lista branca por categoria: só itens da categoria passam.
3. Completo, itens + qualidade: só o item listado **e** na qualidade listada passa (E entre grupos).
4. Completo, duas categorias: qualquer uma das duas passa (OU dentro do grupo).
5. Completo, context tag.
6. Completo, lista negra: inverte; lista negra vazia deixa tudo passar; lista branca vazia nada.
7. Serializador: ida e volta das duas regras; JSON corrompido → regra vazia sem exceção.

**FilterNode**
8. Item aceito atravessa o filtro na velocidade máxima.
9. Item recusado fica parado na esteira anterior, que fica `Blocked`.
10. Filtro não aceita pela lateral; não faz curva.
11. Filtro não junta: pilha igual chegando atrás de uma pilha parada no filtro **não** se junta.
12. Vários filtros em sequência funcionam.
13. Filtro rápido seguido de esteira lenta: o filtro espera o tile liberar.

**Acoplamento**
14. Baú com madeira e pedra → Extrator → Filtro (madeira): só madeira sai; a pedra fica no baú; a esteira **não trava**.
15. Sem acoplamento (filtro depois de uma esteira): a pedra extraída trava a linha (comportamento esperado).
16. Filtro (minério) → Inseridor → fornalha: só minério entra.

**SplitterNode**
17. Portas: esteira apontando para o Divisor = entrada; esteira saindo dele = saída; esteira passando ao lado com esteira atrás = nada; esteira sem nada atrás com o Divisor na lateral = saída em curva.
18. Divisão igual: pilha de 7 com 3 saídas livres → 3, 2, 2, na ordem horária a partir do Norte.
19. Pilha de 1 item com 3 saídas: 3 pilhas seguidas vão uma para cada saída, em ordem horária (N, E, S, se essas forem as saídas).
20. Prioridade de filtro: saídas com filtro de madeira, filtro de pedra e sem filtro; madeira vai toda para o filtro de madeira; pedra para o de pedra; outro item para a sem filtro.
21. Duas saídas com o mesmo filtro: a pilha se divide entre elas.
22. Saída com filtro bloqueada: a madeira vai para a sem filtro (excedente).
23. Sem destino: só saídas com filtros que não aceitam o item → o Divisor trava; ao liberar uma saída válida, volta a andar.
24. Duas entradas com fluxo contínuo: o Divisor alterna entre elas (nenhuma fica parada para sempre).
25. Rodízio começa pelo Norte e continua depois da última porta usada.
26. Junção: duas entradas e uma saída funcionam como "juntar fluxos".
27. Persistência: ida e volta dos ponteiros do Divisor e do slot do Filtro.
28. **Conservação:** cenário grande (Divisor com 3 saídas filtradas, 30 itens de 4 tipos): nenhum item é criado ou perdido e cada tipo termina no baú certo.

---

## Parte B: Adaptador

### B1. Itens

- `ItemIds`: `FilterCommon`, `FilterComplete`, `Splitter`.
- `i18n`: nome e descrição dos três.
- `ContentRegistrar`: `Data/BigCraftables` + loja do Clint (0g).
- `PlaceholderPainter` (Core) + `PlaceholderTextures`: Filtro Comum = losango verde com seta; Filtro Completo = losango duplo verde escuro com seta; Divisor = círculo roxo com 4 saídas (README "Placeholders visuais"). Acrescente os índices na tabela da folha de ícones.
- `NodeFactory` cria `FilterNode` (lendo a regra do `modData`) e `SplitterNode`.
- O Divisor **não** é direcional: `R` não faz nada nele e a seta fantasma não aparece.

### B2. `modData`

- `ModDataKeys.FilterRule`: JSON do `FilterRuleSerializer`.

### B3. Desenho

- Filtro: sprite girado + item em trânsito (como a esteira). O **Filtro Comum** desenha também o ícone do item configurado, pequeno (escala ~0,35), no centro do bloco, por baixo do item em trânsito.
- Divisor: sprite sem rotação + item em trânsito. Para o caminho do item no Divisor, use `EntrySide` → centro durante a primeira metade do progresso e fique no centro na segunda (o destino só é conhecido ao distribuir).

### B4. Novas linhas de interface (`Framework/Menus/`)

- `ItemPickerRow`: mostra um "slot fantasma" com o item atual e, abaixo, o inventário do jogador (`InventoryMenu`). Clicar num item do inventário define o item do filtro **sem tirá-lo do inventário**. Botão "Limpar".
- `ItemListRow`: lista de até 12 itens fantasma; clicar num item do inventário adiciona; clicar num item da lista remove.
- `ToggleRow`: liga/desliga.
- `ToggleGroupRow`: vários toggles numa linha (para qualidades e categorias).
- `TextRow`: campo de texto (`StardewValley.Menus.TextBox`) para as context tags, separadas por vírgula.

### B5. Interfaces dos filtros

- **Filtro Comum:** `ItemPickerRow`.
- **Filtro Completo:**
  - `CycleRow` Modo: Lista branca / Lista negra;
  - `ItemListRow` Itens;
  - `ToggleGroupRow` Qualidade: Normal, Prata, Ouro, Irídio;
  - `ToggleGroupRow` Categorias, com esta lista (use as constantes de categoria do `StardewValley.Object` quando existirem e nomes no i18n): Vegetais, Frutas, Flores, Forrageáveis, Sementes, Peixes, Iscas, Lixo, Minerais, Gemas, Minérios e barras, Recursos (madeira, pedra...), Artesanais, Ovos, Leite, Culinária, Fertilizantes;
  - `TextRow` Context tags.
- Abertura: botão de ação sobre o filtro (como no Extrator). Cada alteração grava no `modData` e atualiza a regra do nó na hora.

### B6. i18n

Títulos e rótulos das interfaces, nomes das categorias e qualidades, modos.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1 + testes 1–7.
3. Core A2–A3 + testes 8–16.
4. Core A4–A5 + testes 17–28.
5. Adaptador B1–B3.
6. Adaptador B4–B6.
7. Docs, changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Os 28 testes desta fase passam, junto com os anteriores.
- [ ] O Divisor segue exatamente as 6 regras do README, na ordem.
- [ ] PR com "APIs a confirmar" (no mínimo: `InventoryMenu`, `TextBox`, constantes de categoria).

## Verificação local (usuário)

- [ ] Filtro Comum: clicar abre a interface; escolher Madeira no inventário configura o filtro **sem gastar** a madeira; o ícone da madeira aparece no bloco.
- [ ] Linha com madeira e pedra passando por um Filtro (madeira) no meio: a madeira passa; a pedra para antes do filtro e trava a linha.
- [ ] Baú misturado → Extrator → Filtro (madeira): só madeira sai do baú; a pedra fica no baú.
- [ ] Filtro Completo: lista branca com a categoria Minerais deixa passar quartzo e não madeira; lista negra com madeira deixa passar tudo menos madeira.
- [ ] Filtro Completo por qualidade: só chirívias ouro passam.
- [ ] **Separador clássico:** esteira com 4 tipos de item → Divisor com Filtro (madeira) ao norte, Filtro (pedra) a leste e esteira sem filtro ao sul; cada tipo vai para o seu baú e o resto vai para o sul.
- [ ] Baú de madeira cheio: a madeira passa a ir para o sul (excedente).
- [ ] Divisor com 3 saídas sem filtro e itens 1 a 1: os itens se alternam norte → leste → sul (ordem horária).
- [ ] Divisão de pilhas (o Extrator de Pilha só chega na Fase 5): coloque o Divisor **sem saídas**, deixe as madeiras se acumularem e se juntarem na esteira de entrada, depois coloque 3 esteiras de saída. A pilha se divide igualmente entre elas.
- [ ] Duas esteiras entrando no Divisor e uma saindo: os fluxos se alternam.
- [ ] `R` sobre o Divisor não faz nada; sobre o Filtro gira.
- [ ] Salvar e carregar mantém as regras dos filtros e os itens dentro do Divisor e dos filtros.
- [ ] Nenhum erro do mod no log do SMAPI.
