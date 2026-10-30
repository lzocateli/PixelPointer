<#
.SYNOPSIS
Executa o smoke test de um pacote de release do pixelpointer sem .NET instalado.

.DESCRIPTION
Confere o pacote pixelpointer-<rid> contra SHA256SUMS, confere o conteudo
(executavel, SDL3, libSkiaSharp, ícone SVG, LICENSE e THIRD-PARTY-NOTICES.md), extrai e
executa: --version (deve comecar pela versao esperada), --help e selftest
(Skia desenha e codifica um PNG; SDL inicializa com o driver de video dummy),
o que prova que as bibliotecas nativas carregam do diretorio do executavel.
No Linux, verifica também instalação e remoção desktop por usuário, preservação
de conflitos e ciclo do daemon (status, instância única, quit e reinício) com SDL
dummy, sem comprovar comportamento gráfico.

Se existir pixelpointer-<rid>.spdx.json, confere o SBOM: SPDX 2.2, nome e versao
do produto e SHA-256 do executavel igual ao do pacote. Para linux-x64 e
linux-arm64, se existir pixelpointer-<rid>.deb, instala o pacote com apt-get em
Ubuntu 22.04 e Debian 12 (dependencias pelo repositorio da distribuicao),
executa /usr/bin/pixelpointer (link para /usr/lib/pixelpointer), confere a
entrada .desktop e remove o pacote.

RIDs Linux rodam em containers limpos, sem .NET, nas distribuicoes mais antigas
suportadas (glibc 2.34, minimo da libSDL3); o container instala somente
libfontconfig, libstdc++ e libX11 antes do smoke. O host deve ter a mesma arquitetura
do RID (sem emulacao). RIDs Windows e macOS rodam nativamente no host
correspondente, tambem por um link simbolico para o executavel.

.PARAMETER Rid
RID do pacote: linux-x64, linux-arm64, win-x64, win-arm64, osx-arm64 ou
osx-x64.

.PARAMETER ReleaseDirectory
Diretorio com os pacotes e SHA256SUMS. Padrao: artifacts/release.

.PARAMETER VersionSuffix
Sufixo de pre-release esperado na versao (por exemplo rc.1). Padrao: vazio.

.PARAMETER Image
Imagens para RIDs Linux. Padrao: ubuntu:22.04, rockylinux:9 e debian:12.

.PARAMETER Engine
Engine de container para RIDs Linux: docker ou podman. Padrao:
PIXELPOINTER_CONTAINER_ENGINE, depois Docker e, na ausencia dele, Podman.

.PARAMETER RemainingArguments
Aceita somente --help; outros valores retornam erro de uso.

.EXAMPLE
./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64 -Engine podman

.EXAMPLE
./tools/scripts/Test-AotPackage.ps1 -Rid win-x64 -ReleaseDirectory dist

.NOTES
Requer PowerShell 7+ e o pacote gerado por Publish-Aot.ps1. RIDs Linux exigem
Docker ou Podman. O script nunca altera perfil, registro ou PATH do host.
Codigos: 0 sucesso; 1 smoke reprovado; 2 erro de uso ou host incompativel.

.LINK
./Publish-Aot.ps1
#>
#requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-arm64', 'osx-x64')]
    [string] $Rid,

    [string] $ReleaseDirectory,

    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z.-]*$')]
    [string] $VersionSuffix,

    [string[]] $Image,

    [ValidateSet('docker', 'podman')]
    [string] $Engine,

    [Parameter(ValueFromRemainingArguments)]
    [string[]] $RemainingArguments
)

$ErrorActionPreference = 'Stop'

if ($RemainingArguments -contains '--help') {
    Get-Help $PSCommandPath -Full
    exit 0
}

if ($RemainingArguments.Count -gt 0) {
    [Console]::Error.WriteLine("Argumento desconhecido: $($RemainingArguments -join ' '). Use --help.")
    exit 2
}

if ([string]::IsNullOrWhiteSpace($Rid)) {
    [Console]::Error.WriteLine('Informe -Rid. Use --help.')
    exit 2
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $ReleaseDirectory = Join-Path $repositoryRoot 'artifacts/release'
}
$ReleaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory, (Get-Location).Path)

$isWindowsRid = $Rid -like 'win-*'
$packageName = if ($isWindowsRid) { "pixelpointer-$Rid.zip" } else { "pixelpointer-$Rid.tar.gz" }
$package = Join-Path $ReleaseDirectory $packageName
$checksums = Join-Path $ReleaseDirectory 'SHA256SUMS'
foreach ($required in @($package, $checksums)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        [Console]::Error.WriteLine("Arquivo nao encontrado: $required. Execute Publish-Aot.ps1. Use --help.")
        exit 2
    }
}

