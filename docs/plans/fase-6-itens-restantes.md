# Fase 6: Itens restantes

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md) (seções "Transporte", "Inseridores", "Regulador"), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-6-itens-restantes`
- **PR:** `Fase 6: Itens restantes`
- **Depende de:** Fase 5 mesclada.

## Objetivo

Completar o catálogo da alpha:

| Item | Tipo | ID |
|---|---|---|
| Esteira Rápida | Bloco | `BeltFast` |
| Esteira Expressa | Bloco | `BeltExpress` |
| Inseridor de Pilha | Bloco | `InserterStack` |
| Inseridor Configurável | Bloco | `InserterConfigurable` |
| Esteira Subterrânea | Bloco | `UndergroundBelt` |
| Ponte | Acessório (`Data/Objects`) | `Bridge` |
| Regulador | Acessório (`Data/Objects`) | `Regulator` |

## Fora de escopo

Dicas ao passar o mouse e revisão geral das interfaces (Fase 7).

---

## Parte A: Core

### A1. Tiers

Os enums `BeltTier` e `TransferTier` já existem desde a Fase 1, e a lógica de quantidade por tier também. Nesta fase:
- garanta testes de velocidade das três esteiras (`Basic` 60 ticks/tile, `Fast` 30, `Express` 15);
- `Stack`: o Inseridor pega a pilha inteira (de um slot do baú, ou a que chegou pela esteira), limitada ao que cabe no destino;
- `Configurable`: `ConfiguredAmount` entre 1 e `Balance.MaxConfigurableAmount` (valores fora disso são corrigidos); de um baú, junta de vários slots até a quantidade (limitado a `MaxStack` e ao que cabe no destino).
- **Máquinas:** o Inseridor de Pilha/Configurável continua pegando **só o que falta** no slot (Fases 2 e 3).
- A lógica por tier já existe no `InserterNode` desde a Fase 3; aqui o foco são os itens novos, os testes e a interface.

### A2. Ponte

- `BeltNode.Accessory == AccessoryKind.Bridge` indica a ponte. Só `BeltNode` aceita ponte (README: não pode no Filtro nem no Divisor).
- `LogisticsGrid.TrySetAccessory(TilePos, AccessoryKind)`: valida o tipo do nó e se o tile já não tem acessório (README: **no máximo um acessório por tile**); retorna bool.
- `LogisticsGrid.RemoveAccessory(TilePos)`: retorna o acessório removido.
- A ponte **não muda** nada no fluxo de itens.
- `Remove` (Fase 1) já devolve o acessório em `RemovalResult`.

### A3. Regulador

```csharp
public enum RegulatorMode { PerItem, General }

public sealed class RegulatorRule
{
    public RegulatorMode Mode { get; set; } = RegulatorMode.General;
    public int GeneralLimit { get; set; } = 100;
    /// <summary>QualifiedItemId → limite. Itens fora da lista não são limitados (plano mestre D7).</summary>
    public Dictionary<string, int> PerItemLimits { get; set; } = new();

