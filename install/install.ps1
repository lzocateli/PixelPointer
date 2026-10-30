<#
.SYNOPSIS
Instala o pixelpointer no Windows a partir das releases do GitHub.

.DESCRIPTION
Baixa pixelpointer-win-<arch>.zip e SHA256SUMS da release, confere o SHA-256,
testa o aplicativo (pixelpointer.exe, SDL3.dll e libSkiaSharp.dll) ao lado do
destino e o instala em -InstallDir, substituindo a versao anterior somente
depois que a nova executa. Compativel com Windows PowerShell 5.1 e PowerShell 7.

O instalador nao altera o PATH nem o registro: se o diretorio de destino nao
estiver no PATH, ele mostra os comandos a executar, um que grava o PATH do
usuario no registro e outro, para ambientes que bloqueiam o registro, que so
acrescenta o diretorio ao perfil do PowerShell.

.PARAMETER Version
Versao a instalar (X.Y.Z ou X.Y.Z-sufixo). Padrao: latest.

.PARAMETER InstallDir
Diretorio absoluto de destino. Padrao: %LOCALAPPDATA%\Programs\pixelpointer.

.PARAMETER BaseUrl
Diretorio com os pacotes e o SHA256SUMS (espelho ou testes). Exige https://,
exceto http://127.0.0.1 e http://localhost. Nao combina com -Version.

.PARAMETER Help
Mostra esta ajuda (equivalente a --help).

.EXAMPLE
powershell -ExecutionPolicy Bypass -File install.ps1

.EXAMPLE
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1))) -Version 0.1.0

A primeira linha habilita TLS 1.2 (exigido pelo GitHub) no Windows PowerShell 5.1
antes de baixar o instalador.

.NOTES
Plataformas: win-x64 e win-arm64. Codigos de saida (execucao com -File):
0 sucesso; 1 falha; 2 erro de uso. Executado como scriptblock, erros sao
lancados como excecao para nao encerrar a sessao do PowerShell.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $Version = 'latest',

    [string] $InstallDir,

    [string] $BaseUrl,

    [switch] $Help,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $RemainingArguments
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$script:RunAsFile = -not [string]::IsNullOrEmpty($PSCommandPath)
$repositoryUrl = 'https://github.com/lzocateli/PixelPointer/releases'

$packageFiles = @('pixelpointer.exe', 'SDL3.dll', 'libSkiaSharp.dll', 'LICENSE', 'THIRD-PARTY-NOTICES.md')

# Antivirus pode manter o arquivo recem-gravado aberto por alguns instantes.
function Move-DirectoryWithRetry {
    param([string] $Source, [string] $Destination)

    for ($attempt = 1; ; $attempt++) {
        try {
            [IO.Directory]::Move($Source, $Destination)
            return
        }
        catch [IO.IOException], [UnauthorizedAccessException] {
            if ($attempt -ge 10) {
                throw
            }
            Start-Sleep -Milliseconds 300
        }
    }
}

function Exit-Install {
    param([int] $Code, [string] $Message)

    if ($Code -eq 2) {
        $Message = "$Message. Use -Help."
    }
    if ($script:RunAsFile) {
        [Console]::Error.WriteLine("pixelpointer-install: $Message")
        exit $Code
    }
    throw "pixelpointer-install: $Message"
}

if ($Help -or ($null -ne $RemainingArguments -and ($RemainingArguments -contains '--help' -or $RemainingArguments -contains '-h'))) {
    if ($script:RunAsFile) {
        Get-Help $PSCommandPath -Full
    }
    else {
        Write-Output 'Uso: install.ps1 [-Version <X.Y.Z|latest>] [-InstallDir <dir>] [-BaseUrl <url>] [-Help]'
    }
    return
}

if ($null -ne $RemainingArguments -and $RemainingArguments.Count -gt 0) {
    Exit-Install 2 "argumento desconhecido: $($RemainingArguments -join ' ')"
}

$onWindows = ($PSVersionTable.PSEdition -ne 'Core') -or ((Get-Variable -Name IsWindows -ValueOnly -ErrorAction SilentlyContinue) -eq $true)
if (-not $onWindows) {
    Exit-Install 1 'este instalador e para Windows; no Linux e macOS use install.sh'
}

$versionSet = $PSBoundParameters.ContainsKey('Version')
if ($Version.StartsWith('v')) {
    $Version = $Version.Substring(1)
}
if ($Version -ne 'latest' -and $Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$') {
    Exit-Install 2 "versao invalida: $Version"
}

