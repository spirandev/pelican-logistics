# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [Unreleased]

### Adicionado
- Estrutura do projeto: Core, mod SMAPI, testes e CI.
- Core: `Direction` (ordem horária) e `TilePos`, com testes.
- Mod: `manifest.json`, `ModEntry` (mensagem ao carregar), `ModConfig` e `i18n/default.json` (pt-BR).
- Guia de desenvolvimento local em `docs/DEVELOPMENT.md`.
- Core: abstrações de item (`ILogisticsItem`) e de baú (`IChestEndpoint`, `IEndpointProvider`), constantes de `Balance`.
- Core: Esteira (reta e curva automática, lateral ignorada, acúmulo e junção de pilhas iguais), Extrator e Inseridor (1 operação a cada 0,5 s).
- Core: `LogisticsGrid` e `LogisticsWorld` com ordem de processamento "de jusante para montante" (`Topology`), para filas compactadas andarem juntas.
- Core: `PathMath` (posição do item em retas e curvas) e `PlaceholderPainter` (sprites placeholder em buffers de pixels).
- Mod: Esteira, Extrator e Inseridor registrados como Big Craftables, com ícones placeholder gerados em tempo de execução.
- Mod: o Clint vende os 3 itens por 0g, com estoque ilimitado (`UnlockAll`).
- Mod: simulação a cada tick em todos os locais (para com o jogo pausado e à noite); desenho dos blocos girados e dos itens em trânsito.
- Mod: rotação com `R` (direção de colocação e bloco sob o cursor) e seta fantasma de colocação.
- Mod: remover um bloco solta no chão os itens que ele carregava.
- Mod: comandos de console `pl_give` e `pl_dump`.
- Core: slots virtuais das máquinas (Principal e Adicional), guardados por máquina na grade (`MachineSlotStore`).
- Core: Inseridor abastece máquinas nos modos Automático, Principal ou Adicional, inserindo só o que falta; regra do Cristalário (só carrega quando vazio).
- Core: máquina acionada quando os slots estão completos e ela está ociosa; o que ela não consumir continua nos slots.
- Core: Extrator retira de máquinas: Produto (padrão), Principal ou Adicional.
- Mod: máquinas de `Data/Machines` como destino e origem (`MachineEndpoint`), sem usar o inventário do jogador; Covo e Barril de carvalho ficam de fora.
- Mod: quebrar uma máquina solta no chão os itens dos slots virtuais.
- Mod: interface base de configuração (`ConfigMenu` + `CycleRow`) e interfaces do Extrator ("Retirar de") e do Inseridor ("Colocar em"), abertas com o botão de ação.
- Mod: comando de console `pl_inspect` (bloco e slots virtuais sob o cursor).
- Core: Inseridor com braço: o item viaja na mão (ida de 15 ticks, volta de 15), entradas laterais (esteiras que terminam nele) em rodízio horário e entrega direta baú → baú e baú → máquina.
- Core: o Inseridor só pega o que o destino aceita agora, e só a quantidade que cabe; se o destino encher na ida, o item espera na mão.
- Core: `ArmMath` (ângulo do braço e posição da mão) e consultas sem efeito colateral do baú (`Find`, `SpaceFor`).
- Mod: desenho do braço, da mão e do item na mão do Inseridor; interface do Inseridor com "Retirar de" e "Colocar em".
- Mod: `pl_dump` e `pl_inspect` mostram a mão, a última entrada e as entradas do Inseridor.
- Core: `RuntimeStateSerializer` exporta e importa o estado em tempo de execução (itens em trânsito, mão, volta do braço e última entrada do Inseridor, slots virtuais); itens sem destino viram órfãos.
- Mod: itens nas esteiras, na mão dos Inseridores e nos slots virtuais sobrevivem ao salvar e carregar (`SaveManager`: estado em JSON e itens num inventário global do jogo). Itens órfãos caem no chão, com aviso no log.
- Core: regras de filtro (`CommonFilterRule`: um item, qualquer qualidade; `CompleteFilterRule`: itens, categorias, qualidades e context tags, em lista branca ou negra) e `FilterRuleSerializer` (JSON tolerante a dados corrompidos).
- Core: Filtro (`FilterNode`), bloco de esteira sempre reto e rápido que só deixa entrar o que a regra aceita; filtro acoplado ao Inseridor (só retira o que o filtro à frente aceita).
- Core: Divisor (`SplitterNode`): portas calculadas pela `Topology`, puxa as entradas em rodízio horário, prioridade para saídas com filtro, divisão igual, excedente para as saídas sem filtro e trava sem destino; ponteiros de rodízio salvos no save.
- Core: placeholders do Filtro Comum, do Filtro Completo e do Divisor; caminho do item no Divisor (`PathMath.GetHubPosition`).
- Mod: Filtro Comum, Filtro Completo e Divisor registrados, à venda no Clint por 0g e no `pl_give`; regra dos filtros no `modData`.
- Mod: interfaces do Filtro Comum (escolha do item pelo inventário, sem gastá-lo) e do Filtro Completo (modo, itens, qualidades, categorias e context tags), com as linhas novas `ItemPickerRow`, `ItemListRow`, `ToggleGroupRow` e `TextRow`.
- Mod: desenho do Filtro (com o ícone do item do Filtro Comum) e do Divisor (sem rotação); `pl_dump` mostra a regra do filtro e as portas e ponteiros do Divisor.

### Alterado
- Interfaces: o botão OK fica no topo, ao lado do título, e a caixa se ajusta à linha mais larga (o inventário dos filtros).
- O Inseridor se conecta ao Divisor: apontando para ele é uma entrada (o Divisor puxa da mão), com a traseira virada para ele é uma saída (o Divisor entrega na mão só o que o Inseridor pegaria). Um Divisor na lateral do Inseridor continua sem conexão.
- Interfaces em colunas: a do Filtro Completo usa duas colunas para caber em 1280×720 e 1366×768.
- Extrator e Inseridor unificados no Inseridor (README "Inseridores"): ele pega atrás (baú, máquina ou esteira) e entrega na frente. O placeholder passa a ser um quadrado azul com um triângulo na borda do destino.

### Removido
- Extrator (bloco, interface e `ExtractorNode`). Extratores de saves das Fases 1 e 2 viram itens de erro do jogo.
