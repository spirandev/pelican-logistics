# Fase 6b: Túneis e Capacete de Engenheiro

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório, em especial D18 a D21), [`README.md`](../../README.md) (seções "Túnel e Esteira Subterrânea", "Capacete de Engenheiro", "Dicas ao passar o mouse"), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-6b-tuneis`
- **PR:** `Fase 6b: Túneis e Capacete de Engenheiro`
- **Depende de:** Fase 6 mesclada.

## Objetivo

Substituir a Esteira Subterrânea de vão fixo (Fase 6) por uma infraestrutura subterrânea de verdade, pedida pelo usuário depois da Fase 6:

| Item | Tipo | ID |
|---|---|---|
| Túnel | Bloco (`Data/BigCraftables`) | `Tunnel` |
| Esteira Subterrânea | Objeto não colocável pelo jogo (`Data/Objects`) | `UndergroundBelt` |
| Capacete de Engenheiro | Chapéu (`Data/Hats`) | `EngineerHelmet` |

```
[Túnel: entrada] → (subterrânea) (subterrânea) (subterrânea) → [Túnel: saída] → esteira
                     plantação     caminho        cerca
```

- O **Túnel** é um bloco na superfície: entrada (leva os itens para baixo) ou saída (traz de volta).
- A **Esteira Subterrânea** gasta 1 item por tile e fica numa **camada subterrânea**: não é objeto do jogo, então o tile continua livre (plantar, caminho, cerca, construção). Faz curva, não tem comprimento máximo e anda a 2 tiles/s (tier 2).
- O **Capacete de Engenheiro** mostra o que é escondido (segmentos, itens neles, slots virtuais, descrições técnicas) e é exigido para construir embaixo da terra (D18).

O bloco `UndergroundBelt` da Fase 6 (Big Craftable, entrada/saída com vão de 5 tiles) **deixa de existir**, sem migração: a alpha ainda não foi distribuída, e saves de teste com ele terão itens de erro do jogo (decisão do usuário).

## Fora de escopo

- Dicas ao passar o mouse (Fase 7). Esta fase só deixa pronto o "capacete ativo" para a Fase 7 usar nos slots das máquinas e nos segmentos.
- Progressão, receitas e sprites finais do capacete.

---

## Parte A: Core

### A1. Remover a Esteira Subterrânea de vão fixo

- Remova `UndergroundBeltNode`, `UndergroundRole`, `UndergroundPairing` e seus testes, e `Balance.UndergroundMaxGap`.
- `NodeKind`: dê valores explícitos aos membros, remova `UndergroundBelt = 4` (o número 4 fica sem uso) e acrescente `Tunnel = 5` e `UndergroundSegment = 6`. Assim um estado salvo antigo com `Kind = 4` não é confundido com os nós novos: os itens dele viram órfãos (caem no chão).
- `Balance.UndergroundSpeed = BeltSpeedFast` (20, 2 tiles/s), com o comentário "tier 2: infraestrutura subterrânea".
- Ajuste a `Topology` e o `SplitterNode` (portas) para o `TunnelNode` no lugar do `UndergroundBeltNode`.

### A2. Duas camadas na grade

A `LogisticsGrid` passa a ter a camada da **superfície** (os nós atuais, um por tile) e a camada **subterrânea** (só `UndergroundSegmentNode`, no máximo um por tile). Um tile pode ter um nó em cada camada.

| Membro | Descrição |
|---|---|
| `Add(LogisticsNode)` | Superfície, como hoje. Lança exceção para `UndergroundSegmentNode`. |
| `AddUnderground(UndergroundSegmentNode)` | Camada subterrânea. Lança exceção se o tile já tem segmento. |
| `GetUnderground(TilePos)` | Segmento do tile, ou null. |
| `RemoveUnderground(TilePos)` | Devolve o `RemovalResult` (o item do segmento; nunca acessório). |
| `UndergroundNodes` | Todos os segmentos. |
| `CanPlaceSegment(TilePos)` | False se já há segmento ou se há um `TunnelNode` na superfície do tile (D21). Outros nós da superfície não impedem. As regras do mapa (água, paredes) ficam no adaptador. |
| `Nodes` / `Count` | Continuam só a superfície. Acrescente `TotalCount` (as duas camadas) e use-o onde hoje se testa "grade vazia" (`LogisticsWorld.Tick`, `RuntimeStateSerializer.Export`). |

`ProcessingOrder` inclui os nós das duas camadas. `TickContext` ganha `UndergroundAt(TilePos)`. Os segmentos **nunca** interagem com nós da superfície, exceto com o Túnel.

### A3. `TunnelNode` (superfície)

```csharp
public enum TunnelRole { Entrance, Exit }
```

| Membro | Descrição |
|---|---|
| `TunnelRole Role` | Definido na colocação (`modData`), fixo depois (D21) |
| `TransitSlot Slot` | O slot do próprio tile |
| `Direction InputSide` | Sempre a traseira |

- **Entrada:** recebe só por trás (sem curva), com o `Slot` vazio, e nunca junta pilhas. Avança com `UndergroundSpeed`; no fim, entrega ao segmento **do tile à frente** (camada subterrânea), pelo lado virado para o Túnel (`segment.Accept(Facing.Opposite(), ...)`). Sem segmento que aceite → o item para no fim (`Blocked`). `ProvidesTo` na superfície: nada.
- **Saída:** recebe só do segmento **do tile atrás** que aponta para ela (o segmento empurra para ela, ver A4). Não recebe de nenhum nó da superfície. Avança e entrega à frente na superfície como uma esteira (`ProvidesTo` = tile à frente; `GetOutgoingSlot` para Inseridor e Divisor).
- Com o Inseridor e o Divisor, vale o que valia para a entrada e a saída da Subterrânea da Fase 6: a entrada é destino do Inseridor e saída do Divisor; a saída é origem do Inseridor (por trás ou pela lateral) e entrada do Divisor; a saída é provedora para curvas de esteira.
- `Downstream`: entrada → o segmento à frente (se houver); saída → o nó da superfície à frente.

### A4. `UndergroundSegmentNode` (camada subterrânea)

Comporta-se como uma esteira (`BeltNode`), só que na camada subterrânea e com velocidade fixa `UndergroundSpeed`. Reaproveite a lógica do `BeltNode` (ex.: uma classe base comum, sem mudar o comportamento da esteira).

- Uma pilha por tile, sentido único, `InputSide` calculado pela `Topology`: reto se algo entrega por trás, curva se nada entrega por trás e exatamente uma lateral entrega. "Algo entrega" aqui = outro segmento cuja frente é este tile, ou a **entrada** de um Túnel no tile vizinho cuja frente é este tile.
- Junção de pilhas iguais quando bloqueado (D11), como a esteira.
- No fim do tile, entrega para a frente: o segmento à frente (pelo `InputSide` dele) ou, se na superfície do tile à frente há a **saída** de um Túnel com a traseira virada para este segmento, para o `Slot` dela (método interno da saída). Qualquer outra coisa à frente → para no fim.
- `Downstream`: o segmento à frente, ou a saída do Túnel à frente.
- Laço 100% cheio trava (D12).

### A5. `TunnelRules` (Simulation)

- `TunnelRole DecideRole(LogisticsGrid grid, TilePos pos, Direction facing)`: **saída** se o tile atrás (`pos.Offset(facing.Opposite())`) tem um segmento cuja frente é `pos` (`segment.Facing == facing`); senão **entrada**. Chamado pelo adaptador antes de criar o nó.
- `bool CanPlaceTunnel(LogisticsGrid grid, TilePos pos)`: false se o tile tem segmento (D21).

### A6. Persistência

- `NodeRuntimeState.Layer` (`int`, 0 = superfície, 1 = subterrânea, padrão 0). Estados antigos sem o campo são da superfície. `WorldRuntimeState.CurrentVersion` continua 1 (o formato é compatível).
- `Export`/`Import` tratam as duas camadas; um segmento salvo sem segmento no tile (ou de outro tipo) vira órfão, como hoje.
- Novo `Persistence/UndergroundLayoutSerializer`: converte a lista de segmentos de uma grade (`X`, `Y`, `Facing`) para JSON e de volta, para o adaptador guardar no `modData` do local (D19). Ler nunca lança exceção: JSON corrompido → lista vazia; entradas inválidas (direção desconhecida, posição repetida) são descartadas.

### A7. Placeholders e informações técnicas

- `PlaceholderPainter.PaintTunnel(TunnelRole)`: o desenho da antiga Subterrânea (cinza escuro, círculo branco/preto, seta).
- `PlaceholderPainter.PaintUndergroundSegment()` e a curva (`PaintUndergroundSegmentCurve(bool fromLeft)`): quadrado cinza escuro com seta, para desenhar semitransparente.
- `PlaceholderPainter.PaintEngineerHelmet()`: folha do chapéu no formato do jogo (20×80: quatro quadros de 20×20, de cima para baixo: baixo, direita, esquerda, cima — **confirme a ordem** em [Modding:Items](https://stardewvalleywiki.com/Modding:Items)). Capacete amarelo com uma lanterna branca na frente.
- `Rendering/TechnicalInfo`: números para as descrições técnicas (README "Capacete de Engenheiro"), calculados de `Balance`, sem textos: tiles por segundo de cada tier de esteira, do Filtro, do Divisor e do subterrâneo; operações por segundo e quantidade por operação de cada Inseridor; vão (sem limite) do subterrâneo. O adaptador formata com o i18n.

### A8. Testes do Core

**Camadas e segmentos**
1. Entrada → 3 segmentos retos → saída → esteira: o item chega, 30 ticks por tile em toda a travessia (Túnel e segmentos).
2. Corrente com curva: o item faz a curva embaixo da terra.
3. Uma esteira da superfície e um segmento no mesmo tile não interagem (cada um com o seu item, sem trocar).
4. Entrada sem segmento à frente: o item para no fim.
5. Corrente sem saída: as pilhas param no último segmento e se acumulam (pilhas iguais se juntam, D11).
6. Segmento apontando para a lateral da saída: não entrega. Segmento atrás da saída apontando para ela: entrega.
7. Corrente longa (30 segmentos) com 3 curvas: chega tudo, na ordem.
8. Girar um segmento recalcula a corrente no tick seguinte.
9. Duas correntes paralelas não interferem.

**Regras**
10. `DecideRole`: sem segmento atrás → entrada; segmento atrás apontando para o tile → saída; segmento atrás apontando para outro lado → entrada.
11. `CanPlaceSegment`: falso com segmento ou Túnel no tile; verdadeiro sob esteira, Filtro, Divisor e Inseridor. `CanPlaceTunnel`: falso com segmento.
12. Remover um segmento devolve o item dele; remover o Túnel devolve só o item do `Slot` (os segmentos ficam).
13. `Add` com segmento lança exceção; `AddUnderground` num tile com segmento lança exceção.

**Integração**
14. Inseridor entrega na entrada; saída alimenta um Inseridor pela lateral; saída é entrada do Divisor; entrada é saída do Divisor; saída faz curva numa esteira.
15. **Conservação** por 3000 ticks: baú → Inseridor → entrada → 20 segmentos com curvas → saída → Divisor → dois baús.

**Persistência**
16. Itens nos segmentos e no Túnel sobrevivem à ida e volta (camada certa).
17. Estado salvo sem `Layer` é da superfície; estado com `Kind = 4` (Subterrânea antiga) vira órfão.
18. `UndergroundLayoutSerializer`: ida e volta; JSON corrompido → lista vazia; direção inválida e posição repetida descartadas.

**Outros**
19. `Balance.UndergroundSpeed == Balance.BeltSpeedFast`; `TechnicalInfo` dá 1, 2 e 4 tiles/s para as esteiras e 2 para o subterrâneo.
20. Placeholders: Túnel (entrada/saída diferentes), segmento (reto e curva) e capacete (20×80, com pixels opacos nos quatro quadros).

---

## Parte B: Adaptador

### B1. Registro

- `ItemIds`: `Tunnel` (bloco direcional), `UndergroundBelt` passa a ser objeto (`(O)`), `EngineerHelmet` (`(H)`). Remova o `UndergroundBelt` de `Data/BigCraftables` e da folha de ícones dos blocos.
- `Data/Objects` para a Esteira Subterrânea, como os acessórios (`Type = "Basic"`, `Category = 0`, tag `not_placeable`).
- `Data/Hats` para o capacete, com textura gerada pelo `PaintEngineerHelmet`. **Confirme o formato** da entrada em [Modding:Items](https://stardewvalleywiki.com/Modding:Items) (na 1.6, texto separado por `/`: nome, descrição, mostrar cabelo, deslocamento do penteado, tags, nome exibido, índice do sprite, textura).
- Loja do Clint (0g) e `pl_give` com os três.
- `ModDataKeys`: `TunnelRole` (no bloco, apagado ao colocar e recalculado); `UndergroundSegments` (no `GameLocation.modData`, JSON do `UndergroundLayoutSerializer`).

### B2. Capacete (`Framework/World/EngineerHelmet.cs`)

- `IsActive`: capacete na cabeça (`Game1.player.hat.Value`) ou selecionado na barra (`Game1.player.CurrentItem`). Usado para **ver** (D18).
- `IsWorn`: só na cabeça. Usado para **construir**.

### B3. Esteiras Subterrâneas no mapa (`Framework/Input/UndergroundController.cs`)

`Input.ButtonPressed`, botão de ação, com o capacete na cabeça (D18, D20):
- **Segurando uma Esteira Subterrânea**, num tile sem segmento: se o tile é válido (dentro do mapa, não é água — `GameLocation.isWaterTile` —, sem tile na camada `Buildings` do mapa e `CanPlaceSegment`) → cria o segmento na direção de colocação (`PlacementController.CurrentDirection`), gasta 1, grava o layout no `modData` do local, suprime o botão. Tile inválido → `showRedMessage` ("Não dá para cavar aqui."), nada gasto.
- **Segurando uma Esteira Subterrânea**, sobre um segmento: retira (devolve o item, solta no chão o item que estava nele), grava o layout.
- **Mão livre** sobre um segmento: retira, mas só se o tile não tem objeto, móvel nem terrain feature além de caminho (`Flooring`) ou grama (`Grass`). Senão, não faz nada e deixa o jogo tratar o clique (colher, abrir o baú...).
- Sem o capacete na cabeça e segurando uma Esteira Subterrânea → mensagem ("Você precisa do Capacete de Engenheiro na cabeça."), suprime.
- Segurando uma Esteira Subterrânea, `R` gira a direção de colocação e a seta fantasma aparece (trate o item como bloco direcional no `PlacementController`).
- `WorldRegistry.Rebuild` recria os segmentos de cada local a partir do `modData` do local, antes do `SaveManager.Load`. Locais com segmentos entram em `locations` mesmo sem blocos.

### B4. Túnel

- Ao colocar (`ObjectListChanged.Added`): `TunnelRules.DecideRole`, grava `TunnelRole` no `modData`, cria o nó.
- **Colocar exige o capacete na cabeça e um tile sem segmento** (D18, D21): patch Harmony **postfix** em `Object.canBePlacedHere` para o Túnel → `false` se `!IsWorn` ou `!CanPlaceTunnel`, com mensagem quando o jogo pedir (`showError`). **Confirme a assinatura** no código descompilado e se é esse o método usado na colocação pelo jogador e na prévia vermelha; se não for, encontre o certo e registre no PR.
- Remova a lógica da Subterrânea da Fase 6 (papel, prévia, linha tracejada).

### B5. Desenho

- O Túnel usa `PaintTunnel` (círculo branco/preto). A prévia de colocação mostra o papel (`DecideRole`) **só com o capacete ativo**; sem ele, mostra a entrada.
- **Com o capacete ativo**, em `Display.RenderedWorld`, para o local atual: cada segmento semitransparente (reto ou curva, girado) e o item dentro dele, no caminho da entrada à saída (`PathMath`). Pode aparecer por cima de objetos e plantas ("visão de raio X"); registre no PR como ficou.
- Sem o capacete, nada da camada subterrânea é desenhado.

### B6. Descrições técnicas

Com o capacete ativo, a descrição dos itens do mod (dica do inventário, da loja) ganha linhas técnicas do i18n com os números do `TechnicalInfo`. Exemplos: "Velocidade: 2 tiles/s" (esteiras, Filtros, Divisor, Túnel e Esteira Subterrânea); "1 operação a cada 0,5 s · 1 item por vez" / "a pilha inteira" / "a quantidade configurada (1 a 999)" (Inseridores); "Limite: 0 a 99.999 por baú" (Regulador). Patch **postfix** em `Object.getDescription()` (ou o método certo para Big Craftables; **confirme** e verifique se a descrição fica em cache, o que impediria a troca ao pôr ou tirar o capacete).

### B7. Console e i18n

- `pl_dump`: lista também os segmentos do local (posição, direção, lado de entrada, item), marcados como subterrâneos, e o papel do Túnel. Os comandos mostram tudo sem o capacete (D18).
- i18n: nomes e descrições dos três itens, as mensagens de erro, as linhas técnicas. Remova os textos da Subterrânea antiga.

### B8. Documentação

`docs/DEVELOPMENT.md` (seção 9: Túnel, Esteira Subterrânea, capacete; seção 10: o layout dos segmentos no `modData` do local), `CHANGELOG.md`, `STATUS.md`.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1–A2 + testes 3, 11, 13.
3. Core A3–A5 + testes 1, 2, 4–10, 12, 14, 15.
4. Core A6–A7 + testes 16–20.
5. Adaptador B1–B2.
6. Adaptador B3–B5.
7. Adaptador B6–B7.
8. Docs, changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Os testes desta fase passam, junto com os anteriores (os da Subterrânea de vão fixo foram removidos ou adaptados).
- [ ] Nenhuma Esteira Subterrânea vira objeto do jogo (o tile continua livre).
- [ ] PR com "APIs a confirmar" (no mínimo: `Data/Hats` e a ordem dos quadros, `Farmer.hat`, `GameLocation.modData`, `isWaterTile`, a camada `Buildings` do mapa, `Object.canBePlacedHere`, `Object.getDescription` e cache).

## Verificação local (usuário)

- [ ] O Clint vende o Túnel, a Esteira Subterrânea e o Capacete de Engenheiro; o capacete pode ser usado na cabeça.
- [ ] **Sem o capacete:** colocar um Túnel ou uma Esteira Subterrânea mostra a mensagem e nada é gasto.
- [ ] **Com o capacete na cabeça:** o primeiro Túnel vira entrada (círculo branco); as Esteiras Subterrâneas são colocadas uma por tile, giram com `R` e fazem curva; o Túnel no fim da corrente vira saída (círculo preto). Os itens chegam do outro lado.
- [ ] Uma corrente de 10+ tiles passando embaixo de terra arada com plantação, de um caminho e de uma cerca: dá para arar, plantar, regar e colher em cima, e os itens continuam passando.
- [ ] Com o capacete na cabeça **ou na mão**, os segmentos e os itens dentro deles aparecem semitransparentes; sem o capacete, nada aparece.
- [ ] Velocidade visivelmente igual à da Esteira Rápida (2 tiles/s).
- [ ] Retirar: segurando a Esteira Subterrânea, o botão de ação devolve o segmento; com a mão livre num tile vazio, também; com a mão livre num tile com plantação madura, o jogo colhe e o segmento fica.
- [ ] Retirar um segmento com item dentro: o item cai no chão.
- [ ] Água e penhascos/paredes do mapa: não dá para colocar (mensagem).
- [ ] Com o capacete, a descrição dos itens do mod no inventário mostra as linhas técnicas (velocidade, operação do Inseridor); sem ele, a descrição normal.
- [ ] Salvar e carregar mantém os segmentos, os itens dentro deles e os papéis dos Túneis.
- [ ] Nenhum erro do mod no log do SMAPI.
