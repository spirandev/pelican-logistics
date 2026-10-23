# Plano Mestre: Pelican Logistics (Alpha)

Este documento é o ponto de partida para **qualquer sessão de implementação**. Leia este arquivo inteiro antes de abrir o plano de uma fase.

- **Documento de design (fonte da verdade do comportamento):** [`README.md`](../../README.md)
- **Status das fases e feedback dos testes locais:** [`STATUS.md`](STATUS.md)
- **Planos das fases:** os arquivos `fase-N-*.md` desta pasta

---

## 1. Objetivo da alpha

Construir uma versão **jogável apenas para testar a lógica** do mod: esteiras, inseridores, máquinas, filtros, divisor, ponte, regulador e esteira subterrânea, conforme o README. Na alpha:

- não há sprites: tudo é desenhado com **formas geométricas e cores** (placeholders);
- tudo fica **liberado e de graça** na loja do Clint;
- só existe o idioma **pt-BR**, mas todos os textos ficam em `i18n`;
- o modo é **single-player**.

---

## 2. Fases

Cada fase é executada **do início ao fim em uma única sessão**, numa branch própria, e termina com um Pull Request. A fase seguinte só começa depois que o PR anterior for revisado, testado localmente e mesclado na `main`.

| Fase | Arquivo | Entrega | Depende de |
|---|---|---|---|
| 0 | [`fase-0-estrutura.md`](fase-0-estrutura.md) | Solução, projetos, manifest, testes, CI, documentação de desenvolvimento | (nada) |
| 1 | [`fase-1-base.md`](fase-1-base.md) | Esteira (reta/curva), Extrator e Inseridor básicos, baú → baú, renderização, rotação, loja | 0 |
| 2 | [`fase-2-maquinas.md`](fase-2-maquinas.md) | Slots virtuais, Inseridor/Extrator com máquinas, interfaces de slot, Cristalário | 1 |
| 3 | [`fase-3-inseridor-unico.md`](fase-3-inseridor-unico.md) | Extrator e Inseridor viram um bloco só (Inseridor): entradas laterais, entrega direta, item na mão e braço | 2 |
| 4 | [`fase-4-persistencia.md`](fase-4-persistencia.md) | Itens em trânsito, itens na mão e slots virtuais sobrevivem ao salvar/carregar | 3 |
| 5 | [`fase-5-roteamento.md`](fase-5-roteamento.md) | Filtro Comum, Filtro Completo, filtro acoplado, Divisor | 4 |
| 6 | [`fase-6-itens-restantes.md`](fase-6-itens-restantes.md) | Ponte, Esteiras Rápida/Expressa, Inseridores de Pilha/Configurável, Esteira Subterrânea, Regulador | 5 |
| 6b | [`fase-6b-tuneis.md`](fase-6b-tuneis.md) | Túnel (entrada/saída) + Esteiras Subterrâneas por tile, embaixo da terra; Capacete de Engenheiro | 6 |
| 7 | [`fase-7-dicas-e-alpha.md`](fase-7-dicas-e-alpha.md) | Dicas ao passar o mouse, revisão das interfaces, empacotamento da alpha 0.1.0 | 6b |

As fases 0 a 2 foram feitas com dois blocos separados (Extrator e Inseridor); seus planos ficam como registro histórico. A partir da Fase 3 existe só o Inseridor (README "Inseridores"). A Esteira Subterrânea de vão fixo da Fase 6 é substituída na Fase 6b pelo Túnel com Esteiras Subterrâneas por tile (README "Túnel e Esteira Subterrânea").

---

## 3. Limitação importante do ambiente em nuvem

O ambiente em nuvem **não tem o Stardew Valley instalado**. As DLLs do jogo e do SMAPI são necessárias para compilar o projeto do mod (`src/PelicanLogistics`), e elas **não podem** ser baixadas nem adicionadas ao repositório (são software proprietário).

Consequências e regras:

1. **Toda a lógica fica em `PelicanLogistics.Core`**, uma biblioteca C# pura, **sem nenhuma referência ao jogo**. Ela compila e é testada na nuvem com `dotnet test`. Quanto mais lógica estiver no Core, mais coisa é verificada automaticamente.
2. O projeto do mod (`src/PelicanLogistics`) é uma **camada fina de adaptação**: converte objetos do jogo para as abstrações do Core, desenha, trata entrada do jogador e salva. Esse código **é escrito na nuvem sem poder ser compilado**.
3. Por isso, ao escrever código do adaptador:
   - use apenas APIs que você consegue confirmar na documentação: [Wiki de Modding](https://stardewvalleywiki.com/Modding:Index), [Migração para 1.6](https://stardewvalleywiki.com/Modding:Migrate_to_Stardew_Valley_1.6), [Modding:Items](https://stardewvalleywiki.com/Modding:Items), [Modding:Machines](https://stardewvalleywiki.com/Modding:Machines), [Modding:Shops](https://stardewvalleywiki.com/Modding:Shops), [documentação do SMAPI](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs), e o código aberto de mods como o [Automate](https://github.com/Pathoschild/StardewMods/tree/develop/Automate);
   - mantenha cada arquivo do adaptador pequeno e focado;
   - **liste no PR, na seção "APIs a confirmar", toda chamada ao jogo da qual você não tem certeza** (assinatura, nome, comportamento);
   - **nunca** adicione DLLs do jogo ao repositório e **nunca** crie "stubs" falsos das classes do jogo para forçar a compilação.
4. O usuário compila e testa localmente. Erros de compilação e problemas encontrados no jogo são registrados em [`STATUS.md`](STATUS.md), na seção **"Pendências para a próxima fase"**. **A primeira tarefa de toda fase é resolver essas pendências.**

---

## 4. Arquitetura

### 4.1 Estrutura do repositório

```
pelican-logistics/
├── PelicanLogistics.sln
├── README.md                          # design (fonte da verdade)
├── CHANGELOG.md
├── .editorconfig
├── .gitignore
├── .github/workflows/ci.yml           # compila e testa SOMENTE o Core
├── docs/
│   ├── DEVELOPMENT.md                 # como compilar e testar localmente
│   └── plans/                         # este plano e os das fases
├── src/
│   ├── PelicanLogistics.Core/         # lógica pura, net6.0, sem dependência do jogo
│   └── PelicanLogistics/              # mod SMAPI (adaptador), net6.0
│       ├── manifest.json
│       ├── i18n/default.json          # textos em pt-BR
│       └── ...
└── tests/
    └── PelicanLogistics.Core.Tests/   # xUnit
```

### 4.2 Core (`PelicanLogistics.Core`)

Namespaces e responsabilidades (criados aos poucos pelas fases):

| Namespace | Conteúdo |
|---|---|
| `Geometry` | `Direction` (North, East, South, West, nessa ordem = **ordem horária**), `TilePos`, extensões (`Opposite`, `Clockwise`, `ToOffset`) |
| `Items` | `ILogisticsItem` (abstração de um item do jogo) |
| `Endpoints` | `IChestEndpoint`, `IMachineEndpoint`, `IEndpointProvider` (o que está ao redor das esteiras) |
| `Nodes` | `LogisticsNode` (base), `BeltNode`, `InserterNode`, `FilterNode`, `SplitterNode`, `TunnelNode`, `UndergroundSegmentNode` (Fase 6b; antes `UndergroundBeltNode`) |
| `Simulation` | `LogisticsGrid` (um por local do jogo, com a camada da superfície e a subterrânea), `LogisticsWorld` (todos os locais), `Topology` (formas, conexões e ordem de processamento), `Balance` (constantes) |
| `Filters` | `IFilterRule`, `CommonFilterRule`, `CompleteFilterRule` |
| `Regulators` | `RegulatorMode`, `RegulatorRule`, `RegulatorRuleSerializer` (Fase 6) |
| `Machines` | `VirtualSlots`, requisitos de máquina |
| `Persistence` | DTOs de estado em tempo de execução (snapshot) |
| `Rendering` | Matemática de posição do item no tile (`PathMath`) e pintura dos placeholders em buffers de pixels (`PlaceholderPainter`) |

Regras do Core:
- **Nenhuma** referência a `StardewValley`, `StardewModdingAPI`, `Microsoft.Xna.Framework`, `Netcode` ou `xTile`.
- Determinístico: sem `DateTime.Now`, sem `Random` sem semente. Tudo avança por **ticks**.
- Matemática inteira para o progresso dos itens (ver `Balance`).
- Todo comportamento descrito no README tem **teste automatizado** no Core.

### 4.3 Adaptador (`PelicanLogistics`)

| Pasta | Conteúdo |
|---|---|
| `ModEntry.cs` | Ponto de entrada do SMAPI: registra eventos, Harmony, comandos de console |
| `ModConfig.cs` | Configuração (`config.json`) |
| `Framework/Content/` | Registro dos itens (`Data/BigCraftables`, `Data/Objects`), loja do Clint (`Data/Shops`), texturas placeholder geradas em tempo de execução |
| `Framework/World/` | `WorldRegistry` (sincroniza o Core com os objetos do jogo), `GameItem` (implementa `ILogisticsItem`), `ChestEndpoint`, `MachineEndpoint`, `GameEndpointProvider` |
| `Framework/Rendering/` | Desenho dos blocos e dos itens em trânsito |
| `Framework/Patches/` | Patches Harmony (desenho dos objetos, passagem pela Ponte) |
| `Framework/Input/` | Rotação, aplicação de acessórios, abertura de interfaces |
| `Framework/Menus/` | Interfaces de configuração |
| `Framework/Persistence/` | Salvar e carregar o estado em tempo de execução |

### 4.4 Como os blocos existem no jogo

- Todo **bloco** (esteiras, inseridores, filtros, divisor, esteira subterrânea) é um **Big Craftable** registrado em `Data/BigCraftables`, com ID qualificado `(BC)spirandev.PelicanLogistics_<Nome>`. Isso dá de graça: colocação no mapa, colisão (o jogador não passa), remoção com ferramenta, salvamento do objeto e do seu `modData`.
- Os **acessórios** (Ponte e Regulador) são **objetos comuns** registrados em `Data/Objects` (`(O)spirandev.PelicanLogistics_<Nome>`), **não colocáveis** pelo jogo. O mod intercepta o clique e aplica o acessório sobre o bloco.
- A **Esteira Subterrânea** (Fase 6b) também é um objeto comum não colocável: o mod intercepta o clique e cria um segmento na camada subterrânea, sem objeto no mapa (D19). O **Capacete de Engenheiro** é um chapéu (`Data/Hats`, `(H)spirandev.PelicanLogistics_EngineerHelmet`).
- A **configuração** de cada bloco (direção, filtro, slot, quantidade, acessório) fica no `modData` do próprio objeto, com chaves prefixadas por `spirandev.PelicanLogistics/`. É salva automaticamente pelo jogo.
- O **estado em tempo de execução** (itens em trânsito, itens na mão dos Inseridores, slots virtuais) é salvo pela Fase 4.
- Os blocos **não são desenhados pelo jogo**: um patch Harmony em `Object.draw` desenha o placeholder (girado conforme a direção), o braço do Inseridor e o item em trânsito.

### 4.5 Simulação

- A cada tick do jogo (`GameLoop.UpdateTicked`, 60 por segundo), se `Context.IsWorldReady` e o tempo do jogo estiver passando (`Game1.shouldTimePass()`), o mod chama `LogisticsWorld.Tick()`.
- A lógica roda em **todos os locais** que têm blocos; o desenho só acontece no local atual (decisão do README).
- Jogo pausado ⇒ sem tick ⇒ esteiras param. À noite não há ticks.
- Cada `LogisticsGrid` processa os nós em **ordem "de jusante para montante"** (quem recebe é processado antes de quem entrega), calculada pela `Topology` sempre que algo é colocado, removido ou girado. Assim uma fila compactada de itens anda junta no mesmo tick.

### 4.6 Constantes de balanceamento (`Balance`)

| Constante | Valor | Significado |
|---|---|---|
| `TicksPerSecond` | 60 | Ticks do jogo por segundo |
| `ProgressPerTile` | 600 | Unidades de progresso para atravessar um tile |
| `BeltSpeedBasic` | 10 | 1 tile/s |
| `BeltSpeedFast` | 20 | 2 tiles/s |
| `BeltSpeedExpress` | 40 | 4 tiles/s |
| `FilterSpeed` / `SplitterSpeed` | 40 | Velocidade máxima |
| `UndergroundSpeed` | 20 | Túnel e Esteira Subterrânea: velocidade do tier 2 (Fase 6b; era 40) |
| `InserterSpeed` | 40 | Progresso da mão do Inseridor por tick: a ida da origem ao destino leva 15 ticks |
| `InserterReturnTicks` | 15 | Ticks da volta do braço, vazio. Ida + volta = 30 ticks = 1 operação a cada 0,5 s (todas as versões) |
| `MaxConfigurableAmount` | 999 | Limite das versões Configuráveis |

Os valores são da alpha e podem mudar. Nunca use números mágicos fora de `Balance`.

---

## 5. Decisões de implementação

O README define o comportamento. Algumas situações não estão cobertas por ele, e foram decididas aqui para a alpha. Se uma fase precisar de outra decisão desse tipo, **não invente em silêncio**: escolha a opção mais simples e coerente com o README, implemente, e registre na seção "Decisões tomadas" do PR e em [`STATUS.md`](STATUS.md).

| # | Situação | Decisão |
|---|---|---|
| D1 | Rotação | Tecla configurável (padrão `R`). Segurando um bloco do mod: gira a direção de colocação. Com a mão livre e o cursor sobre um bloco direcional: gira o bloco. Ao segurar um bloco, uma seta fantasma mostra a direção no tile do cursor. |
| D2 | Direção dos blocos | Esteira, Filtro, Subterrânea: a direção é o sentido do fluxo. **Inseridor**: a direção aponta para o **destino** (frente). Baú ou máquina de origem só **atrás**; esteiras (e Filtro, saída de Subterrânea) que terminam no Inseridor podem chegar por trás ou pelas laterais. |
| D3 | Destino do Inseridor | Baú, máquina, Divisor, ou Esteira/Filtro/entrada de Subterrânea que recebe pelo lado virado para o Inseridor. Inseridor à frente não é destino. (Divisor como destino e origem: pedido do usuário no teste da Fase 5.) |
| D4 | Portas do Divisor | Entradas e saídas do Divisor podem ser Esteira, Filtro, Esteira Subterrânea ou Inseridor (entrada: apontando para o Divisor; saída: com a traseira virada para ele). O Divisor puxa da mão do Inseridor de entrada e entrega na mão do de saída só o que ele pegaria agora. Divisor encostado em Divisor, ou na lateral de um Inseridor, não se conecta. |
| D5 | Divisor puxa | O Divisor **puxa** os itens das entradas (em ordem horária), em vez de as entradas empurrarem. Isso garante o rodízio justo das entradas. |
| D6 | Filtro Comum e qualidade | O Filtro Comum compara apenas o ID do item; **qualquer qualidade** passa. Qualidade é critério do Filtro Completo. |
| D7 | Regulador por item | Itens sem limite definido na lista **não são limitados**. |
| D8 | Regulador em máquinas | O Regulador só tem efeito quando o destino é um baú. |
| D9 | Remover acessórios | Ponte: clicar (botão de ação) com a mão livre na esteira com ponte devolve a ponte ao inventário. Regulador: botão "Remover regulador" na interface do Inseridor. Ao remover o bloco, o acessório cai no chão. |
| D10 | Baús suportados | Baús de jogador comuns e grandes (inclui baú de pedra). Baús especiais (Junimo, auto-alimentador, mini caixa de envio, geladeira) ficam fora da alpha. |
| D11 | Junção de pilhas na esteira | Quando a pilha da frente está **bloqueada** e a de trás chega ao fim do seu tile com um item empilhável, ela se junta à da frente (até o limite da pilha). Visualmente a pilha de trás "pula" para a da frente; refinar a animação é polimento. |
| D12 | Laço de esteiras 100% cheio | Trava. Laços parcialmente cheios funcionam. |
| D13 | Interface do Inseridor + Regulador | O Regulador é configurado dentro da interface do Inseridor, numa seção própria que só aparece quando há regulador. |
| D14 | Textos | Identificadores em inglês; comentários e documentação em pt-BR; textos do jogo em `i18n/default.json` (pt-BR). |
| D15 | Loja | O Clint (loja `Blacksmith`) vende todos os itens do mod por 0g, estoque ilimitado, quando `UnlockAll` = `true` (padrão na alpha). Há também o comando de console `pl_give` que coloca um de cada no inventário. |
| D16 | Inseridor puxa | O Inseridor **puxa** das entradas (como o Divisor, D5): a esteira que termina nele fica parada no fim (`Blocked`) até ele pegar. Várias entradas → rodízio em ordem horária a partir da última usada. |
| D17 | Inseridor sem reserva | O Inseridor calcula na hora de pegar quanto cabe no destino, sem reservar espaço. Se o destino encher durante a ida (ex.: outro Inseridor abasteceu o mesmo slot), o item espera na mão até caber. |
| D18 | Capacete de Engenheiro | Chapéu do mod. **Ver** (Esteiras Subterrâneas e itens nelas, papel do Túnel antes de colocar, slots virtuais das máquinas, descrições técnicas dos itens do mod): com o capacete **na cabeça ou na mão**. **Construir embaixo da terra** (colocar e retirar Esteiras Subterrâneas, colocar Túneis): só **na cabeça**, porque a mão segura o item. Os comandos de console mostram tudo sem o capacete. O que já está visível na superfície continua visível sem ele. |
| D19 | Camada subterrânea | As Esteiras Subterrâneas **não são objetos do jogo**: o tile continua livre (plantar, caminho, cerca, construção). Ficam numa segunda camada da `LogisticsGrid` e, no adaptador, no `modData` do local do jogo (`GameLocation.modData`). Só o Túnel liga as duas camadas. |
| D20 | Retirar Esteira Subterrânea | Com o capacete na cabeça: segurando uma Esteira Subterrânea, o botão de ação ou o de usar ferramenta sobre um segmento o devolve (num tile sem segmento, coloca). Com a mão livre, o botão de ação também devolve, mas só se no tile não houver objeto, móvel nem terrain feature com interação (só caminho ou grama são permitidos); assim nunca rouba o clique de uma colheita ou de um baú. |
| D21 | Túnel | Papel fixo ao colocar: **saída** se a Esteira Subterrânea atrás dele aponta para ele; senão **entrada**. Não pode haver Esteira Subterrânea embaixo de um Túnel (nem Túnel sobre uma Esteira Subterrânea). |

---

## 6. Convenções

- **C#:** `LangVersion` 10, `Nullable` habilitado, namespaces por arquivo (`namespace X;`), uma classe pública por arquivo.
- **Nomes:** IDs de item `spirandev.PelicanLogistics_<PascalCase>`; chaves de `modData` `spirandev.PelicanLogistics/<PascalCase>`; comandos de console com prefixo `pl_`.
- **Testes:** xUnit, um arquivo de teste por classe do Core, nomes descritivos em inglês (`Belt_StopsAtEnd_WhenNoDownstream`). Use **fakes** de `ILogisticsItem` e dos endpoints, criados na Fase 1 em `tests/.../Fakes/`.
- **Commits:** em pt-BR, no formato `tipo(escopo): descrição` (ex.: `feat(core): divisão do Divisor por saídas livres`). Commits pequenos e frequentes.
- **Branch:** `fase-N-<nome>` (ex.: `fase-1-base`), criada a partir da `main` atualizada.
- **PR:** título `Fase N: <nome>`. Corpo com as seções: Resumo, O que foi feito, Decisões tomadas, APIs a confirmar, Como testar localmente, Pendências conhecidas.
- **Nunca** mesclar o PR, nunca fazer push na `main`, nunca reescrever histórico já publicado.

---

## 7. Regras para o agente executor

1. Leia, nesta ordem: este plano mestre, o [`README.md`](../../README.md), o [`STATUS.md`](STATUS.md) e o plano da fase.
2. Resolva primeiro as **pendências** registradas no `STATUS.md` para a sua fase.
3. Siga o plano da fase **na ordem** dos passos. Não implemente nada de fases futuras.
4. Se o plano e o README divergirem, **o README vence**. Registre a divergência no PR.
5. Antes de terminar:
   - `dotnet build src/PelicanLogistics.Core` sem erros e sem avisos novos;
   - `dotnet test tests/PelicanLogistics.Core.Tests` com todos os testes passando;
   - **não** tente compilar `src/PelicanLogistics` nem a solução inteira (falha sem o jogo; isso é esperado);
   - releia todo arquivo do adaptador que você criou ou alterou, procurando erros de digitação, `using` faltando e tipos errados, já que ele não é compilado;
   - atualize `CHANGELOG.md` (seção `Unreleased`) e a linha da fase no `STATUS.md` (status "Aguardando teste local", branch e link do PR);
   - preencha a seção "Verificação local" do PR copiando o checklist do plano da fase.
6. Abra o PR e termine. Não espere revisão.

### Prompt sugerido para iniciar uma fase

```
Leia docs/plans/00-plano-mestre.md e siga as regras dele.
Execute a fase descrita em docs/plans/fase-N-<nome>.md do início ao fim,
em uma branch fase-N-<nome>, e abra um Pull Request ao terminar.
```

---

## 8. Definição de pronto (para toda fase)

- [ ] Pendências do `STATUS.md` para esta fase resolvidas.
- [ ] Todos os passos do plano da fase concluídos.
- [ ] Core compila sem avisos novos; todos os testes passam.
- [ ] Todo comportamento novo do Core tem teste.
- [ ] Código do adaptador revisado manualmente; "APIs a confirmar" listadas no PR.
- [ ] Textos novos em `i18n/default.json`.
- [ ] `CHANGELOG.md` e `STATUS.md` atualizados.
- [ ] PR aberto com o checklist de verificação local.

---

## 9. Fluxo do usuário entre fases

1. Envia o prompt da fase para o Claude na nuvem.
2. Recebe o PR, faz checkout da branch localmente.
3. Compila o mod (ver `docs/DEVELOPMENT.md`) e segue o checklist de verificação local do PR.
4. Registra o resultado no `STATUS.md`: o que funcionou, erros de compilação, bugs no jogo (em "Pendências para a próxima fase").
5. Mescla o PR na `main` e inicia a próxima fase.

Se a compilação local falhar com muitos erros, vale uma sessão curta de correção (local, com o jogo instalado) antes de seguir para a próxima fase.
