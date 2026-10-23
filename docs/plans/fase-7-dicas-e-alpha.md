# Fase 7: Dicas ao passar o mouse e alpha 0.1.0

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md) (seção "Dicas ao passar o mouse"), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-7-dicas-e-alpha`
- **PR:** `Fase 7: Dicas e alpha 0.1.0`
- **Depende de:** Fase 6b mesclada.

## Objetivo

Fechar a alpha:
1. **Dicas ao passar o mouse** nas esteiras, nos blocos e nas máquinas (README: a quantidade só aparece ao passar o mouse). Os slots das máquinas e as Esteiras Subterrâneas só com o Capacete de Engenheiro ativo (plano mestre D18).
2. **Modo de depuração** visual para ajudar nos testes.
3. **Revisão** das interfaces, textos e documentação.
4. **Preparar a versão 0.1.0-alpha** para distribuição aos testadores.

## Fora de escopo

Qualquer item ou mecânica nova. Progressão, sprites e tradução (beta).

---

## Parte A: Core

### A1. `Rendering/TooltipModel.cs`

O Core monta os **dados** da dica; o adaptador só traduz e desenha. Assim a lógica é testável.

```csharp
public sealed record TooltipLine(string Key, IReadOnlyDictionary<string, object> Args);

public sealed record TooltipModel(string TitleKey, IReadOnlyList<TooltipLine> Lines);

public static class TooltipBuilder
{
    public static TooltipModel? ForNode(LogisticsNode node);   // também para UndergroundSegmentNode
    public static TooltipModel? ForMachine(VirtualSlots slots, IMachineEndpoint machine);
}
```

Conteúdo (as chaves são do i18n):

| Alvo | Título | Linhas |
|---|---|---|
| Esteira (qualquer tier) | nome do tier | item em trânsito e quantidade (`Madeira ×50`), ou "vazia"; "Bloqueada" se `Blocked`; "Com ponte" se tiver |
| Filtro Comum | "Filtro Comum" | item configurado ou "nenhum item"; item em trânsito |
| Filtro Completo | "Filtro Completo" | modo; resumo ("3 itens, 2 categorias, qualidade ouro"); item em trânsito |
| Divisor | "Divisor" | número de entradas e saídas; item em trânsito; "Sem destino" se bloqueado |
| Inseridor | nome do tier | "Retirar de" e "Colocar em"; item na mão; quantidade se Configurável; regulador e limite se houver |
| Túnel | "Entrada"/"Saída" | item em trânsito; na entrada, "Sem Esteira Subterrânea à frente" se for o caso |
| Esteira Subterrânea (só com o capacete) | "Esteira Subterrânea" | item em trânsito e quantidade, ou "vazia"; "Bloqueada" se `Blocked` |
| Máquina com slots virtuais (só com o capacete) | nome da máquina (o adaptador preenche) | `Principal: 3/5 Minério de cobre`; uma linha por adicional `Adicional: 1/1 Carvão` |

Itens são referenciados nas linhas pelo `DisplayName` e pela quantidade nos `Args`.

### A2. Testes

1. Esteira vazia, com item, bloqueada e com ponte geram as linhas certas.
2. Fornalha com Principal 3/5 e Adicional 0/1 gera duas linhas com os números certos.
3. Inseridor Configurável com Regulador mostra quantidade e limite.
4. Entrada de Túnel sem Esteira Subterrânea à frente mostra o aviso; segmento bloqueado mostra "Bloqueada".
5. Máquina sem slots virtuais (`slots == null` ou vazio): sem dica do mod (`null`).

### A3. Teste de carga

Um teste que monta **10.000 nós** (linhas longas, curvas, Divisores) e roda **600 ticks**, verificando só que termina e que os itens são conservados. **Sem** limite de tempo na asserção (evita teste instável), mas escreva no log do teste (`ITestOutputHelper`) o tempo médio por tick, e registre o valor no PR.

---

## Parte B: Adaptador

### B1. Dicas (`Framework/Rendering/TooltipRenderer.cs`)

- `Display.RenderedHud`: se não há menu aberto (`Game1.activeClickableMenu == null`) e o jogo está carregado:
  - tile do cursor (`Helper.Input.GetCursorPosition().Tile`) no local atual;
  - nó nosso no tile (superfície) → `TooltipBuilder.ForNode`;
  - senão, com o capacete ativo (`EngineerHelmet.IsActive`), máquina com slots virtuais não vazios → `TooltipBuilder.ForMachine`, com o título = `DisplayName` da máquina;
  - senão, com o capacete ativo, segmento no tile → `TooltipBuilder.ForNode` do segmento;
  - traduz com `Helper.Translation.Get(key, args)` e desenha com `IClickableMenu.drawHoverText(b, texto, Game1.smallFont, …)` junto ao cursor.
- Não mostrar a dica do mod ao mesmo tempo que outra dica do jogo para o mesmo objeto, se isso causar sobreposição; registre no PR como ficou.

### B2. Modo de depuração

- Comando `pl_debug` alterna `ModConfig.DebugMode` (sem gravar no arquivo).
- Com o modo ligado, em `RenderedWorld`, sobre cada nó do local atual:
  - seta pequena indicando o `InputSide` das esteiras;
  - quadrado vermelho translúcido nos slots `Blocked`;
  - nos Divisores, marcas verdes nas entradas e azuis nas saídas;
  - as Esteiras Subterrâneas e o lado de entrada de cada uma, mesmo sem o capacete.

### B3. Revisão das interfaces

Passe por todas as interfaces (Inseridor + Regulador, Filtros) e garanta:
- mesmo estilo de caixa, título, espaçamento e botão OK;
- todas fecham com `Esc` e com a tecla de menu;
- nenhum texto fixo no código: tudo no `i18n/default.json`;
- abrir uma interface não consome o item que o jogador segura;
- as interfaces cabem na tela em 1280×720 (layout com rolagem ou duas colunas, se precisar).

### B4. Revisão dos textos

- Revise todo o `i18n/default.json`: pt-BR correto, com acentos, frases curtas, nomes iguais aos do README.
- Descrições de item explicam em uma frase o que o item faz.

### B5. Versão e documentação

- `manifest.json`: `Version` = `0.1.0-alpha`.
- `CHANGELOG.md`: mova o conteúdo de `Unreleased` para `## [0.1.0-alpha] - <data>` com um resumo por fase.
- `README.md`:
  - Atualize o "Status" no topo: alpha 0.1.0 disponível para testes.
  - Nova seção **"Instalando a alpha"**: requisitos (Stardew Valley 1.6+, SMAPI 4.x), descompactar o zip em `Mods`, como pegar os itens (loja do Clint ou `pl_give`), tecla `R`, comandos de console.
  - Nova seção **"Limitações conhecidas da alpha"**: sem sprites, sem progressão, só pt-BR, só single-player, esteiras param à noite, Covo e baús especiais não suportados, comportamento imprevisível se a mesma máquina for automatizada também pelo Automate, decisões D11 e D12 do plano mestre.
  - **Não** altere as decisões de design; só acrescente essas seções.
