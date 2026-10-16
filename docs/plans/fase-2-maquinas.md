# Fase 2: Máquinas

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md) (seção "Interação com máquinas"), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-2-maquinas`
- **PR:** `Fase 2: Máquinas`
- **Depende de:** Fase 1 mesclada.

## Objetivo

O Inseridor passa a abastecer **máquinas** usando **slots virtuais** (Principal e Adicional), e o Extrator passa a retirar delas (Produto, Principal ou Adicional). Ambos ganham uma **interface** para escolher o slot. Ao final, uma fornalha alimentada por duas esteiras (minério e carvão) produz barras que saem por um Extrator até um baú.

## Escopo

- Core: `IMachineEndpoint`, `VirtualSlots`, armazenamento dos slots por máquina na grade, lógica de inserção e retirada, regra do Cristalário, acionamento das máquinas.
- Adaptador: `MachineEndpoint` (via `Data/Machines`), drop dos slots ao remover a máquina, interface base de configuração, interfaces do Extrator e do Inseridor, comando `pl_inspect`.

## Fora de escopo

Persistência dos slots (Fase 3: nesta fase os slots **somem** ao salvar/carregar), dica ao passar o mouse (Fase 6), Covo e outras máquinas com lógica própria (README: fora do escopo).

---

## Regras do README que esta fase implementa

- Slots virtuais **Principal** (entrada da regra da máquina) e **Adicional** (`AdditionalConsumedItems`).
- Inseridor: modo **Automático**, **Principal** ou **Adicional**; itens que não servem ficam parados na esteira.
- Inseridor em máquina insere **só o que falta** para o slot; o resto espera na esteira.
- Extrator: **Produto** (padrão), **Principal** ou **Adicional**. A posição no chão não importa.
- **Cristalário:** o Inseridor só coloca uma gema quando ele está **vazio**.
- Máquina removida: itens dos slots **caem no chão**.
- **Uso manual** continua vanilla: o mod não interfere; os itens guardados continuam esperando.

---

## Parte A: Core

### A1. `Endpoints/IMachineEndpoint.cs`

```csharp
public sealed record AdditionalRequirement(string Id, int RequiredCount, Func<ILogisticsItem, bool> Matches);

public interface IMachineEndpoint : IEndpoint
{
    /// <summary>Quantidade exigida se 'item' é aceito como entrada principal; null se não é aceito.</summary>
    int? GetMainRequiredCount(ILogisticsItem item);

    /// <summary>Itens adicionais exigidos pela máquina (ex.: 1 carvão). Lista vazia se não houver.</summary>
    IReadOnlyList<AdditionalRequirement> AdditionalRequirements { get; }

    /// <summary>Sem produto guardado e sem processamento em andamento: pode começar uma nova rodada.</summary>
    bool IsIdle { get; }

    /// <summary>Máquina que repete sozinha depois de carregada (ex.: Cristalário).</summary>
    bool IsRepeating { get; }

    /// <summary>Para máquinas repetidoras: já está carregada com uma entrada.</summary>
    bool HasLoadedInput { get; }

    /// <summary>Produto pronto, se houver (sem retirar).</summary>
    ILogisticsItem? PeekOutput();

    /// <summary>Retira até 'max' unidades do produto pronto. Retirar tudo conta como "coletar" (dispara a lógica de coleta do jogo).</summary>
    ILogisticsItem? TakeOutput(int max);