    /// <summary>Quantas unidades de 'item' ainda podem entrar no baú.</summary>
    public int AllowedAmount(ILogisticsItem item, IChestEndpoint chest);
}
```
- `General`: `Max(0, GeneralLimit - chest.CountTotal())`.
- `PerItem`: se houver limite para `item.QualifiedItemId` → `Max(0, limite - chest.CountById(id))`; senão → `int.MaxValue`.
- `InserterNode.Regulator` (`RegulatorRule?`): presente quando `Accessory == Regulator`. Só `InserterNode` aceita o Regulador.
- No `Fits` do Inseridor com destino **baú** (o cálculo de quanto pegar, Fase 3): `Min(espaço, Regulator.AllowedAmount(item, chest))`. Aplique o mesmo limite na entrega, para o caso de o baú mudar durante a ida. Com destino **máquina**, o Regulador é ignorado (plano mestre D8).
- `RegulatorRuleSerializer` (JSON) com o mesmo cuidado do `FilterRuleSerializer`.

### A4. Esteira Subterrânea

```csharp
public enum UndergroundRole { Entrance, Exit }
```

`UndergroundBeltNode`:

| Membro | Descrição |
|---|---|
| `UndergroundRole Role` | Definido na colocação e guardado no `modData` |
| `TransitSlot Slot` | Slot do próprio tile |
| `TransitSlot[] Tunnel` | Só na entrada: um slot por tile do vão (`0..UndergroundMaxGap`) |
| `UndergroundBeltNode? Partner` | Calculado pela `Topology` |
| `Slots` | `Slot` + `Tunnel` (para desenho, persistência e remoção) |

**Definição do papel na colocação** (função pura no Core, `UndergroundPairing.DecideRole(grid, pos, facing)`, chamada pelo adaptador antes de criar o nó): olhando para trás (`facing.Opposite()`) até `UndergroundMaxGap + 1` tiles, se existe uma **entrada** com a mesma direção e sem par → `Exit`; senão → `Entrance`.

**Pareamento** (`Topology`): para cada entrada, procure à frente, de 1 até `UndergroundMaxGap + 1` tiles, a **primeira** Esteira Subterrânea com a mesma direção; se for uma saída ainda sem par → par. O tamanho do túnel é `distância - 1`.

**Comportamento:**
- **Entrada:** recebe só por trás (sem curva), quando o `Slot` está vazio. Avança com `Balance.UndergroundSpeed`; no fim, passa para `Tunnel[0]` (ou direto para o `Slot` da saída se o túnel tiver tamanho 0). Os slots do túnel avançam e passam adiante como uma fila. Sem par → o item para no fim do `Slot` da entrada.
- **Saída:** recebe **só** do seu par. Avança e, no fim, entrega para o nó à frente como uma esteira (`ProvidesTo` = tile à frente).
- O que estiver no vão (cercas, caminhos, outras esteiras, outros blocos) **não interfere**.
- A saída é provedor para curvas e entrada válida de Divisor; a entrada é destino válido do Inseridor e saída válida do Divisor (atualize as regras de portas da Fase 5).
- Implemente `CanReceiveFrom`/`CanAccept` na entrada (por trás, `Slot` vazio) e `GetOutgoingSlot` na saída (o `Slot` dela), para o Inseridor e o Divisor funcionarem com a Subterrânea.
- `Downstream` da entrada = a saída; da saída = o nó à frente.
- Remover a entrada devolve o item do `Slot` **e de todo o túnel**.

### A5. Testes do Core

**Tiers**
1. Velocidades das três esteiras.
2. Inseridor de Pilha: baú → esteira retira a pilha inteira de um slot; esteira → baú insere a pilha inteira.
3. Inseridor de Pilha num baú quase cheio: insere o que cabe, o resto fica na esteira.
4. Inseridor Configurável com 15, baú → esteira: junta 10 + 5 de dois slots numa pilha de 15.
5. Inseridor Configurável com 5: uma pilha de 12 entra em 3 operações (5, 5, 2).
6. `ConfiguredAmount` 0 ou 5000 é corrigido para 1 ou 999.
7. Inseridor de Pilha na fornalha: só 5 minérios entram no Principal.

**Ponte**
8. `TrySetAccessory(Bridge)` funciona na esteira (todas as velocidades); falha no Filtro, no Divisor e no Inseridor.
9. Segundo acessório no mesmo tile falha.
10. A ponte não muda o fluxo; `Remove` da esteira devolve a ponte e o item.

**Regulador**
11. Modo Geral 50: baú com 45 itens recebe só 5 de uma pilha de 20; depois para.
12. Modo Por item: madeira 50 e pedra 100; madeira para em 50, pedra em 100, argila passa sem limite.
13. Regulador ignorado com destino máquina.
14. Regulador só pode ir no Inseridor.
15. Serializador: ida e volta; JSON corrompido → regra padrão.

**Esteira Subterrânea**
16. `DecideRole`: primeira colocada é entrada; segunda, a até 6 tiles à frente na mesma direção, é saída.
17. Pareamento com vão de 0, 3 e 5; com 6 de vão não pareia.
18. Item atravessa por baixo de um bloco qualquer no vão.
19. Entrada sem par: item para.
20. Túnel cheio: entrada bloqueia; ao liberar a saída, a fila anda.
21. Remover a entrada devolve os itens do túnel.
22. Duas subterrâneas paralelas não interferem.
23. Persistência: itens no túnel sobrevivem à ida e volta (Fase 4).
24. **Conservação** num cenário com subterrânea, Divisor e Regulador.

---

## Parte B: Adaptador

### B1. Registro dos itens

- `ItemIds` com todos os IDs acima; `IsOurBigCraftable` e um novo `IsOurAccessory`.
- `Data/BigCraftables` para os 5 blocos; `Data/Objects` para Ponte e Regulador (`ObjectData`: `Type = "Basic"`, `Category = 0`, `Price = 0`, textura `Mods/spirandev.PelicanLogistics/Objects`). Eles **não** devem ser colocáveis pelo jogo; confirme em [Modding:Items](https://stardewvalleywiki.com/Modding:Items) que esse tipo/categoria não é colocável e, se for, escolha outro e registre no PR.
- Loja do Clint (0g) para todos.
- Placeholders (README): esteiras com 2 e 3 setas em cinza médio/escuro; versões de Pilha/Configurável com 2/3 pontos; Subterrânea cinza escuro com círculo **branco** (entrada) ou **preto** (saída); Ponte marrom (faixa horizontal); Regulador amarelo (quadrado pequeno).
- `NodeFactory`: cria todos os tiers; lê `ConfiguredAmount`, `UndergroundRole`, `Accessory`, `RegulatorRule` do `modData`.
- Ao colocar uma Subterrânea (`ObjectListChanged.Added`): calcula o papel com `DecideRole`, grava no `modData`, cria o nó.

### B2. Aplicar e remover acessórios (`Framework/Input/AccessoryController.cs`)

`Input.ButtonPressed`, botão de ação, tile do cursor com nó nosso:
- **Segurando uma Ponte** sobre uma esteira sem acessório → `TrySetAccessory`; se ok, consome 1 Ponte do item ativo, grava `modData`, suprime o botão. Sobre outro bloco → mensagem curta "A ponte só pode ser colocada em esteiras." (`Game1.showRedMessage`), suprime.
- **Segurando um Regulador** sobre um Inseridor (qualquer tier) sem acessório → aplica com a regra padrão; consome 1. Sobre outro bloco → "O regulador só pode ser colocado em inseridores."
- **Mão livre** sobre esteira com ponte → remove a ponte e devolve ao inventário (`Game1.player.addItemToInventoryBool`; se não couber, solta no chão) (plano mestre D9).
- Remoção do bloco: o acessório do `RemovalResult` é criado com `ItemRegistry.Create("(O)" + id)` e solto no chão junto com os itens.

### B3. Passagem pela Ponte (`Framework/Patches/PassablePatch.cs`)

O jogador **passa por cima** de uma esteira com ponte. O jogo decide colisão de objetos chamando `Object.isPassable()`. Faça um patch Harmony **postfix** em `Object.isPassable`: se o objeto é uma esteira nossa com ponte no local atual → `__result = true`. Confirme no PR que esse é o método usado na colisão do jogador; se não for, encontre o método correto (ex.: no código de colisão de `GameLocation`) e registre.

### B4. Desenho

- Ponte: desenhada **por cima** do item em trânsito (`baseDepth + 0.00002f`), semitransparente o bastante para ver o item por baixo.
- Regulador: quadrado amarelo pequeno sobre o Inseridor.
- Subterrânea: só os slots da entrada e da saída aparecem; o túnel é invisível. Uma linha tracejada fraca entre entrada e saída pareadas, só quando o jogador segura uma Subterrânea (ajuda a montar).

### B5. Interfaces

- Nova linha `NumberRow`: rótulo + valor + botões `−10`, `−1`, `+1`, `+10` + campo digitável (`TextBox` aceitando só dígitos); valor limitado ao intervalo da linha.
- **Inseridor Configurável:** a interface ganha `NumberRow` "Quantidade por vez" (1–999).
- **Inseridor com Regulador** (plano mestre D13): a interface do Inseridor ganha uma seção "Regulador", visível só quando há regulador:
  - `CycleRow` Modo: Geral / Por item;
  - Geral: `NumberRow` Limite (0–99.999);
  - Por item: lista de até 8 linhas `item fantasma + NumberRow`, com "Adicionar" (escolhe do inventário) e "Remover";
  - `ButtonRow` "Remover regulador" → devolve o Regulador ao inventário e apaga a regra.

### B6. i18n

Nomes e descrições dos 7 itens, rótulos das novas interfaces, as duas mensagens de erro.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1 + testes 1–7.
3. Core A2–A3 + testes 8–15.
4. Core A4 + testes 16–24 (ajuste a Topologia e as portas do Divisor).
5. Adaptador B1.
6. Adaptador B2–B4.
7. Adaptador B5–B6.
8. Docs, changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Os 24 testes desta fase passam, junto com os anteriores.
- [ ] Nenhum acessório pode ficar em um bloco inválido nem dois acessórios no mesmo tile.
- [ ] PR com "APIs a confirmar" (no mínimo: `Object.isPassable` na colisão, `ObjectData` não colocável, `TextBox` numérico, `showRedMessage`).

## Verificação local (usuário)

- [ ] Esteira Rápida e Expressa: visivelmente 2× e 4× mais rápidas que a básica.
- [ ] Inseridor de Pilha: a pilha inteira sai do baú de uma vez e a pilha inteira entra no baú de destino.
- [ ] Inseridor Configurável com 5: sai de 5 em 5. A interface aceita digitar e usar os botões ±.
- [ ] **Ponte:** segurando a Ponte e clicando numa esteira, a ponte aparece e o jogador **passa por cima**; os itens continuam andando por baixo.
- [ ] Ponte num Filtro ou Divisor: mensagem de erro, nada é gasto.
- [ ] Clicar com a mão livre numa esteira com ponte: a ponte volta ao inventário.
- [ ] Quebrar uma esteira com ponte: esteira, ponte e item caem no chão.
- [ ] **Regulador** Geral 50 num Inseridor de baú: o baú para em 50 itens; o resto fica na esteira.
- [ ] Regulador Por item (madeira 30): madeira para em 30; pedra continua entrando.
- [ ] "Remover regulador" devolve o item.
- [ ] **Esteira Subterrânea:** primeira colocada vira entrada (círculo branco); a segunda, até 5 tiles à frente na mesma direção, vira saída (círculo preto). Os itens passam por baixo de uma cerca.
- [ ] Com 6 tiles de vão, não pareia.
- [ ] Salvar e carregar mantém: pontes, reguladores e suas regras, quantidades configuradas, itens dentro do túnel.
- [ ] Nenhum erro do mod no log do SMAPI.
