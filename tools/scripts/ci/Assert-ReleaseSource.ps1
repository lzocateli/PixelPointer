#!/usr/bin/env pwsh
# Gerenciado pelo Altivy.Groundwork (componente release). Não edite: mudanças vão para o catálogo.

<#
.SYNOPSIS
Recusa novas publicações com SHA desatualizado, CI falho ou código diferente do RC aceito.
.DESCRIPTION
Somente leitura no GitHub. ReleasePlease libera atualização de PR comum, mas valida
release integrada antes da tag. Publication revalida após aprovação. Snapshot exige
Result verde da própria execução. Republish exige release já pública e tag imutável.
Publication com tag e Republish exigem revisão humana de distribution no mesmo run,
SHA e tag; quality e waiver não substituem autorização de divulgação.
.PARAMETER Repository
Repositório owner/nome.
.PARAMETER Commit
SHA completo do checkout aprovado.
.PARAMETER DefaultBranch
Branch padrão; main por padrão.
.PARAMETER Stage
Publication, ReleasePlease ou Snapshot.
.PARAMETER Stable
Exige equivalência com a candidata aceita quando obrigatória.
.PARAMETER Republish
Somente artefatos ausentes de uma release já pública.
.PARAMETER Preflight
Valida a fonte antes do build, sem autorizar publicação. Exige tag e é seguido por uma
revalidação com autorização imediatamente antes da publicação.
.PARAMETER Tag
Tag histórica no modo Republish.
.PARAMETER OutputPath
Saída GitHub: release_ready só é true para uma release integrada validada.
.EXAMPLE
./tools/scripts/ci/Assert-ReleaseSource.ps1 -Repository exemplo/app -Commit $env:GITHUB_SHA -Stable
.NOTES
PowerShell 7, git com fetch-depth 0, gh autenticado com contents/actions/checks/pull-requests read.
Saída 0 para fonte válida, 1 para recusa, 2 para argumento desconhecido; --help não acessa rede.
#>
#Requires -Version 7.0

[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $Repository = '',
    [string] $Commit = '',
    [string] $DefaultBranch = 'main',
    [ValidateSet('Publication', 'ReleasePlease', 'Snapshot')][string] $Stage = 'Publication',
    [switch] $Stable,
    [switch] $Republish,
    [switch] $Preflight,
    [string] $Tag = '',
    [string] $OutputPath = '',
    [Alias('h')][switch] $Help,
    [Parameter(ValueFromRemainingArguments)][string[]] $RemainingArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Help -or ($RemainingArguments | Where-Object { $_ -in @('--help', '-h', '/?', 'help') })) { Get-Help -Full $PSCommandPath; exit 0 }
if ($RemainingArguments) { [Console]::Error.WriteLine('Argumento desconhecido. Use --help.'); exit 2 }
Import-Module (Join-Path $PSScriptRoot 'GroundworkRelease.psm1') -Force

