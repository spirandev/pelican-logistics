# Fase 4: Persistência

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-4-persistencia`
- **PR:** `Fase 4: Persistência`
- **Depende de:** Fase 3 mesclada.

## Objetivo

Os **itens em trânsito** (nas esteiras e em qualquer `TransitSlot`, inclusive a mão dos Inseridores) e os **slots virtuais das máquinas** sobrevivem ao salvar e carregar o jogo, sem duplicar nem perder itens. A configuração dos blocos (direção, slot, etc.) já é salva pelo `modData` desde a Fase 1; esta fase **confirma** isso com testes locais.

## Por que é preciso

O jogo salva automaticamente os objetos colocados e o `modData` deles, mas os itens em trânsito vivem só na memória do Core. Além disso, o salvamento do SMAPI (`WriteSaveData`) grava JSON, e itens do jogo (que podem ser anéis, ferramentas, itens com sabor etc.) não se convertem em JSON de forma confiável.

## Estratégia

- O **estado** (posição, progresso, ponteiros, slots) vai em JSON pelo `Helper.Data.WriteSaveData`.
- Os **itens** vão num **inventário global** do jogo, que o próprio jogo salva com segurança: `Game1.player.team.GetOrCreateGlobalInventory("spirandev.PelicanLogistics/Transit")`.
- O JSON guarda o **índice** de cada item nesse inventário.
- Depois de salvar e depois de carregar, o inventário global é **esvaziado**, para que nenhuma função do jogo que percorre todos os itens (ex.: busca de itens) enxergue cópias.

---

## Parte A: Core

### A1. `Persistence/` (DTOs)

```csharp
public sealed class WorldRuntimeState
{
    public int Version { get; set; } = 1;
    public Dictionary<string, GridRuntimeState> Grids { get; set; } = new();
}

public sealed class GridRuntimeState
{
    public List<NodeRuntimeState> Nodes { get; set; } = new();
    public List<MachineSlotsState> Machines { get; set; } = new();
}

public sealed class NodeRuntimeState
{
    public int X { get; set; }
    public int Y { get; set; }
    public NodeKind Kind { get; set; }
    /// <summary>Um por TransitSlot, na mesma ordem de LogisticsNode.Slots. Slots vazios são gravados com ItemRef = -1.</summary>
    public List<SlotState> Slots { get; set; } = new();
    public int Cooldown { get; set; }
    /// <summary>Ponteiros de rodízio (Inseridor: última entrada; Divisor, Fase 5). -1 = não usado.</summary>
    public int InputPointer { get; set; } = -1;
    public int OutputPointer { get; set; } = -1;
}

public sealed class SlotState
{
    public int ItemRef { get; set; } = -1;
    public int Progress { get; set; }
    public Direction EntrySide { get; set; }
    public bool Blocked { get; set; }
}

public sealed class MachineSlotsState
{
    public int X { get; set; }
    public int Y { get; set; }
    public int MainRef { get; set; } = -1;
    public int MainRequired { get; set; }
    public Dictionary<string, int> AdditionalRefs { get; set; } = new();
}
```

Os DTOs são simples (propriedades públicas, sem lógica) para serem serializáveis pelo SMAPI.

### A2. Exportar e importar (`Persistence/RuntimeStateSerializer.cs`)

```csharp
public static class RuntimeStateSerializer
{
    /// <summary>'register' recebe um item e devolve o índice dele na lista externa.</summary>
    public static WorldRuntimeState Export(LogisticsWorld world, Func<ILogisticsItem, int> register);

    /// <summary>
    /// Restaura o estado em um mundo cujos nós JÁ foram recriados (a partir dos objetos do jogo).
    /// 'resolve' devolve o item de um índice, ou null se não existir.
    /// Itens que não encontram destino (nó removido, tipo diferente, número de slots diferente)
    /// vão para 'orphans', com o local e a posição, para o adaptador soltar no chão.
    /// </summary>
    public static void Import(
        LogisticsWorld world,
        WorldRuntimeState state,
        Func<int, ILogisticsItem?> resolve,
        ICollection<OrphanItem> orphans);
}

