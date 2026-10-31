<#
.SYNOPSIS
Valida a captura ScreenCast + PipeWire do pacote linux-x64 em um Wayland descartavel.
.DESCRIPTION
O host somente orquestra o container. Dentro dele sobem sway headless, PipeWire,
WirePlumber, xdg-desktop-portal e xdg-desktop-portal-wlr, todos isolados, sem rede e
sem acesso a sessao grafica, D-Bus ou diretorios do host. O seletor de monitor do
xdg-desktop-portal-wlr e configurado para a unica saida virtual; o portal continua
aplicando suas regras. Evidencias ficam em artifacts/wayland.
.PARAMETER Package
Diretorio do pacote linux-x64 dentro de artifacts. Padrao: artifacts/package/linux-x64.
.PARAMETER Build
Constroi a imagem antes do teste. Exige acesso ao registry e ao snapshot Debian.
.PARAMETER RenderNode
No de renderizacao DRM do host (por exemplo /dev/dri/renderD128) exposto ao container.
O xdg-desktop-portal-wlr so inicia ScreenCast com DMA-BUF; sem esta opcao o teste
verifica que a indisponibilidade e tratada sem falha abrupta. Somente com Podman rootless e
--userns keep-id, para o usuario do container herdar o acesso do usuario do host ao no;
nenhum outro dispositivo e exposto.
.PARAMETER Engine
docker ou podman; padrao: PIXELPOINTER_CONTAINER_ENGINE, depois Docker ou Podman.
.PARAMETER RemainingArguments
Somente --help. Ajuda nao produz efeitos colaterais.
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Build -Engine podman
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -RenderNode /dev/dri/renderD128
.NOTES
Requer PowerShell 7, Linux x64, Docker/Podman e o pacote gerado por Publish-Aot.ps1.
Prazo do container: 120 segundos. Codigo 0: aprovado. Codigo 1: falha. Codigo 2: uso invalido.
#>
#requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $Package = 'artifacts/package/linux-x64',
    [switch] $Build,
    [string] $RenderNode,
    [ValidateSet('docker', 'podman')] [string] $Engine,
    [Parameter(ValueFromRemainingArguments)] [string[]] $RemainingArguments
)

$ErrorActionPreference = 'Stop'
if ($RemainingArguments -contains '--help') {
    Get-Help $PSCommandPath -Full
    exit 0
}
if ($RemainingArguments.Count -gt 0) {
    [Console]::Error.WriteLine('Argumento desconhecido. Use --help.')
    exit 2
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$image = 'localhost/pixelpointer-wayland:screencast-v1'
if ([string]::IsNullOrWhiteSpace($Engine)) { $Engine = $env:PIXELPOINTER_CONTAINER_ENGINE }
if ([string]::IsNullOrWhiteSpace($Engine)) {
    $Engine = @('docker', 'podman') | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
}
if ($Engine -notin @('docker', 'podman') -or -not (Get-Command $Engine -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('Docker ou Podman obrigatorio. Use --help.')
    exit 2
}

try {
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    $packagePath = [IO.Path]::GetFullPath($Package, $root)
    if (-not $packagePath.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
        throw 'O pacote deve estar dentro de artifacts. Use --help.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $packagePath 'pixelpointer') -PathType Leaf)) {
        throw 'Pacote linux-x64 ausente. Execute Publish-Aot.ps1 antes. Use --help.'
    }
    if (Get-ChildItem -LiteralPath $packagePath -Recurse -Force | Where-Object LinkType) {
        throw 'O pacote nao pode conter links simbolicos.'
    }
    $modeArguments = @('--env', 'EXPECT=unavailable')
    if (-not [string]::IsNullOrWhiteSpace($RenderNode)) {
        if ($RenderNode -notmatch '^/dev/dri/renderD[0-9]+$' -or -not (Test-Path -LiteralPath $RenderNode)) {
            throw 'RenderNode deve ser um no /dev/dri/renderD* existente. Use --help.'
        }
        if ($Engine -ne 'podman') {
            throw 'RenderNode exige Podman rootless. Use --help.'
        }
        $modeArguments = @('--device', $RenderNode, '--userns', 'keep-id:uid=1000,gid=1000', '--env', 'EXPECT=screencast',
            '--env', 'WLR_RENDERER=gles2', '--env', "WLR_RENDER_DRM_DEVICE=$RenderNode")
    }
    if ($Build) {
        & $Engine build --tag $image --file (Join-Path $root 'tests/wayland/Containerfile') (Join-Path $root 'tests/wayland')
        if ($LASTEXITCODE -ne 0) { throw 'Build da imagem Wayland falhou.' }
    }
    $imageId = & $Engine image inspect $image --format '{{.Id}}'
    if ($LASTEXITCODE -ne 0) { throw 'Imagem Wayland ausente. Use -Build.' }

    $outputRoot = Join-Path $artifactRoot 'wayland'
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $runId = 'screencast-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $output = Join-Path $outputRoot $runId
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $container = "pixelpointer-$runId"
    $volume = "$container-evidence"
    $created = $false
    $volumeCreated = $false
    try {
        & $Engine volume create $volume | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar volume de evidencias.' }
        $volumeCreated = $true
        & $Engine run --detach --name $container --network none --read-only --cap-drop ALL `
            --security-opt no-new-privileges --pids-limit 256 --memory 2g --cpus 2 `
            --tmpfs '/tmp:rw,nosuid,nodev,size=512m' --volume "${volume}:/evidence" `
            --volume "${packagePath}:/package:ro,Z" @modeArguments $imageId | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o container Wayland.' }
        $created = $true
        $wait = Start-Process -FilePath $Engine -ArgumentList @('wait', $container) -PassThru -NoNewWindow
        if (-not $wait.WaitForExit(120000)) {
            & $Engine kill $container | Out-Null
            throw 'Timeout do container Wayland; execucao reprovada.'
        }
        $exitCode = [int](& $Engine inspect $container --format '{{.State.ExitCode}}')
    }
    finally {
        if ($created) {
            & $Engine cp "${container}:/evidence/." $output | Out-Null
            & $Engine logs $container *> (Join-Path $output 'container.log')
            & $Engine rm --force $container | Out-Null
        }
        if ($volumeCreated) {
            & $Engine volume rm --force $volume | Out-Null
        }
    }

    Write-Output "Evidencias: $output"
    if ($exitCode -ne 0) {
        [Console]::Error.WriteLine("Validacao Wayland reprovada (codigo $exitCode). Consulte timeline.log.")
        exit 1
    }
    Write-Output 'Validacao Wayland ScreenCast aprovada.'
    exit 0
}
catch {
    [Console]::Error.WriteLine("erro: $($_.Exception.Message)")
    exit 1
}