if (-not [string]::IsNullOrEmpty($BaseUrl)) {
    if ($versionSet) {
        Exit-Install 2 '-BaseUrl e -Version sao mutuamente exclusivos'
    }
    $parsed = $null
    if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref] $parsed)) {
        Exit-Install 2 "-BaseUrl invalida: $BaseUrl"
    }
    if (-not [string]::IsNullOrEmpty($parsed.UserInfo)) {
        Exit-Install 2 '-BaseUrl nao aceita credenciais'
    }
    $loopback = $parsed.Host -eq '127.0.0.1' -or $parsed.Host -eq 'localhost'
    if ($parsed.Scheme -ne 'https' -and -not ($parsed.Scheme -eq 'http' -and $loopback)) {
        Exit-Install 2 '-BaseUrl deve usar https:// (http:// somente para 127.0.0.1 ou localhost)'
    }
    $assetUrl = $BaseUrl.TrimEnd('/')
}
elseif ($Version -eq 'latest') {
    $assetUrl = "$repositoryUrl/latest/download"
}
else {
    $assetUrl = "$repositoryUrl/download/v$Version"
}

if ([string]::IsNullOrEmpty($InstallDir)) {
    if ([string]::IsNullOrEmpty($env:LOCALAPPDATA)) {
        Exit-Install 1 'LOCALAPPDATA nao definido; informe -InstallDir'
    }
    $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\pixelpointer'
}
if ($InstallDir -notmatch '^([A-Za-z]:\\|\\\\[^\\]+\\[^\\]+)') {
    Exit-Install 2 '-InstallDir deve ser um caminho absoluto'
}
$InstallDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')

# Valor nativo do registro: nao e afetado pela emulacao x64/x86 em Windows ARM64.
$nativeArchitecture = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' -Name PROCESSOR_ARCHITECTURE).PROCESSOR_ARCHITECTURE
switch ($nativeArchitecture) {
    'AMD64' { $rid = 'win-x64' }
    'ARM64' { $rid = 'win-arm64' }
    default { Exit-Install 1 "arquitetura nao suportada: $nativeArchitecture" }
}
$asset = "pixelpointer-$rid.zip"

if ($PSVersionTable.PSEdition -ne 'Core') {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
}

