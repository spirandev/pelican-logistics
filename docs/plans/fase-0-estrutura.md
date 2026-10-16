# Fase 0: Estrutura do projeto

> Leia antes: [`00-plano-mestre.md`](00-plano-mestre.md) (obrigatório), [`README.md`](../../README.md), [`STATUS.md`](STATUS.md).

- **Branch:** `fase-0-estrutura`
- **PR:** `Fase 0: Estrutura do projeto`

## Objetivo

Criar o esqueleto do repositório: solução .NET, os três projetos (Core, mod, testes), manifest do SMAPI, i18n, configuração, CI e a documentação de desenvolvimento local. Ao final, o mod **carrega no jogo** e só escreve uma mensagem no console do SMAPI. Nenhuma mecânica é implementada.

## Fora de escopo

- Qualquer item, esteira ou lógica de jogo.
- Texturas, Harmony patches, eventos além de `GameLaunched`.

---

## Passo 1: Ambiente

1. Rode `dotnet --list-sdks`.
2. Se não houver SDK **8.x**, instale com o script oficial, sem `sudo`:
   ```bash
   curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
   bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
   export PATH="$HOME/.dotnet:$PATH"
   ```
3. Confirme com `dotnet --version`. Registre a versão usada no PR.

> O Core mira `net6.0` porque o Stardew Valley 1.6 roda em .NET 6. O SDK 8 compila `net6.0` normalmente (pode emitir aviso de fim de suporte do .NET 6; suprima apenas esse aviso com `<CheckEolTargetFramework>false</CheckEolTargetFramework>`).

## Passo 2: Solução e projetos

Na raiz do repositório:

```bash
dotnet new sln -n PelicanLogistics
dotnet new classlib -n PelicanLogistics.Core -o src/PelicanLogistics.Core -f net6.0
dotnet new classlib -n PelicanLogistics -o src/PelicanLogistics -f net6.0
dotnet new xunit -n PelicanLogistics.Core.Tests -o tests/PelicanLogistics.Core.Tests -f net8.0
dotnet sln add src/PelicanLogistics.Core src/PelicanLogistics tests/PelicanLogistics.Core.Tests
dotnet add tests/PelicanLogistics.Core.Tests reference src/PelicanLogistics.Core
dotnet add src/PelicanLogistics reference src/PelicanLogistics.Core
```

Apague os arquivos `Class1.cs` e `UnitTest1.cs` gerados.

### 2.1 `src/PelicanLogistics.Core/PelicanLogistics.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <RootNamespace>PelicanLogistics.Core</RootNamespace>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

O Core **não pode ter nenhum `PackageReference`** nesta fase.

### 2.2 `src/PelicanLogistics/PelicanLogistics.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <RootNamespace>PelicanLogistics</RootNamespace>
    <AssemblyName>PelicanLogistics</AssemblyName>
    <EnableHarmony>true</EnableHarmony>
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Pathoschild.Stardew.ModBuildConfig" Version="X.Y.Z" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PelicanLogistics.Core\PelicanLogistics.Core.csproj" />
  </ItemGroup>
</Project>
```

- Substitua `X.Y.Z` pela **versão estável mais recente 4.x** do pacote `Pathoschild.Stardew.ModBuildConfig` no NuGet (consulte `https://api.nuget.org/v3-flatcontainer/pathoschild.stardew.modbuildconfig/index.json`). Registre a versão no PR.
- **Não** rode `dotnet build` neste projeto: ele exige o jogo instalado.

### 2.3 `tests/PelicanLogistics.Core.Tests/PelicanLogistics.Core.Tests.csproj`

Mantenha o gerado pelo template, garantindo:
- `<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<LangVersion>10</LangVersion>` (ou superior);
- pacotes `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` (versões estáveis atuais);
- **não** adicione FluentAssertions nem outras bibliotecas de asserção.

## Passo 3: Arquivos do mod

### 3.1 `src/PelicanLogistics/manifest.json`

```json
{
  "Name": "Pelican Logistics",
  "Author": "spirandev",
  "Version": "0.1.0-alpha",
  "Description": "Esteiras, filtros e divisores para automatizar a fazenda.",
  "UniqueID": "spirandev.PelicanLogistics",
  "EntryDll": "PelicanLogistics.dll",
  "MinimumApiVersion": "4.0.0",
  "UpdateKeys": []
}
```

