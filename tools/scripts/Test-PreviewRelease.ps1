<#
.SYNOPSIS
Testa a publicacao de previews sem acessar o GitHub.
.DESCRIPTION
Usa arquivos reais descartaveis e funcoes git/gh sinteticas em processos filhos.
Confere checksum, caminhos, origem, criacao de rascunho, finalizacao e recusa
de releases ou assets incompativeis. Executar somente em container de validacao.
.PARAMETER RemainingArguments
Aceita somente --help.
.EXAMPLE
./tools/scripts/Test-PreviewRelease.ps1
.NOTES
Requer PowerShell 7 e Git; nao publica releases nem altera tags ou PATH.
#>
#requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments)][string[]] $RemainingArguments)
$ErrorActionPreference = 'Stop'
if ($RemainingArguments -contains '--help') { Get-Help $PSCommandPath -Full; exit 0 }
if ($RemainingArguments) { [Console]::Error.WriteLine('Argumento desconhecido. Use --help.'); exit 2 }

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$temporary = Join-Path $root "artifacts/.preview-release-tests-$([Guid]::NewGuid().ToString('N'))"
$directory = Join-Path $temporary 'assets'
$null = New-Item -ItemType Directory -Path $directory -Force
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Git HEAD indisponivel.' }
$count = 0
try {
    foreach ($rid in @('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')) {
        $extension = if ($rid.StartsWith('win-')) { 'zip' } else { 'tar.gz' }
        Set-Content -LiteralPath (Join-Path $directory "pixelpointer-$rid.$extension") -Value 'package fixture'
        Set-Content -LiteralPath (Join-Path $directory "pixelpointer-$rid.spdx.json") -Value '{}'
        if ($rid.StartsWith('linux-')) { Set-Content -LiteralPath (Join-Path $directory "pixelpointer-$rid.deb") -Value 'deb fixture' }
    }
    $checksumLines = @(Get-ChildItem -LiteralPath $directory -File | ForEach-Object {
        "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(), $_.Name
    })
    Set-Content -LiteralPath (Join-Path $directory 'SHA256SUMS') -Value $checksumLines
    $assets = @(Get-ChildItem -LiteralPath $directory -File | ForEach-Object {
        @{ name = $_.Name; digest = 'sha256:' + (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() }
    })
    $fixturePath = Join-Path $temporary 'release.json'
    @{ id = 42; draft = $true; prerelease = $true; html_url = 'https://example.invalid/preview'; assets = $assets } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $fixturePath

    function Invoke-Case {
        param([hashtable] $Overrides = @{}, [string] $Mode = 'draft', [int] $Expected = 0)
        $script:count++
        $calls = Join-Path $temporary "calls-$count.jsonl"
        $output = Join-Path $temporary "output-$count.txt"
        $errors = Join-Path $temporary "errors-$count.txt"
        $parameters = @{
            Version = '1.2.3-preview.42'; Commit = $commit; AssetDirectory = $directory
            EventName = 'workflow_dispatch'; Ref = 'refs/heads/main'
        }
        foreach ($key in $Overrides.Keys) { $parameters[$key] = $Overrides[$key] }
        $parametersJson = $parameters | ConvertTo-Json -Compress
        $program = @"
`$parameters = '$($parametersJson.Replace("'", "''"))' | ConvertFrom-Json -AsHashtable
`$global:mode = '$Mode'
`$global:fixture = Get-Content -LiteralPath '$fixturePath' -Raw | ConvertFrom-Json -AsHashtable
`$global:commit = '$commit'
`$global:calls = '$calls'
function global:git {
    `$global:LASTEXITCODE = 0
    if (`$args.Count -ne 6 -or `$args[2] -ne 'merge-base' -or `$args[3] -ne '--is-ancestor' -or `$args[-2] -ne `$global:commit -or `$args[-1] -ne 'origin/main' -or `$global:mode -eq 'wrong-ancestry') {
        `$global:LASTEXITCODE = 1
    }
}
function global:gh {
    `$arguments = @(`$args)
    `$method = `$arguments[[Array]::IndexOf(`$arguments, '--method') + 1]
    `$path = `$arguments[-1]
    `$payload = (`$input | Out-String).Trim()
    @{ method = `$method; body = `$payload } | ConvertTo-Json -Compress | Add-Content -LiteralPath `$global:calls
    `$global:LASTEXITCODE = 0
    if (`$global:mode -eq 'api-error') { Write-Error 'HTTP 403' -ErrorAction Continue; `$global:LASTEXITCODE = 1; return }
    if (`$path -like '*/git/ref/tags/*') {
        if (`$global:mode -eq 'existing-tag') { @{ object = @{ sha = `$global:commit } } | ConvertTo-Json; return }
        Write-Error 'HTTP 404' -ErrorAction Continue; `$global:LASTEXITCODE = 1; return
    }
    if (`$path -like '*/commits/*') {
        `$sha = if (`$global:mode -eq 'wrong-commit') { 'b' * 40 } else { `$global:commit }
        @{ sha = `$sha } | ConvertTo-Json; return
    }
    if (`$path -like '*/releases?per_page=*') {
        if (`$global:mode -in @('new', 'existing-tag')) { '[]'; return }
        if (`$global:mode -eq 'published') { `$global:fixture.draft = `$false }
        if (`$global:mode -eq 'stable') { `$global:fixture.prerelease = `$false }
        if (`$global:mode -eq 'bad-digest') { `$global:fixture.assets[0].digest = 'sha256:' + ('0' * 64) }
        if (`$global:mode -eq 'missing-assets') { `$global:fixture.assets = @() }
        `$global:fixture.tag_name = 'v1.2.3-preview.42'
        ConvertTo-Json -InputObject @(`$global:fixture) -Depth 8; return
    }
    `$global:fixture | ConvertTo-Json -Depth 8
}
& '$(Join-Path $PSScriptRoot 'Set-PreviewRelease.ps1')' @parameters
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($program))
        $process = Start-Process pwsh -ArgumentList @('-NoProfile', '-EncodedCommand', $encoded) -Wait -PassThru `
            -RedirectStandardOutput $output -RedirectStandardError $errors
        if ($process.ExitCode -ne $Expected) { throw "Caso $count ($Mode): codigo $($process.ExitCode), esperado $Expected. $(Get-Content -LiteralPath $errors -Raw)" }
        $requests = if (Test-Path -LiteralPath $calls) { @(Get-Content -LiteralPath $calls | ConvertFrom-Json) } else { @() }
        $mutations = @($requests | Where-Object method -in @('POST', 'PATCH'))
        if ($Expected -ne 0 -and $mutations.Count -ne 0) { throw "Caso $count alterou GitHub antes da recusa." }
        if ($Expected -eq 0) {
            if (-not $parameters.Publish -or $mutations.Count) {
                $body = if ($mutations.Count) { $mutations[-1].body | ConvertFrom-Json } else { Get-Content -LiteralPath $output -Raw | ConvertFrom-Json }
                $isFinal = $parameters.Stage -eq 'publish'
                if ($body.prerelease -ne $true -or $body.make_latest -ne 'false' -or $body.draft -ne (-not $isFinal)) {
                    throw "Caso ${count}: flags de publicacao incorretas."
                }
            }
        }
    }

    Invoke-Case
    Invoke-Case @{ Stage = 'publish' }
    Invoke-Case @{ Publish = $true } 'new'
    Invoke-Case @{ Publish = $true }
    Invoke-Case @{ Publish = $true; Stage = 'publish' }
    foreach ($mode in @('bad-digest', 'missing-assets', 'published', 'stable', 'wrong-commit', 'wrong-ancestry', 'api-error', 'existing-tag', 'new')) {
        Invoke-Case @{ Publish = $true; Stage = 'publish' } $mode 1
    }
    Invoke-Case @{ Publish = $true } 'existing-tag' 1
    Invoke-Case @{ Version = '1.2.3' } 'draft' 1
    Invoke-Case @{ Version = '1.2.3-preview.042' } 'draft' 1
    Invoke-Case @{ EventName = 'pull_request' } 'draft' 1
    Invoke-Case @{ Ref = 'refs/heads/feature' } 'draft' 1
    Invoke-Case @{ Commit = 'short' } 'draft' 1
    Invoke-Case @{ AssetDirectory = [IO.Path]::GetTempPath() } 'draft' 1
    Add-Content -LiteralPath (Join-Path $directory 'pixelpointer-win-x64.zip') -Value 'tampered'
    Invoke-Case @{} 'draft' 1
    Set-Content -LiteralPath (Join-Path $directory 'unexpected.txt') -Value 'unexpected'
    Invoke-Case @{} 'draft' 1
    Write-Output "Aprovado: $count casos de preview; filesystem real, Git/API sinteticos, nenhuma publicacao."
}
finally {
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
