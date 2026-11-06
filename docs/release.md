# Processo de release

**Português (Brasil)** · [English](en/release.md) · [Español](es/release.md)

O processo comum de release (Release PR, versão, tag, `release-manifest.json`,
republicação e correção de release defeituosa) é o do Altivy.Groundwork, descrito
no runbook [release](runbooks/release.md) e em [CI-CD](../.github/CI-CD.md). Este
documento cobre o que é específico do PixelPointer: o
[`publish.yml`](../.github/workflows/publish.yml) publica o `pixelpointer` em
Native AOT para todos os RIDs suportados. Homebrew e winget são distribuídos por
dispatches independentes que exigem uma stable exata (`vX.Y.Z`); `post-release.yml`
não inicia nem autoriza esses canais. Cada etapa tem um comando local equivalente
em `tools/scripts/`.

## Código de saída dos testes de instaladores

O runner `tools/scripts/Test-Installers.ps1` encerra com `0` quando todos os
cenários passam, mesmo que o último cenário teste uma recusa esperada com código
nativo `1`. Falhas de cenário continuam retornando `1`; erros de uso retornam `2`.
A mensagem “Instaladores aprovados” não substitui a verificação do código de saída.

Na raiz do clone, em um container de validação com PowerShell 7, execute:

```powershell
pwsh -NoProfile -File tools/scripts/Test-InstallerExitCode.ps1
```

O teste não exige pacotes nem servidor HTTP e usa somente processos e arquivos
temporários. Deve aprovar quatro combinações de código nativo anterior e contagem
de falhas, encerrando com `0`. Ele também integra os modos container e `-Native`
de `Invoke-Validation.ps1`; não substitui os testes reais dos instaladores por SO.
Se uma candidata falhar por esse problema, integre a correção, atualize o Release
PR e gere outra `rc.N+1`, sem mover a tag anterior.

## Candidata obrigatória (release preview)

Toda estável `vX.Y.Z` é precedida por uma candidata `vX.Y.Z-rc.N`, validada no
Environment `quality` (estratégia completa em
[estrategia-de-release](estrategia-de-release.md) e passo a passo em
[runbooks/release-preview](runbooks/release-preview.md)). O check `Preview gate`, dentro
do `Result`, impede o merge do Release PR `release-please--branches--main` até que a
candidata do commit-base dele esteja aceita.

1. Com o Release PR aberto, dispare a candidata na `main`:

   ```bash
   gh workflow run gw-preview.yml --repo lzocateli/PixelPointer --ref main
   ```

2. O workflow cria a tag e o rascunho privado, compila os seis RIDs, roda smoke e
   testes dos instaladores, gera SBOMs/checksums e anexa um artifact privado
   `release-assets` ao run. Ainda não publica os assets nem a prerelease.
