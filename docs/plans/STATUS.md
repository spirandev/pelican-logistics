# Status das fases

Atualizado pelo agente ao terminar cada fase e pelo usuário após o teste local.

Status possíveis: `Não iniciada` · `Em andamento` · `Aguardando teste local` · `Com pendências` · `Concluída`

| Fase | Status | Branch | PR | Teste local |
|---|---|---|---|---|
| 0: Estrutura | Concluída | `claude/fase-0-estrutura-ohoxty` | [#1](https://github.com/spirandev/pelican-logistics/pull/1) | OK em 2026-10-16 (Linux, Steam Flatpak, SMAPI 4.5.2, jogo 1.6.15) |
| 1: Base | Concluída | `fase-1-base` | [#2](https://github.com/spirandev/pelican-logistics/pull/2) | OK em 2026-10-17 |
| 2: Máquinas | Não iniciada | | | |
| 3: Persistência | Não iniciada | | | |
| 4: Roteamento | Não iniciada | | | |
| 5: Itens restantes | Não iniciada | | | |
| 6: Dicas e alpha | Não iniciada | | | |

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
