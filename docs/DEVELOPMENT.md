# Desenvolvimento local

Guia para compilar o mod, instalá-lo no jogo e rodar os testes no **Linux**.

O repositório tem três projetos:

| Projeto | O que é | Compila sem o jogo? |
|---|---|---|
| `src/PelicanLogistics.Core` | Lógica pura (esteiras, filtros, divisores...), sem nenhuma referência ao jogo | Sim |
| `src/PelicanLogistics` | Mod SMAPI: camada fina que liga o Core ao jogo | **Não**: precisa do jogo e do SMAPI instalados |
| `tests/PelicanLogistics.Core.Tests` | Testes automatizados do Core (xUnit) | Sim |

O CI do GitHub compila e testa apenas o Core. O mod só é compilado na sua máquina.

---

## 1. Pré-requisitos

### Stardew Valley 1.6+

Pela Steam ou pelo GOG.

### SMAPI 4.x

1. Baixe a versão mais recente em <https://smapi.io>.
2. Extraia o `.zip` e rode o instalador para Linux:
   ```bash
   bash "install on Linux.sh"
   ```
3. **Steam:** o instalador mostra o que colocar nas *Opções de inicialização* do jogo (Propriedades → Geral). Normalmente é algo como:
   ```
   "/caminho/para/Stardew Valley/StardewModdingAPI" %command%
   ```
   Use exatamente o caminho que o instalador indicar.
4. **GOG:** abra o jogo pelo executável `StardewModdingAPI` na pasta do jogo.

### .NET SDK 8

Qualquer uma das opções:

- **Script oficial** (sem `sudo`):
  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"   # acrescente ao ~/.bashrc para ficar permanente
  ```
- **Pacotes da Microsoft para Debian 12:**
  ```bash
  wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
  sudo dpkg -i packages-microsoft-prod.deb
  rm packages-microsoft-prod.deb
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-8.0
  ```

Confira com `dotnet --version` (deve começar com `8.`).

> O mod mira `net6.0`, porque o Stardew Valley 1.6 roda em .NET 6. O SDK 8 compila `net6.0` normalmente.

---

## 2. Localizar o jogo

O pacote `Pathoschild.Stardew.ModBuildConfig` procura o jogo nos caminhos padrão. Se a compilação reclamar que não encontrou o jogo, crie o arquivo `~/stardewvalley.targets`:

```xml
<Project>
  <PropertyGroup>
    <GamePath>/caminho/para/Stardew Valley</GamePath>
  </PropertyGroup>