3. Abra o run pelo link do Environment `distribution`, baixe `release-assets`, confira
   `SHA256SUMS` e teste os pacotes nas máquinas de teste (seção
   [Usar um pacote preview](#usar-um-pacote-preview)). `pixelpointer --version`
   deve mostrar `X.Y.Z-rc.N`. Aprove `distribution` somente depois desse teste; o
   workflow publica os mesmos artifacts, sem recompilar, como **Pre-release** sem
   `Latest`, com o `release-manifest.json` atestado. Homebrew e winget não são atualizados.
4. Aprove o Environment `quality` no deploy disparado pelo `gw-preview.yml` somente
   depois de testar. O [`deploy.yml`](../.github/workflows/deploy.yml) confere que os
   assets publicados são os do manifesto e roda o smoke do pacote `linux-x64`; com
   sucesso, o aceite é anexado à prerelease (`preview-acceptance.json`) e o CI do
   Release PR é reexecutado. Esse aceite `quality` continua separado da autorização
   `distribution` até a consolidação do fluxo ser comprovada.
5. Com o `Preview gate` verde, faça o merge do Release PR. A estável é o mesmo código
   da candidata, e as notas dela trazem "Validada na `vX.Y.Z-rc.N`".

Uma correção depois da candidata gera `rc.N+1` no próximo disparo. Em emergência, um
commit com o rodapé `Preview-Waiver: <motivo>` dispensa a candidata; o motivo aparece
nas notas da release.

## Preview manual (`preview.N`)

A preview manual da `main` continua disponível para testar uma mudança antes do
Release PR. Ela **não** é candidata: nunca libera o `Preview gate`, nunca é implantável
e nunca é promovida.

```bash
gh workflow run publish.yml --repo lzocateli/PixelPointer --ref main -f preview=true
gh run list --repo lzocateli/PixelPointer --workflow publish.yml
```

Após build, smoke, testes de instaladores e atestações de SBOM passarem, o job
`Publish GitHub Pre-release` aguarda sua aprovação no Environment `distribution`.
O link do Environment abre o run validado. Depois da aprovação, o GitHub App
`Altivy Release` cria a tag `vX.Y.Z-preview.N` no commit validado, prepara uma release
em rascunho, atesta a proveniência e anexa os mesmos 14 assets e `SHA256SUMS` testados.
Só então publica com `prerelease=true` e `make_latest=false`.
A versão aparece em [Releases](https://github.com/lzocateli/PixelPointer/releases)
com a marca **Pre-release** e downloads públicos sem login. A estável continua sendo
`Latest`.

Essa é uma exceção autorizada pelo mantenedor em 2026-10-31 para o canal preview:
não altera o fluxo estável do Groundwork, não executa `post-release.yml` e não
atualiza Homebrew/winget. A preview manual usa o próximo patch da versão registrada e
o sufixo `preview.<run_number>`; o prefixo é aplicado somente nos checkouts de build e
smoke, sem editar a versão no Git. Isso não significa que essa será a próxima versão
estável calculada pelo release-please.

Previews e candidatas não incluem atualização de Homebrew/winget e não são servidas por
`releases/latest`.

### Usar um pacote preview

Abra [Releases](https://github.com/lzocateli/PixelPointer/releases) e escolha
uma versão marcada **Pre-release**, não `releases/latest`. Os exemplos usam
`0.1.3-preview.14`; substitua `version` pela versão escolhida, sem o `v` inicial.
Não é necessário ter .NET ou clonar o repositório. Previews não incluem os
instaladores; Homebrew e winget continuam apontando para versões estáveis.

Escolha o RID da máquina onde vai executar, não da máquina onde fez o download:

| Máquina | RID | Pacote portátil |
| --- | --- | --- |
| Linux Intel/AMD 64 bits (`uname -m`: `x86_64`) | `linux-x64` | `.tar.gz` |
| Linux ARM 64 bits (`uname -m`: `aarch64`) | `linux-arm64` | `.tar.gz` |
| Windows Intel/AMD 64 bits | `win-x64` | `.zip` |
| Windows ARM 64 bits | `win-arm64` | `.zip` |

Se houver uma versão estável em execução, encerre-a primeiro com
`pixelpointer quit`: as versões usam a mesma identidade e IPC, e um comando
da preview poderia ser encaminhado ao daemon antigo. Use sempre o caminho
do executável extraído, não um `pixelpointer` antigo encontrado pelo `PATH`.
Os exemplos criam uma pasta nova sem substituir a instalação estável nem
alterar perfil, registro ou `PATH`. O `.deb` é outra opção no Debian/Ubuntu,
mas instala o mesmo pacote de sistema e pode substituir a estável; prefira
o `.tar.gz` para um teste isolado dos binários. Configurações e permissões
por usuário continuam compartilhadas; não é uma sandbox.

#### Linux com Wayland

Execute na máquina de destino, em um terminal da sessão gráfica, não por SSH.
`echo "$XDG_SESSION_TYPE"` deve informar `wayland`. São necessários glibc 2.34+,
Fontconfig, libstdc++, libX11 e os portais XDG com o backend do desktop; a
captura ScreenCast usa também `libpipewire-0.3` do sistema. Não é preciso
instalar SDL3/Skia separadamente: elas acompanham o pacote.

Baixe e confira em uma pasta nova dentro da sua pasta pessoal. Para ARM,
troque `rid` por `linux-arm64`:

```bash
version="0.1.3-preview.14"
rid="linux-x64"
base="https://github.com/lzocateli/PixelPointer/releases/download/v$version"
workdir="$(mktemp -d "$HOME/pixelpointer-preview.XXXXXX")" &&
cd "$workdir" &&
curl -fSLO "$base/pixelpointer-$rid.tar.gz" &&
curl -fSLO "$base/SHA256SUMS" &&
sha256sum --check --ignore-missing SHA256SUMS &&
tar -xzf "pixelpointer-$rid.tar.gz"
```

**Só prossiga se o download e a verificação mostrarem sucesso e `OK`.** Se
falharem, não execute o pacote. Mantenha as bibliotecas junto ao executável:

```bash
./pixelpointer --version
./pixelpointer selftest
./pixelpointer probe
```

A versão deve incluir o sufixo escolhido e o `selftest` deve passar; ele não
comprova captura, atalhos ou a interface gráfica. Se o app ainda não estiver
registrado no desktop, execute `./pixelpointer install-desktop-file`.
Não force a substituição de um registro existente: conflitos são preservados.
Essa etapa registra o aplicativo no menu, sem substituir seus binários.

Se aparecer `App info not found` no registro seguido de `An app id is required`,
o portal não encontrou a identidade do aplicativo; isso não indica checksum
inválido. Execute `./pixelpointer install-desktop-file` nessa pasta e reinicie o
daemon somente após o registro concluir sem conflito. Registrar a identidade não
concede permissão de captura. O daemon trata recusas D-Bus dos atalhos com aviso
e mantém o IPC; previews anteriores a essa correção podem encerrar com exceção.
Sem atalhos globais, use `zoom`/`draw`; a captura ainda exige o consentimento do portal.

No GNOME, execute `./pixelpointer grant`, pressione Enter na janela e autorize
a captura. Depois teste a captura e inicie o daemon em primeiro plano para
observar eventuais erros:

```bash
./pixelpointer capture --out preview.png
./pixelpointer daemon
```

Na solicitação do portal, escolha e autorize o monitor. Fora do GNOME, com um
único monitor, ScreenCast/PipeWire lê um quadro e encerra a sessão; o indicador
de compartilhamento é transitório. No GNOME a captura usa o portal Screenshot,
sem indicador. Cancelar o diálogo cancela a operação.
No GNOME anterior ao 48 e em desktops sem GlobalShortcuts, configure atalhos
do desktop usando o caminho absoluto desta preview com `zoom` e `draw`.
Detalhes: [permissões Wayland](uso.md#linux-com-wayland-gnome-kde-hyprland).
O desenho Wayland permanece sobre uma captura congelada, não sobre conteúdo
ao vivo. Teste inicialmente com um monitor; multi-monitor e outros desktops
têm limites e não herdam automaticamente o aceite de uma máquina testada.

#### Windows

Execute no Windows PowerShell 5.1+ ou PowerShell 7 da máquina de destino.
Para ARM, troque `rid` por `win-arm64`. O exemplo cria uma pasta nova em
`LOCALAPPDATA`, verifica exatamente a entrada do ZIP no `SHA256SUMS` e só
depois extrai o pacote:

```powershell
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
$version = '0.1.3-preview.14'
$rid = 'win-x64'
$base = "https://github.com/lzocateli/PixelPointer/releases/download/v$version"
$dir = Join-Path $env:LOCALAPPDATA ("PixelPointerPreview\" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $dir | Out-Null
$zip = "pixelpointer-$rid.zip"
Invoke-WebRequest "$base/$zip" -UseBasicParsing -OutFile "$dir\$zip"
Invoke-WebRequest "$base/SHA256SUMS" -UseBasicParsing -OutFile "$dir\SHA256SUMS"
$pattern = '^[0-9a-fA-F]{64}  ' + [regex]::Escape($zip) + '$'
$entries = @(Get-Content "$dir\SHA256SUMS" | Where-Object { $_ -match $pattern })
if ($entries.Count -ne 1) { throw 'SHA256SUMS: entrada ausente, invalida ou duplicada.' }
$expected = ($entries[0] -split '\s+')[0]
if ((Get-FileHash "$dir\$zip" -Algorithm SHA256).Hash -ne $expected) {
      throw 'Checksum invalido; nao execute o pacote.'
}
Expand-Archive "$dir\$zip" -DestinationPath "$dir\app"
Set-Location "$dir\app"
```

Em caso de erro, pare: não execute nem extraia manualmente o arquivo recusado.
Depois de verificar e extrair, mantenha `SDL3.dll` e `libSkiaSharp.dll` junto
ao executável e execute:

```powershell
.\pixelpointer.exe --version
.\pixelpointer.exe selftest
.\pixelpointer.exe probe
.\pixelpointer.exe daemon
```

O daemon fica em primeiro plano; Ctrl+C o encerra. Depois de encerrar, para
iniciar sem bloquear o PowerShell:

```powershell
Start-Process "$PWD\pixelpointer.exe" -ArgumentList daemon -WindowStyle Hidden
```

Consulte depois `.\pixelpointer.exe status` e encerre com
`.\pixelpointer.exe quit`. Se um atalho já estiver ocupado, execute em primeiro
plano para ver o conflito. Os binários não têm assinatura Authenticode;
não desative proteções do Windows para contornar alertas.

#### Roteiro de teste e retorno à estável

- Confirme a versão escolhida, o `selftest` e a sessão/portais no `probe`.
- Teste Ctrl+1, roda do mouse, Ctrl+2, Ctrl+3 (timer), Ctrl+4 (Live Zoom, no Windows e no X11), Ctrl+5 (gravação GIF; no Wayland, conferir o indicador após salvar), Ctrl+6 (recorte, com e sem Shift), cores, marca-texto (Shift+cor), desfoque (X e Shift+X), formas (Shift, Ctrl, Ctrl+Shift e Tab ao arrastar), texto (T), Ctrl+C e Ctrl+Z.
- Teste Ctrl+S: PNG na pasta XDG Imagens/`Screenshots` do Linux ou na pasta
   especial Imagens/`Screenshots` do Windows, respeitando OneDrive.
- Esc fecha a sobreposição; `status` deve confirmar que o daemon continua
   ativo e `quit` deve encerrá-lo.

Para retornar à estável, encerre o daemon da preview pelo seu executável e
inicie o da instalação estável. Se registrou uma entrada desktop para a
preview, remova-a com `./pixelpointer uninstall-desktop-file` antes de remover
seus arquivos e registre a estável novamente se necessário. Preserve registros
modificados e configurações; apagar a pasta portátil não apaga capturas nem
revoga permissões do portal.

Ao relatar uma falha, informe versão, SO, arquitetura, desktop/versão,
X11/Wayland, quantidade de monitores, passos e mensagem de erro. Não compartilhe
capturas sensíveis, tokens nem logs integrais. Estes exemplos são um roteiro
manual: a aprovação dos smokes no CI não comprova o aceite gráfico da preview
em toda combinação de máquina e desktop.

### Segurança e recuperação da preview

| Risco | Controle |
| --- | --- |
| Publicação a partir de PR ou branch não aprovada | Job somente em dispatch da `main`, aprovação no Environment `distribution`, checkout do commit da execução e ancestralidade de `origin/main` |
| Uso de token pessoal para criar tags protegidas | Somente o App autorizado, com token limitado a `contents: write`; token e chave não entram nos artifacts nem na saída dos scripts |
| Pacotes incompletos, adulterados ou de outra execução | Build e smoke com a mesma versão, atestações de SBOM/proveniência, conjunto exato de assets, SHA-256 local e digest remoto conferidos antes de publicar |
| Reexecução sobrescrever release ou promover preview a estável | Rascunho deve corresponder à mesma tag/commit; nunca mover tags ou sobrescrever assets; `prerelease=true`, `make_latest=false` e nenhum post-release |

Se a execução falhar após criar a tag ou o rascunho, eles são preservados e a
preview não fica pública. Não exclua/mova tags para contornar a falha. Uma
reexecução da mesma execução só finaliza se todos os digests forem idênticos;
builds podem não ser reprodutíveis byte a byte. Se houver divergência, inicie
um novo dispatch, com outro `preview.N`. Uma tag existente sem rascunho exige
investigação do mantenedor; o script recusa reutilizá-la. Após uma preview já
pública, uma reexecução recusa alterá-la; para corrigir, publique outra preview.

O comando local equivalente é
`tools/scripts/Set-PreviewRelease.ps1`: sem `-Publish`, valida arquivos e emite
o payload sem acessar o GitHub; `-Publish` é reservado ao job aprovado com o
App. `tools/scripts/Test-PreviewRelease.ps1`, incluído no
`Invoke-Validation.ps1`, testa filesystem real e API sintética em container.
Esses testes não substituem a primeira publicação real dos seis RIDs no Actions
nem o aceite gráfico nas máquinas de destino.

## Primeira versão funcional

`v0.1.2` é a primeira versão funcional aceita como estável pelo mantenedor em
2026-10-30 no Ubuntu 24.04.5/GNOME X11 e Ubuntu 26.04/GNOME Wayland. O
[plano E2E](plano-e2e-grafico-uv.md) registra o aceite manual e seus limites;
não representa validação nativa Windows/macOS nem conclusão das fases F2/F3.

A release `v0.1.1` inclui o SVG no pacote, mas seu instalador não o copia.
A `v0.1.2` inclui a correção do PR #8: `install.sh` e `install.ps1` exigem e
copiam `pixelpointer.svg`. Não sobrescreva assets nem mova tags das versões
anteriores. Para reproduzir a versão aceita, a partir de qualquer diretório no
Linux/macOS, com `curl`, `sh` e as dependências nativas do README disponíveis:

```bash
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/download/v0.1.2/install.sh | sh -s -- --version 0.1.2
```

O instalador verifica SHA-256 e instala/substitui o pacote no prefixo padrão,
sem alterar perfis ou PATH. Confirme `pixelpointer --version` e, no Linux,
execute `pixelpointer install-desktop-file`. Um registro desktop antigo
modificado é preservado e exige migração explícita, não sobrescrita silenciosa.

A publicação GitHub e a submissão winget são etapas distintas. Um job winget
aguardando aprovação pode manter a fila `release-main` ocupada mesmo após a
release pública; se o mantenedor decidir adiar o envio, rejeite somente a
aprovação `winget` da execução antiga para liberar o próximo Release PR.
Não aprove winget para desbloquear uma release. A correção estrutural dessa
concorrência pertence ao catálogo Groundwork, não a edições locais em `gw-*`.

## Artefatos

| Arquivo | Conteúdo |
| --- | --- |
| `pixelpointer-<rid>.tar.gz` | `pixelpointer` (modo 0755), `libSDL3` e `libSkiaSharp` (`.so` ou `.dylib`), `pixelpointer.svg`, `LICENSE` e `THIRD-PARTY-NOTICES.md`, dono `0:0`; Linux e macOS |
| `pixelpointer-<rid>.zip` | `pixelpointer.exe`, `SDL3.dll`, `libSkiaSharp.dll`, `pixelpointer.svg`, `LICENSE` e `THIRD-PARTY-NOTICES.md`; Windows |
| `pixelpointer-<rid>.spdx.json` | SBOM SPDX 2.2 do pacote do RID: arquivos do pacote e dependências NuGet do build |
| `pixelpointer-linux-x64.deb`, `pixelpointer-linux-arm64.deb` | aplicativo em `/usr/lib/pixelpointer`, link `/usr/bin/pixelpointer`, entrada desktop em `/usr/share/applications`, ícone próprio em `/usr/share/icons/hicolor/scalable/apps` e documentação em `/usr/share/doc/pixelpointer`, dono `root`; `Depends: libc6 (>= 2.34), libstdc++6, libfontconfig1` |
| `install.sh` | instalador POSIX (Linux e macOS) |
| `install.ps1` | instalador Windows PowerShell 5.1+ e PowerShell 7 |
| `SHA256SUMS` | SHA-256 de todos os arquivos acima, formato `sha256sum` |

São 16 arquivos além do `SHA256SUMS` (6 pacotes, 6 SBOMs, 2 `.deb` e 2
instaladores); o job `Prepare release assets` falha se a contagem divergir. Os nomes não levam
versão para que `releases/latest/download/<arquivo>` seja estável; a versão fica
na tag e em `pixelpointer --version` (no `.deb`, sufixos usam `~`, por exemplo
`0.4.0~rc.1`, para ordenar antes da versão final). Não há pacote `linux-musl`:
o SDL3-CS não publica binário musl, e o mínimo no Linux é glibc 2.34, exigido
pela `libSDL3` distribuída (D12 do [plano](plano-implementacao.md)).

Cada arquivo tem atestação de proveniência do GitHub (`gw-github-assets.yml`), a
release tem o `release-manifest.json` atestado com o SHA-256 de cada asset e cada pacote (`.tar.gz`,
`.zip` e `.deb`) tem atestação do SBOM do seu RID, ambas Sigstore keyless via
OIDC, sem custo. Os binários não têm assinatura Authenticode nem Developer ID
(exigem certificados pagos; D5 do [plano](plano-implementacao.md)).

## Verificação pelo usuário

```bash
sha256sum --check --ignore-missing SHA256SUMS
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer \
  --predicate-type https://spdx.dev/Document/v2.2
```

## Canais de distribuição

| Canal | Comando | Atualização |
| --- | --- | --- |
| Instaladores | `install.sh` / `install.ps1` (ver [README](../README.md#instalação)) | `releases/latest`; `pixelpointer update` opt-in em instalações de usuário ([guia](uso.md#atualizar)) |
| `.deb` (asset, sem repositório APT) | `sudo apt install ./pixelpointer-linux-x64.deb` | manual, baixando a nova release |
| Homebrew (macOS e Linux) | `brew install lzocateli/tap/pixelpointer` | `brew upgrade`; iniciar manualmente `distribute-homebrew.yml` para `vX.Y.Z` |
| winget (Windows) | `winget install lzocateli.PixelPointer` | `winget upgrade`; iniciar manualmente `winget.yml` para `vX.Y.Z`, sujeito à moderação da Microsoft |

O atualizador do aplicativo aceita somente pacotes de release estável cuja
atestação de proveniência SLSA v1 foi emitida pelo workflow
`gw-github-assets.yml` deste repositório (identificadores numéricos do
repositório e do dono, runner hospedado pelo GitHub, ref `main` ou tag `v*`).
Mudar o workflow que atesta os assets, a ref de execução, o repositório ou deixar
de anexar o `SHA256SUMS` faz as versões já instaladas recusarem a atualização:
publique antes uma versão que aceite a nova identidade e oriente a atualização
pelo instalador. Rotações do Sigstore público (Fulcio, Rekor v1, log CT) têm o
mesmo efeito, porque o material de confiança está embutido no binário.

A fórmula vive em [`lzocateli/homebrew-tap`](https://github.com/lzocateli/homebrew-tap)
e é gerada por [`New-HomebrewFormula.ps1`](../tools/scripts/New-HomebrewFormula.ps1)
a partir do `SHA256SUMS`; instala o pacote em `libexec` (`.tar.gz` de
`osx-arm64`, `osx-x64`, `linux-arm64` e `linux-x64`), cria o link
`bin/pixelpointer`, depende de `fontconfig` no Linux e o `brew test` confere
`--version` e `selftest`.

O manifesto winget (`lzocateli.PixelPointer`, schema 1.10.0) é gerado por
[`New-WingetManifest.ps1`](../tools/scripts/New-WingetManifest.ps1) a partir do
`SHA256SUMS`: `.zip` de `win-x64` e `win-arm64` com `pixelpointer.exe` portátil
(alias `pixelpointer`) e as DLLs nativas no mesmo diretório; o aplicativo
resolve as bibliotecas pelo diretório real do executável, então o alias
funciona, o que o `winget.yml` confere com `selftest`.

## Build por RID

| RID | Onde compila | Toolchain |
| --- | --- | --- |
| `linux-x64`, `linux-arm64` | container em host Linux x64 | imagem oficial de cross-build `azurelinux-3.0-net10.0-cross-*` (sysroot glibc 2.27, clang, lld) |
| `win-x64`, `win-arm64` | `windows-2025`, `windows-11-arm` | SDK do `global.json` e Visual Studio C++ |
| `osx-arm64`, `osx-x64` | `macos-15`, `macos-15-intel` | SDK do `global.json` e Xcode |

Native AOT não compila entre sistemas operacionais. No Linux, o SDK oficial é
copiado sobre a imagem de cross-build por
[`linux-cross.Dockerfile`](../tools/build/linux-cross.Dockerfile); imagens são
fixadas por tag e digest em `Publish-Aot.ps1` e devem ser atualizadas juntas.

```bash
pwsh -c './tools/scripts/Publish-Aot.ps1 -Rid linux-x64, linux-arm64'
pwsh ./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64   # ubuntu:22.04, rockylinux:9, debian:12 e o .deb
pwsh ./tools/scripts/Test-Installers.ps1                   # install.sh com servidor HTTP local
```

O SBOM é gerado por `microsoft/sbom-tool` (MIT), ferramenta local fixada em
[`.config/dotnet-tools.json`](../.config/dotnet-tools.json): no container de
cross-build para Linux e com o SDK do runner para Windows e macOS. O `.deb` é
montado com `dpkg-deb --root-owner-group` em `docker.io/library/debian:12`.

O smoke (`Test-AotPackage.ps1`) confere o SHA-256, o conteúdo do pacote e
executa `--version`, `--help` e `selftest` (Skia codifica um PNG e o SDL
inicializa com o driver `dummy`) diretamente e por um link simbólico, em
containers sem .NET que instalam `libfontconfig`, `libstdc++` e `libX11`. No Linux,
valida também instalação desktop por usuário idempotente, preservação de conflitos,
remoção segura, instância única, `status`, `quit` e reinício com SDL dummy. Esses
testes não comprovam atalhos físicos, tray ou transparência do compositor. Quando
presentes, confere também o SBOM (SPDX 2.2, nome `pixelpointer <versão>` e
SHA-256 do executável) e instala, executa e remove o `.deb` com `apt-get` em
`ubuntu:22.04` e `debian:12`. `Test-Installers.ps1` serve os
pacotes em `127.0.0.1` e cobre ajuda, erros de uso, checksum adulterado sem
efeitos, download falho, instalação, reinstalação sem resíduos, aviso de
`PATH`, destino padrão e `--global`; no Windows, executa `install.ps1` em
Windows PowerShell 5.1 e PowerShell 7, por `-File` e como scriptblock.

## Publicação

Somente o mantenedor publica versões. A aprovação de um PR e a exceção para
seus próprios PRs não substituem a aprovação do Environment `release`; consulte
a [governança](../.github/GOVERNANCE.md).

### Releases de segurança e suporte

Versões pré-1.0 não têm suporte de segurança nem backports. A partir de
`1.0.0`, somente a minor `1.x` mais recente é suportada; a publicação de uma
minor nova encerra o suporte à anterior. O suporte da linha `1.x` termina com
`2.0.0`. Não há SLA contratual. Correções são publicadas na linha suportada;
consulte a [política de segurança](../.github/SECURITY.md#versões-suportadas).

### Fluxo

1. PRs `feat`, `fix` (ou com `!`) integrados na `main` atualizam o Release PR
   `chore(main): release X.Y.Z`, aberto pelo release-please com o GitHub App
   `Altivy Release`. O release-please calcula a versão, atualiza `VersionPrefix`
   em `Directory.Build.props`, `.release-please-manifest.json` e o `CHANGELOG.md`.
   Não edite a versão nem crie tags manualmente.
2. O merge do Release PR pelo mantenedor executa o `gw-release.yml`: o App cria a
   tag `vX.Y.Z` e a GitHub Release em rascunho, e o workflow confere que a tag
   corresponde às versões e que o commit é ancestral de `origin/main`.
3. O `publish.yml`, na tag, executa build, testes, publicação dos seis RIDs,
   smoke (`--version`, `--help` e `selftest`, também por link simbólico) e
   testes dos instaladores em seis runners.
4. O job `attest-sbom` atesta o SBOM de cada RID (sujeitos: o pacote e, no
   Linux, o `.deb`) antes dos assets, para que a release nunca fique
   pública sem essas atestações.
5. `Prepare release assets` regenera o `SHA256SUMS` com os instaladores e confere
   os 16 arquivos; `Publish release assets` (`gw-github-assets.yml`) aguarda
   aprovação no Environment `release`, gera a atestação de proveniência e anexa os
   arquivos à release em rascunho, sem sobrescrever asset já publicado.
6. O `gw-release.yml` gera e atesta o `release-manifest.json` e publica a
   release. As notas vêm do `CHANGELOG.md` gerado pelo release-please.
7. A stable publicada encerra o fluxo canônico; nenhum registry é atualizado
    automaticamente. Para cada destino, confira a versão e inicie somente o workflow
    correspondente a partir de `main`:

    ```bash
    gh workflow run distribute-homebrew.yml --repo lzocateli/PixelPointer --ref main -f version=vX.Y.Z
    gh workflow run winget.yml --repo lzocateli/PixelPointer --ref main -f version=vX.Y.Z -f submit=true
    ```

    Homebrew valida a release pública, o manifesto, `SHA256SUMS` e suas atestações
    antes da aprovação no Environment `release`; depois atualiza e verifica o tap.
    Winget valida e instala o manifesto local antes da aprovação no Environment
    `winget`, então submete o PR com `wingetcreate`; a Microsoft ainda precisa aprová-lo.
    Cada execução altera somente o destino e a versão informados. `post-release.yml`
    não faz fan-out.

Pull requests que alteram a cadeia de release e `workflow_dispatch` do
`publish.yml` executam build, smoke e testes dos instaladores, sem publicar.
Prereleases (`vX.Y.Z-sufixo`) não fazem parte do fluxo automático.

Se a publicação falhar parcialmente, a release continua em rascunho: corrija a
causa e use o modo `republish` descrito no [runbook](runbooks/release.md).

### Winget

O `winget.yml` roda em pull requests que o alteram (valida com a última
release, sem submeter) e por `workflow_dispatch` independente a partir de `main`,
sempre com versão stable exata e `submit`, para a primeira submissão ou um retry:

```bash
gh workflow run winget.yml --repo lzocateli/PixelPointer --ref main -f version=v0.4.0 -f submit=true
```

`submit=true` é permitido somente no dispatch manual em `main` ou no republish
na tag correspondente. Pushes e `post-release.yml` não podem submeter ao winget.

O secret `WINGET_CREATE_GITHUB_TOKEN` fica somente no Environment `winget`
com `@lzocateli` como revisor obrigatório. `prevent_self_review` está
desativado para permitir a aprovação consciente do único mantenedor quando ele
inicia a execução; o GitHub também permite bypass administrativo, atualmente
limitado ao mesmo mantenedor. O `wingetcreate` aceita apenas PAT clássico com
escopo `public_repo` (tokens fine-grained não são suportados), que dá escrita a
todos os repositórios públicos da conta; por isso use expiração curta e
renove o secret ao expirar. O token é lido da variável de ambiente, nunca da
linha de comando. Cada PR passa pela validação automática e pela moderação do
`microsoft/winget-pkgs`; a versão fica disponível no `winget` após o merge.

### Acompanhamento das submissões ao winget

Uma versão só fica disponível no `winget install` depois que o PR dela é integrado
ao [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs). Até lá, o
PR passa pela validação automática da Microsoft e pela moderação voluntária, que
pode levar dias. Os comandos abaixo são somente leitura e funcionam com qualquer
`gh` autenticado:

```bash
# Versões já publicadas no catálogo (uma pasta por versão; HTTP 404 = nenhuma ainda)
gh api repos/microsoft/winget-pkgs/contents/manifests/l/lzocateli/PixelPointer --jq '.[].name'

# PRs submetidos, abertos e encerrados
gh pr list --repo microsoft/winget-pkgs --author lzocateli --state all \
  --search "lzocateli.PixelPointer in:title" --json number,title,state,createdAt,url

# Estado de um PR: labels, checks e últimos comentários
gh pr view <número> --repo microsoft/winget-pkgs --json state,labels,mergedAt,url
gh pr view <número> --repo microsoft/winget-pkgs --comments | tail -n 40

# Dispatches do winget.yml e execuções aguardando aprovação de Environment
gh run list --repo lzocateli/PixelPointer --workflow winget.yml --limit 5
gh run list --repo lzocateli/PixelPointer --status waiting
```

No Windows, `winget show lzocateli.PixelPointer --versions` lista as versões que o
cliente já enxerga; o índice pode levar algumas horas para refletir um merge.

Labels mais comuns no PR (referência: [Moderation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Moderation.md)
e [Validation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Validation.md)):

| Label | Significado | Ação |
| --- | --- | --- |
| `Azure-Pipeline-Passed`, `Validation-Completed` | Validação automática aprovada | Aguardar moderação |
| `Needs-CLA` | Falta o Contributor License Agreement da Microsoft | Ver abaixo; nenhum PR é integrado sem ele |
| `Needs-Author-Feedback` | O moderador pediu ajuste ou resposta | Responder no PR; se o manifesto mudar, corrigir no `New-WingetManifest.ps1` e ressubmeter |
| `Validation-*-Error` (por exemplo `Validation-Installation-Error`, `Validation-Hash-Verification-Failed`) | A validação automática falhou | Ler o comentário do bot; o asset ou o manifesto precisa de correção |
| `Moderator-Approved` | Aprovado pelo moderador | Aguardar o merge automático |
| `New-Package` | Primeira versão do pacote no catálogo | Exige revisão manual mais detalhada |

**Contributor License Agreement (uma vez por conta).** O primeiro PR de uma
conta no `microsoft/winget-pkgs` recebe `Needs-CLA`. O titular da conta lê o
acordo publicado pelo bot no PR e, se concordar, responde nesse PR com
`@microsoft-github-policy-service agree` (contribuição pessoal) ou com a
variante `company="..."`, quando a contribuição for feita em nome de um
empregador. A assinatura é uma decisão jurídica do titular: não a automatize nem
a delegue. Depois dela, o label sai e o PR segue para a moderação; os PRs
seguintes da mesma conta não pedem o CLA de novo.

### Versão anterior ainda pendente no winget

O `winget.yml` não submete uma versão que já esteja no catálogo ou que já tenha
PR aberto, mas **não** verifica se uma versão anterior ainda aguarda moderação.
Enquanto a primeira versão não for integrada, o PR da seguinte também sai como
`New package`, e os moderadores podem fechar um dos dois. Para evitar PRs
concorrentes, mantenha no máximo um PR aberto por vez:

1. Antes de iniciar `winget.yml`, consulte os PRs abertos (comando `gh pr list`
   acima, com `--state open`).
2. Se não houver PR aberto do pacote, faça dispatch da versão stable desejada.
3. Se houver PR de uma versão anterior aberto, aguarde ou resolva-o antes de
   iniciar outro envio. A release canônica e o Homebrew permanecem independentes;
   nenhum dos dois será alterado pelo dispatch de winget.
4. Resolva o PR pendente: assine o CLA se houver `Needs-CLA` e responda a
   `Needs-Author-Feedback`. Se o PR antigo for encerrado sem merge, não o
   reabra: ele já foi superado.
5. Quando o PR pendente for integrado ou encerrado, submeta somente a versão
   mais recente publicada no GitHub; versões intermediárias podem ser puladas,
   porque o `winget upgrade` instala sempre a mais nova:

   ```bash
   gh workflow run winget.yml --repo lzocateli/PixelPointer --ref main \
   -f version=vX.Y.Z -f submit=true
   ```

   O dispatch em `main` passa pela mesma validação, instalação local e aprovação
   do Environment `winget`.

### Token do tap Homebrew

O secret `HOMEBREW_TAP_TOKEN` fica somente no Environment `release`, usado pelo
job de publicação de `distribute-homebrew.yml`, e é um PAT
fine-grained com acesso apenas a `lzocateli/homebrew-tap` e permissão
`Contents: Read and write`. O script o lê da variável de ambiente e o repassa ao
`git` por `GIT_CONFIG_*`, fora da linha de comando. Ao expirar, gere um novo PAT
com o mesmo escopo e substitua o secret; sem ele, a release é publicada e o passo
do tap falha, podendo ser reexecutado (re-run do job) após a correção.

Para testar localmente sem publicar (commit só no clone temporário):

```bash
pwsh ./tools/scripts/Update-HomebrewTap.ps1 -Version 0.4.0 -ReleaseDirectory dist
```

## Limitações conhecidas

- O modo `republish` recompila os pacotes. Assets já anexados são mantidos com o
  digest publicado, mas um asset ausente é enviado a partir do novo build, que pode
  não reproduzir byte a byte o `SHA256SUMS` já publicado. Se o `SHA256SUMS` já
  estiver anexado, confira o resultado com `sha256sum --check` antes de divulgar.

- A configuração do GitHub permite bypass administrativo do Environment
  `winget` (`can_admins_bypass=true`). O único administrador consultado é
  `@lzocateli`; mudanças nesse grupo exigem revisão desta política.
- A primeira submissão ao `microsoft/winget-pkgs` cria um pacote novo e passa por
  moderação manual da Microsoft; até lá, `winget install lzocateli.PixelPointer`
  não encontra o pacote.
- Validação local em 2026-10-30: publish, smoke (Ubuntu 22.04, Rocky 9,
  Debian 12 e `.deb`) e instaladores aprovados para `linux-x64` com Podman.
  Os demais RIDs são validados pelos runners do GitHub.
