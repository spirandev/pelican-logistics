# Status das fases

Atualizado pelo agente ao terminar cada fase e pelo usuário após o teste local.

Status possíveis: `Não iniciada` · `Em andamento` · `Aguardando teste local` · `Com pendências` · `Concluída`

| Fase | Status | Branch | PR | Teste local |
|---|---|---|---|---|
| 0: Estrutura | Concluída | `claude/fase-0-estrutura-ohoxty` | [#1](https://github.com/spirandev/pelican-logistics/pull/1) | OK em 2026-10-16 (Linux, Steam Flatpak, SMAPI 4.5.2, jogo 1.6.15) |
| 1: Base | Não iniciada | | | |
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

---

## Decisões tomadas durante a implementação

Registradas pelo agente quando precisou decidir algo que nem o README nem o plano cobriam.

### Fase 0
- Branch: a sessão na nuvem só pode publicar numa branch com prefixo `claude/`, então a Fase 0 usou `claude/fase-0-estrutura-ohoxty` em vez de `fase-0-estrutura`.
- Templates: o SDK 8 não aceita `dotnet new classlib -f net6.0`; os projetos foram gerados com o padrão e os `.csproj` substituídos pelos do plano (`net6.0`).
