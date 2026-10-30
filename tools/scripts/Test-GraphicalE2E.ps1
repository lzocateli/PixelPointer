<#
.SYNOPSIS
Executa E2E X11 isolados do pacote Native AOT com video obrigatorio.
.DESCRIPTION
O host somente orquestra containers. Constroi a imagem fixada quando solicitado,
cria rede privada e nginx sem portas publicadas, executa o runner uv sem rede
externa e preserva evidencias em artifacts/e2e. Nao acessa a sessao grafica do host.
.PARAMETER Package
Diretorio do pacote linux-x64. Padrao: artifacts/package/linux-x64.
.PARAMETER Build
Constroi a imagem antes dos testes. Exige acesso aos registries e ao snapshot Debian.
.PARAMETER Repeat
Numero de execucoes limpas consecutivas (1 a 10). Padrao: 1. Para estabilidade, use 10.
.PARAMETER InjectFailure
Interrompe o daemon durante o cenario. Deve retornar codigo diferente de zero.
.PARAMETER NoCompositor
Executa a variante sem compositor e exige captura congelada com aviso.
.PARAMETER NoTray
Executa a variante sem painel StatusNotifier e verifica status/quit.
.PARAMETER Engine
docker ou podman; padrao: PIXELPOINTER_CONTAINER_ENGINE, depois Docker ou Podman.
.PARAMETER RemainingArguments
Somente --help. Ajuda nao produz efeitos colaterais.
.EXAMPLE
pwsh ./tools/scripts/Test-GraphicalE2E.ps1 -Build -Engine podman
.EXAMPLE
pwsh ./tools/scripts/Test-GraphicalE2E.ps1 -Repeat 10 -Engine podman
.NOTES
Requer PowerShell 7, Linux x64, Docker/Podman e pacote Native AOT previamente gerado.
Prazo por container: 180 segundos. Videos nao sao publicados nem removidos automaticamente.
Codigo 0: todas as verificacoes passaram. Codigo 1: falha. Codigo 2: uso invalido.
#>
#requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $Package = 'artifacts/package/linux-x64',
    [switch] $Build,
    [ValidateRange(1, 10)] [int] $Repeat = 1,
    [switch] $InjectFailure,
    [switch] $NoCompositor,
    [switch] $NoTray,
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
$image = 'localhost/pixelpointer-e2e:x11-v1'
if ([string]::IsNullOrWhiteSpace($Engine)) { $Engine = $env:PIXELPOINTER_CONTAINER_ENGINE }
if ([string]::IsNullOrWhiteSpace($Engine)) {
    $Engine = @('docker', 'podman') | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
}
if ($Engine -notin @('docker', 'podman') -or -not (Get-Command $Engine -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('Docker ou Podman obrigatorio. Use --help.')
    exit 2
}

try {
    $packagePath = [IO.Path]::GetFullPath($Package, $root)
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    if (-not $packagePath.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
        throw 'O pacote deve estar dentro de artifacts. Use --help.'
    }
    $current = Get-Item -LiteralPath $packagePath -Force
    while ($current.FullName -ne $root) {
        if ($current.LinkType) { throw 'Links simbolicos nao sao aceitos no caminho do pacote.' }
        $current = $current.Parent
    }
    if (-not (Test-Path -LiteralPath (Join-Path $packagePath 'pixelpointer') -PathType Leaf)) {
        throw 'Pacote linux-x64 ausente. Execute Publish-Aot.ps1 antes. Use --help.'
    }
    if (Get-ChildItem -LiteralPath $packagePath -Recurse -Force | Where-Object LinkType) {
        throw 'O pacote nao pode conter links simbolicos.'
    }
    if ($Build) {
        & $Engine build --tag $image --file (Join-Path $root 'tests/e2e/Containerfile') (Join-Path $root 'tests/e2e')
        if ($LASTEXITCODE -ne 0) { throw 'Build da imagem E2E falhou.' }
    }
    $imageId = & $Engine image inspect $image --format '{{.Id}}'
    if ($LASTEXITCODE -ne 0) { throw 'Imagem E2E ausente. Use -Build.' }
    & $Engine run --rm --network none --read-only --cap-drop ALL --security-opt no-new-privileges --entrypoint python $imageId -B -m unittest -v test_evidence
    if ($LASTEXITCODE -ne 0) { throw 'Testes do coletor de evidencias falharam.' }

    $outputRoot = Join-Path $artifactRoot 'e2e'
    if (Test-Path -LiteralPath $outputRoot) {
        if ((Get-Item -LiteralPath $outputRoot -Force).LinkType) { throw 'artifacts/e2e nao pode ser link.' }
    }
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $runId = 'x11-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $output = Join-Path $outputRoot $runId
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $network = "pixelpointer-$runId"
    $proxy = "$network-nginx"
    $networkCreated = $false
    $proxyCreated = $false
    try {
        & $Engine network create --internal $network | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar rede privada.' }
        $networkCreated = $true
        & $Engine run --detach --rm --name $proxy --network $network --read-only --cap-drop ALL --security-opt no-new-privileges --pids-limit 32 --memory 128m --tmpfs /tmp:rw,nosuid,nodev,size=32m --entrypoint nginx $imageId -c /opt/nginx.conf -g 'daemon off;' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar nginx.' }
        $proxyCreated = $true
        for ($iteration = 1; $iteration -le $Repeat; $iteration++) {
            & $Engine exec $proxy python -c 'import http.client; connection = http.client.HTTPConnection("127.0.0.1", 8080, timeout=3); connection.request("GET", "/"); assert connection.getresponse().status == 404'
            if ($LASTEXITCODE -ne 0) { throw 'nginx indisponivel; execucao reprovada.' }
            $container = "$network-run-$iteration"
            $volume = "$container-evidence"
            & $Engine volume create $volume | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar volume de evidencias.' }
            $copied = $false
            $arguments = @('run', '--detach', '--name', $container, '--network', $network,
                '--read-only', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
                '--pids-limit', '128', '--memory', '2g', '--cpus', '2',
                '--tmpfs', '/tmp:rw,nosuid,nodev,size=512m',
                '--volume', "${packagePath}:/package:ro,Z", '--volume', "${volume}:/evidence",
                '--env', "E2E_IMAGE_ID=$imageId")
            $arguments += @($imageId, '--output', '/evidence/results')
            if ($InjectFailure) { $arguments += '--inject-failure' }
            if ($NoCompositor) { $arguments += '--no-compositor' }
            if ($NoTray) { $arguments += '--no-tray' }
            $containerCreated = $false
            try {
                & $Engine @arguments | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar container grafico.' }
                $containerCreated = $true
                $wait = Start-Process -FilePath $Engine -ArgumentList @('wait', $container) -PassThru -NoNewWindow
                if (-not $wait.WaitForExit(180000)) {
                    & $Engine kill $container | Out-Null
                    throw 'Timeout do container grafico; execucao reprovada.'
                }
                $state = & $Engine inspect $container --format '{{.State.ExitCode}}'
                if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel consultar resultado.' }
                $destination = Join-Path $output ('attempt-' + $iteration.ToString('00'))
                [IO.Directory]::CreateDirectory($destination) | Out-Null
                & $Engine cp "${container}:/evidence/results/." $destination
                if ($LASTEXITCODE -ne 0) { throw 'Evidencias ausentes; execucao reprovada.' }
                $copied = $true
                Write-Output "Evidencias: $destination"
                $resultPath = Join-Path $destination 'result.json'
                if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Relatorio ausente.' }
                $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
                $proxyRunning = & $Engine inspect $proxy --format '{{.State.Running}}'
                if ($LASTEXITCODE -ne 0 -or $proxyRunning.Trim() -ne 'true') { throw 'nginx encerrou durante o cenario.' }
                if ($state.Trim() -ne '0' -or -not $result.passed) {
                    throw "E2E reprovado: $($result.error)"
                }
            }
            finally {
                if ($containerCreated) { & $Engine rm --force $container | Out-Null }
                if ($copied) { & $Engine volume rm $volume | Out-Null }
                else { Write-Warning "Volume preservado para recuperacao: $volume" }
            }
        }
    }
    finally {
        if ($proxyCreated) { & $Engine rm --force $proxy | Out-Null }
        if ($networkCreated) { & $Engine network rm $network | Out-Null }
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
exit 0