[xml] $props = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$expectedVersion = @($props.Project.PropertyGroup | ForEach-Object { $_.VersionPrefix } | Where-Object { $_ })[0]
if (-not [string]::IsNullOrWhiteSpace($VersionSuffix)) {
    $expectedVersion = "$expectedVersion-$VersionSuffix"
}

$expectedHash = @(Get-Content -LiteralPath $checksums |
    Where-Object { $_ -match '^([0-9a-f]{64})  (.+)$' -and $Matches[2] -eq $packageName } |
    ForEach-Object { $_.Substring(0, 64) })
$actualHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHash.Count -ne 1 -or $expectedHash[0] -ne $actualHash) {
    [Console]::Error.WriteLine("SHA256 de $packageName nao confere com SHA256SUMS.")
    exit 1
}
Write-Output "SHA256 conferido: $packageName"

$workDirectory = Join-Path ([IO.Path]::GetTempPath()) "pixelpointer-smoke-$([Guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $workDirectory
try {
    $extractDirectory = Join-Path $workDirectory 'package'
    $null = New-Item -ItemType Directory -Path $extractDirectory
    if ($isWindowsRid) {
        Expand-Archive -LiteralPath $package -DestinationPath $extractDirectory
    }
    else {
        & tar -xzf $package -C $extractDirectory
        if ($LASTEXITCODE -ne 0) {
            [Console]::Error.WriteLine("Falha ao extrair $packageName.")
            exit 1
        }
    }

    $binaryName = if ($isWindowsRid) { 'pixelpointer.exe' } else { 'pixelpointer' }
    $libraries = if ($isWindowsRid) { @('SDL3.dll', 'libSkiaSharp.dll') } elseif ($Rid -like 'osx-*') { @('libSDL3.dylib', 'libSkiaSharp.dylib') } else { @('libSDL3.so', 'libSkiaSharp.so') }
    $expectedEntries = @($binaryName) + $libraries + @('pixelpointer.svg', 'LICENSE', 'THIRD-PARTY-NOTICES.md')
    $entries = @(Get-ChildItem -LiteralPath $extractDirectory -Force | ForEach-Object Name)
    $unexpected = @(Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $entries -CaseSensitive)
    if ($unexpected.Count -gt 0) {
        [Console]::Error.WriteLine("Conteudo inesperado em ${packageName}: $($entries -join ', ').")
        exit 1
    }
    $binary = Join-Path $extractDirectory $binaryName
    if ((Get-Item -LiteralPath (Join-Path $extractDirectory 'pixelpointer.svg')).Length -eq 0) {
        [Console]::Error.WriteLine("Ícone SVG vazio no pacote $packageName.")
        exit 1
    }

    $sbom = Join-Path $ReleaseDirectory "pixelpointer-$Rid.spdx.json"
    if (Test-Path -LiteralPath $sbom -PathType Leaf) {
        $document = Get-Content -LiteralPath $sbom -Raw | ConvertFrom-Json
        $binaryHash = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant()
        $entry = @($document.files | Where-Object { $_.fileName -eq "./$binaryName" })
        $listed = if ($entry.Count -eq 1) { @($entry[0].checksums | Where-Object { $_.algorithm -eq 'SHA256' } | ForEach-Object { $_.checksumValue }) } else { @() }
        if ($document.spdxVersion -ne 'SPDX-2.2' -or $document.name -ne "pixelpointer $expectedVersion" -or $listed -notcontains $binaryHash) {
            [Console]::Error.WriteLine("SBOM invalido em $(Split-Path -Leaf $sbom): versao '$($document.spdxVersion)', nome '$($document.name)', hash do binario ausente.")
            exit 1
        }

        Write-Output "SBOM conferido: $(Split-Path -Leaf $sbom)"
    }

    if ($Rid -like 'linux-*') {
        $ridArchitecture = if ($Rid -like '*-arm64') { [Runtime.InteropServices.Architecture]::Arm64 } else { [Runtime.InteropServices.Architecture]::X64 }
        if (-not $IsLinux -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne $ridArchitecture) {
            [Console]::Error.WriteLine("$Rid exige host Linux $ridArchitecture (sem emulacao). Use --help.")
            exit 2
        }

        if ($null -eq $Image -or $Image.Count -eq 0) {
            $Image = @('docker.io/library/ubuntu:22.04', 'docker.io/library/rockylinux:9', 'docker.io/library/debian:12')
        }

        $containerEngine = $Engine
        if ([string]::IsNullOrWhiteSpace($containerEngine)) {
            $containerEngine = $env:PIXELPOINTER_CONTAINER_ENGINE
        }
        if ([string]::IsNullOrWhiteSpace($containerEngine)) {
            $containerEngine = @('docker', 'podman') |
                Where-Object { $null -ne (Get-Command $_ -ErrorAction SilentlyContinue) } |
                Select-Object -First 1
        }
        if ($containerEngine -notin @('docker', 'podman') -or $null -eq (Get-Command $containerEngine -ErrorAction SilentlyContinue)) {
            [Console]::Error.WriteLine('Docker ou Podman nao foi encontrado no PATH. Use --help.')
            exit 2
        }

        # Dependências nativas para o smoke sem tela e a inicialização dos atalhos.
        $smoke = @'
set -eu
if command -v dotnet >/dev/null 2>&1; then echo "imagem possui dotnet no PATH" >&2; exit 1; fi
if command -v apt-get >/dev/null 2>&1; then
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq >/dev/null && apt-get install -y -qq --no-install-recommends libfontconfig1 libstdc++6 libx11-6 >/dev/null
else
  dnf install -y -q fontconfig libstdc++ libX11 >/dev/null
fi
cp -R /pkg /opt/pixelpointer
mkdir -p /opt/bin && ln -s /opt/pixelpointer/pixelpointer /opt/bin/pixelpointer
for bin in /opt/pixelpointer/pixelpointer /opt/bin/pixelpointer; do
  version=$("$bin" --version)
  case "$version" in
    "$EXPECTED_VERSION"|"$EXPECTED_VERSION"+*) ;;
    *) echo "versao inesperada: $version (esperada $EXPECTED_VERSION)" >&2; exit 1 ;;
  esac
  "$bin" --help >/dev/null
  "$bin" selftest
