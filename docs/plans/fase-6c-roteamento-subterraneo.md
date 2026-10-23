# Fase 6c: Roteamento subterrâneo

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório, em especial D18 a D23), [`README.md`](../../README.md) (seções "Divisor", "Filtro", "Túnel e Esteira Subterrânea", "Roteamento subterrâneo", "Capacete de Engenheiro"), [`STATUS.md`](STATUS.md), e o plano da [Fase 6b](fase-6b-tuneis.md) (como a camada subterrânea foi feita).

- **Branch:** `fase-6c-roteamento-subterraneo`
- **PR:** `Fase 6c: Roteamento subterrâneo`
- **Depende de:** Fase 6b mesclada.

## Objetivo

Levar o Divisor e os Filtros para baixo da terra, para montar redes e separadores subterrâneos (pedido do usuário depois do teste da Fase 6b):

| Item | Tipo | ID |
|---|---|---|
| Divisor Subterrâneo | Objeto não colocável pelo jogo (`Data/Objects`) | `UndergroundSplitter` |
| Filtro Comum Subterrâneo | Objeto não colocável pelo jogo | `UndergroundFilterCommon` |
| Filtro Completo Subterrâneo | Objeto não colocável pelo jogo | `UndergroundFilterComplete` |

```
                                   (Filtro Subterrâneo: Madeira) → … → [Túnel: saída] → baú de madeira
                                              ↑
[Túnel: entrada] → (subterrânea) → (Divisor Subterrâneo) → (subterrânea) → … → [Túnel: saída] → resto
                                              ↓
                                   (Filtro Subterrâneo: Pedra)   → … → [Túnel: saída] → baú de pedra
```

Decisões do usuário (plano mestre D22, D23):
1. Divisor **e** Filtros (Comum e Completo) embaixo da terra.
2. **Itens próprios**, objetos como a Esteira Subterrânea.
3. **Tudo que é subterrâneo segue a mesma lógica:** capacete na cabeça para colocar, retirar e configurar; um bloco subterrâneo por tile; não ocupa o tile da superfície; invisível sem o capacete; nada embaixo de um Túnel, na água ou em paredes do mapa.
4. **Conexões:** Esteiras, Divisores e Filtros Subterrâneos e Túneis (a entrada com a frente virada para o bloco entrega nele; a saída com a traseira virada para o bloco recebe dele). Inseridores não descem.
5. **Velocidade:** o Divisor e os Filtros continuam rápidos (4 tiles/s), como na superfície; a Esteira Subterrânea (2 tiles/s) é o limite.
6. **Configurar o Filtro Subterrâneo:** capacete na cabeça e mão livre, botão de ação no tile abre a interface; nesse tile a mão livre não retira.
7. **Retirar:** segurando qualquer item subterrâneo, o clique num bloco subterrâneo devolve o item daquele bloco; num tile vazio, coloca o item da mão.

## Fora de escopo

- Dicas ao passar o mouse (Fase 7).
- Inseridores, Ponte e Regulador embaixo da terra.

---

## Parte A: Core

### A1. Camada subterrânea com qualquer bloco subterrâneo

Hoje a camada subterrânea da `LogisticsGrid` guarda só `UndergroundSegmentNode`. Ela passa a guardar também `FilterNode` e `SplitterNode` (as mesmas classes da superfície, sem duplicar a lógica).

- Os membros da camada (`AddUnderground`, `GetUnderground`, `RemoveUnderground`, `UndergroundNodes`) passam a trabalhar com `LogisticsNode`. `AddUnderground` só aceita `UndergroundSegmentNode`, `FilterNode` e `SplitterNode`; `Add` (superfície) continua recusando `UndergroundSegmentNode`. `CanPlaceSegment` vira `CanPlaceUnderground` (mesma regra: sem bloco subterrâneo e sem Túnel na superfície).
- **Vizinhos pela camada (D22).** Hoje o Filtro e o Divisor procuram os vizinhos com `ctx.NodeAt` (superfície). Embaixo da terra, os vizinhos de um bloco são **só os da camada subterrânea**, mais os Túneis:
  - a **entrada** de um Túnel entrega no bloco subterrâneo do tile à frente dela;
  - a **saída** de um Túnel recebe do bloco subterrâneo do tile atrás dela, se ele entrega naquele tile (esteira ou filtro apontando para ela, ou Divisor com a saída para ela).

  Sugestão: dar ao `LogisticsNode` a camada em que ele está (`NodeLayer Layer`, definida pela grade ao adicionar) e centralizar a busca de vizinhos no `TickContext` (ex.: `NeighborAt(LogisticsNode from, Direction side)`), com as regras do Túnel num só lugar. O `ctx.PushForward`, o `Pull` e as saídas do Divisor, a entrega da Esteira Subterrânea e a do Filtro passam a usar essa busca. Escolha a forma mais simples que cumpra as regras e registre no PR.
