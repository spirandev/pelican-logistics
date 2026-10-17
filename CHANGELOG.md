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

### Alterado
- Extrator e Inseridor unificados no Inseridor (README "Inseridores"): ele pega atrás (baú, máquina ou esteira) e entrega na frente. O placeholder passa a ser um quadrado azul com um triângulo na borda do destino.
- Saves das Fases 1 e 2: os Extratores colocados no mapa são trocados por Inseridores ao carregar, na mesma direção e com o mesmo "Retirar de".

### Removido
- Extrator (bloco, interface e `ExtractorNode`). Extratores guardados em baús ou no inventário viram itens de erro do jogo.