done
echo "ok: $version"
export XDG_DATA_HOME=/tmp/pixelpointer-data XDG_RUNTIME_DIR=/tmp/pixelpointer-runtime SDL_VIDEODRIVER=dummy
mkdir -m 700 "$XDG_RUNTIME_DIR"
/opt/bin/pixelpointer install-desktop-file
/opt/bin/pixelpointer install-desktop-file
desktop="$XDG_DATA_HOME/applications/io.github.lzocateli.PixelPointer.desktop"
icon="$XDG_DATA_HOME/icons/hicolor/scalable/apps/io.github.lzocateli.PixelPointer.svg"
test -s "$icon"
cp "$desktop" /tmp/pixelpointer-owned.desktop
printf 'conteudo do usuario\n' > "$desktop"
if /opt/bin/pixelpointer install-desktop-file >/dev/null 2>&1; then echo "conflito sobrescrito" >&2; exit 1; fi
if /opt/bin/pixelpointer uninstall-desktop-file >/dev/null 2>&1; then echo "conflito removido" >&2; exit 1; fi
grep -qx 'conteudo do usuario' "$desktop"
test -s "$icon"
cp /tmp/pixelpointer-owned.desktop "$desktop"
/opt/bin/pixelpointer uninstall-desktop-file
test ! -e "$desktop"
test ! -e "$icon"
for cycle in 1 2; do
  timeout 20 /opt/bin/pixelpointer daemon >/tmp/pixelpointer-daemon.out 2>&1 &
  daemon_pid=$!
  attempt=0
  until /opt/bin/pixelpointer status >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 30 ]; then echo "daemon nao ficou pronto" >&2; exit 1; fi
    sleep 0.1
  done
  duplicate_status=0
  timeout 5 /opt/bin/pixelpointer daemon >/dev/null 2>&1 || duplicate_status=$?
  test "$duplicate_status" -eq 1
  /opt/bin/pixelpointer status
  /opt/bin/pixelpointer quit
  wait "$daemon_pid"
  test ! -e "$XDG_RUNTIME_DIR/pixelpointer.sock"
