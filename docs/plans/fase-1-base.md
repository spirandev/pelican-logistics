# Fase 1: Base (Esteira, Extrator, Inseridor, baú → baú)

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-1-base`
- **PR:** `Fase 1: Base`
- **Depende de:** Fase 0 mesclada.

## Objetivo

Ao final desta fase, o jogador consegue pegar no Clint (ou com `pl_give`) uma **Esteira**, um **Extrator** e um **Inseridor**, montar `baú → Extrator → esteiras → Inseridor → baú` e **ver os itens andando** de um baú para o outro, com curvas, rotação, acúmulo e junção de pilhas.

Esta fase também cria **toda a fundação** que as próximas usam: abstrações do Core, topologia, ordem de processamento, registro do mundo, renderização e entrada. Projete pensando nas próximas fases, mas **implemente só o que está aqui**.

## Escopo

- Core: itens, endpoints de baú, nós (Esteira, Extrator, Inseridor), grade, mundo, topologia, `Balance`, `PathMath`, `PlaceholderPainter`.
- Adaptador: registro dos 3 itens, loja do Clint, texturas placeholder, registro do mundo, tick, desenho (Harmony), rotação, seta fantasma, remoção com drop, comandos `pl_give` e `pl_dump`.

## Fora de escopo

Máquinas (Fase 2), salvar itens em trânsito (Fase 3: nesta fase os itens em trânsito **somem** ao salvar/carregar, isso é esperado), filtros e divisor (Fase 4), ponte, tiers e subterrânea (Fase 5), dicas ao passar o mouse (Fase 6).

---

## Parte A: Core

### A1. `Items/ILogisticsItem.cs`

Abstração de uma pilha de itens do jogo. O Core **nunca** cria itens do nada: só divide e junta os que recebe.

```csharp
namespace PelicanLogistics.Core.Items;

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
```

**Contrato de transferência** (vale para todo o Core, documente como comentário em `ILogisticsItem`):
- `Split` só é chamado com `1 <= amount < Count`. Para mover a pilha inteira, passe a **própria instância**.
- Quem recebe uma pilha inteira guarda a própria instância; quem entregou **descarta** sua referência.

### A2. `Endpoints/`

```csharp
namespace PelicanLogistics.Core.Endpoints;

/// <summary>Algo que não é um nó do mod mas com que os nós interagem (baú, máquina).</summary>
public interface IEndpoint { }

public enum ExtractAmount { One, Stack, Exactly }

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

