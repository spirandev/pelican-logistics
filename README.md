# Pelican Logistics

> **Status:** alpha em implementação, seguindo as fases de [`docs/plans/`](docs/plans/00-plano-mestre.md); o andamento está em [`STATUS.md`](docs/plans/STATUS.md).
> Tudo marcado com **[A DEFINIR]** é ponto aberto para conversarmos.

Mod de logística e automação para Stardew Valley, inspirado em mods técnicos de Minecraft (Create, Mekanism, Applied Energistics) e em jogos como Factorio. A ideia é transportar itens **visualmente** por esteiras entre baús e máquinas, com filtros, divisores e roteamento, para montar linhas de produção de verdade na fazenda.

---

## Nome

Nome provisório: **Pelican Logistics**, em referência a Pelican Town.

Alternativas **[A DEFINIR]**:
- Valley Logistics
- Junimo Logistics
- Farm Factory
- Valley Industries

- **Autor:** spirandev
- **UniqueID:** `spirandev.PelicanLogistics` (segue o padrão `Autor.NomeDoMod` e muda junto se o nome mudar)

---

## Visão

### Pilares

1. **Tudo visual.** O jogador vê cada item andando pelas esteiras. Nada de transporte invisível.
2. **Técnico, mas legível.** Filtros, divisores e regras que permitem montar sistemas complexos, sem exigir uma interface complicada.
3. **Parece parte do jogo.** Pixel art no estilo vanilla, itens vendidos por NPCs da cidade, receitas de crafting como as do jogo.
4. **Não altera o vanilla.** O mod adiciona comportamento, mas nada do jogo original deixa de funcionar como antes.
5. **Progressão.** Começa simples e barato; os componentes avançados chegam com o avanço do jogador.

### Diferencial em relação a mods existentes

