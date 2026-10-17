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
| `pl_give` | Coloca no inventário 10 de cada item do mod (Esteira, Extrator, Inseridor). Útil quando a loja do Clint está fechada. | Fase 1 |
| `pl_dump` | Lista os blocos do mod no local atual: posição, tipo, direção, lado de entrada (esteiras), cooldown (Extrator/Inseridor) e, para cada espaço, item, quantidade, progresso (`0..600`), lado de entrada e se está `blocked`. | Fase 1 |

Exemplo de saída do `pl_dump`:

```
3 bloco(s) em Farm:
  (60, 15) Extractor facing=East tier=Single cooldown=12
  (61, 15) Belt facing=East tier=Basic input=West [Madeira x1 340/600 entry=West]
  (62, 15) Belt facing=South tier=Basic input=West [vazio]
```

---

## 9. Usando os blocos (alpha)

- **Rotação:** segurando um bloco do mod, `R` (configurável em `RotateKey`) gira a direção de colocação; uma seta semitransparente mostra a direção no tile do cursor. Com a mão livre e o cursor sobre um bloco colocado, `R` gira o bloco.
- **Extrator:** o baú fica **atrás** dele e a esteira **na frente** (o triângulo laranja aponta para a esteira).
- **Inseridor:** a esteira fica **atrás** dele e o baú **na frente** (o triângulo azul aponta para o baú).
- **Esteira:** só recebe por trás. Uma esteira sem nada atrás e com outra entregando pela lateral vira curva.
- **Remover:** quebre o bloco com a picareta; ele e o item que carregava caem no chão.
- **Limitação da Fase 1:** os itens em trânsito somem ao salvar e carregar o jogo (a Fase 3 resolve).