public interface IEndpointProvider
{
    /// <summary>Endpoint no tile, ou null. Nunca retorna nós do próprio mod.</summary>
    IEndpoint? GetEndpoint(TilePos position);
}
```

### A3. `Simulation/Balance.cs`

Classe estática com as constantes da seção 4.6 do plano mestre. Inclua `public const int End = ProgressPerTile;` para legibilidade.

### A4. Enums de nós (`Nodes/`)

```csharp
public enum NodeKind { Belt, Extractor, Inserter, Filter, Splitter, UndergroundBelt }
public enum BeltTier { Basic, Fast, Express }
public enum TransferTier { Single, Stack, Configurable }
public enum AccessoryKind { None, Bridge, Regulator }
```

Já defina todos os valores (as próximas fases usam), mas só implemente os comportamentos desta fase. `Balance` deve ter `static int SpeedOf(BeltTier tier)`.

### A5. `Nodes/TransitSlot.cs`

Um espaço que segura **uma pilha** em movimento.

| Membro | Descrição |
|---|---|
| `ILogisticsItem? Item` | Pilha atual |
| `int Progress` | 0 a `Balance.End` |
| `Direction EntrySide` | Lado por onde a pilha entrou no tile (para o desenho) |
| `bool Blocked` | True se chegou ao fim e não conseguiu sair (total ou parcialmente) no último tick |
| `bool IsEmpty` | `Item == null` |
| `void Put(ILogisticsItem item, Direction entrySide)` | Progress = 0, Blocked = false |
| `ILogisticsItem? Clear()` | Esvazia e devolve |
| `bool Advance(int speed)` | Soma `speed`, limita a `End`, retorna true se está em `End` |

### A6. `Nodes/LogisticsNode.cs` (base abstrata)

| Membro | Descrição |
|---|---|
| `TilePos Position { get; }` | |
| `Direction Facing { get; set; }` | Para nós sem direção (Divisor), ignorado |
| `abstract NodeKind Kind { get; }` | |
| `AccessoryKind Accessory { get; set; }` | Fase 5 usa; inicia `None` |
| `virtual IEnumerable<TransitSlot> Slots` | Todos os espaços do nó (vazio por padrão). Usado no desenho, na persistência e na remoção |
| `virtual bool ProvidesTo(TilePos neighbor)` | True se este nó **entrega itens** naquele vizinho. Padrão: false |
| `virtual bool CanAccept(Direction entrySide, ILogisticsItem item)` | Consulta sem efeito colateral |
| `virtual int Accept(Direction entrySide, ILogisticsItem item, TickContext ctx)` | Aceita até `item.Count`; retorna quantos aceitou (ver contrato em A1). Padrão: 0 |
| `abstract void Update(TickContext ctx)` | Um tick de lógica |

**Convenção de lados:** `entrySide` é o **lado deste nó** por onde o item entra. Se o item vem do vizinho a oeste, `entrySide = West`. Para entregar a um vizinho na direção `d`, o nó chama `vizinho.Accept(d.Opposite(), item, ctx)`.

### A7. `Simulation/TickContext.cs`

```csharp
public sealed class TickContext
{
    public LogisticsGrid Grid { get; }
    public IEndpointProvider Endpoints { get; }
    public LogisticsNode? NodeAt(TilePos pos) => this.Grid.Get(pos);
}
```

Inclua um método auxiliar usado por todos os nós que empurram itens:

```csharp
/// <summary>
/// Tenta entregar a pilha do slot ao nó vizinho na direção 'outSide'.
/// Se o vizinho aceitar tudo, o slot é esvaziado. Senão o slot fica Blocked.
/// </summary>
public void PushForward(LogisticsNode from, TransitSlot slot, Direction outSide)
```

Lógica: `before = slot.Item.Count`; `target = NodeAt(from.Position.Offset(outSide))`; se null → `Blocked = true`; senão `taken = target.Accept(outSide.Opposite(), slot.Item, this)`; se `taken == before` → `slot.Clear()`; senão `Blocked = true`.

### A8. `Nodes/BeltNode.cs`

| Membro | Descrição |
|---|---|
| `BeltTier Tier` | Velocidade via `Balance.SpeedOf` |
| `Direction InputSide` | Calculado pela `Topology` (ver A11). Valor inicial: `Facing.Opposite()` |
| `TransitSlot Slot` | Um único slot (README: 1 pilha por tile) |
| `ProvidesTo(n)` | `n == Position.Offset(Facing)` |

**`CanAccept(entrySide, item)`**: `entrySide == InputSide && Slot.IsEmpty`.

**`Accept(entrySide, item, ctx)`**:
1. Se `entrySide != InputSide` → 0. (**Lateral ignorada**, README "Regras de conexão".)
2. Se `Slot.IsEmpty` → `Slot.Put(item, entrySide)`, retorna `item.Count`.
3. Se `Slot.Blocked && Slot.Progress == End && Slot.Item.CanStackWith(item)` → **junção** (README: "Pilhas iguais se juntam... só quando a esteira está acumulando"; plano mestre D11): `n = Slot.Item.MergeFrom(item, item.Count)`; retorna `n` (pode ser parcial ou 0 se a pilha estiver cheia).
4. Senão → 0.

**`Update(ctx)`**: se o slot tem item e `Slot.Advance(speed)` retornou true → `ctx.PushForward(this, Slot, Facing)`. Se o push esvaziou o slot, `Blocked = false` (feito pelo `Clear`).

### A9. `Nodes/ExtractorNode.cs`

Origem **atrás** (`Position.Offset(Facing.Opposite())`), saída **na frente** (plano mestre D2).

| Membro | Descrição |
|---|---|
| `TransferTier Tier` | Esta fase só usa `Single` |
| `int ConfiguredAmount` | Fase 5 |
| `int Cooldown` | Ticks até a próxima operação |
| `ProvidesTo(n)` | `n == Position.Offset(Facing)` |

**`Update(ctx)`**:
1. Se `Cooldown > 0` → `Cooldown--` e retorna.
2. `front = ctx.NodeAt(Position.Offset(Facing))`. Se `front` não for `BeltNode` (Fases 4 e 5 acrescentam Filtro e entrada de Subterrânea; deixe um método `IsValidOutput(LogisticsNode)` para isso, plano mestre D3) → retorna.
3. Se `!front.CanAccept(Facing.Opposite(), <qualquer>)`: como `CanAccept` precisa de um item, crie um método `bool CanReceiveFrom(Direction entrySide)` na base (padrão false) e implemente em `BeltNode` como `entrySide == InputSide && Slot.IsEmpty`. Use-o aqui. Se false → retorna.
4. `source = ctx.Endpoints.GetEndpoint(Position.Offset(Facing.Opposite()))`. Se for `IChestEndpoint chest`:
   - `predicate = this.GetExtractionPredicate(ctx)` (nesta fase: aceita tudo; a Fase 4 acopla o filtro);
   - modo: `Single → (One, 1)`, `Stack → (Stack, 0)`, `Configurable → (Exactly, ConfiguredAmount)`;
   - `item = chest.Extract(predicate, modo, n)`.
   - Se `item != null`: `taken = front.Accept(Facing.Opposite(), item, ctx)`. Se `taken < item.Count` (não deveria acontecer), devolva o resto com `chest.Insert(item, item.Count)`. `Cooldown = Balance.TransferCooldownTicks`.
5. Outros endpoints (máquinas) → Fase 2.

### A10. `Nodes/InserterNode.cs`

Entrada **atrás**, destino **na frente**. O Inseridor é **passivo**: quem entrega é a esteira, chamando `Accept`.

| Membro | Descrição |
|---|---|
| `TransferTier Tier`, `int ConfiguredAmount`, `int Cooldown` | Como no Extrator |

**`Accept(entrySide, item, ctx)`**:
1. Se `entrySide != Facing.Opposite()` ou `Cooldown > 0` → 0.
2. `max = Tier switch { Single => 1, Stack => item.Count, Configurable => Math.Min(ConfiguredAmount, item.Count) }`.
3. `target = ctx.Endpoints.GetEndpoint(Position.Offset(Facing))`.
4. Se `IChestEndpoint chest` → `inserted = chest.Insert(item, max)`.
5. Outros endpoints → Fase 2 (retorne 0).
6. Se `inserted > 0` → `Cooldown = Balance.TransferCooldownTicks`. Retorna `inserted`.

**`Update(ctx)`**: `if (Cooldown > 0) Cooldown--;`

> Por causa da ordem de processamento (A11), o Inseridor é atualizado **antes** da esteira que o alimenta, então o cooldown já diminuiu quando a esteira tenta entregar.

### A11. `Simulation/Topology.cs`

Classe estática `Topology.Recompute(LogisticsGrid grid)`, chamada pela grade quando ela está "suja".

**1. Lado de entrada das esteiras** (README "Regras de conexão" e curvas):

Para cada `BeltNode b`:
- `back = b.Facing.Opposite()`, `left = b.Facing.CounterClockwise()`, `right = b.Facing.Clockwise()`.
- `provides(side)` = existe um nó em `b.Position.Offset(side)` e ele `ProvidesTo(b.Position)`.
- Se `provides(back)` → `InputSide = back` (reta; laterais ignoradas).
- Senão, se **exatamente um** de `provides(left)`, `provides(right)` → `InputSide` = esse lado (**curva**).
- Senão → `InputSide = back`.

**2. Ordem de processamento ("de jusante para montante")**:
- `Downstream(node)`: lista de nós para onde o nó entrega. Esteira e Extrator: o nó em `Position.Offset(Facing)`, se existir. Inseridor: nenhum. (Fases 4 e 5 acrescentam Filtro, Divisor e Subterrânea; deixe `Downstream` como método virtual em `LogisticsNode`, padrão vazio.)
- Faça uma DFS em pós-ordem seguindo `Downstream`, a partir de cada nó ainda não visitado (iterando os nós em ordem estável: Y crescente, depois X crescente, para a ordem ser determinística). Um nó é adicionado à lista **depois** de todos os seus nós de jusante. Ciclos são cortados pela marcação de visitado (plano mestre D12).
- A lista resultante é a ordem de `Update`.

### A12. `Simulation/LogisticsGrid.cs`

Uma grade por local do jogo.

| Membro | Descrição |
|---|---|
| `void Add(LogisticsNode node)` | Lança exceção se já houver nó na posição. Marca suja |
| `RemovalResult Remove(TilePos pos)` | Remove e devolve os itens de todos os `Slots` e o acessório. Marca suja. Retorna `RemovalResult.Empty` se não havia nó |
| `LogisticsNode? Get(TilePos pos)` | |
| `IEnumerable<LogisticsNode> Nodes` | |
| `void MarkDirty()` | Força `Topology.Recompute` no próximo tick (chamado também ao girar um nó) |
| `void Tick(IEndpointProvider endpoints)` | Se suja → recalcula. Cria o `TickContext` e chama `Update` de cada nó na ordem |

```csharp
public sealed record RemovalResult(IReadOnlyList<ILogisticsItem> Items, AccessoryKind Accessory)
{
    public static readonly RemovalResult Empty = new(Array.Empty<ILogisticsItem>(), AccessoryKind.None);
}
```

### A13. `Simulation/LogisticsWorld.cs`

| Membro | Descrição |
|---|---|
| `LogisticsGrid GetOrCreate(string locationKey)` | |
| `LogisticsGrid? Get(string locationKey)` | |
| `IEnumerable<KeyValuePair<string, LogisticsGrid>> Grids` | |
| `void Clear()` | |
| `void Tick(Func<string, IEndpointProvider?> endpointsFor)` | Para cada grade com nós: obtém o provider; se null (local indisponível), pula |

### A14. `Rendering/PathMath.cs`

```csharp
/// <summary>
/// Posição do centro do item dentro do tile, em unidades de tile (0..1, origem no canto superior esquerdo).
/// Reta: do meio da borda de entrada ao meio da borda de saída.
/// Curva (entrada e saída em lados adjacentes): arco de quarto de círculo com centro no canto
/// compartilhado pelos dois lados e raio 0,5.
/// Entrada e saída no mesmo lado: trate como reta do centro ao lado (não deve acontecer).
/// </summary>
public static (float X, float Y) GetPosition(Direction entrySide, Direction exitSide, int progress)
```

Meios das bordas: North (0,5; 0), East (1; 0,5), South (0,5; 1), West (0; 0,5).

### A15. `Rendering/PlaceholderPainter.cs` e `Rendering/Rgba.cs`

- `public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)`.
- `public sealed class PixelBuffer { int Width; int Height; Rgba[] Pixels; void Set(x, y, c); void FillRect(...); void FillTriangle(...); void FillDiamond(...); void FillCircle(...); void DrawArrow(int cx, int cy, Direction dir, Rgba c); void Blit(PixelBuffer src, int dx, int dy); }`.
- `public static class PlaceholderPainter` com métodos que pintam **16×16, apontando para o Norte** (o adaptador gira ao desenhar):
  - `PaintBelt(BeltTier tier)`: quadrado cinza (claro/médio/escuro conforme o tier) com 1/2/3 setas brancas apontando para cima (README "Placeholders visuais").
  - `PaintExtractor(TransferTier tier)`: fundo transparente, triângulo laranja apontando para cima; 2 ou 3 pontos brancos no canto para Stack/Configurable.
  - `PaintInserter(TransferTier tier)`: igual, azul.
  - Deixe `PaintFilter`, `PaintSplitter`, `PaintUnderground`, `PaintBridge`, `PaintRegulator` **para as fases que os usam**.
  - `PaintIcon(PixelBuffer sprite16)`: devolve um buffer 16×32 com o sprite centralizado na metade de baixo (ícone de Big Craftable).
- `public static class PlaceholderColors`: as cores do README como constantes `Rgba`.

### A16. Testes do Core (Parte A)

Crie em `tests/.../Fakes/`:
- `FakeItem : ILogisticsItem`: construtor `(string id, int count, int quality = 0, int maxStack = 999, int category = 0, params string[] tags)`. `CanStackWith` = mesmo id e mesma qualidade.
- `FakeChest : IChestEndpoint`: lista de `FakeItem` com capacidade configurável (número de slots); implementa todos os modos de `Extract` e `Insert` com junção em pilhas existentes.
- `FakeEndpoints : IEndpointProvider`: dicionário `TilePos → IEndpoint`.
- `GridBuilder`: helper para montar grades em testes com pouco código (ex.: `.Belt(0,0, East).Belt(1,0, East).Chest(…)`) e rodar `n` ticks.

Testes obrigatórios (um arquivo por classe testada):

**BeltNode / esteiras**
1. Item numa esteira básica chega ao fim em exatamente 60 ticks.
2. Item atravessa 3 esteiras em linha; ao entrar numa esteira começa com progresso 0 e `EntrySide` correto.
3. Sem nada à frente: o item para no fim e fica `Blocked`.
4. **Fila compactada anda junta:** duas pilhas diferentes em esteiras vizinhas, ambas no fim, com espaço à frente da primeira: num único tick as duas avançam um tile (nenhum buraco aparece).
5. **Lateral ignorada:** esteira A apontando para a lateral de uma esteira B reta (que tem esteira atrás): o item de A fica bloqueado.
6. **Curva:** esteira B sem nada atrás e com A entregando pela lateral: `InputSide` de B = lado de A, e o item atravessa.
7. **Duas laterais e nada atrás:** nenhuma curva; os dois lados ficam bloqueados.
8. **Junção:** pilha da frente bloqueada; pilha de trás do mesmo item chega ao fim e se junta.
9. Junção parcial: 990 + 20 com MaxStack 999 → frente 999, trás 11 e continua bloqueada.
10. Itens diferentes não se juntam; qualidades diferentes não se juntam.
11. Não há junção com a pilha da frente **em movimento** (não bloqueada).
12. Girar uma esteira (`Facing` + `MarkDirty`) muda o fluxo no tick seguinte.

**ExtractorNode**
13. Retira 1 item a cada 30 ticks de um baú com 5 itens para uma esteira vazia.
14. Não retira se a esteira da frente está ocupada.
15. Não retira se a esteira da frente não recebe por aquele lado (ex.: esteira de lado).
16. Não retira se não há baú atrás; não retira se o baú está vazio.

**InserterNode**
17. Pilha de 5 no fim da esteira: o Inseridor insere 1 a cada 30 ticks; o resto fica parado na esteira até acabar.
18. Baú cheio: nada é inserido e a pilha fica bloqueada.
19. Inseridor recebendo pela lateral ou pela frente: recusa.

**Fluxo completo e conservação**
20. Baú A (10 madeiras + 3 pedras) → Extrator → 4 esteiras com uma curva → Inseridor → baú B: depois de ticks suficientes, B tem exatamente 10 madeiras e 3 pedras, A está vazio e nenhum item foi criado ou destruído.

**Topologia e grade**
21. Ordem de processamento: numa linha de esteiras, cada esteira aparece antes da que a alimenta.
22. Laço de 4 esteiras com 2 pilhas: as pilhas circulam. Laço com 4 pilhas (cheio): ninguém se move (D12).
23. `Remove` devolve o item do slot; remover posição vazia devolve `Empty`.
24. `Add` em posição ocupada lança exceção.

**PathMath e PlaceholderPainter**
25. Reta Oeste→Leste: progresso 0 = (0; 0,5), metade = (0,5; 0,5), fim = (1; 0,5).
26. Curva Oeste→Norte: início (0; 0,5), fim (0,5; 0), e no meio a distância até o canto (0; 0) é 0,5.
27. `PaintBelt` gera 16×16 com pixels não transparentes; `PaintIcon` gera 16×32.

---

## Parte B: Adaptador (mod)

> Lembre-se: este código **não compila na nuvem**. Siga a seção 3 do plano mestre e liste as APIs incertas no PR.

### B1. `Framework/ItemIds.cs`

Constantes:
```csharp
public const string Prefix = "spirandev.PelicanLogistics_";
public const string Belt = Prefix + "Belt";
public const string Extractor = Prefix + "Extractor";
public const string Inserter = Prefix + "Inserter";
public static string Qualified(string id) => "(BC)" + id;
```
e um método `bool IsOurBigCraftable(string? qualifiedItemId)`.

### B2. `Framework/ModDataKeys.cs`

```csharp
public const string Prefix = "spirandev.PelicanLogistics/";
public const string Facing = Prefix + "Facing";   // "North" | "East" | "South" | "West"
```

### B3. `Framework/Content/PlaceholderTextures.cs`

- Converte `PixelBuffer` → `Texture2D` (`new Texture2D(Game1.graphics.GraphicsDevice, w, h)` + `SetData(Color[])`).
- Gera e guarda em cache:
  - **folha de ícones** de Big Craftables: largura 8 células (128 px), altura 32 px por linha; índice 0 = Esteira, 1 = Extrator, 2 = Inseridor (as próximas fases acrescentam índices; documente a tabela de índices no topo do arquivo);
  - **sprites de mundo** 16×16 por tipo/tier (dicionário).
- Cria as texturas sob demanda (primeiro uso), não no `Entry`.

### B4. `Framework/Content/ContentRegistrar.cs`

Assina `Helper.Events.Content.AssetRequested`:

- `Mods/spirandev.PelicanLogistics/BigCraftables` → `e.LoadFrom(() => PlaceholderTextures.IconSheet, AssetLoadPriority.Exclusive)`.
- `Data/BigCraftables` → `e.Edit(asset => { var data = asset.AsDictionary<string, BigCraftableData>().Data; data[ItemIds.Belt] = new BigCraftableData { ... }; ... })` para os 3 itens:
  - `Name` = ID interno; `DisplayName` e `Description` vindos do i18n (`item.belt.name`, `item.belt.description` etc.);
  - `Price = 0`, `Fragility = 0`, `CanBePlacedIndoors = true`, `CanBePlacedOutdoors = true`, `IsLamp = false`;
  - `Texture = "Mods/spirandev.PelicanLogistics/BigCraftables"`, `SpriteIndex` conforme a tabela.
- `Data/Shops` → se `config.UnlockAll`, acrescenta à loja `Blacksmith` um `ShopItemData` por item: `Id = <id do item>`, `ItemId = "(BC)<id>"`, `Price = 0`, `AvailableStock = -1` (plano mestre D15).

Confira os nomes das classes e propriedades em [Modding:Items](https://stardewvalleywiki.com/Modding:Items) (Big Craftables) e [Modding:Shops](https://stardewvalleywiki.com/Modding:Shops).

### B5. `Framework/World/GameItem.cs`

Implementa `ILogisticsItem` envolvendo `StardewValley.Item`:
- `QualifiedItemId` → `Item.QualifiedItemId`; `Quality` → `Item.Quality`; `Category` → `Item.Category`; `DisplayName` → `Item.DisplayName`; `Count` → `Item.Stack`; `MaxStack` → `Item.maximumStackSize()`; `HasContextTag` → `Item.HasContextTag(tag)`.
- `CanStackWith(other)` → `Item.canStackWith(((GameItem)other).Item)`.
- `Split(amount)` → `var part = Item.getOne(); part.Stack = amount; Item.Stack -= amount; return new GameItem(part);`
- `MergeFrom(other, max)` → `n = Min(max, other.Count, MaxStack - Count)`; ajusta os dois `Stack`; retorna `n`.
- Propriedade pública `Item Item` para o adaptador.

### B6. `Framework/World/ChestEndpoint.cs`

Envolve `StardewValley.Objects.Chest`. Use `chest.Items` (pode conter `null`) e `chest.GetActualCapacity()`.
- `Extract`: percorre os slots em ordem; ignora `null`; aplica o predicado sobre um `GameItem` temporário. Ao retirar uma pilha inteira, coloque `null` no slot e chame `chest.clearNulls()`.
- `Insert(item, max)`: se `max < item.Count`, divide primeiro; usa `chest.addItem(item)`, que devolve o que sobrou (ou `null`); se sobrou algo de uma divisão, devolve ao item original. Retorna quantos entraram.
- `CountById`, `CountTotal`: somam `Stack`.
- **Baús suportados (D10):** só `chest.playerChest.Value == true` e `SpecialChestType` igual a `None` ou `BigChest`. Confira os nomes do enum.

### B7. `Framework/World/GameEndpointProvider.cs`

Recebe um `GameLocation`. `GetEndpoint(pos)`: `location.objects.TryGetValue(new Vector2(pos.X, pos.Y), out var obj)`; se for `Chest` suportado → `ChestEndpoint`; se `ItemIds.IsOurBigCraftable(obj.QualifiedItemId)` → `null`; senão → `null` (máquinas na Fase 2). Pode guardar os `ChestEndpoint` em cache por tick, mas não é obrigatório.

### B8. `Framework/World/WorldRegistry.cs`

Mantém o `LogisticsWorld` sincronizado com o jogo.

- Guarda `Dictionary<string, GameLocation>` (chave = `location.NameOrUniqueName`).
- **`Rebuild()`** (em `SaveLoaded`): limpa o mundo e percorre todos os locais com `Utility.ForEachLocation(...)` (incluindo interiores de construções). Para cada objeto nosso em `location.objects`, cria o nó com `NodeFactory`.
- **`NodeFactory.Create(Object obj, TilePos pos)`**: pelo `ItemId` decide o tipo e o tier; lê `Facing` do `modData` (padrão `East`).
- **`World.ObjectListChanged`**:
  - `e.Added`: para cada objeto nosso: se não tem `Facing` no `modData`, grava `PlacementController.CurrentDirection`; cria e adiciona o nó.
  - `e.Removed`: para cada objeto nosso: `grid.Remove(pos)` e solta cada item com `Game1.createItemDebris(gameItem.Item, new Vector2(pos.X * 64 + 32, pos.Y * 64 + 32), -1, e.Location)`.
- **`ReturnedToTitle`**: limpa tudo.
- `void Rotate(GameLocation loc, TilePos pos)`: gira o nó no sentido horário, grava `modData`, `grid.MarkDirty()`.
- `LogisticsNode? GetNode(GameLocation loc, TilePos pos)`.

### B9. Tick (`ModEntry`)

Em `GameLoop.UpdateTicked`:
```csharp
if (!Context.IsWorldReady || !Context.IsMainPlayer || !Game1.shouldTimePass())
    return;
