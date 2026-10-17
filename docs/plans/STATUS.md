# Status das fases

Atualizado pelo agente ao terminar cada fase e pelo usuário após o teste local.

Status possíveis: `Não iniciada` · `Em andamento` · `Aguardando teste local` · `Com pendências` · `Concluída`

| Fase | Status | Branch | PR | Teste local |
|---|---|---|---|---|
| 0: Estrutura | Concluída | `claude/fase-0-estrutura-ohoxty` | [#1](https://github.com/spirandev/pelican-logistics/pull/1) | OK em 2026-10-16 (Linux, Steam Flatpak, SMAPI 4.5.2, jogo 1.6.15) |
| 1: Base | Concluída | `fase-1-base` | [#2](https://github.com/spirandev/pelican-logistics/pull/2) | OK em 2026-10-17 |
| 2: Máquinas | Concluída | `fase-2-maquinas` | [#3](https://github.com/spirandev/pelican-logistics/pull/3) | OK em 2026-10-17 (Barril e Cristalário não testados) |
| 3: Inseridor único | Não iniciada | | | |
| 4: Persistência | Não iniciada | | | |
| 5: Roteamento | Não iniciada | | | |
| 6: Itens restantes | Não iniciada | | | |
| 7: Dicas e alpha | Não iniciada | | | |

A Fase 3 (Inseridor único) foi criada depois do teste local da Fase 2; as antigas fases 3 a 6 viraram 4 a 7.

---

## Pendências para a próxima fase

Escritas pelo usuário depois do teste local. A próxima fase **começa resolvendo estes itens**.

Formato sugerido:

```
### Da fase N
- [ ] Erro de compilação: <arquivo>:<linha> <mensagem>
- [ ] Bug no jogo: <o que fiz> → <o que aconteceu> (esperado: <o que deveria acontecer>)
- [ ] Ajuste: <descrição>
```

### Da fase 0
- Nenhuma pendência. O mod compila (0 avisos), carrega no SMAPI com a mensagem esperada e cria o `config.json` com `UnlockAll`, `RotateKey` e `DebugMode`. Nenhum erro do mod no log (os erros de "Galaxy API" são do próprio jogo rodando pela Steam e não têm relação com o mod).

### Da fase 1
- Nenhuma pendência. O mod compila contra o jogo (0 avisos), o checklist de verificação local passou e não há erros do mod no log.
- Corrigido durante o teste local: um bloco recolhido e colocado de novo voltava na direção antiga, porque o jogo copia o `modData` do item segurado ao colocar (`Object.getOne`). Agora a direção da seta sempre é gravada ao colocar.

### Da fase 2
- Nenhuma pendência de correção. O mod compila contra o jogo (0 avisos) e não há erros do mod no log.
- Testado e funcionando: interfaces do Extrator e do Inseridor, fornalha com Principal e Adicional, máquina de maionese, `pl_inspect`, itens dos slots caindo no chão ao quebrar a fornalha, uso manual da fornalha com itens guardados nos slots.
- Não testado (sem os itens no save): Barril e Cristalário. Teste os dois na Fase 3 (`player_add (BC)12`, `player_add (BC)21`).
- Corrigido durante o teste local: a estatística de carga (`StatsToIncrementWhenLoaded`) contava a quantidade consumida; agora conta 1 por carga, como o `PlaceInMachine` do jogo.
- Pedido de mudança do usuário: Extrator e Inseridor viram um bloco só, com entradas laterais e entrega direta (baú → máquina). Virou a nova Fase 3 (`fase-3-inseridor-unico.md`); README e plano mestre (D2, D3, D16, D17) já atualizados.

---

## Decisões tomadas durante a implementação

Registradas pelo agente quando precisou decidir algo que nem o README nem o plano cobriam.

### Fase 0
- Branch: a sessão na nuvem só pode publicar numa branch com prefixo `claude/`, então a Fase 0 usou `claude/fase-0-estrutura-ohoxty` em vez de `fase-0-estrutura`.
- Templates: o SDK 8 não aceita `dotnet new classlib -f net6.0`; os projetos foram gerados com o padrão e os `.csproj` substituídos pelos do plano (`net6.0`).

### Fase 1
- Branch: o push para `fase-1-base` funcionou nesta sessão, então foi usado o nome do plano (sem o prefixo `claude/`).
- Cooldown do Extrator: decrementa e testa no mesmo tick, para o período ser exatamente `TransferCooldownTicks` (30). O passo literal do plano (A9) daria 31 ticks.
- Prévia de colocação: quando o jogo desenha o item segurado sobre o cursor, o bloco aparece na direção de colocação atual. Um bloco colocado ainda sem nó usa a direção do `modData` (o plano dizia `East`).
- Folha de ícones: cada carregamento do asset cria uma textura nova (ela pertence ao gerenciador de conteúdo do jogo). Só os sprites de mundo ficam em cache.
- `ChestEndpoint`: inserção no padrão do Automate (`addToStack` + slot livre dentro de `GetActualCapacity()`), em vez de `chest.addItem`; retirada de pilha inteira com `RemoveAt`; inventário via `GetItemsForPlayer()`; geladeiras (`fridge`) excluídas (D10).
- Patch de `Object.draw` com argumentos por posição (`__0`..`__3`); erro no desenho é registrado uma vez e o desenho volta para o do jogo.
- Textos dos comandos de console também ficam no `i18n` (D14).
- Tiers Stack/Configurable do Extrator e do Inseridor já funcionam no Core (A9/A10), com testes; o adaptador só cria a versão `Single`.
- `PathMath`: pontas da curva exatas, sem erro de arredondamento.

### Fase 2
- Branch: o push para `fase-2-maquinas` funcionou nesta sessão, então foi usado o nome do plano (sem o prefixo `claude/`).
- Consumo pela máquina: em vez de `slots.Clear()` depois de `TryStart` (A6), a máquina diminui o `Count` das pilhas que usou e o Core descarta só as que zeraram. O que a máquina não consumir continua nos slots, para nunca sumir item.
- Grade sem nenhum bloco, mas com itens nos slots virtuais (o Inseridor foi removido), continua sendo processada: a máquina ainda é acionada quando completar.
- Adicional: um requisito só recebe itens que empilham com o que já está guardado nele (ex.: mesma qualidade); senão o próximo requisito é tentado.
- Extrator em máquina: se a frente não aceitar tudo (não deveria acontecer), o resto volta para o slot de onde saiu; no Produto, volta para o produto guardado.
- `MachineEndpoint`: requisitos adicionais com o mesmo item são somados; os com quantidade 0 são ignorados. Depois de acionar, grava `lastInputItem` (usado pelo Cristalário para recomeçar) e atualiza as estatísticas de carga; a coleta segue o Automate (estatísticas, regra `OutputCollected`, torneira, experiência).
- Barril de carvalho (`Cask`) também fica de fora, como o Covo: tem lógica própria (envelhecimento, só na adega).
- Bloco colocado começa com a configuração padrão (Produto / Automático): como na direção (Fase 1), o jogo copiaria o `modData` do item segurado.
- `pl_inspect` também mostra se a máquina está ociosa, o produto pronto e se ela repete sozinha.