- `docs/DEVELOPMENT.md`: como gerar o zip de distribuição (`dotnet build src/PelicanLogistics -c Release`; o `ModBuildConfig` gera o zip na pasta de saída), todos os comandos de console (`pl_give`, `pl_dump`, `pl_inspect`, `pl_debug`).

### B6. Varredura final do adaptador

Releia **todo** o código de `src/PelicanLogistics` procurando:
- `using` faltando, nomes de tipos e métodos inconsistentes entre arquivos;
- eventos registrados duas vezes ou nunca removidos;
- exceções que podem escapar de handlers de evento (envolva com `try/catch` + log onde um erro derrubaria o jogo);
- código morto de fases anteriores.

Liste no PR tudo que foi corrigido.

---

## Passo a passo sugerido

1. Pendências do `STATUS.md`.
2. Core A1–A3 + testes.
3. Adaptador B1–B2.
4. B3–B4.
5. B5–B6.
6. Changelog, status, PR.

## Critérios de aceite (nuvem)

- [ ] Testes desta fase passam, junto com todos os anteriores.
- [ ] Tempo médio por tick do teste de carga registrado no PR.
- [ ] README com "Instalando a alpha" e "Limitações conhecidas"; changelog com a versão 0.1.0-alpha.
- [ ] PR com "APIs a confirmar" (no mínimo: `drawHoverText`, `RenderedHud`).

## Verificação local (usuário)

- [ ] Passar o mouse numa esteira com itens mostra `Madeira ×50`; numa vazia mostra "vazia"; numa travada mostra "Bloqueada".
- [ ] Com o Capacete de Engenheiro, passar o mouse numa fornalha abastecida mostra `Principal: 3/5 Minério de cobre` e `Adicional: 1/1 Carvão`; sem o capacete, não mostra nada.
- [ ] Dicas de Inseridor (com e sem Regulador), Filtros, Divisor e Túnel mostram as informações da tabela; a da Esteira Subterrânea só aparece com o capacete.
- [ ] Nenhuma dica aparece com um menu aberto.
- [ ] `pl_debug` mostra setas de entrada, bloqueios em vermelho e portas do Divisor.
- [ ] Todas as interfaces têm o mesmo estilo, fecham com Esc e cabem na tela.
- [ ] Todos os textos estão em pt-BR correto.
- [ ] `dotnet build src/PelicanLogistics -c Release` gera o zip; instalar o zip numa pasta `Mods` limpa funciona.
- [ ] **Teste de desempenho:** uma fazenda com ~500 esteiras ativas não causa queda perceptível de FPS. Anote o resultado no `STATUS.md`.
- [ ] Jogar um dia inteiro com uma fábrica completa (fornalha, barris, separador com Divisor, túnel, regulador), dormir, salvar e carregar: tudo continua funcionando e nada se perdeu.
- [ ] Nenhum erro do mod no log do SMAPI.