    /// <summary>Tenta iniciar a máquina com a entrada principal e os adicionais. Consome os itens só se iniciar.</summary>
    bool TryStart(ILogisticsItem main, IReadOnlyList<ILogisticsItem> additional);
}
```

### A2. Enums

```csharp
public enum InserterSlotMode { Auto, Main, Additional }
public enum ExtractorSource { Product, Main, Additional }
```

Adicione `InserterSlotMode SlotMode` em `InserterNode` (padrão `Auto`) e `ExtractorSource Source` em `ExtractorNode` (padrão `Product`).

### A3. `Machines/VirtualSlots.cs`

| Membro | Descrição |
|---|---|
| `ILogisticsItem? Main` | Pilha da entrada principal |
| `int MainRequired` | Quantidade exigida para a pilha atual (definida quando o primeiro item entra) |
| `Dictionary<string, ILogisticsItem> Additional` | Por `AdditionalRequirement.Id` |
| `bool IsEmpty` | Sem itens |
| `bool IsComplete(IReadOnlyList<AdditionalRequirement> reqs)` | `Main != null && Main.Count >= MainRequired` **e** cada requisito com `Count >= RequiredCount` |
| `IEnumerable<ILogisticsItem> AllItems()` | Para drop e persistência |
| `void Clear()` | |

### A4. `Machines/MachineSlotStore.cs`

Guardado na `LogisticsGrid` (`grid.MachineSlots`): `Dictionary<TilePos, VirtualSlots>`.
- `VirtualSlots GetOrCreate(TilePos)`, `VirtualSlots? Get(TilePos)`.
- `IReadOnlyList<ILogisticsItem> RemoveMachine(TilePos)`: remove a entrada e devolve todos os itens (para o drop).
- Entradas vazias são removidas no fim de cada tick.

### A5. Inserção em máquina (`InserterNode.Accept`)

Depois de calcular `max` (Fase 1), se o destino for `IMachineEndpoint machine`:

```
slots = grid.MachineSlots.GetOrCreate(targetPos)
alvo = SlotMode:
  Auto       → Adicional, se algum requisito adicional aceita o item e ainda não está completo;
               senão Principal, se GetMainRequiredCount(item) != null; senão nenhum
  Main       → Principal
  Additional → Adicional