try {
    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Repositório inválido.' }
    if ($Commit -notmatch '^[0-9a-f]{40}$') { throw 'SHA inválido.' }
    if ($DefaultBranch -notmatch '^[A-Za-z0-9._/-]+$' -or $DefaultBranch.Contains('..')) { throw 'Branch padrão inválida.' }
    if ($Preflight -and ($Stage -ne 'Publication' -or -not (Test-ReleaseTagName $Tag))) { throw 'Preflight exige Stage Publication e uma tag de release válida.' }
    if ($Republish) {
        if (-not $Preflight) { Assert-DistributionApproval -Repository $Repository -Commit $Commit -Tag $Tag }
        if ($Tag -cnotmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Republish exige tag estável.' }
        $release = Find-GitHubRelease -Repository $Repository -Tag $Tag
        if ($null -eq $release -or $release['draft'] -or $release['prerelease']) { throw 'Republish histórico exige release estável já pública; rascunho exige nova validação de fonte.' }
        $tagCommit = Invoke-GhJson -Path "repos/$Repository/commits/$Tag"
        if ($tagCommit['sha'] -cne $Commit) { throw 'Checkout de republish difere da tag histórica.' }
        Write-Output "Republish histórico validado em $Tag/$Commit; nenhum novo código ou tag."
        exit 0
    }
    if ($Stage -eq 'Snapshot') {
        Assert-ReleaseDependencies -Repository $Repository -Commit $Commit -DefaultBranch $DefaultBranch
        Assert-CurrentReleaseSource -Repository $Repository -Commit $Commit -DefaultBranch $DefaultBranch -AllowRunningCi
        exit 0
    }
    $owner = $Repository.Split('/')[0]
    $pages = Invoke-GhJson -Path "repos/$Repository/pulls?state=closed&head=${owner}:release-please--branches--$DefaultBranch&per_page=100" -Paginate
    $mergedPulls = @($pages | Where-Object { $_['merged_at'] })
    $pending = @($mergedPulls | Where-Object { @($_['labels'] | Where-Object { $_['name'] -ceq 'autorelease: pending' }).Count -ne 0 })
    if ($Stage -eq 'ReleasePlease') {
        $currentPending = @($pending | Where-Object { $_['merge_commit_sha'] -ceq $Commit })
        if ($currentPending.Count -ne 1) {
            Write-GitHubOutput -Path $OutputPath -Values @{ release_ready = 'false' } | Out-Null
            if ($currentPending.Count -gt 1) {
                Write-Output "Atualização do Release PR: mais de um release PR integrado corresponde ao SHA $Commit; criação de tag e release desabilitada."
            }
            elseif ($pending.Count -gt 0) {
                $pendingShas = (@($pending | ForEach-Object { [string]$_['merge_commit_sha'] } | Where-Object { $_ }) -join ', ')
                Write-Output "Atualização do Release PR: release pendente em outro SHA ($pendingShas); release-please pode atualizar o PR, mas criação de tag e release estão desabilitadas até um merge corresponder ao HEAD atual $Commit."
            }
            else {
                Write-Output 'Atualização do Release PR: sem release integrada pendente de tag; criação de release desabilitada.'
            }
            exit 0
        }
        $pending = $currentPending
    }
    if ($Stage -eq 'ReleasePlease') {
        if ((Get-DistributionApprovalMode -Config (Read-CiCdConfig)) -ceq 'manual-dispatch') {
            if ($env:CONFIRM_DISTRIBUTION -cne 'true' -or $env:AUTHORIZE_COMMIT -cne $Commit) { throw "Distribuição manual exige confirmação explícita da tag e do SHA $Commit; dispare Release novamente como administrador." }
            $version = [string]((Get-Content -LiteralPath '.release-please-manifest.json' -Raw | ConvertFrom-Json -AsHashtable)['.'])
            $expectedTag = "v$version"
            if ($env:AUTHORIZE_TAG -cne $expectedTag) { throw "Tag autorizada difere da versão integrada $expectedTag." }
            $run = Invoke-GhJson -Path "repos/$Repository/actions/runs/$env:GITHUB_RUN_ID"
            if ($run['head_sha'] -cne $Commit) { throw 'Execução de autorização pertence a outro SHA.' }
            Assert-ManualDistributionApproval -Repository $Repository -Commit $Commit -Tag $expectedTag -Run $run
        }
        $releaseConfig = Get-Content -LiteralPath 'release-please-config.json' -Raw | ConvertFrom-Json -AsHashtable
        if ($releaseConfig['draft'] -isnot [bool] -or -not $releaseConfig['draft']) { throw 'Distribuição bloqueada: release-please precisa de draft=true até a aprovação de distribution.' }
        foreach ($package in ($releaseConfig['packages'] ?? @{}).Values) {
            if ($package.ContainsKey('draft') -and ($package['draft'] -isnot [bool] -or -not $package['draft'])) { throw 'Distribuição bloqueada: pacote do release-please não pode desativar draft antes da autorização.' }
        }
    }
    Assert-ReleaseDependencies -Repository $Repository -Commit $Commit -DefaultBranch $DefaultBranch
    foreach ($pull in $mergedPulls) {
        if ($pull['merge_commit_sha'] -ceq $Commit) { Assert-ReleaseDependencies -Repository $Repository -Commit $Commit -Body ([string]($pull['body'] ?? '')) -DefaultBranch $DefaultBranch }
    }
    if (-not $Stable -and $Stage -eq 'Publication') {
        $pulls = @(Invoke-GhJson -Path "repos/$Repository/pulls?state=open&head=${owner}:release-please--branches--$DefaultBranch&per_page=10" | Where-Object { $_ })
        foreach ($pull in $pulls) { Assert-ReleaseDependencies -Repository $Repository -Commit $Commit -Body ([string]($pull['body'] ?? '')) -DefaultBranch $DefaultBranch }
    }
    if ($Stable -or $Stage -eq 'ReleasePlease') { Assert-StableCandidateSource -Repository $Repository -Commit $Commit -Config (Read-CiCdConfig) }
    Assert-CurrentReleaseSource -Repository $Repository -Commit $Commit -DefaultBranch $DefaultBranch
    if ($Stage -eq 'Publication' -and ($Stable -or $Tag)) {
        if (-not $Tag) { $Tag = 'v' + [string]((Get-Content -LiteralPath '.release-please-manifest.json' -Raw | ConvertFrom-Json -AsHashtable)['.']) }
        if (-not $Preflight) { Assert-DistributionApproval -Repository $Repository -Commit $Commit -Tag $Tag }
    }
    if ($Stage -eq 'ReleasePlease') { Write-GitHubOutput -Path $OutputPath -Values @{ release_ready = 'true' } }
}
catch {
    Write-Output "::error title=Fonte da release::$($_.Exception.Message) Use --help."
    exit 1
}
exit 0