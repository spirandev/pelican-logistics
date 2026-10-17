# Fase 3: Inseridor único

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório, principalmente D2, D3, D16 e D17), [`README.md`](../../README.md) (seções "Inseridores", "Regras de conexão", "Ordem horária", "Slots virtuais", "Retirada (Inseridor)"), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-3-inseridor-unico`
- **PR:** `Fase 3: Inseridor único`
- **Depende de:** Fase 2 mesclada.

## Objetivo

O **Extrator** e o **Inseridor** viram **um bloco só**, o Inseridor, no estilo do inserter do Factorio:

```
[Baú]      → [Inseridor] → esteira        retira do baú e coloca na esteira
esteira    → [Inseridor] → [Fornalha]     pega da esteira e abastece a máquina
[Baú]      → [Inseridor] → [Fornalha]     direto, sem esteira
[Fornalha] → [Inseridor] → [Baú]          retira o produto e guarda
```

1. **Frente = destino** (fixo, girado com `R`): baú, máquina ou esteira que começa no Inseridor.
2. **Origem:** baú ou máquina **só atrás**; esteiras que **terminam** no Inseridor por trás **ou pelas laterais**. Várias entradas → rodízio em ordem horária.
3. **Só pega o que o destino aceita agora**, e só a quantidade que cabe. Nunca trava com um item errado.
4. O item viaja **na mão**: o braço pega, gira até o destino (15 ticks), entrega e volta vazio (15 ticks). O braço é desenhado com placeholders.

O comportamento das máquinas (slots virtuais, Cristalário, retirada de Produto/Principal/Adicional) **não muda**: só passa a morar no Inseridor.

## Fora de escopo

Persistência do item na mão (Fase 4), Filtro e Divisor (Fase 5), Inseridores de Pilha/Configurável como itens e Regulador (Fase 6), sprites finais do braço (beta). Uma esteira passando **de lado** atrás do Inseridor **não** é entrada (README).

---

## Parte A: Core

### A1. `Balance`

- Remova `TransferCooldownTicks`.
- Acrescente `InserterSpeed = 40` (progresso da mão por tick: a ida leva `End / 40 = 15` ticks) e `InserterReturnTicks = 15`.
- Teste: ida + volta = 30 ticks, ou seja, 1 operação a cada 0,5 s, como antes.

### A2. Remoções e renomeações

- Apague `ExtractorNode` e `NodeKind.Extractor`.
- Renomeie `ExtractorSource` para `PickupSource` (`Product`, `Main`, `Additional`).
- Migre os testes de `ExtractorNodeTests` que continuam valendo para `InserterNodeTests` (baú → esteira, retirada de Produto/Principal/Adicional, tiers).

### A3. `LogisticsNode.GetOutgoingSlot`

```csharp
/// <summary>O slot que este nó está entregando para 'target', ou null. Sem efeito colateral.</summary>
public virtual TransitSlot? GetOutgoingSlot(TilePos target) => null;
```

- `BeltNode`: devolve `Slot` se `Position.Offset(Facing) == target` e `Slot.Item != null`.
- Quem puxa confere `Progress == Balance.End` antes de pegar. A Fase 5 reaproveita isso no Divisor e no Filtro.

### A4. `IChestEndpoint`: consultas sem efeito colateral

```csharp
/// <summary>Primeira pilha que satisfaz 'predicate', sem retirar. Null se não houver.</summary>
ILogisticsItem? Find(Func<ILogisticsItem, bool> predicate);