$workDirectory = Join-Path ([IO.Path]::GetTempPath()) ('pixelpointer-install-' + [Guid]::NewGuid().ToString('N'))
$stagedDirectory = $null
$previousProgress = $ProgressPreference
try {
    $ProgressPreference = 'SilentlyContinue'
    $null = New-Item -ItemType Directory -Path $workDirectory

    Write-Output "Baixando $asset de $assetUrl"
    $checksums = Join-Path $workDirectory 'SHA256SUMS'
    $package = Join-Path $workDirectory $asset
    try {
        Invoke-WebRequest -Uri "$assetUrl/SHA256SUMS" -OutFile $checksums -UseBasicParsing
    }
    catch {
        Exit-Install 1 "falha ao baixar SHA256SUMS de ${assetUrl}: $($_.Exception.Message)"
    }
    try {
        Invoke-WebRequest -Uri "$assetUrl/$asset" -OutFile $package -UseBasicParsing
    }
    catch {
        Exit-Install 1 "falha ao baixar $asset (versao ou plataforma inexistente?): $($_.Exception.Message)"
    }

    $expected = @(Get-Content -LiteralPath $checksums |
        Where-Object { $_ -cmatch '^[0-9a-f]{64}  (.+)$' -and $Matches[1] -ceq $asset } |
        ForEach-Object { $_.Substring(0, 64) })
    if ($expected.Count -ne 1) {
        Exit-Install 1 "SHA256SUMS nao contem uma entrada valida para $asset"
    }
    $actual = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne $expected[0]) {
        Exit-Install 1 "SHA-256 de $asset nao confere (esperado $($expected[0]), obtido $actual); nada foi instalado"
    }
    Write-Output "SHA-256 conferido: $actual"

    $extractDirectory = Join-Path $workDirectory 'extract'
    Expand-Archive -LiteralPath $package -DestinationPath $extractDirectory
    foreach ($name in $packageFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $extractDirectory $name) -PathType Leaf)) {
            Exit-Install 1 "$asset nao contem $name"
        }
    }

    $target = Join-Path $InstallDir 'pixelpointer.exe'
    if (Test-Path -LiteralPath $InstallDir -PathType Leaf) {
        Exit-Install 1 "$InstallDir e um arquivo; remova-o antes de instalar"
    }
    # Nunca substitui um diretorio que nao seja do PixelPointer.
    if ((Test-Path -LiteralPath $InstallDir -PathType Container) -and
        @(Get-ChildItem -LiteralPath $InstallDir -Force).Count -gt 0 -and
        -not (Test-Path -LiteralPath $target -PathType Leaf)) {
        Exit-Install 1 "$InstallDir nao esta vazio e nao contem o pixelpointer; remova-o antes de instalar"
    }

    $parent = Split-Path -Parent $InstallDir
    $null = New-Item -ItemType Directory -Force -Path $parent
    Get-ChildItem -LiteralPath $parent -Filter '.pixelpointer.old.*' -Directory -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    $stagedDirectory = Join-Path $parent ".pixelpointer.install.$PID"
    $null = New-Item -ItemType Directory -Force -Path $stagedDirectory
    foreach ($name in $packageFiles) {
        Copy-Item -LiteralPath (Join-Path $extractDirectory $name) -Destination (Join-Path $stagedDirectory $name) -Force
    }

    $stagedBinary = Join-Path $stagedDirectory 'pixelpointer.exe'
    try {
        $installedVersion = & $stagedBinary --version
        $versionExitCode = $LASTEXITCODE
        if ($versionExitCode -eq 0) {
            $null = & $stagedBinary selftest
            $versionExitCode = $LASTEXITCODE
        }
    }
    catch {
        $versionExitCode = -1
    }
    if ($versionExitCode -ne 0) {
        Exit-Install 1 "o aplicativo baixado nao executa nesta plataforma ($rid); nada foi substituido"
    }

    # Troca de diretorios: o backup restaura a versao anterior se a troca falhar e e removido em seguida.
    $backup = $null
    try {
        if (Test-Path -LiteralPath $InstallDir -PathType Container) {
            $backup = Join-Path $parent ".pixelpointer.old.$PID"
            Move-DirectoryWithRetry -Source $InstallDir -Destination $backup
        }
        Move-DirectoryWithRetry -Source $stagedDirectory -Destination $InstallDir
    }
    catch {
        if ($null -ne $backup -and -not (Test-Path -LiteralPath $InstallDir) -and (Test-Path -LiteralPath $backup)) {
            [IO.Directory]::Move($backup, $InstallDir)
        }
        Exit-Install 1 "falha ao instalar em ${InstallDir} (feche o PixelPointer se estiver em execucao): $($_.Exception.Message)"
    }
    $stagedDirectory = $null
    if ($null -ne $backup) {
        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Output "pixelpointer $installedVersion instalado em $target"

    $pathEntries = @(
        [Environment]::GetEnvironmentVariable('Path', 'User'),
        [Environment]::GetEnvironmentVariable('Path', 'Machine')
    ) -join ';' -split ';' | Where-Object { $_ } | ForEach-Object { $_.TrimEnd('\') }
    if ($pathEntries -notcontains $InstallDir) {
        Write-Output ''
        Write-Output "Aviso: $InstallDir nao esta no PATH. Para adicionar ao PATH do usuario:"
        Write-Output "  [Environment]::SetEnvironmentVariable('Path', '$InstallDir;' + [Environment]::GetEnvironmentVariable('Path', 'User'), 'User')"
        # A linha do perfil usa $env:LOCALAPPDATA quando possivel: fica ASCII mesmo com nome de usuario acentuado,
        # que o Add-Content do Windows PowerShell 5.1 gravaria em ANSI.
        $pathExpression = "'$($InstallDir.Replace("'", "''"))'"
        if (-not [string]::IsNullOrEmpty($env:LOCALAPPDATA)) {
            $localPrefix = $env:LOCALAPPDATA.TrimEnd('\') + '\'
            if ($InstallDir.StartsWith($localPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                $relative = $InstallDir.Substring($localPrefix.Length)
                $pathExpression = "(`$env:LOCALAPPDATA + '\$($relative.Replace("'", "''"))')"
            }
        }
        $profileLine = "`$env:PATH = $pathExpression + ';' + `$env:PATH"
        Write-Output 'Sem alterar o registro (somente terminais PowerShell):'
        Write-Output '  New-Item -ItemType Directory -Force -Path (Split-Path $PROFILE.CurrentUserAllHosts) | Out-Null'
        Write-Output "  Add-Content -LiteralPath `$PROFILE.CurrentUserAllHosts -Value @('', '$($profileLine.Replace("'", "''"))')"
    }

    Write-Output ''
    Write-Output 'Proximo passo: pixelpointer daemon (atalhos Ctrl+1 zoom e Ctrl+2 desenho)'
}
finally {
    $ProgressPreference = $previousProgress
    if ($null -ne $stagedDirectory -and (Test-Path -LiteralPath $stagedDirectory)) {
        Remove-Item -LiteralPath $stagedDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