</Project>
```

Caminhos comuns:

| Instalação | Caminho |
|---|---|
| Steam | `~/.steam/steam/steamapps/common/Stardew Valley` |
| Steam (alternativo) | `~/.local/share/Steam/steamapps/common/Stardew Valley` |
| Steam (Flatpak) | `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Stardew Valley` |
| GOG | `~/GOG Games/Stardew Valley/game` |

---

## 3. Compilar e instalar o mod

Na raiz do repositório:

```bash
dotnet build src/PelicanLogistics
```

O `ModBuildConfig` copia o mod compilado para `Mods/PelicanLogistics`, dentro da pasta do jogo. A pasta deve conter, entre outros arquivos:

- `PelicanLogistics.dll`
- `PelicanLogistics.Core.dll`
- `manifest.json`
- `i18n/`

> Compile o projeto `src/PelicanLogistics` (ou a solução inteira). Compilar só o Core não instala nada no jogo.

---

## 4. Rodar os testes do Core

```bash
dotnet test tests/PelicanLogistics.Core.Tests
```

Os testes não precisam do jogo.

---

## 5. Abrir o jogo

Abra o jogo pelo SMAPI (Steam com as opções de inicialização configuradas, ou o executável `StardewModdingAPI`). No console do SMAPI deve aparecer:

```
Pelican Logistics 0.1.0-alpha carregado.
```

Na primeira execução, o SMAPI cria `Mods/PelicanLogistics/config.json` com as opções do mod (`UnlockAll`, `RotateKey`, `DebugMode`).

---

## 6. Logs

O log completo da última execução fica em:

```
~/.config/StardewValley/ErrorLogs/SMAPI-latest.txt
```

Para pedir ajuda ou reportar um problema, anexe esse arquivo (ou envie para <https://smapi.io/log> e compartilhe o link).

---

## 7. Como reportar o resultado de uma fase

Depois de seguir o checklist de "Verificação local" do PR da fase, registre o resultado em [`docs/plans/STATUS.md`](plans/STATUS.md):

1. Na tabela, preencha a coluna **Teste local** e ajuste o **Status** (`Concluída` ou `Com pendências`).
2. Em **Pendências para a próxima fase**, anote erros de compilação, bugs no jogo e ajustes, no formato indicado lá. A próxima fase começa resolvendo esses itens.

---

## 8. Comandos de console do mod

Digite no console do SMAPI (com um save carregado).

| Comando | O que faz | Desde |
|---|---|---|
| `pl_give` | Coloca no inventário 10 de cada item do mod (Esteira, Inseridor, Filtro Comum, Filtro Completo, Divisor). Útil quando a loja do Clint está fechada. | Fase 1 |
| `pl_dump` | Lista os blocos do mod no local atual: posição, tipo, direção, lado de entrada (esteiras) e, para o Inseridor, `source=` (Retirar de), `slot=` (Colocar em), `cooldown=` (ticks da volta do braço), `lastInput=` (última entrada usada) e `inputs=` (entradas). Para o Filtro, `rule=` (regra). Para o Divisor, `inputs=`, `outputs=`, `lastInput=` e `lastOutput=` (o `facing=` do Divisor não tem significado). Para cada espaço (o da esteira, do filtro, do Divisor, ou a mão do Inseridor em `hand=`): item, quantidade, progresso (`0..600`), lado de entrada e se está `blocked`. | Fase 1 (Inseridor: Fase 3; Filtro e Divisor: Fase 5) |
| `pl_inspect` | Mostra o bloco sob o cursor (como uma linha do `pl_dump`) e, se o tile for uma máquina, os slots virtuais (`Principal: 3/5 Minério de cobre`, `Adicional: 1/1 Carvão`) e o estado da máquina. | Fase 2 |

Exemplo de saída do `pl_inspect` sobre uma fornalha:

```
Máquina: Fornalha em (64, 15)
  Principal: 3/5 Minério de cobre
  Adicional: 1/1 Carvão
  Ociosa: True · Produto pronto: - · Repete sozinha: False
```

Exemplo de saída do `pl_dump`:

```
3 bloco(s) em Farm:
  (60, 15) Inserter facing=East tier=Single source=Product slot=Auto cooldown=0 lastInput=West inputs=North,West hand=[Madeira x1 240/600 entry=West]
  (61, 15) Belt facing=East tier=Basic input=West [Madeira x1 340/600 entry=West]
  (62, 15) Belt facing=South tier=Basic input=West [vazio]
```

No Inseridor, o progresso da mão vai de `0` (na origem) a `600` (no destino) em 15 ticks; depois de entregar, `cooldown` conta os 15 ticks da volta. `blocked` na mão indica que o destino encheu e o item espera para entrar.

Filtro e Divisor no `pl_dump`:

```
  (61, 15) Filter facing=East rule=Common((O)388) [Madeira x1 320/600 entry=West]
  (62, 15) Filter facing=East rule=Complete(Blacklist items=(O)388) [vazio]
  (63, 15) Splitter facing=North inputs=West outputs=North,East,South lastInput=West lastOutput=East [Pedra x3 600/600 entry=West blocked]