done
echo "ok: desktop e ciclo de vida IPC"
'@
        # O arquivo .ps1 usa CRLF; o shell do container exige LF.
        $smoke = $smoke -replace "`r", ''

        $failures = @()
        foreach ($name in $Image) {
            Write-Output "== $Rid em $name"
            & $containerEngine run --rm `
                --volume "${extractDirectory}:/pkg:ro" `
                --env "EXPECTED_VERSION=$expectedVersion" `
                $name sh -c $smoke
            if ($LASTEXITCODE -ne 0) {
                $failures += "$name (codigo $LASTEXITCODE)"
            }
        }

        if ($failures.Count -gt 0) {
            [Console]::Error.WriteLine("Smoke de $Rid falhou em: $($failures -join ', ')")
            exit 1
        }
        Write-Output "Smoke de $Rid aprovado em: $($Image -join ', ')"

        $deb = Join-Path $ReleaseDirectory "pixelpointer-$Rid.deb"
        if ($Rid -in 'linux-x64', 'linux-arm64' -and (Test-Path -LiteralPath $deb -PathType Leaf)) {
            $debVersion = $expectedVersion -replace '-', '~'
            $debSmoke = @'
set -eu
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq >/dev/null
apt-get install -y -qq --no-install-recommends /pkg/pixelpointer.deb >/dev/null
[ "$(dpkg-query -W -f='${Version}' pixelpointer)" = "$EXPECTED_DEB" ] || { echo "versao do pacote inesperada" >&2; exit 1; }
case "$(/usr/bin/pixelpointer --version)" in "$EXPECTED_VERSION"|"$EXPECTED_VERSION"+*) ;; *) echo "versao inesperada" >&2; exit 1 ;; esac
/usr/bin/pixelpointer selftest
dpkg -L pixelpointer | grep -qx /usr/share/doc/pixelpointer/copyright
dpkg -L pixelpointer | grep -qx /usr/share/icons/hicolor/scalable/apps/io.github.lzocateli.PixelPointer.svg
test -s /usr/share/icons/hicolor/scalable/apps/io.github.lzocateli.PixelPointer.svg
grep -qx 'Exec=/usr/bin/pixelpointer daemon' /usr/share/applications/io.github.lzocateli.PixelPointer.desktop
apt-get remove -y -qq pixelpointer >/dev/null
[ ! -e /usr/bin/pixelpointer ] && [ ! -e /usr/lib/pixelpointer ] || { echo "arquivos permaneceram apos remover o pacote" >&2; exit 1; }
echo "deb ok"
'@ -replace "`r", ''
            foreach ($name in @('docker.io/library/ubuntu:22.04', 'docker.io/library/debian:12')) {
                Write-Output "== $Rid .deb em $name"
                & $containerEngine run --rm `
                    --volume "${deb}:/pkg/pixelpointer.deb:ro" `
                    --env "EXPECTED_VERSION=$expectedVersion" `
                    --env "EXPECTED_DEB=$debVersion" `
                    $name sh -c $debSmoke
                if ($LASTEXITCODE -ne 0) {
                    [Console]::Error.WriteLine("Pacote .deb de $Rid falhou em $name (codigo $LASTEXITCODE).")
                    exit 1
                }
            }
        }
        exit 0
    }

    $hostMatches = if ($isWindowsRid) { $IsWindows } else { $IsMacOS }
    if (-not $hostMatches) {
        [Console]::Error.WriteLine("$Rid exige host $(if ($isWindowsRid) { 'Windows' } else { 'macOS' }). Use --help.")
        exit 2
    }

    if (-not $isWindowsRid) {
        & chmod 0755 $binary
    }

    # Executa tambem por link simbolico, como Homebrew e winget publicam o comando.
    $linkDirectory = Join-Path $workDirectory 'bin'
    $null = New-Item -ItemType Directory -Path $linkDirectory
    $link = Join-Path $linkDirectory $binaryName
    $targets = @($binary)
    try {
        $null = New-Item -ItemType SymbolicLink -Path $link -Target $binary
        $targets += $link
    }
    catch {
        Write-Output "::warning::Link simbolico indisponivel neste host ($($_.Exception.Message)); smoke somente do executavel."
    }

    foreach ($target in $targets) {
        $version = (& $target --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or -not ($version -eq $expectedVersion -or $version.StartsWith("$expectedVersion+", [StringComparison]::Ordinal))) {
            [Console]::Error.WriteLine("versao inesperada em ${target}: '$version' (codigo $LASTEXITCODE, esperada $expectedVersion)")
            exit 1
        }

        $null = & $target --help
        if ($LASTEXITCODE -ne 0) {
            [Console]::Error.WriteLine("--help falhou em $target (codigo $LASTEXITCODE)")
            exit 1
        }

        & $target selftest
        if ($LASTEXITCODE -ne 0) {
            [Console]::Error.WriteLine("selftest falhou em $target (codigo $LASTEXITCODE)")
            exit 1
        }
    }

    Write-Output "Smoke de $Rid aprovado: $version"
}
finally {
    Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
