<#
.SYNOPSIS
Prepara ou publica uma GitHub Pre-release da main.

.DESCRIPTION
Valida origem, versao, commit, arquivos e SHA256SUMS. Sem -Publish, apenas
valida e emite o payload JSON, sem acessar GitHub. Com -Publish, usa o GitHub
App autenticado via GH_TOKEN; tags protegidas nao aceitam tokens pessoais.
O estagio draft cria ou reutiliza um rascunho do mesmo commit. O estagio
publish exige assets remotos identicos antes de tornar a preview publica.
Nunca sobrescreve assets, move tags nem marca a preview como Latest.

.PARAMETER Stage
draft ou publish. Padrao: draft.
.PARAMETER Version
Versao X.Y.Z-preview.N, sem prefixo v.
.PARAMETER Commit
SHA completo do commit, ancestral de origin/main na publicacao.
.PARAMETER AssetDirectory
Diretorio dentro do repositorio com 14 arquivos e SHA256SUMS.
Padrao: artifacts/release-assets.
.PARAMETER EventName
Origem da execucao. Padrao: GITHUB_EVENT_NAME. Exige workflow_dispatch.
.PARAMETER Ref
Referencia da execucao. Padrao: GITHUB_REF. Exige refs/heads/main.
.PARAMETER Publish
Autoriza a operacao remota; exige aprovacao previa do Environment release.
.PARAMETER RemainingArguments
Aceita somente --help.
.EXAMPLE
./tools/scripts/Set-PreviewRelease.ps1 -Version 1.2.3-preview.42 -Commit ('a' * 40) -EventName workflow_dispatch -Ref refs/heads/main
.NOTES
Requer PowerShell 7. Para publicar: Git, gh autenticado pelo App e origem
origin/main disponivel. Execute na raiz do clone. --help nao produz efeitos.
#>
#requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet('draft', 'publish')]
    [string] $Stage = 'draft',
    [string] $Version,
    [string] $Commit,
    [string] $AssetDirectory = 'artifacts/release-assets',
    [string] $EventName = $env:GITHUB_EVENT_NAME,
    [string] $Ref = $env:GITHUB_REF,
    [switch] $Publish,
    [Parameter(ValueFromRemainingArguments)]
    [string[]] $RemainingArguments
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($RemainingArguments -contains '--help') {
    Get-Help $PSCommandPath -Full
    exit 0
}
if ($RemainingArguments) {
    [Console]::Error.WriteLine('Argumento desconhecido. Use --help.')
    exit 2
}