- **Nenhum vazamento entre camadas.** Nós da superfície nunca empurram, puxam nem enxergam blocos subterrâneos, e vice-versa, exceto pelos Túneis. Cuidado com o `GetOutgoingSlot`: o Divisor Subterrâneo vai **puxar** da Esteira Subterrânea, do Filtro Subterrâneo e da entrada do Túnel, mas um Inseridor da superfície ao lado não pode puxar deles. A entrada do Túnel continua entregando direto quando o vizinho subterrâneo aceita empurrado (Esteira e Filtro); para o Divisor, que puxa, ela precisa oferecer o slot só ao Divisor subterrâneo à frente.

### A2. Filtro Subterrâneo

`FilterNode` na camada subterrânea, com o mesmo comportamento (sempre reto, rápido, só entra o que a regra aceita, uma pilha, nunca junta). Recebe por trás de uma Esteira, Filtro ou Divisor Subterrâneo, ou da entrada de um Túnel à frente; entrega à frente: Esteira, Filtro ou Divisor Subterrâneo, ou a saída de um Túnel com a traseira virada para ele. O filtro acoplado ao Inseridor não existe embaixo da terra.

### A3. Divisor Subterrâneo

`SplitterNode` na camada subterrânea, com as mesmas regras (README "Divisor"). Portas calculadas pela `Topology` com os vizinhos subterrâneos:
- **entrada:** Esteira ou Filtro Subterrâneo cuja frente é o Divisor, ou a entrada de um Túnel cuja frente é o Divisor;
- **saída:** Esteira Subterrânea que recebe pelo lado virado para ele (com o Divisor atrás, ou sem nada atrás e o Divisor na lateral, fazendo curva), Filtro Subterrâneo com a traseira virada para ele, ou a saída de um Túnel com a traseira virada para ele;
- Divisor encostado em Divisor não se conecta (como na superfície). A prioridade para filtros vale para os Filtros Subterrâneos nas saídas.

### A4. Topologia e persistência

- A `Topology` calcula o lado de entrada das Esteiras Subterrâneas (agora o Divisor Subterrâneo também pode fazê-las virar curva), as portas dos Divisores das duas camadas e a ordem de processamento das duas camadas.
- O estado salvo já tem `Layer` (Fase 6b): Filtros e Divisores Subterrâneos são exportados e importados na camada 1, com os ponteiros do Divisor.
- `UndergroundLayoutSerializer`: cada entrada ganha `Kind` (`UndergroundSegment`, `Filter`, `Splitter`) e, no filtro, a regra (o JSON do `FilterRuleSerializer`) e se é Comum ou Completo. Entradas antigas sem `Kind` são Esteiras Subterrâneas. Entrada inválida (tipo desconhecido, regra corrompida → regra vazia, como na superfície) não derruba as outras.

### A5. Testes do Core

**Camadas**
1. `AddUnderground` aceita Esteira, Filtro e Divisor; recusa Inseridor, Esteira da superfície e Túnel. `CanPlaceUnderground`: falso com bloco subterrâneo ou Túnel em cima.
2. Um Divisor da superfície e um Divisor Subterrâneo no mesmo tile não interagem; um Inseridor da superfície ao lado de uma Esteira ou Filtro Subterrâneo não puxa dela.
3. Uma esteira da superfície apontando para um tile com Filtro Subterrâneo não entrega nele.

**Filtro Subterrâneo**
4. Entrada → Esteira Subterrânea → Filtro Subterrâneo (madeira) → Esteira Subterrânea → saída: madeira passa; pedra fica parada antes do filtro.
5. Entrada do Túnel com a frente para um Filtro Subterrâneo entrega nele; Filtro Subterrâneo entrega na saída de um Túnel à frente.

**Divisor Subterrâneo**
6. Separador clássico embaixo da terra: entrada → Divisor Subterrâneo com saídas Filtro (madeira), Filtro (pedra) e Esteira (resto), cada uma até uma saída de Túnel e um baú: cada item no baú certo.
7. Divisão igual entre duas Esteiras Subterrâneas de saída (7 → 4 + 3); excedente para as saídas sem filtro quando o filtro está ocupado.
8. Portas: entrada de Túnel com a frente para o Divisor é entrada; saída de Túnel com a traseira para ele é saída; Esteira Subterrânea sem nada atrás e com o Divisor na lateral vira curva saindo dele; Divisor Subterrâneo encostado em outro não se conecta.
9. Entrada do Túnel com a frente para o Divisor: o Divisor puxa o item dela.

