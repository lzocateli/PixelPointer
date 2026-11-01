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
.PARAMETER DenyScreenCastGetAll
Injeta recusa de Properties.GetAll pela politica do D-Bus descartavel, sem
substituir o portal. Exige RenderNode e um pacote com diagnostico de capacidades;
ambas as capturas devem usar Get e continuar no ScreenCast/PipeWire.
.PARAMETER RepeatDaemonCaptures
Depois do baseline, inicia um daemon e exercita draw/zoom por IPC na mesma
conexao. Confere quadros, status e quit; com recusa de GetAll exige uma unica
rejeicao do processo do daemon. Exige RenderNode. Nao testa atalhos fisicos.
.PARAMETER CheckDaemonLifecycle
Exige RepeatDaemonCaptures. Testa quit com sobreposicao aberta, status parado,
reinicio com nova conexao do portal e captura valida no daemon reiniciado.
.PARAMETER RemainingArguments
Somente --help. Ajuda nao produz efeitos colaterais.
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Build -Engine podman
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -RenderNode /dev/dri/renderD128
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -RenderNode /dev/dri/renderD128 -DenyScreenCastGetAll
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -RenderNode /dev/dri/renderD128 -DenyScreenCastGetAll -RepeatDaemonCaptures
.EXAMPLE
pwsh ./tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle
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
    [switch] $DenyScreenCastGetAll,
    [switch] $RepeatDaemonCaptures,
    [switch] $CheckDaemonLifecycle,
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
    if ($CheckDaemonLifecycle -and -not $RepeatDaemonCaptures) {
        throw 'CheckDaemonLifecycle exige RepeatDaemonCaptures. Use --help.'
    }
    if (($DenyScreenCastGetAll -or $RepeatDaemonCaptures) -and [string]::IsNullOrWhiteSpace($RenderNode)) {
        throw 'DenyScreenCastGetAll e RepeatDaemonCaptures exigem RenderNode para validar captura real. Use --help.'
    }
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
    if ($DenyScreenCastGetAll) {
        $originalConfig = & $Engine run --rm --network none --read-only --cap-drop ALL `
            --security-opt no-new-privileges --entrypoint /bin/cat $imageId /usr/share/dbus-1/session.conf
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao ler configuracao D-Bus da imagem.' }
        $settings = [System.Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
        $settings.XmlResolver = $null
        $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]::new(($originalConfig -join "`n")), $settings)
        $config = [System.Xml.XmlDocument]::new()
        $config.XmlResolver = $null
        try { $config.Load($reader) }
        finally { $reader.Dispose() }
        if ($config.DocumentElement.Name -ne 'busconfig') { throw 'Configuracao D-Bus inesperada.' }
        $policy = $config.CreateElement('policy')
        $policy.SetAttribute('context', 'mandatory')
        $deny = $config.CreateElement('deny')
        $deny.SetAttribute('send_destination', 'org.freedesktop.portal.Desktop')
        $deny.SetAttribute('send_interface', 'org.freedesktop.DBus.Properties')
        $deny.SetAttribute('send_member', 'GetAll')
        $deny.SetAttribute('send_type', 'method_call')
        $null = $policy.AppendChild($deny)
        $null = $config.DocumentElement.AppendChild($policy)
        $configPath = Join-Path $output 'dbus-session.conf'
        $stream = [System.IO.File]::Open($configPath, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try { $config.Save($stream) }
        finally { $stream.Dispose() }
        $modeArguments += @('--volume', "${configPath}:/usr/share/dbus-1/session.conf:ro,Z")
    }
    $container = "pixelpointer-$runId"
    if ($RepeatDaemonCaptures) {
        $driver = Join-Path $root 'tests/wayland/daemon.py'
        $driverFile = Get-Item -LiteralPath $driver -Force
        if ($driverFile.PSIsContainer -or ($driverFile.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw 'Driver Wayland invalido ou link recusado.'
        }
        $deny = if ($DenyScreenCastGetAll) { '1' } else { '0' }
        $lifecycle = if ($CheckDaemonLifecycle) { '1' } else { '0' }
        $modeArguments += @('--volume', "${driver}:/opt/wayland/daemon.py:ro,Z", '--env', "DENY_GETALL=$deny",
            '--env', "CHECK_LIFECYCLE=$lifecycle",
            '--entrypoint', 'python')
    }
    $volume = "$container-evidence"
    $created = $false
    $volumeCreated = $false
    try {
        & $Engine volume create $volume | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar volume de evidencias.' }
        $volumeCreated = $true
        [string[]] $entryArguments = if ($RepeatDaemonCaptures) { @('/opt/wayland/daemon.py') } else { @() }
        & $Engine run --detach --name $container --network none --read-only --cap-drop ALL `
            --security-opt no-new-privileges --pids-limit 256 --memory 2g --cpus 2 `
            --tmpfs '/tmp:rw,nosuid,nodev,size=512m' --volume "${volume}:/evidence" `
            --volume "${packagePath}:/package:ro,Z" @modeArguments $imageId @entryArguments | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o container Wayland.' }
        $created = $true
        $wait = Start-Process -FilePath $Engine -ArgumentList @('wait', $container) -PassThru -NoNewWindow
        if (-not $wait.WaitForExit(120000)) {
            & $Engine kill $container | Out-Null
            throw 'Timeout do container Wayland; execucao reprovada.'
        }
        $exitCode = [int](& $Engine inspect $container --format '{{.State.ExitCode}}')
        if ($exitCode -ne 0) {
            $runtimeError = & $Engine inspect $container --format '{{.State.Error}}'
            if ($runtimeError) { [Console]::Error.WriteLine("Runtime do container: $runtimeError") }
        }
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
        if ($RepeatDaemonCaptures) {
            @{ passed = $false; mode = 'daemon-screencast'; error = 'container failed' } |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8NoBOM
        }
        [Console]::Error.WriteLine("Validacao Wayland reprovada (codigo $exitCode). Consulte timeline.log.")
        exit 1
    }
    if ($DenyScreenCastGetAll) {
        try {
            $rejectionPattern = 'Rejected send message.*interface="org\.freedesktop\.DBus\.Properties" member="GetAll".*destination="org\.freedesktop\.portal\.Desktop"'
            $rejections = @(Select-String -LiteralPath (Join-Path $output 'dbus.log') -Pattern $rejectionPattern)
            if ($rejections.Count -lt 2) { throw 'Politica D-Bus nao confirmou duas recusas de GetAll.' }
            foreach ($name in @('first.err', 'second.err')) {
                $diagnostic = Get-Content -LiteralPath (Join-Path $output $name) -Raw
                if ($diagnostic -notmatch '(?m)^ScreenCast capacidades: [0-9]+ ms; consulta Get\r?$') {
                    throw 'Captura nao confirmou o caminho Get apos recusa de GetAll.'
                }
            }
            @{ passed = $true; mode = 'getall-denied'; rejections = $rejections.Count } |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8NoBOM
        }
        catch {
            @{ passed = $false; mode = 'getall-denied'; error = $_.Exception.Message } |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8NoBOM
            throw
        }
        Write-Output 'Compatibilidade aprovada: GetAll recusado pela politica D-Bus, Get e duas capturas reais preservados.'
    }
    if ($RepeatDaemonCaptures) {
        try {
            $daemon = Get-Content -LiteralPath (Join-Path $output 'daemon-result.json') -Raw | ConvertFrom-Json
            $expectedMethod = if ($DenyScreenCastGetAll) { 'Get' } else { 'GetAll' }
            $expectedRejections = if ($DenyScreenCastGetAll) { 1 } else { 0 }
            $invalidQueries = @($daemon.queries | Where-Object { $_ -ne $expectedMethod }).Count -ne 0
            if ($daemon.passed -ne $true -or $daemon.quit -ne $true -or $daemon.sessions.Count -ne 2 -or
                $daemon.queries.Count -ne 2 -or $invalidQueries -or $daemon.rejections -ne $expectedRejections) {
                throw 'Resultado incompleto ou incoerente do daemon Wayland.'
            }
            if ($CheckDaemonLifecycle -and ($null -eq $daemon.lifecycle -or
                $daemon.lifecycle.quit_open_overlay -ne $true -or $daemon.lifecycle.stopped_status -ne $true -or
                $daemon.lifecycle.fresh_portal_connection -ne $true -or $daemon.lifecycle.restart_capture -ne $true)) {
                throw 'Ciclo de vida do daemon incompleto.'
            }
            @{ passed = $true; mode = 'daemon-screencast'; daemon = $daemon } |
                ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8NoBOM
        }
        catch {
            @{ passed = $false; mode = 'daemon-screencast'; error = $_.Exception.Message } |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8NoBOM
            throw
        }
        Write-Output 'Daemon aprovado: draw/zoom por IPC, capacidades atualizadas na mesma conexao, status e quit.'
    }
    Write-Output 'Validacao Wayland ScreenCast aprovada.'
    exit 0
}
catch {
    [Console]::Error.WriteLine("erro: $($_.Exception.Message)")
    exit 1
}
