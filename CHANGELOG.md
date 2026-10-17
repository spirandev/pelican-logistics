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