/// <summary>Quantas unidades de 'item' caberiam agora (pilhas existentes + slots livres). Sem efeito colateral.</summary>
int SpaceFor(ILogisticsItem item);
```

Atualize o `FakeChest` e teste os dois (baú cheio, pilha incompleta, slot livre, qualidade diferente).

### A5. `InserterNode`

| Membro | Descrição |
|---|---|
| `Tier`, `ConfiguredAmount` | Como hoje |
| `PickupSource Source` | De onde retirar quando a origem é máquina (padrão `Product`) |
| `InserterSlotMode SlotMode` | Onde colocar quando o destino é máquina (padrão `Auto`) |
| `TransitSlot Hand` | A mão. `EntrySide` = lado da origem; `Progress` 0 → `End` durante a ida |
| `int Cooldown` | Ticks restantes da volta (0 = pronto para pegar) |
| `Direction? LastInput` | Última entrada usada no rodízio. `null` = começar pelo Norte |
| `IReadOnlyList<Direction> Inputs` | Calculado pela `Topology` (A6), em ordem horária N → E → S → W |
| `FrontPosition`, `BackPosition` | `Position.Offset(Facing)` e `Position.Offset(Facing.Opposite())` |
| `Slots` | `[Hand]` (desenho, remoção e, na Fase 4, persistência) |
| `ProvidesTo(n)` | `n == FrontPosition` (assim a esteira da frente fica reta ou faz curva saindo dele) |
| `Downstream` | O nó da frente, se houver |
| `CanReceiveFrom`, `CanAccept`, `Accept` | `false` / `false` / `0`: o Inseridor **puxa** (D16). A esteira que termina nele fica `Blocked` no fim até ser puxada |
| `GetOutgoingSlot` | `null`: Inseridores não se conectam entre si |

**`Update(ctx)`**, nesta ordem:

1. **Carregando** (`Hand.Item != null`):
   - se `Hand.Progress < End` → `Hand.Advance(InserterSpeed)`; se ainda não chegou ao fim, retorna;
   - no fim → `Deliver`: entrega o que couber no destino. Se entregou tudo → `Hand.Clear()`, `Cooldown = InserterReturnTicks`. Se sobrou → `Hand.Blocked = true` e tenta de novo no próximo tick (D17).
   - retorna.
2. **Voltando** (`Cooldown > 0`): `Cooldown--`; se ainda for > 0, retorna (se chegar a 0, já tenta pegar neste tick, para o ciclo ser exatamente 30 ticks).
3. **Pegando:** se não há destino válido (A5.1) → retorna. Percorre `Inputs` em ordem horária começando **depois** de `LastInput` (se `null`, começa pelo Norte). Na primeira entrada que fornecer um item (A5.2): `Hand.Put(item, lado)`, `LastInput = lado`, retorna.

#### A5.1 Destino (frente) e `Fits`

`Fits(item)` = quantas unidades o destino aceitaria agora, sem efeito colateral:

| Destino na frente | `Fits(item)` | Entrega |
|---|---|---|
| Nó `n` que não é `InserterNode` nem `SplitterNode` | `n.CanAccept(Facing.Opposite(), item) ? item.Count : 0` | `n.Accept(Facing.Opposite(), item, ctx)` |
| Baú | `chest.SpaceFor(item)` | `chest.Insert(item, item.Count)` |
| Máquina | O que o slot do `SlotMode` aceitaria (A5.3) | A inserção atual nos slots virtuais |
| Nada / outro | 0 (não pega) | — |

Regra: um nó no tile da frente tem prioridade sobre o endpoint (um tile tem um objeto só, mas o teste deve cobrir).

#### A5.2 Entradas (origem)

Para cada lado `s` em `Inputs`:

- **Nó** em `Position.Offset(s)` com `slot = n.GetOutgoingSlot(Position)`, `slot.Item != null` e `slot.Progress == End`:
  `amount = Min(TierAmount(slot.Item), Fits(slot.Item))`; se `amount > 0`, tira `amount` do slot (`Split` se for menos que a pilha; senão a pilha inteira e `slot.Clear()`).
- **Só quando `s` é o lado de trás**, sem nó ali:
  - **Baú:** `stack = chest.Find(i => Fits(i) > 0)`; `amount = Min(TierAmount(stack), Fits(stack))`; `chest.Extract(i => i.CanStackWith(stack), ExtractAmount.Exactly, amount)`.
  - **Máquina:** a retirada atual do Extrator conforme `Source` (Produto / Principal / Adicional), com a quantidade limitada por `Fits`. Produto: `PeekOutput`, confere `Fits`, `TakeOutput(amount)`.
- Lados laterais **nunca** olham baús ou máquinas.

`TierAmount(stack)`: `Single` = 1; `Stack` = `stack.Count`; `Configurable` = `ConfiguredAmount`.

#### A5.3 Máquina como destino

Separe a lógica atual de `InsertMain`/`InsertAdditional` em duas partes: **quanto caberia** (usada por `Fits`, sem mexer nos slots) e **inserir** (usada por `Deliver`). As regras não mudam: Automático/Principal/Adicional, só o que falta, Principal só com o mesmo item, regra do Cristalário.

### A6. `Topology`

- Depois do `InputSide` das esteiras, calcule `InserterNode.Inputs`:
  - o lado de **trás** sempre entra (pode haver baú ou máquina ali; o Core não sabe);
  - cada **lateral** `s` entra se o vizinho em `Position.Offset(s)` `ProvidesTo(Position)` (uma esteira que **termina** no Inseridor);
  - a frente nunca entra.
- `BeltNode.Downstream` já inclui o Inseridor quando a esteira aponta para ele; a ordem de processamento continua correta (o Inseridor é atualizado antes da esteira que o alimenta).

### A7. `Rendering/ArmMath`

Ângulos em radianos, com 0 = norte e sentido horário positivo (a mesma convenção da rotação dos sprites).

```csharp
public static class ArmMath
{
    /// <summary>Distância da mão ao centro, em tiles: a mão chega ao meio da borda, onde as esteiras entregam.</summary>
    public const float HandRadius = 0.5f;