```

**Principal:**
1. Se `machine.IsRepeating && (machine.HasLoadedInput || slots.Main != null)` → 0 (regra do Cristalário).
2. Se `slots.Main == null`: `req = GetMainRequiredCount(item)`; se null → 0; `n = Min(max, req)`; guarda `n` itens em `slots.Main` (pilha inteira se `n == item.Count`, senão `Split(n)`); `MainRequired = req`.
3. Senão, se `slots.Main.CanStackWith(item)`: `n = Min(max, MainRequired - Main.Count)`; se `n > 0`, `Main.MergeFrom(item, n)`.
4. Senão → 0 (o slot Principal só aceita o mesmo item até completar).

**Adicional:** primeiro requisito (em ordem) que `Matches(item)` e ainda não está completo: `n = Min(max, RequiredCount - atual)`; guarda/junta. Se nenhum → 0.

Retorna `n` e aplica o cooldown como na Fase 1.

### A6. Acionamento das máquinas

No fim de `LogisticsGrid.Tick`, para cada `(pos, slots)` em `MachineSlots`:
- `machine = endpoints.GetEndpoint(pos) as IMachineEndpoint`; se null → ignore (o adaptador trata a remoção);
- se `slots.IsComplete(machine.AdditionalRequirements) && machine.IsIdle` → `machine.TryStart(slots.Main, adicionais na ordem dos requisitos)`; se retornou true → `slots.Clear()`.

Os slots podem ficar completos enquanto a máquina trabalha: eles esperam o produto ser retirado.

### A7. Retirada de máquina (`ExtractorNode.Update`)

Se a origem for `IMachineEndpoint machine`:
- `Product`: `out = machine.PeekOutput()`; se `out != null && predicate(out)` → `machine.TakeOutput(quantidade do tier)`.
- `Main`: retira de `slots.Main` (se `predicate` aceitar) a quantidade do tier.
- `Additional`: retira do primeiro adicional que o `predicate` aceitar.
- Entrega à frente como na Fase 1. Se a frente não aceitar tudo, devolva o resto ao slot de onde saiu.

Quantidade do tier: `Single = 1`, `Stack = tudo`, `Configurable = ConfiguredAmount`.

### A8. Testes do Core

Crie `Fakes/FakeMachine : IMachineEndpoint` configurável: tabela `id → quantidade exigida`, lista de requisitos adicionais, `IsRepeating`, tempo de processamento em ticks (avançado por um método `Advance()` chamado pelo teste ou por `TryStart` + contador), produto gerado.

Cenários (nomes de exemplo):
- **Fornalha** (`(O)378` minério de cobre exige 5; adicional `coal` = `(O)382` ×1; produz `(O)334` barra de cobre).
- **Barril** (qualquer fruta `cat -79` exige 1; sem adicionais).
- **Cristalário** (qualquer gema exige 1; repetidora).

Testes obrigatórios:
1. Inseridor básico (Auto) enche o Principal da fornalha 1 a 1 até 5 e para; o resto da pilha espera na esteira.
2. Carvão em Auto vai para o Adicional; minério vai para o Principal.
3. Modo Principal com carvão: recusado, o carvão fica parado na esteira.
4. Modo Adicional com minério: recusado.
5. Com Principal e Adicional completos e a máquina ociosa, `TryStart` é chamado **uma vez** com 5 minérios e 1 carvão; os slots ficam vazios.
6. Máquina ocupada: os slots completam e esperam; `TryStart` só acontece depois que o produto é retirado.
7. Principal com cobre aceita só cobre até completar; ferro é recusado.
8. Barril: 1 fruta inicia imediatamente; a pilha de 10 frutas na esteira é consumida uma a uma a cada ciclo.
9. Cristalário: aceita a primeira gema; depois de carregado (`HasLoadedInput`) recusa novas gemas, mesmo com o slot vazio.
10. Extrator Produto retira o produto pronto; com o tier `Single` e produto ×3, retira 1 por operação.
11. Extrator Produto não retira enquanto não há produto.
12. Extrator Principal devolve os minérios guardados para a esteira.
13. `RemoveMachine` devolve todos os itens dos slots.
14. **Fluxo completo:** baú de minério → Extrator → esteiras → Inseridor (Principal) → fornalha ← Inseridor (Adicional) ← esteiras ← Extrator ← baú de carvão; Extrator (Produto) → esteiras → Inseridor → baú de barras. Com 10 minérios e 2 carvões, ao final há 2 barras no baú e nenhum item foi perdido ou criado.

---

## Parte B: Adaptador

### B1. `Framework/World/MachineEndpoint.cs`

Envolve um `StardewValley.Object` cujo `GetMachineData()` não é null. **Use como referência a implementação do Automate** para máquinas genéricas de `Data/Machines` (no repositório `Pathoschild/StardewMods`, pasta `Automate`, procure a classe que trata máquinas definidas por `MachineData`, por exemplo `GenericObjectMachine`). Copie a **abordagem**, não o código.

- `GetMainRequiredCount(item)`: encontra a regra de saída (`OutputRules`) com gatilho `ItemPlacedInMachine` que aceita o item **ignorando a quantidade**, e devolve o `RequiredCount` do gatilho. O jogo tem utilitários para isso em `MachineDataUtility` (ex.: `TryGetMachineOutputRule`, que devolve a regra e também a regra "ignorando quantidade"); confira a assinatura.
- `AdditionalRequirements`: um `AdditionalRequirement` por item em `MachineData.AdditionalConsumedItems` (`Id` = `ItemId` qualificado; `Matches` compara o `QualifiedItemId`; `RequiredCount`).
- `IsIdle`: `machine.heldObject.Value == null`.
- `PeekOutput`: `heldObject` se `readyForHarvest` for true.
- `TakeOutput(max)`: se `max >= Stack`, faz a **coleta** como o Automate faz (limpa `heldObject`, `readyForHarvest = false`, e dispara a lógica de "produto coletado", que é o que faz o Cristalário recomeçar); senão reduz o `Stack` e devolve a parte.
- `IsRepeating`: alguma regra tem gatilho `OutputCollected`.
- `HasLoadedInput`: `heldObject.Value != null || lastInputItem.Value != null`.
- `TryStart(main, additional)`: **nunca use o inventário do jogador.** Monte uma lista temporária com os adicionais, verifique os requisitos contra ela, acione a máquina com a entrada principal (ex.: `OutputMachine` ou o método equivalente que o Automate usa), e consuma manualmente os itens da entrada e dos adicionais só se a máquina iniciou. Não mostre mensagens nem toque sons de erro.
- **Exclusões:** `CrabPot` (Covo) e objetos nossos nunca viram `MachineEndpoint`.

### B2. `GameEndpointProvider`

Ordem: baú suportado → `ChestEndpoint`; objeto nosso → null; `CrabPot` → null; `obj.GetMachineData() != null` → `MachineEndpoint`; senão → null.

### B3. Drop dos slots

Em `ObjectListChanged.Removed`: para objetos que **não são nossos**, se `grid.MachineSlots.Get(pos) != null` → `RemoveMachine(pos)` e solte os itens no chão como na Fase 1.

### B4. Interface base: `Framework/Menus/ConfigMenu.cs`

Menu simples reutilizável (`IClickableMenu`), base para todas as interfaces do mod. Layout livre (README), mas siga:
- caixa centralizada com o título do bloco no topo (`Game1.drawDialogueBox` ou `IClickableMenu.drawTextureBox`);
- **linhas** empilhadas verticalmente; cada linha é um objeto `ConfigRow` com `Draw`, `ReceiveLeftClick`, `ReceiveKeyPress`, `Height`;
- nesta fase implemente `CycleRow` (rótulo + valor atual + setas ◀ ▶ que alternam entre opções);
- botão "OK" no canto, fecha também com `Esc`/tecla de menu;
- toda alteração chama um callback `onChanged`, que grava no `modData` na hora;
- textos do i18n.

As Fases 4 e 5 acrescentam `ItemPickerRow`, `ToggleRow`, `NumberRow`, `TextRow`, `ButtonRow`. Deixe a classe pronta para isso.

### B5. Interfaces do Extrator e do Inseridor

- `ExtractorMenu`: uma `CycleRow` "Retirar de" com Produto / Principal / Adicional.
- `InserterMenu`: uma `CycleRow` "Colocar em" com Automático / Principal / Adicional.
- Abertura: `Input.ButtonPressed` com `e.Button.IsActionButton()` e o tile do cursor (`e.Cursor.GrabTile`) contendo um Extrator ou Inseridor nosso → suprimir o botão e `Game1.activeClickableMenu = new ...`.
- `modData`: `ModDataKeys.ExtractorSource`, `ModDataKeys.InserterSlotMode` (valores como nome do enum). `NodeFactory` passa a ler esses valores.

### B6. Comando `pl_inspect`

Imprime o nó sob o cursor (tipo, direção, configuração, slots) e, se o tile for uma máquina, o conteúdo dos slots virtuais.

### B7. i18n

```json
"menu.extractor.title": "Extrator",
"menu.extractor.source": "Retirar de",
"menu.inserter.title": "Inseridor",
"menu.inserter.slot": "Colocar em",
"slot.auto": "Automático",
"slot.main": "Principal",
"slot.additional": "Adicional",
"slot.product": "Produto",
"menu.ok": "OK"
```

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1–A4 + `FakeMachine`.
3. Core A5–A7 + testes 1–14.
4. Adaptador B1–B3.
5. Adaptador B4–B5.
6. B6, B7, docs, changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Os 14 testes desta fase passam, junto com os anteriores.
- [ ] `MachineEndpoint` nunca usa o inventário do jogador.
- [ ] PR com "APIs a confirmar" (no mínimo: `GetMachineData`, `MachineDataUtility`, o método usado para acionar a máquina, a lógica de coleta, `lastInputItem`, `IClickableMenu`, `IsActionButton`).

## Verificação local (usuário)

- [ ] Interfaces: clicar com o botão de ação num Extrator abre "Retirar de"; num Inseridor abre "Colocar em". As escolhas continuam lá ao reabrir.
- [ ] **Fornalha:** baú de minério de cobre → esteiras → Inseridor (Principal) → fornalha; baú de carvão → esteiras → Inseridor (Adicional) → mesma fornalha; Extrator (Produto) → esteiras → Inseridor → baú. Barras de cobre chegam ao baú final.
- [ ] Os mesmos dois inseridores em **Automático**, com minério e carvão misturados numa única esteira: a fornalha também funciona.
- [ ] Inseridor em **Principal** recebendo carvão: o carvão fica parado na esteira.
- [ ] **Barril** com frutas: produz vinho; o vinho sai pelo Extrator.
- [ ] **Cristalário** com uma esteira de diamantes: só o primeiro diamante entra; os outros ficam parados. O Extrator retira as cópias e o Cristalário continua produzindo.
- [ ] `pl_inspect` sobre a fornalha mostra os slots (ex.: Principal 3/5).
- [ ] Quebrar a fornalha com itens nos slots: os itens caem no chão.
- [ ] Colocar minério **na mão** numa fornalha com itens guardados nos slots: funciona como no jogo normal.
- [ ] Nenhuma mensagem de erro do jogo aparece na tela durante a automação.
- [ ] Nenhum erro do mod no log do SMAPI.
- [ ] (Esperado nesta fase) Salvar e carregar faz os itens dos slots e das esteiras sumirem.