this.registry.World.Tick(key => this.registry.TryGetLocation(key, out var loc) ? new GameEndpointProvider(loc) : null);
```
(README: o jogo pausado pausa as esteiras; à noite não há ticks.)

### B10. Desenho (`Framework/Patches/ObjectDrawPatch.cs` + `Framework/Rendering/NodeRenderer.cs`)

- Harmony **prefix** em `StardewValley.Object.draw(SpriteBatch spriteBatch, int x, int y, float alpha = 1f)` (é o método usado para desenhar objetos no mapa; `x` e `y` são o tile). Se o objeto é nosso → `NodeRenderer.Draw(...)` e `return false`; senão `return true`.
- `NodeRenderer.Draw(SpriteBatch b, Object obj, int x, int y, float alpha)`:
  - encontra o nó em `Game1.currentLocation` na posição `(x, y)`; se não achar, desenha o sprite com `Facing = East`;
  - posição na tela: `Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64))`;
  - `baseDepth = Math.Max(0f, (y * 64f) / 10000f) + 0.0001f` (abaixo do jogador quando ele está ao sul; documente que é um valor a ajustar no teste local);
  - desenha o sprite 16×16 com escala 4, **girado** conforme `Facing` (rotação `(int)Facing * π/2`, origem no centro do sprite (8,8), posição = canto + (32,32));
  - para cada `TransitSlot` com item: `PathMath.GetPosition(slot.EntrySide, node.Facing, slot.Progress)` → posição do centro em pixels → desenha com `gameItem.Item.drawInMenu(b, centro - new Vector2(32, 32), 0.5f, 1f, baseDepth + 0.00001f, StackDrawType.Hide, Color.White, false)`.
- O desenho do ícone no inventário continua sendo do jogo (usa a folha de ícones).

### B11. Rotação e seta fantasma (`Framework/Input/PlacementController.cs`)

- `Direction CurrentDirection` (padrão `East`).
- `Input.ButtonsChanged`: se `config.RotateKey.JustPressed()`:
  - se o item ativo do jogador (`Game1.player.ActiveObject`) é um bloco direcional nosso → `CurrentDirection = CurrentDirection.Clockwise()`;
  - senão, se o tile sob o cursor (`Helper.Input.GetCursorPosition().Tile`) tem um nó direcional nosso → `registry.Rotate(...)`;
  - em ambos os casos, suprima a tecla (`Helper.Input.SuppressActiveKeybinds(config.RotateKey)`).
- `Display.RenderedWorld`: se o jogador segura um bloco direcional nosso, desenha uma seta semitransparente (sprite da seta do placeholder) no tile do cursor apontando para `CurrentDirection`.

### B12. Comandos de console

Registre em `Entry` com `helper.ConsoleCommands.Add`:
- `pl_give`: coloca 10 de cada item do mod no inventário (`ItemRegistry.Create("(BC)" + id, 10)` + `Game1.player.addItemToInventoryBool` ou `addItemByMenuIfNecessary`).
- `pl_dump`: imprime no console os nós do local atual: posição, tipo, direção, `InputSide` (esteiras), item e progresso.

Documente os comandos em `docs/DEVELOPMENT.md`.

### B13. `i18n/default.json`

Acrescente:
```json
"item.belt.name": "Esteira",
"item.belt.description": "Move itens em um único sentido. Gire com R.",
"item.extractor.name": "Extrator",
"item.extractor.description": "Retira itens do baú atrás dele e coloca na esteira à frente, um por vez.",
"item.inserter.name": "Inseridor",
"item.inserter.description": "Pega itens da esteira atrás dele e coloca no baú à frente, um por vez."
```

### B14. `ModEntry`

Instancia e conecta: `ContentRegistrar`, `WorldRegistry`, `PlacementController`, `NodeRenderer`, Harmony (`new Harmony(this.ModManifest.UniqueID).PatchAll()` ou patches explícitos), comandos. Eventos: `SaveLoaded → registry.Rebuild()`, `ReturnedToTitle → registry.Clear()`, `UpdateTicked`, `ObjectListChanged`, `ButtonsChanged`, `RenderedWorld`, `AssetRequested`.

---

## Passo a passo sugerido

1. Resolver pendências do `STATUS.md`.
2. Core A1–A7 + fakes + testes básicos.
3. Core A8–A13 + testes 1–24.
4. Core A14–A15 + testes 25–27.
5. Adaptador B1–B7.
6. Adaptador B8–B9.
7. Adaptador B10–B12.
8. i18n, `ModEntry`, `DEVELOPMENT.md`, `CHANGELOG.md`, `STATUS.md`.
9. Revisão manual de todo o adaptador; abrir PR.

## Critérios de aceite (nuvem)

- [ ] Todos os 27 testes listados existem e passam (mais os da Fase 0).
- [ ] O Core continua sem nenhuma referência ao jogo.
- [ ] Todo o adaptador listado em B1–B14 existe e foi revisado manualmente.
- [ ] PR com "APIs a confirmar" (no mínimo: `Object.draw`, `Utility.ForEachLocation`, `ObjectListChanged`, `Chest.addItem`, `SpecialChestTypes`, `BigCraftableData`, `ShopItemData`, `Game1.shouldTimePass`, `drawInMenu`).

## Verificação local (usuário)

Use uma save de teste.

- [ ] O mod compila e carrega sem erros.
- [ ] Na loja do Clint aparecem Esteira, Extrator e Inseridor por 0g. Se a loja estiver fechada, use `pl_give`.
- [ ] Ícones placeholder aparecem no inventário.
- [ ] Segurando uma Esteira, aparece uma seta no tile do cursor; `R` gira a seta.
- [ ] A esteira colocada aparece no mapa como um quadrado cinza com seta, na direção escolhida.
- [ ] `R` com a mão livre sobre uma esteira colocada gira a esteira.
- [ ] O jogador **não consegue andar** sobre a esteira.
- [ ] Montagem: baú com madeira → Extrator (de costas para o baú) → 5 esteiras com uma curva → Inseridor → baú vazio. A madeira **anda suavemente** e chega ao segundo baú.
- [ ] Esteira apontando para a **lateral** de uma esteira reta: os itens param (não entram).
- [ ] Sem Inseridor no fim: os itens se acumulam; madeiras **se juntam** na mesma pilha; pedra fica atrás da madeira.
- [ ] Abrir o inventário ou um diálogo: os itens param. Fechar: voltam a andar.
- [ ] Sair da fazenda por um tempo e voltar: os itens avançaram (a lógica roda fora da tela).
- [ ] Quebrar uma esteira com picareta: a esteira e o item que ela levava caem no chão.
- [ ] `pl_dump` lista os nós do local.
- [ ] Profundidade de desenho: os itens aparecem por baixo do jogador quando ele está ao sul da esteira. Anote no `STATUS.md` se algo estiver desenhado na camada errada.
- [ ] Nenhum erro do mod no log do SMAPI.
- [ ] (Esperado nesta fase) Salvar e carregar faz os itens em trânsito sumirem; as esteiras e suas direções permanecem.