public sealed record OrphanItem(string LocationKey, TilePos Position, ILogisticsItem Item);
```

Regras:
- Um nó só recebe o estado se existir na mesma posição **e** com o mesmo `Kind`. Caso contrário, todos os itens do estado dele viram órfãos.
- Se o número de slots for diferente, os slots que sobram viram órfãos.
- `Progress` é limitado a `0..Balance.End` ao importar.
- `InputPointer` e `OutputPointer` são lidos e gravados por métodos virtuais em `LogisticsNode` (`ExportPointers` / `ImportPointers`), com implementação vazia por padrão. Nesta fase, implemente no Inseridor (`InputPointer` = `LastInput`, `-1` = null, senão `(int)Direction`); a Fase 5 implementa no Divisor.
- Slots virtuais: restaurados na grade (`MachineSlots`), sem verificar se a máquina ainda existe (o Core não sabe). O adaptador remove os órfãos depois (B3).
- `Version` diferente de 1 → importa nada e devolve todos os itens como órfãos (comportamento seguro).

### A3. Testes do Core

1. **Ida e volta:** monte uma grade com itens em várias esteiras (progressos diferentes, uma bloqueada), um Inseridor com um item na mão (no meio da ida), outro voltando (`Cooldown` > 0) com `LastInput` definido, e slots virtuais de uma máquina; exporte; crie um mundo novo com os mesmos nós vazios; importe; compare item a item (mesma instância via lista externa), progresso, `EntrySide`, `Blocked`, cooldown, `LastInput` e slots.
2. Nó que não existe mais no mundo novo: seus itens vão para `orphans` com local e posição corretos.
3. Nó com o mesmo lugar e tipo diferente: itens viram órfãos.
4. `Progress` fora do intervalo é corrigido.
5. Índice inexistente (`resolve` devolve null): o slot fica vazio e não lança exceção.
6. `Version` desconhecida: nada é importado; todos os itens viram órfãos.
7. Vários locais: cada grade restaura o próprio estado.
8. **Conservação:** número de itens exportados = itens restaurados + órfãos.

---

## Parte B: Adaptador

### B1. `Framework/Persistence/SaveManager.cs`

Constantes: `InventoryId = "spirandev.PelicanLogistics/Transit"`, `SaveDataKey = "runtime-state"`.

**`GameLoop.Saving`** (só se `Context.IsMainPlayer`):
1. `items = new List<Item>()`.
2. `state = RuntimeStateSerializer.Export(world, it => { items.Add(((GameItem)it).Item); return items.Count - 1; })`.
3. `inv = Game1.player.team.GetOrCreateGlobalInventory(InventoryId)`; `inv.Clear()`; adiciona todos os `items` na ordem.
4. `Helper.Data.WriteSaveData(SaveDataKey, state)`.

**`GameLoop.Saved`**: `GetOrCreateGlobalInventory(InventoryId).Clear()`.

> Os itens continuam vivos na memória do Core durante todo o processo; o inventário global é só o "veículo" para o arquivo de save.

**`GameLoop.SaveLoaded`** (depois de `registry.Rebuild()`, garanta a ordem chamando os dois no mesmo handler):
1. `state = Helper.Data.ReadSaveData<WorldRuntimeState>(SaveDataKey)`; se null → nada a fazer.
2. `inv = GetOrCreateGlobalInventory(InventoryId)`; `snapshot = inv.ToList()`.
3. `RuntimeStateSerializer.Import(world, state, i => i >= 0 && i < snapshot.Count && snapshot[i] != null ? new GameItem(snapshot[i]) : null, orphans)`.
4. `inv.Clear()`.
5. Para cada órfão: solta no chão na posição dele (`Game1.createItemDebris`) no local correspondente, e registra um aviso no log.
6. Remove slots virtuais de máquinas que não existem mais (B3) e solta seus itens.

### B2. Robustez

- Envolva `Saving` e `SaveLoaded` em `try/catch`: em caso de erro, registre no log com `LogLevel.Error` e **nunca** impeça o jogo de salvar. Se o `Import` falhar, devolva todos os itens do inventário global ao chão da fazenda, perto da casa, em vez de perdê-los.
- Se o inventário global tiver itens no carregamento mas não houver estado (`state == null`), solte todos perto da casa da fazenda e avise no log.

### B3. Limpeza de slots órfãos

Depois do `Import`, para cada entrada de `grid.MachineSlots`: se `GameEndpointProvider.GetEndpoint(pos)` não for `IMachineEndpoint`, remova a entrada e solte os itens no chão.

### B4. `DEVELOPMENT.md`

Acrescente uma seção "Como o mod salva" explicando o inventário global e o JSON, e o que acontece com itens órfãos.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1–A2 + testes 1–8.
3. Adaptador B1–B3.
4. Docs, changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Os 8 testes desta fase passam, junto com os anteriores.
- [ ] Nenhum caminho de código perde itens silenciosamente: todo item sem destino vira órfão e é solto no chão.
- [ ] PR com "APIs a confirmar" (no mínimo: `GetOrCreateGlobalInventory`, o tipo `Inventory`, `WriteSaveData`/`ReadSaveData`, ordem dos eventos `Saving`/`Saved`/`SaveLoaded`).

## Verificação local (usuário)

Use uma save de teste e **anote a quantidade de itens** antes de cada teste.

- [ ] Monte uma linha baú → esteiras → baú com 50 madeiras andando. Durma. Ao acordar, as madeiras continuam nas mesmas posições das esteiras.
- [ ] Salve e saia para o menu principal. Carregue a save: os itens estão nas esteiras, com o progresso preservado, e voltam a andar.
- [ ] Soma das madeiras (baús + esteiras) é a mesma antes e depois.
- [ ] Fornalha com 3 minérios no slot Principal: salve, carregue, `pl_inspect` mostra os 3 minérios; a fornalha continua quando chegarem os outros.
- [ ] Direções das esteiras e configurações dos Inseridores ("Retirar de" e "Colocar em") continuam as mesmas após carregar.
- [ ] Baú → Inseridor → baú com o braço no meio do caminho: salve e carregue; o item continua na mão e é entregue, sem duplicar.
- [ ] Carregue a save, **não salve**, saia e carregue de novo: nada duplicou.
- [ ] Desinstalar o mod não está no escopo (blocos viram itens de erro); **não** teste isso na sua save principal.
- [ ] Nenhum erro do mod no log do SMAPI.