    public static float AngleOf(Direction d);

    /// <summary>Gira de 'from' para 'to' pelo caminho mais curto; meia-volta (180°) gira no sentido horário. t em 0..1.</summary>
    public static float GetArmAngle(Direction from, Direction to, float t);

    /// <summary>Posição da mão no tile (0..1, origem no canto superior esquerdo).</summary>
    public static (float X, float Y) GetHandPosition(float angle);

    /// <summary>Ângulo atual do braço de um Inseridor.</summary>
    public static float GetArmAngle(InserterNode node);
}
```

`GetArmAngle(node)`:
- carregando: de `Hand.EntrySide` até `Facing`, com `t = Hand.Progress / End`;
- voltando: de `Facing` até `LastInput`, com `t = 1 - Cooldown / InserterReturnTicks`;
- parado: apontando para `LastInput`; se nunca pegou, para a única lateral em `Inputs` quando há exatamente uma; senão, para trás.

### A8. Placeholder

`PlaceholderPainter.PaintInserter(tier)`: quadrado azul com um **triângulo pequeno na borda da frente** (indica o destino) e os pontos de tier. O braço não faz parte do sprite: o adaptador desenha (B4).

### A9. Testes do Core

**Ciclo e balance**
1. Baú → Inseridor → esteira: sai 1 item por vez; entre duas retiradas passam exatamente 30 ticks; a mão leva 15 ticks na ida.
2. Esteira → Inseridor → baú: a esteira para no fim (`Blocked`) e o Inseridor puxa 1 por vez.
3. **Baú → Inseridor → baú**, sem esteira.

**Só o que o destino aceita**
4. Baú com madeira e minério de cobre → Inseridor (Automático) → fornalha: só minério sai, no máximo 5 até o Principal completar; a madeira fica no baú.
5. Baú misto com minério e carvão → Inseridor (Automático) → fornalha: o minério vai para o Principal e o carvão para o Adicional; a fornalha liga.
6. Baú só com madeira → Inseridor → fornalha: nada sai e nada trava.
7. Esteira com carvão → Inseridor (Principal) → fornalha: o carvão fica parado na esteira.
8. Destino esteira ocupada: o Inseridor não pega até ela liberar.
9. Destino baú cheio: não pega; com espaço para 3 e Inseridor de Pilha, pega só 3.

**Origens e lados**
10. Esteira que termina no Inseridor pela **lateral** (nada atrás) funciona.
11. Esteira **passando de lado** (paralela, sem apontar para ele) não entrega nada.
12. Esteira apontando para a **frente** do Inseridor não entrega nada.
13. Baú numa **lateral** é ignorado; baú atrás funciona.
14. Baú atrás + esteira numa lateral: alterna entre os dois.
15. Esteira atrás + duas laterais: alterna entre as três em ordem horária, começando pelo Norte e continuando depois da última usada.
16. Inseridor na frente de outro Inseridor: não se conectam (o de trás não pega nada).
17. Esteira na frente do Inseridor e sem nada atrás dela, com o Inseridor na lateral: vira curva saindo do Inseridor.

**Máquinas (migrados do Extrator)**
18. Fornalha → Inseridor (Produto) → baú: as barras saem.
19. Retirar de Principal e de Adicional devolve o que está nos slots virtuais.
20. Cristalário: só recebe a gema quando vazio; o Inseridor que retira dele pega as cópias.

**Mão**
21. Destino enche durante a ida (outro Inseridor completa o slot): o item espera na mão (`Hand.Blocked`) e é entregue quando cabe; nada se perde.
22. Remover o Inseridor com item na mão: o item vem no `RemovalResult`.
23. Tiers: `Stack` de um baú pega um slot inteiro (limitado ao que cabe); `Configurable` 15 junta 10 + 5 de dois slots.

**Desenho**
24. `ArmMath`: trás → frente gira 180° no sentido horário; lateral → frente gira 90° pelo caminho curto; nas pontas, a mão fica no meio da borda.
25. `GetArmAngle(node)` nos três estados (carregando, voltando, parado) e o fallback do estado parado.

**Conservação**
26. Baú → Inseridor → esteiras → Inseridor → fornalha → Inseridor (Produto) → baú, com um baú de carvão abastecendo pela lateral, por 3000 ticks: a soma de todos os itens (baús + esteiras + mãos + slots + produtos) só muda pelo consumo da fornalha.

---

## Parte B: Adaptador

### B1. Itens

- `ItemIds`: remova `Extractor`. Mantenha `LegacyExtractor = Prefix + "Extractor"` **só** para a migração (B2). `AllBlocks` = Esteira e Inseridor.
- `ContentRegistrar`, `PlaceholderTextures` (refaça a tabela de índices da folha de ícones), `pl_give`, `i18n`: sem o Extrator.
- `i18n`: descrição do Inseridor em uma frase, por exemplo "Pega itens atrás dele (baú, máquina ou esteira) e entrega na frente."

### B2. Migração de saves de teste

No `Rebuild` (carregamento do save), **antes** de registrar os nós: cada objeto colocado com o ID `LegacyExtractor` é trocado por um Inseridor no mesmo tile (`ItemRegistry.Create`), com a mesma direção (`Facing`) e a configuração copiada (a chave antiga de `ExtractorSource` vira a de `PickupSource`). A direção e o sentido batem: o Extrator tinha a origem atrás e a saída na frente, como o Inseridor. Registre no log quantos foram migrados.

- O SMAPI avisa as mudanças em `ObjectListChanged` **depois** (no próximo tick). Para o aviso da troca não sobrescrever a direção com a seta de colocação, o handler `Added` passa a **ignorar tiles que já têm nó registrado**. Isso não afeta a colocação normal: um bloco quebrado e colocado de novo gera `Removed` antes de `Added`.
- Extratores guardados em baús ou no inventário **não** são migrados (viram itens de erro do jogo). Registre isso no PR e no `STATUS.md` como limitação da alpha.

### B3. Endpoints, `modData` e fábrica

- `ChestEndpoint`: implemente `Find` e `SpaceFor` (pilhas que empilham: `maximumStackSize() - Stack`; slots vazios dentro de `GetActualCapacity()`: `maximumStackSize()` cada).
- `ModDataKeys`: troque `ExtractorSource` por `PickupSource`; `Config` passa a ter `PickupSource` e `InserterSlotMode`.
- `NodeFactory`: cria `InserterNode` lendo `Facing`, `PickupSource` e `InserterSlotMode`.

### B4. Desenho do Inseridor (`NodeRenderer`)

1. Base: o sprite do Inseridor girado conforme `Facing` (como hoje).
2. Braço: um traço escuro do centro do tile até a mão, no ângulo de `ArmMath.GetArmAngle(node)`. Use uma textura de **1×1 pixel branco** gerada em `PlaceholderTextures` (não dependa de texturas do jogo), esticada para 8 px de largura e `HandRadius × 64` px de comprimento, com origem na base do traço.
3. Mão: um quadradinho escuro de 12×12 px em `ArmMath.GetHandPosition(ângulo)`.
4. Item na mão: o ícone do item na posição da mão, com a mesma escala dos itens nas esteiras.
5. Profundidade: base < braço < mão < item, com incrementos pequenos (ex.: `+0.00001` cada) a partir da profundidade da base.
6. A prévia de colocação (segurando o bloco) mostra a base e o braço parado apontando para trás.

### B5. Interface

- Apague `ExtractorMenu`. O `InserterMenu` passa a ter duas linhas:
  - `CycleRow` **"Retirar de"**: Produto / Principal / Adicional (vale quando atrás há uma máquina);
  - `CycleRow` **"Colocar em"**: Automático / Principal / Adicional (vale quando na frente há uma máquina).
- Cada mudança grava no `modData` e no nó na hora (como hoje).

### B6. Comandos e documentação

- `pl_dump` e `pl_inspect`: para o Inseridor, mostre `source=`, `slot=`, o item na mão com o progresso, `cooldown=`, `lastInput=` e `inputs=`.
- `docs/DEVELOPMENT.md`: atualize os comandos, o exemplo de saída do `pl_dump` e o "uso dos blocos" (sem Extrator; lados do Inseridor; interface com as duas linhas).
- `CHANGELOG.md` (`Unreleased`): Extrator e Inseridor unificados.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1–A4 + testes do `FakeChest`.
3. Core A5–A6 + testes 1–23 (migre os do Extrator primeiro, depois os novos).
4. Core A7–A8 + testes 24–26.
5. Adaptador B1–B3.
6. Adaptador B4–B6.
7. Docs, changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Os 26 testes desta fase passam, junto com os anteriores que continuam valendo.
- [ ] Não existe mais `ExtractorNode` nem item Extrator (só a constante de migração).
- [ ] Nenhum caminho perde itens: o que não pode ser entregue fica na mão ou na origem.
- [ ] PR com "APIs a confirmar" (no mínimo: troca de objeto em `location.objects` durante o `SaveLoaded`, momento em que o `ObjectListChanged` dispara essa troca, `ItemRegistry.Create` para Big Craftables, `SpriteBatch.Draw` com textura 1×1 esticada e rotação).

## Verificação local (usuário)

- [ ] Um save da Fase 2 com Extratores colocados carrega com eles trocados por Inseridores, na mesma direção e com o mesmo "Retirar de".
- [ ] **Baú → Inseridor → baú:** os itens passam direto, um por vez, e o braço gira de trás para a frente com o item na mão.
- [ ] **Baú → Inseridor → esteira → Inseridor → baú** funciona como a antiga linha Extrator → esteira → Inseridor.
- [ ] **Baú misto (madeira, minério e carvão) → Inseridor (Automático) → fornalha:** só minério e carvão saem; a madeira fica no baú; as barras saem por outro Inseridor (Produto) para um baú.
- [ ] Esteira chegando pela **lateral** do Inseridor funciona, e o braço vira para o lado dela.
- [ ] Esteira passando **de lado**, sem apontar para o Inseridor, não entrega nada.
- [ ] Baú atrás e esteira na lateral ao mesmo tempo: o braço alterna entre os dois.
- [ ] Baú numa lateral do Inseridor é ignorado.
- [ ] A interface do Inseridor mostra "Retirar de" e "Colocar em", e as escolhas continuam lá ao reabrir.
- [ ] Destino enche (baú cheio): o braço para com o item na mão e entrega quando abre espaço.
- [ ] Quebrar um Inseridor com item na mão: o Inseridor e o item caem no chão.
- [ ] `pl_inspect` sobre o Inseridor mostra a mão, a última entrada e as entradas.
- [ ] (Pendente da Fase 2) **Barril** com frutas produz vinho, que sai por um Inseridor (Produto). **Cristalário** com uma esteira de diamantes: só o primeiro entra, e as cópias saem por um Inseridor (Produto). Para pegar os itens: `player_add (BC)12`, `player_add (BC)21`, `player_add (O)72 5`.
- [ ] Nenhum erro do mod no log do SMAPI.
- [ ] (Esperado nesta fase) Salvar e carregar faz o item na mão sumir, como os itens das esteiras (Fase 4).
