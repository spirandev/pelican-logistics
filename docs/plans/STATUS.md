# Status das fases

Atualizado pelo agente ao terminar cada fase e pelo usuário após o teste local.

Status possíveis: `Não iniciada` · `Em andamento` · `Aguardando teste local` · `Com pendências` · `Concluída`

| Fase | Status | Branch | PR | Teste local |
|---|---|---|---|---|
| 0: Estrutura | Concluída | `claude/fase-0-estrutura-ohoxty` | [#1](https://github.com/spirandev/pelican-logistics/pull/1) | OK em 2026-10-16 (Linux, Steam Flatpak, SMAPI 4.5.2, jogo 1.6.15) |
| 1: Base | Concluída | `fase-1-base` | [#2](https://github.com/spirandev/pelican-logistics/pull/2) | OK em 2026-10-17 |
| 2: Máquinas | Concluída | `fase-2-maquinas` | [#3](https://github.com/spirandev/pelican-logistics/pull/3) | OK em 2026-10-17 (Barril e Cristalário não testados) |
| 3: Inseridor único | Concluída | `fase-3-inseridor-unico` | [#4](https://github.com/spirandev/pelican-logistics/pull/4) | OK em 2026-10-17 (alguns itens não testados, ver pendências) |
| 4: Persistência | Concluída | `fase-4-persistencia` | [#5](https://github.com/spirandev/pelican-logistics/pull/5) | Compila contra o jogo; salvar/carregar não testado no jogo (ver pendências) |
| 5: Roteamento | Concluída | `fase-5-roteamento` | [#6](https://github.com/spirandev/pelican-logistics/pull/6) | OK em 2026-10-18 (salvar/carregar não testado, ver pendências) |
| 6: Itens restantes | Concluída | `fase-6-itens-restantes` | [#7](https://github.com/spirandev/pelican-logistics/pull/7) | OK em 2026-10-22 |
| 6b: Túneis | Aguardando teste local | `fase-6b-tuneis` | (a preencher) | Compila contra o jogo; instalado na pasta `Mods`; não testado no jogo |
| 7: Dicas e alpha | Não iniciada | | | |

A Fase 3 (Inseridor único) foi criada depois do teste local da Fase 2; as antigas fases 3 a 6 viraram 4 a 7. A Fase 6b (Túneis) foi criada em 2026-10-19, antes do teste local da Fase 6, a pedido do usuário: a Esteira Subterrânea de vão fixo vira Túnel + Esteiras Subterrâneas por tile, com o Capacete de Engenheiro (README e plano mestre D18 a D21 já atualizados).

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
- Não testado (sem os itens no save): Barril e Cristalário. Adiado pelo usuário; não precisa entrar no checklist das próximas fases.
- Corrigido durante o teste local: a estatística de carga (`StatsToIncrementWhenLoaded`) contava a quantidade consumida; agora conta 1 por carga, como o `PlaceInMachine` do jogo.
- Pedido de mudança do usuário: Extrator e Inseridor viram um bloco só, com entradas laterais e entrega direta (baú → máquina). Virou a nova Fase 3 (`fase-3-inseridor-unico.md`); README e plano mestre (D2, D3, D16, D17) já atualizados.

### Da fase 3
- O mod compila contra o jogo (0 avisos) e não há erros do mod no log.
- Testado e funcionando: baú → Inseridor → baú com o braço, baú → Inseridor → esteira → Inseridor → baú, baú misto → Inseridor (Automático) → fornalha com as barras saindo por outro Inseridor, esteira chegando pela lateral, esteira passando de lado ignorada, rodízio entre baú atrás e esteira na lateral, interface com "Retirar de" e "Colocar em".
- Não testado: destino cheio com o item esperando na mão e quebrar um Inseridor com item na mão (os testes do Core cobrem os dois).
- [x] Ajuste: remover a migração dos Extratores (`LegacyMigration`, `ItemIds.LegacyExtractor`, `ModDataKeys.LegacyExtractorSource` e a chamada no `WorldRegistry.Rebuild`). O usuário não tem mais saves com Extratores e não vai precisar dela. A proteção do `Added` (ignorar tile que já tem nó) pode ficar. **Feito na Fase 4.**
- Observação de desempenho (não é bug): sem nada para pegar (ex.: baú só com madeira abastecendo uma fornalha), o Inseridor testa todas as pilhas do baú contra as regras da máquina a cada tick. Se o teste de desempenho da Fase 7 mostrar queda de FPS, fazer o Inseridor esperar alguns ticks antes de tentar de novo depois de uma tentativa sem sucesso.

### Da fase 4
- Nenhuma pendência de correção. O mod compila contra o jogo (0 avisos) e os 252 testes do Core passam. As "APIs a confirmar" foram conferidas no código descompilado: `GetOrCreateGlobalInventory`, `Inventory.Add`/`Clear`/enumeração e `Farm.GetMainFarmHouseEntry()` existem, e o jogo grava e restaura os inventários globais no save (`SaveGame`, como os baús Junimo; inventários vazios são removidos antes de salvar).
- Não testado no jogo: salvar e carregar (itens em trânsito, mão do Inseridor, slots virtuais, não duplicar ao carregar sem salvar). O usuário não quis usar a save principal nem montar uma save nova só para isso; o teste fica para o uso normal do mod, e um erro encontrado jogando vira pendência da fase em andamento.

### Da fase 5
- Nenhuma pendência de correção. O mod compila contra o jogo (0 avisos), os 336 testes do Core passam e não há erros do mod no log.
- Testado e funcionando (2026-10-18, 1366×768): Filtro Comum (configurar sem gastar o item, ícone no bloco, item recusado trava a linha), filtro acoplado ao Inseridor, Filtro Completo (lista branca por categoria, lista negra, qualidade, campo de context tags), Divisor (separador clássico, excedente com baú cheio, ordem horária, divisão de pilha, duas entradas), Inseridor como entrada e saída do Divisor, `R` no Filtro e no Divisor.
- Corrigido durante o primeiro teste local, na própria branch:
  - Bug na interface: em 1366×768, a interface do Filtro Completo cortava a parte de baixo (só apareciam as categorias até "Artesanais, Ovos..."). Agora ela tem duas colunas (cerca de 1224×630 pixels).
  - Pedido de mudança: o Inseridor precisa funcionar com todos os blocos, como funciona com baús e máquinas. Ex.: baú → Inseridor → esteira → Divisor → Inseridor → fornalha não funcionava do Divisor para o Inseridor; baú → Inseridor → filtro → Divisor → Inseridor → fornalha também precisa funcionar. Agora o Inseridor é entrada e saída do Divisor (README "Divisor"; plano mestre D3 e D4 atualizados).
- Não testado: salvar e carregar das regras dos filtros e dos itens dentro do Divisor e dos filtros (mesmo motivo da Fase 4). O placeholder da esteira em curva (`a739034`) foi compilado depois do teste e ainda não foi visto no jogo.

### Da fase 6
- Nenhuma pendência de correção. O mod compila contra o jogo (0 avisos), os 399 testes do Core passam e foi instalado na pasta `Mods`.
- Testado pelo usuário em 2026-10-22: os itens novos aparecem no Clint e "tudo certo" no teste.
- A Esteira Subterrânea de vão fixo será substituída na Fase 6b (Túnel + Esteiras Subterrâneas por tile + Capacete de Engenheiro, `fase-6b-tuneis.md`).

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

### Fase 3
- Branch: o push para `fase-3-inseridor-unico` funcionou nesta sessão, então foi usado o nome do plano (sem o prefixo `claude/`).
- Pendências da Fase 2: nenhuma de correção. O teste do Barril e do Cristalário ficou no checklist de verificação local desta fase.
- Destino que é um nó (esteira): `Fits` devolve `MaxStack` quando o nó aceita, e não `item.Count` (A5.1). Uma esteira guarda uma pilha inteira, e assim o Inseridor Configurável junta vários slots de um baú (teste 23).
- Entradas laterais: um Inseridor vizinho com a frente apontando para este não conta como entrada (Inseridores não se conectam).
- O destino é resolvido uma vez por tick: o adaptador cria um endpoint novo a cada consulta, e a busca no baú chama `Fits` para cada pilha.
- Rodízio: começa depois de `LastInput` mesmo que ela não seja mais uma entrada (ex.: depois de girar o bloco).
- Placeholder: o triângulo da frente é escuro (cor de contorno), e os pontos de tier ficam na coluna da esquerda, longe do triângulo e do braço parado.
- `ItemIds.IsOurBigCraftable` passou a aceitar só os blocos registrados. Assim o Extrator antigo não é tratado como bloco do mod: o aviso `Removed` da migração não apaga o nó do Inseridor novo, e o patch de desenho não tenta desenhá-lo.
- `pl_dump`: a mão do Inseridor aparece como `hand=[...]`, no mesmo formato dos espaços das esteiras.
- Limitação da alpha: Extratores guardados em baús ou no inventário não são migrados e viram itens de erro do jogo. Só os colocados no mapa viram Inseridores.

### Fase 4
- Branch: o push para `fase-4-persistencia` funcionou nesta sessão, então foi usado o nome do plano (sem o prefixo `claude/`).
- Pendência da Fase 3 resolvida: migração dos Extratores removida (`LegacyMigration`, `ItemIds.LegacyExtractor`, `ModDataKeys.LegacyExtractorSource`). A proteção do `Added` ficou.
- Volta do braço: o `Cooldown` do DTO é lido e gravado por métodos virtuais (`ExportCooldown`/`ImportCooldown`), como os ponteiros; ao importar, fica limitado a `0..InserterReturnTicks`.
- Valores inválidos ao importar: `InputPointer` fora de `Direction` → `LastInput = null`; `EntrySide` inválido → traseira do nó; `Blocked` só vale com o progresso no fim; `MainRequired` no mínimo 1.
- Cada índice da lista de itens é resolvido no máximo uma vez: um estado corrompido não duplica itens. Slot (ou slots virtuais) já ocupado ao importar não é sobrescrito; o item salvo vira órfão.
- Slots virtuais de uma grade sem nós: a grade é criada, e o adaptador acha o local (`WorldRegistry.FindLocation`) e o registra, para a máquina continuar sendo acionada.
- Ao carregar, o inventário global é esvaziado logo depois de copiado (antes do `Import`). Erro no `Import`: os nós são recriados vazios e todos os itens caem perto da casa (sem duplicar o que já tinha sido restaurado). Itens do inventário que nenhum slot usou também caem perto da casa.
- Erro ao salvar: todos os itens vão para o inventário global sem estado (`WriteSaveData` com `null`); ao carregar, caem perto da casa.
- "Perto da casa da fazenda" = tile de `Farm.GetMainFarmHouseEntry()`.
- Mensagens de log em pt-BR no código, como as já existentes no `WorldRegistry`; sem textos novos para o jogador no `i18n`.

### Fase 5
- Branch: o plano pede `fase-5-roteamento`; esta sessão rodou localmente (não na nuvem), então foi usado o nome do plano.
- Pendências da Fase 4: nenhuma registrada (ver acima).
- Compilação do adaptador: esta sessão tinha o jogo instalado, então o mod foi compilado contra o jogo (0 avisos, 0 erros), sem instalar na pasta `Mods` (`-p:EnableModDeploy=false`). As APIs novas foram conferidas no código descompilado; falta só o teste no jogo.
- Regras do filtro no `modData`: JSON ausente ou corrompido, ou modo inválido, dá a regra vazia (que não deixa passar nada); valores inválidos dentro das listas (nulos, vazios, qualidade fora de 0/1/2/4) são descartados. O modo é gravado pelo nome (`"Whitelist"`/`"Blacklist"`).
- Context tags: a interface separa por vírgula, tira os espaços, passa para minúsculas (como as tags do jogo) e não repete.
- Categorias do Filtro Completo: uma opção por categoria do jogo, com as constantes de `StardewValley.Object`. O jogo mostra "Mineral" para as categorias -2 e -12: o **quartzo** e as gemas são da -2 (**Gemas**), os minerais dos geodos da -12 (**Minerais**). Por isso o item do checklist "lista branca com Minerais deixa passar quartzo" virou "com **Gemas**".
- Divisor: puxa a próxima entrada no mesmo tick em que distribuiu (como uma esteira que recebe logo depois de entregar), então cada pilha leva 15 ticks para atravessá-lo.
- Divisor: a "saída livre" é a que aceita o item agora (`CanAccept`): um filtro que ainda está passando o item anterior conta como ocupado e o item vai como excedente para as saídas sem filtro (regra 4 do README).
- Inseridor e Divisor (depois do primeiro teste local): um Inseridor apontando para o Divisor é entrada, e o Divisor puxa o item da mão dele (o braço volta vazio, com a volta normal de 15 ticks); o Inseridor só pega quando o Divisor está vazio, como faz com uma esteira ocupada. Um Inseridor com a traseira virada para o Divisor é saída sem filtro: é "livre" se pegaria o item agora (braço parado e vazio, destino aceita) e recebe só a quantidade do tier; o resto fica no Divisor, bloqueado, e é distribuído de novo no próximo tick. Um Divisor na lateral de um Inseridor não se conecta (as laterais do Inseridor são só para esteiras que terminam nele), e dois Inseridores encostados continuam sem conexão (README "Inseridores").
- Divisor com uma única saída para um Inseridor que abastece uma máquina: se a máquina não aceita o item da vez (ex.: minério com o Principal já completo, esperando carvão), o Divisor trava, como uma esteira que termina num Inseridor. Na esteira única, o carvão precisa vir entre os minérios.
- Caminho do item no Divisor: calculado no Core (`PathMath.GetHubPosition`), com teste, em vez de no adaptador.
- Interfaces: o botão OK foi para o topo, ao lado do título, e a caixa fica encostada no canto de cima da tela quando não cabe. O `ConfigMenu` aceita colunas (`ConfigColumn`); o Filtro Completo usa duas: modo, qualidade e context tags à esquerda (360 px), itens com o inventário e categorias à direita.
- `ToggleRow` não foi criado: nenhuma interface desta fase tem um liga/desliga isolado, e o `ToggleGroupRow` cobre qualidades e categorias. `ToggleGroupRow` e `CycleRow` são genéricos no tipo do valor.
- `ItemListRow`: os 12 slots fantasma são um pouco menores (52 px) para o rótulo caber na mesma linha; item repetido ou lista cheia toca o som de "cancelar".
- Um filtro recolhido e colocado de novo volta vazio, como a configuração do Inseridor (o jogo copia o `modData` do item segurado).

### Fase 6
- Branch: esta sessão rodou localmente (não na nuvem), então foi usado o nome do plano, `fase-6-itens-restantes`.
- Pendências da Fase 5: nenhuma de correção registrada.
- Compilação do adaptador: o jogo está instalado nesta máquina, então o mod foi compilado contra ele (0 avisos, 0 erros), sem instalar na pasta `Mods` (`-p:EnableModDeploy=false`). As "APIs a confirmar" do plano foram conferidas no código descompilado; falta o teste no jogo.
- Namespace novo no Core: `Regulators` (`RegulatorMode`, `RegulatorRule`, `RegulatorRuleSerializer`), acrescentado à tabela do plano mestre.
- `Balance`: `RegulatorDefaultLimit = 100` (Regulador recém-colocado e item recém-adicionado à lista) e `MaxRegulatorLimit = 99.999`. Limites fora de `0..99.999` vindos do `modData` são corrigidos; itens com ID vazio são descartados; JSON corrompido ou modo inválido dá a regra padrão (Geral, 100).
- Acessórios: `LogisticsNode.Accessory` só muda pela grade (`TrySetAccessory`/`RemoveAccessory`), para nenhum acessório ficar num bloco inválido. `TrySetAccessory` recebe a regra do Regulador (usada ao carregar o save) ou cria a padrão. Ao carregar, um acessório inválido no `modData` (não deveria acontecer) cai no chão, com aviso no log.
- Regulador: "sem limite" é `int.MaxValue`; o limite vale ao pegar e também ao entregar (se o baú mudou durante a ida, o resto espera na mão, como no D17). Por item conta o ID em qualquer qualidade (`CountById`).
- Esteira Subterrânea: o túnel tem sempre 5 slots (a persistência não depende do pareamento), e só os primeiros "vão" slots são usados. Se o par for refeito mais perto, os itens além do vão novo seguem direto para a saída; se a saída for removida, os itens esperam no túnel até haver outra saída. A entrada não junta pilhas (como o Filtro), e a saída não recebe de nenhum vizinho, só do par.
- `DecideRole`: "entrada sem par" é calculado na hora (a primeira Subterrânea à frente dela não é uma saída), e não pelo `Partner`, que só é atualizado no tick seguinte. Uma Subterrânea colocada entre um par já formado vira entrada.
- Teste 24 (conservação): com os limites atingidos, um item fica esperando na esteira e outro no filtro (README "Regulador": o resto fica na esteira), então o teste confere a conservação a cada tick e os 2 itens parados, em vez de "nada em trânsito".
- Mensagens: além das duas do plano, "Este bloco já tem um acessório." (Ponte ou Regulador num bloco certo que já tem acessório). Bloco errado tem prioridade sobre essa mensagem.
- "Mão livre" para tirar a Ponte = nenhum item selecionado na barra (com uma ferramenta na mão, nada acontece).
- Placeholders: a Ponte é uma faixa horizontal que não gira com a esteira (README "faixa horizontal"), com 60% de opacidade. O Regulador é um quadrado 6×6 no canto inferior direito do Inseridor. O ícone da Subterrânea no inventário é o da entrada; segurando uma, a prévia já mostra o círculo que ela terá no tile do cursor.
- Ponte passável: o patch lê a ponte do `modData` do objeto, sem consultar a grade (é chamado muitas vezes por quadro). `GameLocation.isCollidingPosition` pula objetos com `isPassable()` verdadeiro; a picareta não usa `isPassable`, então a esteira com ponte continua quebrável.
- Acessórios em `Data/Objects`: `Type = "Basic"`, `Category = 0`, `Price = 0` e a tag `not_placeable` (pelo código do jogo, `Object.isPlaceable` já recusa esse tipo/categoria; a tag é uma garantia extra), sem presentear nem entrar em coleções ou vendas aleatórias.
- Interface do Inseridor: quando a forma muda (modo do Regulador, item adicionado ou removido, "Remover regulador"), a interface é fechada e aberta de novo com as linhas certas. A lista Por item fica numa segunda coluna; "Adicionar item" abre uma tela própria com o inventário (o inventário tem 768 px e não caberia ao lado da lista em 1366×768). O Regulador removido vai para o inventário, ou cai no chão se não couber.
- `NumberField`: botões com texto ASCII (`-10`, `-1`, `+1`, `+10`), o campo aceita no máximo os dígitos do limite (3 para 999, 5 para 99.999) e, solto, mostra o valor corrigido. Os textos dos itens usam "2 vezes" em vez de "2×" (o sinal pode não existir na fonte do jogo).
- Quebrar um bloco apaga o acessório do `modData` do objeto recolhido (o acessório já caiu no chão).

### Fase 6b
- Branch: sessão local, com o nome do plano (`fase-6b-tuneis`).
- Pendências da Fase 6: nenhuma de correção.
- Compilação: o mod foi compilado contra o jogo (0 avisos, 0 erros) e instalado na pasta `Mods`. As "APIs a confirmar" foram conferidas no código descompilado (ver o PR).
- `NodeKind` com valores explícitos: o 4 (Subterrânea antiga) fica sem uso; `Tunnel = 5`, `UndergroundSegment = 6`. Um estado salvo com a Subterrânea antiga vira órfão (os itens caem no chão).
- Esteira e Esteira Subterrânea compartilham a base `ConveyorNode` (movimento, curva e junção D11); só o "para onde entregar" muda. A Esteira Subterrânea nunca é puxada por nós da superfície (`GetOutgoingSlot` nulo).
- A saída do Túnel recebe da Esteira Subterrânea por um método interno (`ReceiveFromUnderground`), não pelo `Accept`, para uma esteira da superfície atrás da saída não conseguir empurrar nela.
- A Esteira Subterrânea entrega na saída do Túnel só se ela estiver no tile à frente **com a mesma direção** (a traseira da saída virada para a esteira); pela lateral, não.
- `LogisticsGrid.Nodes`/`Count` continuam só a superfície; `UndergroundNodes` e `TotalCount` cobrem a camada nova (usados no tick do mundo, na exportação do estado e no caminho de emergência do `SaveManager`).
- Layout das Esteiras Subterrâneas no `modData` do local: a direção vai pelo nome e é lida à mão, para uma direção inválida descartar só aquela entrada; posição repetida também é descartada. Ao carregar, uma posição que não aceita mais Esteira Subterrânea (ex.: Túnel em cima) é descartada com aviso, e o layout é regravado.
- "Dá para cavar": dentro do mapa, fora da água (`isWaterTile`), com tile na camada `Back` e sem tile na camada `Buildings` do mapa (paredes, penhascos). Construções do jogador (`location.buildings`) não impedem.
- Retirar com a mão livre (D20): o tile não pode ter objeto ou móvel (`getObjectAtTile`), construção (`getBuildingAt`), arbusto (`getLargeTerrainFeatureAt`), toco/pedra grande (`resourceClumps`) nem terrain feature além de `Flooring` e `Grass`.
- Girar uma Esteira Subterrânea: `R` com o capacete na cabeça e a mão livre, quando o tile não tem bloco na superfície (com bloco, `R` gira o bloco, como antes).
- Túnel sem capacete: o patch em `canBePlacedHere` barra a colocação e a prévia fica vermelha; a mensagem ("Você precisa do Capacete de Engenheiro na cabeça.") vem do clique, porque o jogo chama o método sem pedir erro. Túnel sobre uma Esteira Subterrânea: "Há uma Esteira Subterrânea embaixo deste tile."
- Capacete: folha 20×80 na ordem do `FarmerRenderer` (baixo, direita, esquerda, cima, conferida no código descompilado); `Data/Hats` com "mostrar o cabelo" = `true` e sem tags. A posição do desenho na cabeça é um palpite do placeholder.
- Descrições técnicas: `Object.getDescription` não guarda cache (a descrição é montada a cada chamada), então pôr ou tirar o capacete muda a dica na hora. O texto técnico é quebrado com a mesma largura do jogo (`getDescriptionWidth` é protegido; a conta foi copiada). Os números usam a cultura do i18n (`tech.culture`, "0,5" em pt-BR).
- A camada subterrânea é desenhada no `RenderedWorld`, por cima de tudo ("visão de raio X"), com 55% de opacidade.
- Sons: `dirtyHit` ao colocar e retirar Esteira Subterrânea (o jogo usa esse som).