```

No Divisor, `blocked` indica que nenhuma saída livre aceita o item (regra 5 do README: erro de montagem).

---

## 9. Usando os blocos (alpha)

- **Rotação:** segurando um bloco do mod, `R` (configurável em `RotateKey`) gira a direção de colocação; uma seta semitransparente mostra a direção no tile do cursor. Com a mão livre e o cursor sobre um bloco colocado, `R` gira o bloco.
- **Inseridor:** pega **atrás** e entrega **na frente** (o triângulo escuro na borda do quadrado azul aponta para o destino). Exemplos: baú → Inseridor → esteira; esteira → Inseridor → fornalha; baú → Inseridor → fornalha (direto); fornalha → Inseridor → baú.
  - **Frente (destino):** baú, máquina, Divisor, ou esteira/filtro que começa nele (com a traseira virada para ele, ou fazendo curva saindo dele). Outro Inseridor na frente não recebe.
  - **Trás (origem):** baú, máquina, Divisor ou esteira/filtro que termina nele. **Laterais:** só esteiras que **terminam** nele; baús e máquinas nas laterais e esteiras passando ao lado são ignorados.
  - **Várias entradas:** alterna entre elas em ordem horária (N → L → S → O), continuando depois da última usada.
  - **Só pega o que o destino aceita**, e só a quantidade que cabe: de um baú com madeira e minério, abastecendo uma fornalha, só sai minério, e no máximo o que falta no slot.
  - **Braço:** o item viaja na mão; o braço gira da origem até o destino (0,25 s), entrega e volta vazio (0,25 s). Se o destino encher no caminho, o item espera na mão. Parado, o braço aponta para a última entrada usada.
- **Interface:** clique com o botão de ação (direito) num Inseridor. **Retirar de** (Produto, Principal ou Adicional) vale quando atrás há uma máquina; **Colocar em** (Automático, Principal ou Adicional) vale quando na frente há uma máquina. A escolha vale na hora. Um bloco recolhido e colocado de novo volta à configuração padrão (vale também para os filtros). O botão OK fica no canto de cima da interface; `Esc` também fecha.
- **Máquinas:** o Inseridor guarda os itens nos **slots virtuais** da máquina (Principal e Adicional) e a aciona quando tudo está completo. Quebrar a máquina solta no chão os itens guardados. Covo e Barril de carvalho não são suportados na alpha.
- **Esteira:** só recebe por trás. Uma esteira sem nada atrás e com outra (ou um Inseridor, ou um Divisor) entregando pela lateral vira curva, desenhada com um traço em L da lateral de onde recebe até a frente.
- **Filtro Comum e Filtro Completo:** blocos de esteira **sempre retos** (entram por trás, saem pela frente) e **rápidos** (4 tiles/s). Só entra o que o filtro aceita; o resto fica parado no bloco anterior, sem saída lateral. Seguram uma pilha por vez e nunca juntam itens. Giram com `R`.
  - **Interface:** botão de ação sobre o filtro. **Filtro Comum:** clique num item do seu inventário para escolhê-lo (o item **não** sai do inventário); "Limpar" esvazia. Compara só o item: qualquer qualidade passa. O ícone do item aparece pequeno no centro do bloco. Filtro vazio não deixa passar nada.
  - **Filtro Completo:** **Modo** (lista branca: passa o que corresponde; lista negra: passa o que não corresponde), **Itens** (até 12; clique no inventário para adicionar e na lista para remover), **Qualidade**, **Categorias** e **Context tags** (separadas por vírgula, ex.: `color_red, season_spring`). Dentro de um grupo vale **ou**; entre grupos vale **e** (ex.: item Chirívia + qualidade Ouro = só chirívias ouro). Lista branca vazia não deixa passar nada; lista negra vazia deixa passar tudo.
  - **Categorias:** são as do jogo, uma por opção. Atenção: o jogo mostra "Mineral" para duas categorias diferentes; o **quartzo** e as gemas (diamante, rubi...) ficam em **Gemas**, e os minerais dos geodos (alamita, jaspe...) em **Minerais**.
  - **Filtro acoplado:** um Inseridor com um filtro logo à frente só retira o que o filtro aceita (o baú misturado não trava a linha). Um filtro logo atrás do Inseridor só entrega a ele o que aceita.
- **Divisor:** bloco sem direção (`R` não faz nada nele) com 4 portas. Cada esteira, filtro ou Inseridor apontando para ele é uma **entrada**; cada esteira ou filtro saindo dele (com o Divisor atrás, ou sem nada atrás e com o Divisor na lateral, fazendo curva) e cada Inseridor com a traseira virada para ele é uma **saída** (ex.: baú → Inseridor → esteira → Divisor → Inseridor → fornalha). Um Inseridor de saída recebe só o que pegaria naquele momento (1 item no básico, e só o que o destino dele aceita); o resto fica no Divisor e sai no próximo tick. Outros Divisores encostados, e Inseridores com o Divisor na lateral, não se conectam. Regras, em ordem:
  1. Saídas com filtro que aceita o item recebem tudo (divididas igualmente entre elas, se houver mais de uma).
  2. Senão, a pilha é dividida igualmente entre as saídas livres sem filtro (7 → 3 + 2 + 2).
  3. Pilha menor que o número de saídas: um item por saída, em ordem horária.
  4. Saída ocupada é pulada; um filtro ocupado (ex.: baú de madeira cheio) manda o excedente para as saídas sem filtro.
  5. Nenhuma saída livre aceita o item: o Divisor trava até alguma liberar.
  6. Várias entradas: alterna entre elas. Entradas e saídas seguem a ordem horária (N → L → S → O), começando pelo norte e continuando depois da última usada.
- **Remover:** quebre o bloco com a picareta; ele e o item que carregava (na esteira ou na mão do Inseridor) caem no chão.
- **Salvar e carregar:** os itens nas esteiras, nos filtros, nos Divisores, na mão dos Inseridores e nos slots virtuais das máquinas continuam onde estavam (ver a seção 10).

---

## 10. Como o mod salva

O jogo salva sozinho os blocos colocados e o `modData` deles: direção, "Retirar de" e "Colocar em" do Inseridor e a regra dos filtros (em JSON, na chave `spirandev.PelicanLogistics/FilterRule`). O resto (o **estado em tempo de execução**) é salvo pelo mod desde a Fase 4, em duas partes:

- **Estado em JSON**, nos dados do mod dentro do save (`Helper.Data.WriteSaveData`, chave `runtime-state`): para cada local, cada bloco com a posição, o tipo, o progresso, o lado de entrada e se está bloqueado de cada espaço, a volta do braço e a última entrada do Inseridor, a última entrada e a última saída do Divisor; e os slots virtuais de cada máquina. Os itens **não** vão no JSON: cada espaço guarda só o **índice** do item na lista abaixo.
- **Itens num inventário global do jogo** (`spirandev.PelicanLogistics/Transit`), que o próprio jogo grava no save com segurança (anéis, ferramentas, itens com sabor etc.).

O fluxo:

1. **Ao salvar** (`GameLoop.Saving`, também ao dormir): o mod gera o JSON, coloca os itens no inventário global na ordem dos índices e grava o JSON. Os itens continuam nas esteiras; o inventário é só o "veículo" para o arquivo.
2. **Depois de salvar** (`GameLoop.Saved`): o inventário global é esvaziado, para nenhuma função do jogo que percorre todos os itens enxergar cópias.
3. **Ao carregar** (`GameLoop.SaveLoaded`): o mod recria os blocos a partir dos objetos do mapa, devolve cada item ao seu espaço pelo índice e esvazia o inventário global.

**Itens órfãos.** Nenhum item é perdido em silêncio. Cada caso é registrado no log do SMAPI como aviso (`WARN`):

| Situação ao carregar | O que acontece com os itens |
|---|---|
| O bloco não existe mais naquele tile, ou é de outro tipo | Caem no chão, no tile do bloco |
| Slots virtuais de uma máquina que não existe mais | Caem no chão, no tile da máquina |
| O local não existe mais | Caem no chão perto da porta da casa da fazenda |
| Itens no inventário sem estado, ou que nenhum espaço usou | Caem no chão perto da porta da casa da fazenda |
| Versão do estado desconhecida | Nada é restaurado; todos caem no chão onde estavam |
| Erro ao restaurar (`ERROR` no log) | Os blocos ficam vazios e todos os itens caem perto da casa da fazenda |

Se a gravação falhar (`ERROR` no log), o jogo salva assim mesmo: os itens vão para o inventário global sem o estado e, ao carregar, caem perto da casa da fazenda.

Desinstalar o mod com itens em trânsito não é suportado na alpha: os blocos viram itens de erro do jogo e os itens do inventário global ficam inacessíveis no save.