### 3.2 `src/PelicanLogistics/i18n/default.json`

Os textos ficam em pt-BR no `default.json` (é o idioma de fallback do SMAPI). Quando houver inglês, o inglês vai para `default.json` e o português para `pt.json`.

```json
{
  "mod.loaded": "Pelican Logistics {{version}} carregado."
}
```

### 3.3 `src/PelicanLogistics/ModConfig.cs`

```csharp
using StardewModdingAPI.Utilities;

namespace PelicanLogistics;

/// <summary>Configuração do mod, lida de config.json.</summary>
public sealed class ModConfig
{
    /// <summary>Alpha: todos os itens à venda por 0g na loja do Clint.</summary>
    public bool UnlockAll { get; set; } = true;

    /// <summary>Tecla para girar a direção de colocação ou o bloco sob o cursor.</summary>
    public KeybindList RotateKey { get; set; } = KeybindList.Parse("R");

    /// <summary>Mostra informações de depuração (usado a partir da Fase 6).</summary>
    public bool DebugMode { get; set; } = false;
}
```

### 3.4 `src/PelicanLogistics/ModEntry.cs`

```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace PelicanLogistics;

/// <summary>Ponto de entrada do mod.</summary>
public sealed class ModEntry : Mod
{
    private ModConfig config = null!;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Monitor.Log(
            this.Helper.Translation.Get("mod.loaded", new { version = this.ModManifest.Version }),
            LogLevel.Info);
    }
}
```

## Passo 4: Primeiro código do Core: geometria

Crie em `src/PelicanLogistics.Core/Geometry/`:

### 4.1 `Direction.cs`

```csharp
namespace PelicanLogistics.Core.Geometry;

/// <summary>Direções cardeais. A ordem dos valores é a ORDEM HORÁRIA usada em todo o mod (README: "Ordem horária").</summary>
public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}
```

### 4.2 `DirectionExtensions.cs`

Métodos estáticos de extensão:
- `Direction Opposite(this Direction d)`
- `Direction Clockwise(this Direction d)`: N→E→S→W→N
- `Direction CounterClockwise(this Direction d)`
- `(int Dx, int Dy) ToOffset(this Direction d)`: **o eixo Y do jogo cresce para baixo**, então North = (0, -1), East = (1, 0), South = (0, 1), West = (-1, 0)
- `static IReadOnlyList<Direction> ClockwiseFrom(Direction start)`: as 4 direções em ordem horária começando por `start`
- `static readonly IReadOnlyList<Direction> All`: N, E, S, W

### 4.3 `TilePos.cs`

```csharp
namespace PelicanLogistics.Core.Geometry;

/// <summary>Posição de um tile no mapa de um local do jogo.</summary>
public readonly record struct TilePos(int X, int Y)
{
    public TilePos Offset(Direction direction) { /* usa ToOffset */ }

    /// <summary>Direção de 'this' para um vizinho adjacente, ou null se não for adjacente.</summary>
    public Direction? DirectionTo(TilePos neighbor) { /* ... */ }

    public override string ToString() => $"({this.X}, {this.Y})";
}
```

### 4.4 Testes

Em `tests/PelicanLogistics.Core.Tests/Geometry/`:
- `DirectionExtensionsTests`: `Opposite` das 4 direções; `Clockwise` aplicado 4 vezes volta ao início; `ClockwiseFrom(North)` = N, E, S, W; `ClockwiseFrom(West)` = W, N, E, S; offsets das 4 direções.
- `TilePosTests`: `Offset` em cada direção; `DirectionTo` para os 4 vizinhos; `DirectionTo` retorna `null` para a própria posição, diagonais e tiles distantes.

## Passo 5: Arquivos de apoio

### 5.1 `.editorconfig` (raiz)

Configuração básica para C#: indentação de 4 espaços, `charset = utf-8`, final de linha `lf`, `insert_final_newline = true`, `csharp_style_namespace_declarations = file_scoped:warning`, `dotnet_style_qualification_for_field = true:suggestion` (o projeto usa `this.`).

### 5.2 `.gitignore`

Já existe. Acrescente `TestResults/` e `*.received.*` se não estiverem lá.

### 5.3 `CHANGELOG.md` (raiz)

```markdown
# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [Unreleased]

### Adicionado
- Estrutura do projeto: Core, mod SMAPI, testes e CI.
```