try {
    if ($EventName -cne 'workflow_dispatch' -or $Ref -cne 'refs/heads/main') {
        throw 'Preview publica exige workflow_dispatch em refs/heads/main.'
    }
    if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-preview\.(0|[1-9][0-9]{0,8})$') {
        throw 'Versao deve ser X.Y.Z-preview.N.'
    }
    if ($Commit -cnotmatch '^[a-f0-9]{40}$') { throw 'Commit deve ser um SHA completo.' }
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $directory = [IO.Path]::GetFullPath($AssetDirectory, $repositoryRoot)
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not $directory.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw 'Assets devem estar dentro do repositorio.'
    }
    $ancestor = $directory
    while ($ancestor -ne $repositoryRoot) {
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Links nao sao permitidos no diretorio de assets.'
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
    $expected = @('SHA256SUMS')
    foreach ($rid in @('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')) {
        $extension = if ($rid.StartsWith('win-')) { 'zip' } else { 'tar.gz' }
        $expected += "pixelpointer-$rid.$extension", "pixelpointer-$rid.spdx.json"
        if ($rid.StartsWith('linux-')) { $expected += "pixelpointer-$rid.deb" }
    }
    $files = @(Get-ChildItem -LiteralPath $directory -Force)
    if ($files.Count -ne $expected.Count) { throw 'Preview exige exatamente 14 assets e SHA256SUMS.' }
    $digests = @{}
    foreach ($file in $files) {
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.Name -cnotin $expected) {
            throw 'Asset desconhecido, diretorio ou link recusado.'
        }
        $digests[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $seen = @{}
    foreach ($line in Get-Content -LiteralPath (Join-Path $directory 'SHA256SUMS')) {
        if ($line -notmatch '^([a-fA-F0-9]{64})[ \t]+\*?(.+)$') { throw 'SHA256SUMS invalido.' }
        $name = $Matches[2]
        if ($name -ceq 'SHA256SUMS' -or $name -cnotin $expected -or $seen.ContainsKey($name)) {
            throw 'Entrada desconhecida ou duplicada em SHA256SUMS.'
        }
        if ($Matches[1].ToLowerInvariant() -cne $digests[$name]) { throw 'Checksum de asset divergente.' }
        $seen[$name] = $true
    }
    if ($seen.Count -ne 14) { throw 'SHA256SUMS deve cobrir todos os 14 assets.' }

    $tag = "v$Version"
    $body = if ($Stage -eq 'draft') {
        @{
            tag_name = $tag; target_commitish = $Commit; name = $tag
            draft = $true; prerelease = $true; make_latest = 'false'
            body = "Preview para testes; nao e uma versao estavel. / Testing preview; not a stable release. / Preview para pruebas; no es una version estable.`n`nCommit: $Commit`nSHA256SUMS e SBOM / SHA256SUMS and SBOM / SHA256SUMS y SBOM incluidos."
        }
    }
    else { @{ draft = $false; prerelease = $true; make_latest = 'false' } }
    if (-not $Publish) {
        $body | ConvertTo-Json -Depth 4
        exit 0
    }

    & git -C $repositoryRoot merge-base --is-ancestor $Commit origin/main
    if ($LASTEXITCODE -ne 0) { throw 'Commit nao e ancestral de origin/main.' }
    Import-Module (Join-Path $PSScriptRoot 'ci/GroundworkRelease.psm1') -Force
    $repository = 'lzocateli/PixelPointer'
    $release = Find-GitHubRelease -Repository $repository -Tag $tag
    if ($null -eq $release) {
        if ($Stage -ne 'draft') { throw 'Rascunho de preview inexistente.' }
        $existingTag = Invoke-GhJson -Path "repos/$repository/git/ref/tags/$tag" -AllowNotFound
        if ($null -ne $existingTag) { throw 'Tag existente sem rascunho; recusa reutilizacao.' }
        $null = Invoke-GhJson -Path "repos/$repository/git/refs" -Method POST -Body @{ ref = "refs/tags/$tag"; sha = $Commit }
        $release = Invoke-GhJson -Path "repos/$repository/releases" -Method POST -Body $body
    }
    if (-not $release['draft'] -or -not $release['prerelease']) { throw 'Release existente nao e um rascunho de preview.' }
    $tagCommit = Invoke-GhJson -Path "repos/$repository/commits/$tag"
    if ($tagCommit['sha'] -cne $Commit) { throw 'Tag de preview aponta para outro commit.' }
    if ($Stage -eq 'publish') {
        $assets = @($release['assets'])
        if ($assets.Count -ne $expected.Count) { throw 'Assets remotos incompletos ou inesperados.' }
        $remoteNames = @{}
        foreach ($asset in $assets) {
            $name = $asset['name']
            if ($name -cnotin $expected -or $remoteNames.ContainsKey($name) -or $asset['digest'] -cne "sha256:$($digests[$name])") {
                throw 'Asset remoto diverge do pacote validado; rascunho preservado.'
            }
            $remoteNames[$name] = $true
        }
        $null = Invoke-GhJson -Path "repos/$repository/releases/$($release['id'])" -Method PATCH -Body $body
    }
    Write-Output $release['html_url']
}
catch {
    [Console]::Error.WriteLine("$($_.Exception.Message) Use --help.")
    exit 1
}
exit 0