| Mod | O que faz | O que falta (para nós) |
|---|---|---|
| [Automate](https://nexusmods.com/stardewvalley/mods/1063) | Baú encostado em máquina abastece e recolhe; grupos conectados por caminhos | Sem transporte visual, sem filtros de verdade |
| [Conveyor belts](https://www.nexusmods.com/stardewvalley/mods/34075) (Jok) | Esteiras que puxam/empurram de baús e máquinas | Pouco foco em filtragem e roteamento **[A VERIFICAR jogando]** |

**Decidido:** por enquanto, **sem integração** com outros mods. O Pelican Logistics e o Automate podem ficar instalados juntos (nossos blocos não são baús, então o Automate os ignora), mas uma mesma máquina abastecida pelos dois sistemas terá comportamento imprevisível.

---

## Itens

Os nomes abaixo são provisórios. Todos os itens são colocados no mapa, como blocos ou como acessórios sobre outro bloco, e são craftáveis e compráveis (ver [Progressão](#progressão-e-compra)). Não há componentes instaláveis: as versões melhores são **itens próprios**, feitos com materiais mais raros ou comprados. O tier indicado é provisório e serve para a progressão final.

**Interface:** todo item que tem algo configurável tem **uma interface própria**, aberta ao clicar nele, com todas as configurações disponíveis.

### Transporte

| Item | Tier | Função |
|---|---|---|
| **Esteira** (Conveyor Belt) | 1 | Move itens em **um único sentido**, com **uma única saída**. Rotacionável. Bloqueia a passagem do jogador. Esteiras encostadas pela lateral **não interagem**: para dividir ou juntar fluxos é preciso usar o Divisor (ver [Regras de conexão](#regras-de-conexão)). |
| **Esteira Rápida** | 2 | Igual à esteira, com velocidade maior. |
| **Esteira Expressa** | 3 | A mais rápida. |
| **Ponte** (Bridge) | 1 | Acessório exclusivo da esteira. Colocado sobre uma esteira existente, permite que o jogador passe por cima daquele tile, e a esteira continua funcionando por baixo. Deve ser barata. |
| **Esteira Subterrânea** | 3 | Par entrada/saída que passa itens por baixo de até N tiles (cercas, caminhos, outras esteiras). |

### Inseridores

Um **único bloco** pega e entrega os itens, como o inserter do Factorio: ele **pega atrás** (ou de uma esteira que termina nele) e **entrega na frente**, seja lá o que houver em cada lado. Começou como dois blocos (Extrator e Inseridor) e foi unificado.

| Item | Tier | Função |
|---|---|---|
| **Inseridor** (Inserter) | 1 | Move **1 item por vez**. |
| **Inseridor de Pilha** | 2 | Igual, mas move **a pilha inteira** de uma vez. |
| **Inseridor Configurável** | 3 | Igual, mas move uma **quantidade configurável** por vez. |

```
[Baú]      → [Inseridor] → esteira        retira do baú e coloca na esteira
esteira    → [Inseridor] → [Fornalha]     pega da esteira e abastece a máquina
[Baú]      → [Inseridor] → [Fornalha]     direto, sem esteira
[Fornalha] → [Inseridor] → [Baú]          retira o produto e guarda
```

| Aspecto | Comportamento |
|---|---|
| Frente (destino) | Fixa, definida ao girar com `R`. Pode ser um baú, uma máquina ou uma esteira que **começa** no Inseridor (com a traseira virada para ele). Uma esteira passando de lado na frente não recebe nada. |
| Trás (origem) | Um baú, uma máquina ou uma esteira que termina no Inseridor. **Baús e máquinas só contam atrás.** |
| Laterais | Só **esteiras que terminam no Inseridor** (apontando para ele). Baús e máquinas nas laterais são ignorados. Uma esteira que passa ao lado, sem apontar para ele, também é ignorada: para dividir um fluxo, usa-se o [Divisor](#divisor). |
| Várias entradas | Alterna entre elas em [ordem horária](#ordem-horária). |
| O que ele pega | **Só o que o destino aceita agora**, e só a quantidade que cabe. Por exemplo, de um baú com madeira e minério, um Inseridor que abastece uma fornalha só pega minério, e no máximo o que falta no slot. Por isso ele **nunca trava** com um item errado. |
| Braço | O item viaja **na mão** do Inseridor: o braço pega na origem, gira até o destino, entrega e volta. Se o destino encher no meio do caminho, o item espera na mão até caber. |
| Slots ao entregar em máquina | **Sempre configurável:** Automático, Principal ou Adicional (ver [Slots virtuais](#slots-virtuais)). |
| Slots ao retirar de máquina | Configurável: Produto (padrão), Principal ou Adicional (ver [Retirada](#retirada-inseridor)). De um baú, retira de qualquer posição. |
| Entre Inseridores | Dois Inseridores encostados não se conectam. |

O Inseridor tem **uma interface** com as duas configurações: "Retirar de" (vale quando atrás há uma máquina) e "Colocar em" (vale quando na frente há uma máquina).

### Filtros e roteamento

| Item | Tier | Função |
|---|---|---|
| **Filtro Comum** | 1 | **Bloco de esteira** que só deixa passar **um único item**. |
| **Filtro Completo** | 2 | **Bloco de esteira** totalmente configurável: lista de itens, categoria, qualidade, context tags, lista branca ou lista negra. |
| **Divisor** (Splitter) | 2 | Bloco com **4 portas** (norte, sul, leste, oeste). Toda esteira apontando para ele é uma entrada, e toda esteira saindo dele é uma saída. É o **único** ponto onde fluxos se dividem ou se juntam. |
| **Regulador** (Limiter) | 3 | **Acessório colocado sobre um Inseridor.** O inseridor para de inserir quando o destino atinge o limite configurado. |

**Acessórios:** a Ponte vai sobre uma esteira e o Regulador vai sobre um Inseridor. Cada tile aceita **no máximo um acessório**.

### Filtro

O filtro **não é um acessório**: é um bloco de esteira próprio, que move os itens sozinho como uma esteira, mas só deixa passar o que o filtro aceita.

```
→ → [Filtro: Madeira] → → →
```

- Pode ser colocado sozinho, no meio de uma esteira ou vários em sequência.
- Tem sentido único, como a esteira: entra por trás e sai pela frente.
- O item que **bate** com o filtro entra e segue em frente.
- O item que **não bate** **não entra** e fica parado na esteira anterior. **Não há saída lateral.** Para separar itens, usa-se um Divisor com filtros nas saídas (ver [Divisor](#divisor)).
- Clicar no filtro **abre a interface**, onde o item ou as regras são escolhidos. O item usado para configurar **não é gasto**.
- É **sempre reto**: não faz curva.
- É **sempre rápido**: move o item com a velocidade máxima, para nunca ser o gargalo. Se o tile à frente estiver ocupado (ex.: uma esteira lenta), ele espera liberar.
- Segura **no máximo uma pilha** e **nunca junta itens**: só passa para a frente o que recebeu.

**Filtro acoplado.** Quando um filtro fica **imediatamente encostado** em um Inseridor, na direção do fluxo, os dois passam a funcionar como uma unidade:

```
[Baú] → [Inseridor] → [Filtro: Madeira] → esteira →        só retira Madeira do baú
esteira → [Filtro: Minério] → [Inseridor] → [Fornalha]     só insere Minério na fornalha
```

- **Inseridor + Filtro à frente:** o inseridor só retira do baú ou máquina os itens que o filtro aceita. Assim a esteira não trava com itens rejeitados. É consequência direta da regra "só pega o que o destino aceita".
- **Filtro + Inseridor:** o inseridor só recebe os itens que o filtro deixou passar.
- Vale para todas as versões (básica, de Pilha, Configurável) e para os dois filtros (Comum e Completo).

### Divisor

Também é um bloco que move os itens sozinho. Cada uma das 4 portas vira entrada ou saída conforme a esteira (ou filtro) encostada nela.

```
                  [Filtro: Madeira] → baú de madeira
                          ↑
esteira → → → ──── [Divisor] ──── → esteira (resto)
                          ↓
                  [Filtro: Pedra]   → baú de pedra
```

O Divisor não tem modos. Ele sempre segue estas regras, em ordem:

1. **Prioridade para filtros.** Se alguma saída tem um filtro que aceita o item, o item vai **todo** para ela. Se **várias saídas com filtro** aceitam o item, ele é dividido igualmente entre elas.
2. **Divisão igual.** Se nenhum filtro aceita o item, a pilha é dividida igualmente entre as saídas livres **sem filtro**. Com 3 saídas, divide por 3; com 2, divide por 2 (ex.: 7 viram 3 + 2 + 2).
3. **Pilha menor que o número de saídas** (ex.: 1 item e 3 saídas): um item por vez em cada saída, seguindo a [ordem horária](#ordem-horária).
4. **Saída bloqueada:** o item segue para a **próxima saída livre** que o aceite. Isso vale também para uma saída com filtro bloqueada (ex.: baú de madeira cheio): a madeira vai para as saídas sem filtro como excedente.
5. **Sem destino:** se não há nenhuma saída livre que aceite o item, o Divisor **trava** até alguma liberar. Isso indica um erro de montagem.
6. **Entradas:** com várias entradas, o Divisor **alterna** entre elas, seguindo a ordem horária.

### Regulador

```
esteira → [Filtro: Madeira] → [Inseridor + Regulador: 50] → [Baú]
```

- É um acessório colocado **sobre um Inseridor**. Sozinho não faz nada.
- **Tipos de limite**, escolhidos na interface:
  - **Por item:** limite separado para cada item (ex.: até 50 madeiras e até 100 pedras).
  - **Geral:** limite do total de itens no destino.
- A quantidade é configurada na interface do Regulador, digitando o número.

### Regras de conexão

- Uma esteira tem **um único sentido** e **uma única saída**.
- Esteiras encostadas pela lateral **não interagem**. Uma esteira apontando para a lateral de outra esteira reta não entrega itens nela.
- Só o **Divisor** divide ou junta fluxos.
- **Curvas:** uma esteira que **não tem nada atrás dela** e recebe uma esteira pela lateral vira uma **curva** automaticamente. Se já houver uma esteira atrás, ela continua reta e ignora a lateral.
- **Inseridor:** é o único bloco que recebe de esteiras pela lateral, e só das que **terminam** nele (ver [Inseridores](#inseridores)).

```
Curva (permitida):           Lateral ignorada:
→ → ┐                        → → → → →
    ↓                            ↑
    ↓                            ↑   (não entrega)
```

### Ordem horária

**Decidido:** sempre que um bloco precisa escolher entre várias portas, em rodízio, ele segue a **ordem horária**, vista de cima com o norte para cima. Assim o comportamento é previsível em todo o mod.

```
        N (1)
          ↑
W (4) ←  [ ]  → E (2)
          ↓
        S (3)
```

- Hoje se aplica às **saídas** e às **entradas** do Divisor e às **entradas** do Inseridor. Vale para qualquer item futuro com rodízio.
- O bloco lembra a última porta usada e continua da seguinte.
- A contagem **sempre começa pelo norte** (N → E → S → W), independente de onde está a entrada.

### Ideias para o futuro **[A DEFINIR]**

- **Incinerador**: destrói o que chega nele, útil para descartar lixo da pesca.
- **Integração com a caixa de envio**: vender automaticamente o que chega.
- **Transporte entre locais**: fazenda ↔ estufa ↔ galpão, talvez com temática Junimo.
- **Esteira com prioridade**: a saída preferencial recebe primeiro e as outras só recebem o excedente.
- **Contador/Sensor**: exibe quantos itens passaram por ali.
- **Inseridor rápido**: versões com velocidade maior.
- **Braço mecânico** (inspirado no Mechanical Arm do Create): bloco que alcança vários tiles num raio. Na interface, o jogador escolhe de onde pegar e onde colocar (baús, máquinas, esteiras) e o modo de distribuição entre as saídas: alternar, alternar esperando a próxima ou preferir a primeira. Desenhado com o braço animado girando até cada ponto.
- **Simulação noturna**: calcular o que as esteiras teriam feito durante a noite e entregar o resultado de manhã.
- **Integração com o Automate** e outros mods de automação.

---

## Comportamento das esteiras

Pontos centrais para definir antes de codar, porque determinam o modelo de dados e a renderização.

| Questão | Opções | Decisão |
|---|---|---|
| Itens por tile de esteira | 1 item por tile / vários em fila / duas faixas (estilo Factorio) | **Decidido:** 1 pilha de um único item por tile |
| Movimento | Suave (pixel a pixel) / pula de tile em tile | **Decidido:** suave |
| O que a esteira carrega | Item unitário / pilha (stack) | **Decidido:** pilha |
| Pilhas iguais se juntam na esteira? | Sim / não | **Decidido:** só quando a esteira está **acumulando**. Uma pilha que chega a um tile parado com o **mesmo item** (e mesma qualidade) se junta a ele, até o limite da pilha. Itens diferentes ficam no tile de trás. Em movimento, as pilhas não se juntam. |
| Qualidades diferentes | Mesma pilha / pilhas separadas | **Decidido:** pilhas separadas, como no inventário |
| Pilha chegando em máquina | Fica parada no fim da esteira enquanto o inseridor consome aos poucos | **Decidido:** fica parada no último tile; o restante espera o próximo ciclo da máquina |
| Fim da esteira sem destino | Itens param e acumulam (backpressure) / caem no chão | **Decidido:** param e acumulam |
| Extrator e inseridor | Blocos separados / um bloco só / a própria esteira puxa e empurra | **Decidido:** um bloco só, o [Inseridor](#inseridores), que pega atrás e entrega na frente. Antes eram dois blocos (Extrator e Inseridor) |
| Sentido e saídas | Livre / sentido único com uma saída | **Decidido:** sentido único, uma saída, sem interação lateral (ver [Regras de conexão](#regras-de-conexão)) |
| O jogador pode andar sobre a esteira? | Sim (como piso) / não (como objeto sólido) | **Decidido:** não; usa-se a Ponte |
| Exibição da quantidade da pilha | Número sempre visível / só ao passar o mouse / tamanho do monte varia com a quantidade / opção de configuração | **Decidido:** só ao passar o mouse |
| Ponte em quais esteiras | Todas as velocidades / também em filtro, divisor etc. | **Decidido:** só nas esteiras (todas as velocidades). Não pode no Filtro nem no Divisor. |
| Fora da tela | Simulação completa / simulação simplificada | **Decidido:** a lógica roda em passos fixos em **todos os locais** com esteiras; os itens só são **desenhados** no local onde o jogador está, com o movimento suavizado entre os passos |
| Jogo pausado | Esteiras continuam / pausam | **Decidido:** pausam junto com o tempo do jogo (menus, diálogos, cutscenes) |
| Noite (jogador dormindo) | Esteiras param / simulação rápida da noite | **Decidido:** param. A simulação noturna fica como ideia futura |

## Interação com máquinas

- Na 1.6, as máquinas são definidas em `Data/Machines`. Com isso dá para saber o que cada máquina aceita, inclusive máquinas de outros mods.

### Slots virtuais

No jogo original as máquinas não têm slots: a fornalha consome o minério da mão do jogador e o carvão do inventário. O mod cria **slots virtuais** em cada máquina a partir de `Data/Machines`:

- **Principal:** a entrada da regra da máquina (ex.: 5 minérios de cobre).
- **Adicional:** os `AdditionalConsumedItems` (ex.: 1 carvão).

O mod guarda os itens nos slots. Quando todos estão completos, a máquina é acionada.

**Decidido:** o jogador escolhe em cada inseridor qual slot ele abastece:
- **Automático:** o mod descobre o slot certo para cada item.
- **Principal** ou **Adicional:** forçado. Itens que não servem para aquele slot ficam parados na esteira.

Exemplo, uma fornalha com três inseridores:

```
[Baú de minério] → esteira → [Inseridor: Principal] → [Fornalha] ← [Inseridor: Adicional] ← esteira ← [Baú de carvão]
                                                           ↓
                                                [Inseridor: retirar Produto] → esteira → [Baú de barras]
```

Como o Inseridor só pega o que o destino aceita, um único baú com minério e carvão misturados também funciona: `[Baú misto] → [Inseridor: Automático] → [Fornalha]`.

**Decidido:**
- **Dica ao passar o mouse:** em máquinas ligadas ao sistema, mostra o conteúdo dos slots:
  ```
  Fornalha
  Principal: 3/5 Minério de cobre
  Adicional: 1/1 Carvão
  ```
- **Máquina removida:** os itens guardados nos slots **caem no chão**, como acontece ao quebrar um baú.
- **Uso manual:** o jogo funciona exatamente como no vanilla. Os slots virtuais são só um "estoque" do mod para máquinas abastecidas por inseridores; se o jogador usar a máquina na mão, ela processa o que ele colocou e os itens guardados continuam esperando.
- O layout da interface do Inseridor é livre na alpha.

### Retirada (Inseridor)

**Decidido:** para retirar de uma máquina, ela fica **atrás** do Inseridor. O lado da máquina onde o Inseridor está (norte, sul, leste ou oeste) **não importa**. O slot de retirada é escolhido na interface do Inseridor, em "Retirar de":
- **Produto** (padrão): o que a máquina fabricou.
- **Principal** ou **Adicional:** devolve o que está guardado nos slots virtuais esperando completar (ex.: 3 de 5 minérios).

### Máquinas suportadas

**Decidido:**
- O MVP suporta as máquinas "normais" definidas em `Data/Machines` (fornalha, barril, jarra de conserva, prensa de queijo...), inclusive as de outros mods.
- **Cristalário:** recebe uma gema e a copia para sempre. O Inseridor só coloca uma gema quando ele está **vazio**; depois disso só o Inseridor que retira dele trabalha nele.
- **Covo:** **fora do escopo por enquanto**. Ele fica na água, precisa de isca a cada captura e tem lógica própria, fora de `Data/Machines`.
- Outras máquinas com lógica própria entram aos poucos, caso a caso.

---

## Progressão e compra

Os componentes são **receitas de crafting compradas em loja**. Depois de comprar a receita, o jogador fabrica os itens com materiais.

### Alpha (atual)

**Decidido:** a alpha serve **só para testar a lógica**. Por isso:
- Tudo fica liberado desde o primeiro dia.
- O Clint vende **todos os itens prontos por 0g**, sem limite de quantidade.
- Receitas de crafting, materiais e preços ficam para depois da alpha.
- O modo "tudo liberado" é uma opção de configuração, para que a progressão real possa ser ligada depois sem mexer no código.

### Progressão final

Proposta inicial, com valores a balancear:

| Tier | Loja | Disponível quando | Preço da receita | Materiais (por unidade) |
|---|---|---|---|---|
| 1: Básico | Clint (ferreiro) | Andar 10 da mina **[A DEFINIR]** | ~500g **[A DEFINIR]** | Barra de cobre, madeira **[A DEFINIR]** |
| 2: Intermediário | Clint | Andar 40 da mina **[A DEFINIR]** | ~2.500g **[A DEFINIR]** | Barra de ferro, quartzo refinado **[A DEFINIR]** |
| 3: Avançado | Clint ou Robin **[A DEFINIR]** | Andar 80 da mina ou marco do Centro Comunitário **[A DEFINIR]** | ~10.000g **[A DEFINIR]** | Barra de ouro, barra de irídio **[A DEFINIR]** |

Por que o Clint: ele é o ferreiro, o personagem mais "técnico" da cidade.

**[A DEFINIR]**:
- A liberação deve chegar por carta? Por exemplo, "o Clint mandou uma carta dizendo que tem novidades na loja".
- Cada item tem a própria receita, ou uma compra libera o tier inteiro?
- Quantas unidades cada fabricação produz? Esteiras precisam ser baratas e vir em quantidade (ex.: 1 craft = 10 esteiras).

---

## Escopo técnico

- **Plataforma:** Stardew Valley 1.6+, [SMAPI](https://smapi.io), C# / .NET 6.
- **Modo de jogo:** single-player. Multiplayer fica para o futuro, mas a arquitetura deve evitar decisões que impeçam o suporte depois.
- **Idiomas:** por enquanto, **apenas pt-BR**. Mesmo assim, todos os textos ficam em arquivos `i18n` desde o início, para que adicionar inglês depois seja só traduzir um arquivo.
- **Interfaces:** layout livre na alpha, escolhido durante a implementação.
- **Sprites:** na alpha, **sem sprites**. Os itens são desenhados com formas geométricas e cores (ver abaixo).

### Placeholders visuais (alpha)

| Item | Cor | Forma |
|---|---|---|
| Esteira / Rápida / Expressa | Cinza claro / médio / escuro | Quadrado com **1, 2 ou 3 setas** indicando sentido e velocidade |
| Ponte | Marrom | Faixa horizontal sobre a esteira |
| Esteira Subterrânea | Cinza escuro | Quadrado com círculo: **branco** na entrada e **preto** na saída |
| Inseridor | Azul | Quadrado com um triângulo pequeno na borda do destino e um **braço** (traço escuro com a mão na ponta) que gira da origem até o destino carregando o item. Parado, o braço aponta para a última entrada usada |
| Versões de Pilha / Configurável | Mesma cor do básico | **2 ou 3 pontos** no canto |
| Filtro Comum | Verde | Losango com seta; mostra o ícone do item filtrado |
| Filtro Completo | Verde escuro | Losango duplo com seta |
| Divisor | Roxo | Círculo com 4 saídas |
| Regulador | Amarelo | Quadrado pequeno sobre o Inseridor |

Os itens transportados usam o **ícone normal do jogo**, que já existe.

### Dicas ao passar o mouse

**Decidido:** a quantidade e os detalhes só aparecem ao passar o mouse, tanto nas esteiras quanto nas máquinas:
- **Esteira:** item e quantidade da pilha naquele tile (ex.: "Madeira ×50").
- **Máquina:** conteúdo dos slots virtuais (ver [Slots virtuais](#slots-virtuais)).

## Roadmap

### Alpha: testar a lógica

Tudo com placeholders, liberado e de graça na loja do Clint. O plano detalhado de cada fase está em [`docs/plans/`](docs/plans/00-plano-mestre.md). As interfaces de configuração são feitas dentro da fase de cada item.

0. **Estrutura:** solução, projetos, testes, CI e guia de desenvolvimento.
1. **Base:** esteira (reta e curva), Extrator e Inseridor básicos; baú → esteira → baú funcionando visualmente.
2. **Máquinas:** slots virtuais, Inseridor entregando e Extrator retirando produtos.
3. **Inseridor único:** Extrator e Inseridor viram um bloco só, com entradas laterais, entrega direta (baú → máquina) e o braço com o item na mão.
4. **Salvar/carregar:** itens em trânsito, itens na mão dos Inseridores e slots virtuais persistem no save.
5. **Roteamento:** Filtros (Comum e Completo), filtro acoplado e Divisor.
6. **Restante dos itens:** Ponte, esteiras Rápida e Expressa, Inseridores de Pilha e Configurável, Esteira Subterrânea e Regulador.
7. **Dicas ao passar o mouse**, modo de depuração e versão 0.1.0-alpha.

### Beta: virar um mod jogável

8. **Progressão:** receitas, materiais, preços e condições de liberação na loja do Clint.
9. **Sprites** finais (inclusive a animação do braço do Inseridor) e sons.
10. **Tradução** para inglês e configuração (Generic Mod Config Menu).

### Futuro

11. Multiplayer, transporte entre locais, simulação noturna, braço mecânico, integração com outros mods.

## Referências

- [Wiki de Modding do Stardew Valley](https://stardewvalleywiki.com/Modding:Index)
- [Guia inicial de mods SMAPI](https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started)
- [Código do Automate](https://github.com/Pathoschild/StardewMods/tree/develop/Automate) (referência de integração com máquinas)