### 5.4 `.github/workflows/ci.yml`

Workflow que roda em `push` e `pull_request`:
- `runs-on: ubuntu-latest`
- `actions/checkout@v4`
- `actions/setup-dotnet@v4` com `dotnet-version: 8.0.x`
- `dotnet build src/PelicanLogistics.Core -c Release`
- `dotnet test tests/PelicanLogistics.Core.Tests -c Release`

Comente no arquivo que o projeto do mod não é compilado no CI porque depende do jogo.

### 5.5 `docs/DEVELOPMENT.md`

Guia **para o usuário** compilar e testar localmente no Linux. Conteúdo:

1. **Pré-requisitos**
   - Stardew Valley 1.6+ (Steam ou GOG).
   - SMAPI 4.x: baixar de https://smapi.io e rodar o instalador para Linux (`install on Linux.sh`). Na Steam, configurar as opções de inicialização conforme o instalador indicar.
   - .NET SDK 8: pelo script `dotnet-install.sh` (mesmo comando do Passo 1) ou pelos pacotes da Microsoft para Debian 12.
2. **Localizar o jogo**: o `ModBuildConfig` procura o jogo nos caminhos padrão. Se não encontrar, criar `~/stardewvalley.targets`:
   ```xml
   <Project>
     <PropertyGroup>
       <GamePath>/caminho/para/Stardew Valley</GamePath>
     </PropertyGroup>
   </Project>
   ```
   Caminhos comuns: `~/.steam/steam/steamapps/common/Stardew Valley`, `~/.local/share/Steam/steamapps/common/Stardew Valley`, Flatpak: `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Stardew Valley`.
3. **Compilar e instalar**: `dotnet build src/PelicanLogistics`. O `ModBuildConfig` copia o mod para `Mods/PelicanLogistics` dentro da pasta do jogo.
4. **Rodar os testes do Core**: `dotnet test tests/PelicanLogistics.Core.Tests`.
5. **Abrir o jogo** pelo SMAPI e conferir no console a mensagem `Pelican Logistics 0.1.0-alpha carregado.`
6. **Logs**: `~/.config/StardewValley/ErrorLogs/SMAPI-latest.txt`.
7. **Como reportar o resultado de uma fase**: preencher o `docs/plans/STATUS.md` (ver formato lá).
8. **Comandos de console do mod**: seção vazia "(adicionados nas próximas fases)", que cada fase vai preencher.

## Passo 6: Verificação na nuvem

```bash
dotnet build src/PelicanLogistics.Core -c Release
dotnet test tests/PelicanLogistics.Core.Tests -c Release
```

Ambos sem erros. Não compile `src/PelicanLogistics`.

## Passo 7: Entrega

- Atualize `STATUS.md`: Fase 0 → "Aguardando teste local", branch e PR.
- Commits sugeridos:
  1. `chore: solução e projetos .NET`
  2. `feat(core): Direction e TilePos com testes`
  3. `feat(mod): manifest, ModEntry, config e i18n`
  4. `ci: compila e testa o Core`
  5. `docs: guia de desenvolvimento local e changelog`
- Abra o PR.

---

## Critérios de aceite (nuvem)

- [ ] `dotnet build src/PelicanLogistics.Core` e `dotnet test tests/PelicanLogistics.Core.Tests` passam.
- [ ] O Core não referencia nenhum pacote nem namespace do jogo.
- [ ] `manifest.json`, `i18n/default.json`, `ModConfig.cs`, `ModEntry.cs` existem e batem com este plano.
- [ ] `docs/DEVELOPMENT.md`, `CHANGELOG.md`, `.editorconfig` e `ci.yml` existem.

## Verificação local (usuário)

- [ ] `dotnet build src/PelicanLogistics` compila sem erros.
- [ ] A pasta `Mods/PelicanLogistics` foi criada dentro da pasta do jogo, com `PelicanLogistics.dll`, `PelicanLogistics.Core.dll`, `manifest.json` e `i18n/`.
- [ ] Ao abrir o jogo pelo SMAPI, o console mostra `Pelican Logistics 0.1.0-alpha carregado.`
- [ ] Um arquivo `config.json` foi criado em `Mods/PelicanLogistics` com `UnlockAll`, `RotateKey` e `DebugMode`.
- [ ] Nenhum erro do mod no log do SMAPI.