**Persistência e conservação**
10. Itens no Filtro e no Divisor Subterrâneos (e os ponteiros do Divisor) sobrevivem à ida e volta, na camada certa.
11. `UndergroundLayoutSerializer`: ida e volta com os três tipos e as regras dos filtros; entrada antiga sem `Kind` → Esteira Subterrânea; tipo desconhecido descartado; regra corrompida → regra vazia.
12. **Conservação** por 3000 ticks num separador subterrâneo com 3 saídas, 4 tipos de item e Túneis nas pontas.

---

## Parte B: Adaptador

### B1. Registro

- `ItemIds`: `UndergroundSplitter`, `UndergroundFilterCommon`, `UndergroundFilterComplete` em `AllObjects` (`Data/Objects`, como a Esteira Subterrânea: `Type = "Basic"`, `Category = 0`, tag `not_placeable`). Um ajudante "é item subterrâneo" (os quatro) e "tipo do bloco subterrâneo de um item".
- Ícones na folha dos objetos: os placeholders do Divisor e dos Filtros com o fundo cinza escuro do subterrâneo (ou um contorno que os diferencie dos da superfície). Acrescente os índices no fim da folha.
- Loja do Clint (0g) e `pl_give`.
- Layout no `modData` do local (A4): grave e leia os três tipos e as regras.

### B2. Colocar, retirar e configurar (`UndergroundController`)

Generalize o que existe para a Esteira Subterrânea (D22, D23):
- **Segurando qualquer item subterrâneo** (botão de ação ou de usar ferramenta), com o capacete na cabeça: sobre um bloco subterrâneo → retira e devolve **o item daquele bloco** (e solta no chão o item que estava nele); num tile vazio e válido → coloca o item da mão (Filtro na direção de colocação; Divisor sem direção), com a regra vazia no filtro.
- **Mão livre** (botão de ação), capacete na cabeça, nada por cima que responda ao clique (D20): Filtro Subterrâneo → abre a interface do Filtro Comum ou Completo (as mesmas da superfície), gravando a regra no nó e no layout do local; Esteira ou Divisor Subterrâneo → retira.
- `R`: segurando a Esteira ou o Filtro Subterrâneo, gira a direção de colocação (seta fantasma); o Divisor não tem direção. Com o capacete na cabeça e a mão livre, `R` gira a Esteira ou o Filtro Subterrâneo sob o cursor (o Divisor, não).

### B3. Desenho

Com o capacete ativo, o `UndergroundRenderer` desenha também os Filtros (com o ícone do item no Filtro Comum) e os Divisores Subterrâneos, semitransparentes, e os itens dentro deles (no Divisor, `PathMath.GetHubPosition`).

### B4. Descrições, console e i18n

- Descrições técnicas (com o capacete): as mesmas linhas do Divisor e dos Filtros da superfície (velocidade 4 tiles/s, uma pilha por vez).
- `pl_dump`/`pl_inspect`: os novos blocos subterrâneos, com regra do filtro e portas do Divisor.
- i18n: nomes e descrições dos três itens.

### B5. Documentação

`docs/DEVELOPMENT.md` (seção 9 e o layout no `modData` do local), `CHANGELOG.md`, `STATUS.md`.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1 + testes 1–3.
3. Core A2–A3 + testes 4–9.
4. Core A4 + testes 10–12.
5. Adaptador B1–B2.
6. Adaptador B3–B4.
7. Docs, changelog, status, PR.

## Critérios de aceite

- [ ] Os testes desta fase passam, junto com os anteriores.
- [ ] Nenhuma interação entre as camadas fora dos Túneis (testes 2 e 3).
- [ ] O adaptador compila contra o jogo, se ele estiver instalado na máquina da sessão; senão, PR com "APIs a confirmar".

## Verificação local (usuário)

- [ ] O Clint vende o Divisor Subterrâneo, o Filtro Comum Subterrâneo e o Filtro Completo Subterrâneo.
- [ ] Sem o capacete na cabeça, não dá para colocar nenhum deles (mensagem, nada gasto).
- [ ] **Separador subterrâneo:** entrada → Esteiras Subterrâneas → Divisor Subterrâneo com saídas Filtro Subterrâneo (madeira), Filtro Subterrâneo (pedra) e Esteira Subterrânea (resto), cada uma terminando num Túnel de saída e num baú: cada item vai para o baú certo, tudo embaixo de uma plantação.
- [ ] Com o capacete na cabeça e a mão livre, clicar no tile de um Filtro Subterrâneo abre a interface; a regra escolhida vale na hora e o ícone aparece no filtro (com o capacete).
- [ ] Segurando uma Esteira Subterrânea, clicar num Divisor Subterrâneo devolve um **Divisor Subterrâneo** (o item daquele bloco).
- [ ] Com o capacete (na cabeça ou na mão), os blocos e os itens neles aparecem; sem ele, nada aparece.
- [ ] Salvar e carregar mantém os blocos, as regras dos filtros e os itens dentro deles.
- [ ] Nenhum erro do mod no log do SMAPI.
