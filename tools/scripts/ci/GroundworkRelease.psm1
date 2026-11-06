# Gerenciado pelo Altivy.Groundwork (componente release). Não edite: mudanças vão para o catálogo.
# Funções compartilhadas pelos scripts de release, publicação e deploy em tools/scripts/ci.
#Requires -Version 7.0
Set-StrictMode -Version Latest

$script:SemVerPattern = '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(-(?<pre>[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*))?$'
$script:ManifestKinds = @('nuget', 'container', 'asset')

function Test-SemVer {
    [OutputType([bool])]
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Version)
    return $Version -match $script:SemVerPattern
}

function Test-ReleaseTagName {
    [OutputType([bool])]
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Tag)
    return $Tag.StartsWith('v', [System.StringComparison]::Ordinal) -and (Test-SemVer $Tag.Substring(1))
}

function Compare-SemVer {
    <# Retorna -1, 0 ou 1 conforme a precedência SemVer 2.0.0 (metadados de build não são aceitos). #>
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][string] $Left,
        [Parameter(Mandatory)][string] $Right
    )

    $a = [regex]::Match($Left, $script:SemVerPattern)
    $b = [regex]::Match($Right, $script:SemVerPattern)
    if (-not $a.Success -or -not $b.Success) {
        throw "Versão SemVer inválida: '$Left' ou '$Right'."
    }

    foreach ($part in 'major', 'minor', 'patch') {
        $comparison = ([bigint]$a.Groups[$part].Value).CompareTo([bigint]$b.Groups[$part].Value)
        if ($comparison -ne 0) { return [Math]::Sign($comparison) }
    }

    $preA = $a.Groups['pre'].Success ? $a.Groups['pre'].Value : ''
    $preB = $b.Groups['pre'].Success ? $b.Groups['pre'].Value : ''
    if ($preA -eq $preB) { return 0 }
    if (-not $preA) { return 1 }
    if (-not $preB) { return -1 }

    $idsA = $preA.Split('.')
    $idsB = $preB.Split('.')
    for ($i = 0; $i -lt [Math]::Min($idsA.Count, $idsB.Count); $i++) {
        $numA = $idsA[$i] -match '^\d+$'
        $numB = $idsB[$i] -match '^\d+$'
        if ($numA -and $numB) {
            $comparison = ([bigint]$idsA[$i]).CompareTo([bigint]$idsB[$i])
        }
        elseif ($numA) { $comparison = -1 }
        elseif ($numB) { $comparison = 1 }
        else { $comparison = [string]::CompareOrdinal($idsA[$i], $idsB[$i]) }

        if ($comparison -ne 0) { return [Math]::Sign($comparison) }
    }

    return [Math]::Sign($idsA.Count.CompareTo($idsB.Count))
}

function Read-CiCdConfig {
    param([string] $Path = '.github/ci-cd.json')
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Configuração do pipeline ausente: $Path."
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    }
    catch {
        throw "Configuração do pipeline inválida: $Path."
    }
}

function Get-AttestationPolicy {
    <#
    Lê "attestations" de .github/ci-cd.json. Ausente significa true (padrão seguro); somente o booleano
    false desativa a atestação. Qualquer outro valor é erro, para não haver interpretação ambígua.
    #>
    [OutputType([bool])]
    param([Parameter(Mandatory)][hashtable] $Config)
    if (-not $Config.ContainsKey('attestations')) { return $true }
    $value = $Config['attestations']
    if ($value -isnot [bool]) { throw 'attestations em .github/ci-cd.json deve ser o booleano true ou false.' }
    return $value
}

function Get-VersionFromFile {
    <# Lê <VersionPrefix> de .props/.csproj/.targets ou $.version de .json. #>
    [OutputType([string])]
    param([Parameter(Mandatory)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Arquivo de versão ausente: $Path."
    }

    $content = Get-Content -LiteralPath $Path -Raw
    if ($Path -match '\.json$') {
        $version = [string]((ConvertFrom-Json -InputObject $content -AsHashtable)['version'] ?? '')
    }
    else {
        $match = [regex]::Match($content, '<VersionPrefix>\s*(?<v>[^<\s]+)\s*</VersionPrefix>')
        $version = $match.Success ? $match.Groups['v'].Value : ''
    }

    if (-not (Test-SemVer $version)) {
        throw "Versão ausente ou fora de SemVer em ${Path}: '$version'."
    }

    return $version
}

# Arquivos que o próprio Release PR altera: changelog-path e extra-files de cada pacote do
# release-please-config.json, além do manifesto. São os únicos que a estável pode mudar em relação à candidata.
function Get-ReleasePleaseFiles {
    [OutputType([string[]])]
    param([string] $Path = 'release-please-config.json')
    $files = [System.Collections.Generic.List[string]]::new()
    $files.Add('.release-please-manifest.json')
    $files.Add('CHANGELOG.md')
    if (Test-Path -LiteralPath $Path) {
        $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
        foreach ($package in @(($config['packages'] ?? @{}).Values)) {
            if ($package['changelog-path']) { $files.Add([string]$package['changelog-path']) }
            foreach ($extra in @($package['extra-files'] ?? @())) {
                $file = $extra -is [string] ? $extra : [string]$extra['path']
                if ($file) { $files.Add($file.TrimStart('/')) }
            }
        }
    }

    return @($files | Select-Object -Unique)
}

function Get-ProjectVersionFiles {
    [OutputType([string[]])]
    param([Parameter(Mandatory)][hashtable] $Config)
    $files = @($Config['versionFiles'] ?? @() | Where-Object { $_ })
    if ($files.Count -eq 0) {
        throw 'versionFiles vazio em .github/ci-cd.json.'
    }

    foreach ($file in $files) {
        if ([System.IO.Path]::IsPathRooted($file) -or $file -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "Arquivo de versão fora do repositório: $file."
        }
    }

    return $files
}

function Invoke-GhJson {
    <#
    Executa 'gh api' e devolve o JSON convertido; retorna $null para HTTP 404 quando -AllowNotFound.
    Nunca repassa a saída de erro do gh, que pode conter dados do ambiente.
    #>
    param(
        [Parameter(Mandatory)][string] $Path,
        [string] $Method = 'GET',
        [hashtable] $Body = $null,
        [switch] $AllowNotFound,
        [switch] $Paginate
    )

    $arguments = @('api', '--method', $Method, '-H', 'Accept: application/vnd.github+json', '-H', 'X-GitHub-Api-Version: 2022-11-28')
    if ($Paginate) { $arguments += @('--paginate', '--slurp') }
    if ($null -ne $Body) { $arguments += @('--input', '-') }
    $arguments += $Path
    $errorFile = New-TemporaryFile
    try {
        if ($null -ne $Body) {
            $output = ConvertTo-Json -InputObject $Body -Depth 32 -Compress | & gh @arguments 2> $errorFile.FullName
        }
        else {
            $output = & gh @arguments 2> $errorFile.FullName
        }

        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            if ($AllowNotFound -and (Get-Content -LiteralPath $errorFile.FullName -Raw) -match 'HTTP 404') {
                return $null
            }

            throw "GitHub API falhou ($Method $Path, código $exitCode)."
        }
    }
    finally {
        Remove-Item -LiteralPath $errorFile.FullName -Force -ErrorAction SilentlyContinue
    }

    $text = ($output | Out-String).Trim()
    if (-not $text) { return $null }
    $result = ConvertFrom-Json -InputObject $text -AsHashtable -Depth 64
    if ($Paginate) {
        # --slurp devolve uma lista de páginas; listas paginadas são achatadas em um único array.
        return , @($result | ForEach-Object { $_ })
    }

    return $result
}

function Assert-ReleaseManifest {
    <# Valida o schema do release-manifest.json e, opcionalmente, a tag e o commit esperados. #>
    param(
        [Parameter(Mandatory)][hashtable] $Manifest,
        [string] $ExpectedTag = '',
        [string] $ExpectedCommit = ''
    )

    if ($Manifest['schemaVersion'] -ne 1) { throw 'release-manifest.json com schemaVersion não suportado.' }
    foreach ($field in 'repository', 'version', 'tag', 'commit', 'rollbackMinimumVersion') {
        if (-not ($Manifest[$field] -is [string]) -or -not $Manifest[$field]) {
            throw "release-manifest.json sem o campo $field."
        }
    }

    if (-not (Test-SemVer $Manifest['version']) -or $Manifest['tag'] -ne "v$($Manifest['version'])") {
        throw 'release-manifest.json com versão ou tag inconsistentes.'
    }

    if (-not (Test-SemVer $Manifest['rollbackMinimumVersion'])) {
        throw 'release-manifest.json com rollbackMinimumVersion fora de SemVer.'
    }

    if ($Manifest['commit'] -notmatch '^[0-9a-f]{40}$') { throw 'release-manifest.json com commit inválido.' }
    if ($Manifest.ContainsKey('attestation') -and $Manifest['attestation'] -notin @('github', 'none')) {
        throw 'release-manifest.json com attestation inválido; use github ou none.'
    }
    if ($ExpectedTag -and $Manifest['tag'] -ne $ExpectedTag) { throw "release-manifest.json pertence a $($Manifest['tag']), não a $ExpectedTag." }
    if ($ExpectedCommit -and $Manifest['commit'] -ne $ExpectedCommit) { throw 'release-manifest.json aponta para outro commit.' }

    $artifacts = @($Manifest['artifacts'] ?? @())
    if ($artifacts.Count -eq 0) { throw 'release-manifest.json sem artefatos.' }
    Assert-ReleaseEntries -Entries $artifacts
}

function Assert-ReleaseEntries {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]] $Entries)
    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $Entries) {
        if ($entry -isnot [System.Collections.IDictionary]) { throw 'Entrada de artefato inválida.' }
        if ($entry['kind'] -notin $script:ManifestKinds) { throw "Tipo de artefato inválido: $($entry['kind'])." }
        foreach ($field in 'name', 'reference') {
            if (-not ($entry[$field] -is [string]) -or -not $entry[$field] -or $entry[$field] -match '[\x00-\x1f]') {
                throw "Artefato sem $field válido."
            }
        }

        if ($entry['digest'] -notmatch '^sha256:[0-9a-f]{64}$') { throw "Digest inválido para $($entry['name'])." }
        if (-not $keys.Add("$($entry['kind'])/$($entry['name'])")) { throw "Artefato duplicado: $($entry['kind'])/$($entry['name'])." }
    }
}

function Assert-RepublishAssetDigest {
    param(
        [Parameter(Mandatory)][hashtable] $Manifest,
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Digest
    )

    Assert-ReleaseManifest -Manifest $Manifest
    $matches = @($Manifest['artifacts'] | Where-Object { $_['kind'] -ceq 'asset' -and $_['name'] -ceq $Name })
    if ($matches.Count -ne 1) { throw "Republish recusado: o manifesto imutável não contém o asset $Name." }
    if ($matches[0]['digest'] -cne $Digest) { throw "Republish recusado: digest reconstruído de $Name diverge do manifesto imutável." }
}

function Test-RollbackAllowed {
    <#
    Decide a trava de rollback: alvo abaixo da versão implantada só é aceito se não for menor
    que rollbackMinimumVersion do manifesto da versão implantada. Não há bypass.
    #>
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)][string] $Target,
        [AllowEmptyString()][string] $Current = '',
        [AllowEmptyString()][string] $CurrentMinimum = ''
    )

    if (-not $Current -or (Compare-SemVer $Target $Current) -ge 0) {
        return @{ Allowed = $true; Rollback = $false; Reason = '' }
    }

    if (-not $CurrentMinimum) {
        return @{ Allowed = $false; Rollback = $true; Reason = "Manifesto da versão implantada $Current sem rollbackMinimumVersion; rollback recusado." }
    }

    if ((Compare-SemVer $Target $CurrentMinimum) -lt 0) {
        return @{
            Allowed  = $false
            Rollback = $true
            Reason   = "Rollback de $Current para $Target recusado: rollbackMinimumVersion de $Current é $CurrentMinimum. Faça roll-forward com nova release."
        }
    }

    return @{ Allowed = $true; Rollback = $true; Reason = '' }
}

function Find-GitHubRelease {
    <# Localiza a release pela tag, inclusive rascunhos (releases/tags/{tag} não retorna rascunhos). #>
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Tag
    )

    $releases = Invoke-GhJson -Path "repos/$Repository/releases?per_page=100" -Paginate
    return @($releases ?? @()) | Where-Object { $_['tag_name'] -eq $Tag } | Select-Object -First 1
}

function Save-ReleaseAsset {
    <# Baixa o asset preservando os bytes (PowerShell 7.4+ não recodifica redirecionamento nativo para arquivo). #>
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][hashtable] $Release,
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Destination
    )

    $asset = @($Release['assets'] ?? @()) | Where-Object { $_['name'] -eq $Name } | Select-Object -First 1
    if ($null -eq $asset) { return $false }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent ([System.IO.Path]::GetFullPath($Destination))) | Out-Null
    & gh api -H 'Accept: application/octet-stream' "repos/$Repository/releases/assets/$($asset['id'])" > $Destination
    if ($LASTEXITCODE -ne 0) { throw "Falha ao baixar o asset $Name de $($Release['tag_name'])." }
    return $true
}

function Get-ReleaseAssetText {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][hashtable] $Release,
        [Parameter(Mandatory)][string] $Name
    )

    $temporary = New-TemporaryFile
    try {
        if (-not (Save-ReleaseAsset -Repository $Repository -Release $Release -Name $Name -Destination $temporary.FullName)) { return $null }
        return [System.IO.File]::ReadAllText($temporary.FullName)
    }
    finally {
        Remove-Item -LiteralPath $temporary.FullName -Force -ErrorAction SilentlyContinue
    }
}

function Get-PreviewPolicy {
    <#
    Lê "preview" e "deploy.previewEnvironment" de .github/ci-cd.json (docs/estrategia-de-release.md). Ausente
    significa preview desligada, para projetos ainda não migrados. Só o sufixo "rc" é promovível; "acceptance" é
    "deploy" (serviço) ou "approval" (aplicativo, aceite humano no Environment).
    #>
    [OutputType([hashtable])]
    param([Parameter(Mandatory)][hashtable] $Config)
    $preview = $Config['preview'] ?? @{}
    if ($preview -isnot [hashtable]) { throw 'preview em .github/ci-cd.json deve ser um objeto.' }
    $enabled = $preview['enabled'] ?? $false
    $required = $preview['requiredForRelease'] ?? $enabled
    if ($enabled -isnot [bool] -or $required -isnot [bool]) { throw 'preview.enabled e preview.requiredForRelease devem ser booleanos.' }
    $suffix = [string]($preview['suffix'] ?? 'rc')
    if ($suffix -cne 'rc') { throw "preview.suffix '$suffix' não suportado; use 'rc'." }
    $acceptance = [string]($preview['acceptance'] ?? 'deploy')
    if ($acceptance -cnotin @('deploy', 'approval')) { throw "preview.acceptance '$acceptance' inválido; use deploy ou approval." }
    $deploy = $Config['deploy'] ?? @{}
    $environment = [string]($deploy['previewEnvironment'] ?? 'quality')
    if ($environment -notmatch '^[A-Za-z0-9_.-]{1,255}$' -or $environment -ieq 'production') {
        throw "deploy.previewEnvironment '$environment' inválido; nunca production."
    }

    return @{ Enabled = $enabled; Required = $enabled -and $required; Suffix = $suffix; Acceptance = $acceptance; Environment = $environment }
}

function Test-CandidateTag {
    <# Tag de candidata promovível: vX.Y.Z-rc.N (N inteiro sem zeros à esquerda). #>
    [OutputType([bool])]
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Tag)
    return $Tag -cmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)-rc\.(0|[1-9]\d*)$'
}

function Get-CandidateBaseVersion {
    <# Versão estável de uma candidata: v1.4.0-rc.2 -> 1.4.0. #>
    [OutputType([string])]
    param([Parameter(Mandatory)][string] $Tag)
    if (-not (Test-CandidateTag $Tag)) { throw "Tag de candidata inválida: '$Tag'. Use vX.Y.Z-rc.N." }
    return $Tag.Substring(1).Split('-', 2)[0]
}

function Get-TagCommit {
    <# Commit apontado pela tag no checkout local; vazio se ausente. #>
    [OutputType([string])]
    param([Parameter(Mandatory)][string] $Tag)
    $commit = (git rev-parse --verify --quiet "refs/tags/$Tag^{commit}" 2>$null | Out-String).Trim()
    return $commit -match '^[0-9a-f]{40}$' ? $commit : ''
}

function Find-ValidatedCandidate {
    <#
    Candidata publicada vX.Y.Z-rc.N com a versão pedida, cujo asset preview-acceptance.json registra sucesso no
    Environment de preview. Com -Commit, exige também que a tag aponte para esse commit (commit-base do Release PR).
    Devolve a candidata mais recente que atende ou $null. O asset é anexado pelo gw-deploy.yml depois do aceite.
    #>
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][string] $Environment,
        [string] $Commit = ''
    )

    if (-not (Test-SemVer $Version) -or $Version.Contains('-')) { throw "Versão estável inválida: '$Version'." }
    $all = Invoke-GhJson -Path "repos/$Repository/releases?per_page=100" -Paginate
    $releases = @($all ?? @()) |
        Where-Object { $_ -and -not $_['draft'] -and $_['prerelease'] -and (Test-CandidateTag ([string]$_['tag_name'])) -and (Get-CandidateBaseVersion $_['tag_name']) -eq $Version } |
        Sort-Object -Property @{ Expression = { [int](([string]$_['tag_name']).Split('.')[-1]) } } -Descending
    foreach ($release in $releases) {
        $tag = [string]$release['tag_name']
        if ($Commit -and (Get-TagCommit $tag) -ne $Commit) { continue }
        $text = Get-ReleaseAssetText -Repository $Repository -Release $release -Name 'preview-acceptance.json'
        if (-not $text) { continue }
        try { $acceptance = ConvertFrom-Json -InputObject $text -AsHashtable -Depth 8 } catch { continue }
        if ($acceptance['schemaVersion'] -eq 1 -and $acceptance['tag'] -ceq $tag -and $acceptance['environment'] -ceq $Environment -and $acceptance['result'] -ceq 'success') {
            # ConvertFrom-Json converte datas ISO em DateTime; as mensagens usam sempre ISO 8601 em UTC.
            if ($acceptance['acceptedAt'] -is [datetime]) { $acceptance['acceptedAt'] = $acceptance['acceptedAt'].ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') }
            return @{ Tag = $tag; Release = $release; Acceptance = $acceptance }
        }
    }

    return $null
}

function Find-PreviewWaiver {
    <#
    Rodapé "Preview-Waiver: <justificativa>" em algum commit de <desde>..<até>. Devolve a justificativa ou vazio.
    Sem <desde> (primeira release), considera todo o histórico até <até>.
    #>
    [OutputType([string])]
    param(
        [string] $Since = '',
        [Parameter(Mandatory)][string] $Until
    )

    $range = $Since ? "$Since..$Until" : $Until
    $messages = (git log --format='%B%x00' $range 2>$null | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Histórico $range indisponível; o checkout precisa de fetch-depth 0." }
    # %x00 separa as mensagens e cola o NUL na última linha de cada uma, justamente onde fica o rodapé.
    foreach ($line in $messages -split "[`n`0]") {
        if ($line -cmatch '^Preview-Waiver:\s*(?<reason>\S.{0,299})\s*$') { return $Matches['reason'].Trim() }
    }

    return ''
}

function Assert-ReleaseDependencies {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Commit,
        [AllowEmptyString()][string] $Body = '',
        [string] $DefaultBranch = 'main'
    )

    $configured = (Read-CiCdConfig)['releaseDependencies'] ?? @()
    if ($configured -isnot [array]) { throw 'releaseDependencies deve ser um array de números de PR.' }
    $numbers = [System.Collections.Generic.List[int]]::new()
    foreach ($number in $configured) {
        if (($number -isnot [int] -and $number -isnot [long]) -or $number -lt 1 -or $number -gt [int]::MaxValue) { throw 'releaseDependencies contém número de PR inválido.' }
        $numbers.Add([int]$number)
    }
    foreach ($line in $Body -split '\r?\n' | Where-Object { $_ -match '^Release-Depends-On:' }) {
        if ($line -notmatch '^Release-Depends-On:\s*(#[1-9]\d*(?:\s*,\s*#[1-9]\d*)*)\s*$') { throw 'Release-Depends-On inválido; use Release-Depends-On: #123, #456.' }
        foreach ($number in $Matches[1] -split '\s*,\s*' | ForEach-Object { $_.Substring(1) }) { $numbers.Add([int]$number) }
    }
    foreach ($number in $numbers | Select-Object -Unique) {
        $pull = Invoke-GhJson -Path "repos/$Repository/pulls/$number"
        $merged = [string]$pull['merge_commit_sha']
        if (-not $pull['merged_at'] -or $pull['base']['ref'] -cne $DefaultBranch -or $pull['base']['repo']['full_name'] -cne $Repository -or $merged -notmatch '^[0-9a-f]{40}$') {
            throw "Integração obrigatória pendente: PR #$number não integrado em $Repository/$DefaultBranch. Integre o PR antes de gerar a candidata/release."
        }
        $comparison = Invoke-GhJson -Path "repos/$Repository/compare/$merged...$Commit"
        if ($comparison['status'] -cnotin @('ahead', 'identical')) { throw "PR #$number integrado, mas ausente do SHA $Commit; atualize a base e gere nova candidata/release." }
    }
}

function Assert-StableCandidateSource {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Commit,
        [Parameter(Mandatory)][hashtable] $Config
    )

    $policy = Get-PreviewPolicy -Config $Config
    $manifest = Get-Content -LiteralPath '.release-please-manifest.json' -Raw | ConvertFrom-Json -AsHashtable
    $version = [string]$manifest['.']
    if (-not (Test-SemVer $version) -or $version.Contains('-')) { throw 'Versão estável inválida no manifesto.' }
    foreach ($file in Get-ProjectVersionFiles -Config $Config) {
        if ((Get-VersionFromFile -Path $file) -cne $version) { throw "Versão de $file difere do manifesto $version; recusa criar tag." }
    }
    if (-not $policy.Required) { return }
    $base = (git rev-parse --verify "$Commit^1" 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $base -notmatch '^[0-9a-f]{40}$') { throw 'Base da release indisponível; use fetch-depth 0.' }
    $candidate = Find-ValidatedCandidate -Repository $Repository -Version $version -Environment $policy.Environment -Commit $base
    if ($null -eq $candidate) {
        $previous = (git describe --tags --abbrev=0 --match 'v[0-9]*' --exclude '*-*' $base 2>$null | Out-String).Trim()
        if (Find-PreviewWaiver -Since $previous -Until $Commit) { Write-Output '::warning::Aceite de RC dispensado por Preview-Waiver; SHA atual e CI continuam obrigatórios.'; return }
        throw "Release v$version bloqueada: nenhuma candidata aceita em $($policy.Environment) na base $base. Gere e aceite um novo RC."
    }
    $allowed = @(Get-ProjectVersionFiles -Config $Config) + @(Get-ReleasePleaseFiles)
    $changed = @(git diff --name-only $base $Commit 2>$null | Where-Object { $_ })
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível comparar o código da candidata e da release.' }
    $other = @($changed | Where-Object { $_ -cnotin $allowed })
    if ($other.Count -ne 0) { throw "Release difere do RC além dos metadados permitidos: $($other -join ', '). Integre o código e gere um novo RC." }
}

function Assert-CurrentReleaseSource {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Commit,
        [string] $DefaultBranch = 'main',
        [switch] $AllowRunningCi
    )

    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $Commit -notmatch '^[0-9a-f]{40}$') { throw 'Repositório ou SHA de release inválido.' }
    if ($DefaultBranch -notmatch '^[A-Za-z0-9._/-]+$' -or $DefaultBranch.Contains('..')) { throw 'Branch padrão inválida.' }
    $branch = [uri]::EscapeDataString($DefaultBranch)
    $current = Invoke-GhJson -Path "repos/$Repository/git/ref/heads/$branch"
    $actual = [string]$current['object']['sha']
    if ($actual -cne $Commit) {
        throw "Fonte desatualizada: esperado $Commit, HEAD de $DefaultBranch $actual. Aguarde a integração e atualização do Release PR; gere uma nova execução/candidata."
    }

    $pages = Invoke-GhJson -Path "repos/$Repository/actions/workflows/ci.yml/runs?branch=$branch&head_sha=$Commit&event=push&per_page=100" -Paginate
    $runs = @($pages | ForEach-Object { $_['workflow_runs'] } | Where-Object { $_['head_sha'] -ceq $Commit -and $_['head_branch'] -ceq $DefaultBranch -and $_['event'] -ceq 'push' } | Sort-Object { [long]$_['id'] } -Descending)
    $ownRunning = $AllowRunningCi -and $runs.Count -gt 0 -and [string]$runs[0]['id'] -ceq $env:GITHUB_RUN_ID -and $runs[0]['status'] -ceq 'in_progress'
    if ($runs.Count -eq 0 -or (-not $ownRunning -and ($runs[0]['status'] -cne 'completed' -or $runs[0]['conclusion'] -cne 'success'))) {
        throw "CI da $DefaultBranch pendente, ausente ou sem sucesso no SHA $Commit. Aguarde Result verde e dispare novamente."
    }
    $suite = $runs[0]['check_suite_id']
    $pages = Invoke-GhJson -Path "repos/$Repository/commits/$Commit/check-runs?check_name=Result&filter=latest&per_page=100" -Paginate
    $checks = @($pages | ForEach-Object { $_['check_runs'] } | Where-Object { $_['name'] -ceq 'Result' -and $_['head_sha'] -ceq $Commit -and $_['app']['id'] -eq 15368 -and $_['check_suite']['id'] -eq $suite })
    if ($checks.Count -eq 0 -or @($checks | Where-Object { $_['status'] -cne 'completed' -or $_['conclusion'] -cne 'success' }).Count -ne 0) {
        throw "Result obrigatório ausente ou sem sucesso na execução CI da $DefaultBranch no SHA $Commit."
    }
    $current = Invoke-GhJson -Path "repos/$Repository/git/ref/heads/$branch"
    if ([string]$current['object']['sha'] -cne $Commit) { throw "A $DefaultBranch avançou durante a validação do SHA $Commit; gere uma nova execução/candidata." }
    Write-Output "Fonte atualizada: $DefaultBranch em $Commit, CI e Result verdes no mesmo SHA."
}

function Assert-InternalSnapshotPackage {
    param(
        [Parameter(Mandatory)][string] $Owner,
        [Parameter(Mandatory)][string] $Package
    )

    if ($Owner -notmatch '^[A-Za-z0-9-]+$' -or [string]::IsNullOrWhiteSpace($Package)) { throw 'Destino de snapshot inválido.' }
    $account = Invoke-GhJson -Path "users/$Owner"
    $scope = switch ($account['type']) { 'Organization' { 'orgs' }; 'User' { 'users' }; default { throw 'Tipo de proprietário do feed interno desconhecido.' } }
    $name = [uri]::EscapeDataString($Package)
    $packageInfo = Invoke-GhJson -Path "$scope/$Owner/packages/nuget/$name" -AllowNotFound
    if ($null -ne $packageInfo -and $packageInfo['visibility'] -cnotin @('private', 'internal')) {
        throw "Snapshot bloqueado: pacote $Owner/$Package não é interno. Use candidata/release com aprovação de distribution para divulgação pública."
    }
}

function Get-DistributionApprovalMode {
    param([Parameter(Mandatory)][hashtable] $Config)
    $policy = $Config['distributionApproval'] ?? @{}
    if ($policy -isnot [hashtable]) { throw 'distributionApproval deve ser um objeto.' }
    $mode = $policy['mode'] ?? 'environment'
    if ($mode -isnot [string] -or $mode -cnotin @('environment', 'manual-dispatch')) { throw 'distributionApproval.mode deve ser environment ou manual-dispatch.' }
    return $mode
}

function Assert-ManualDistributionApproval {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Commit,
        [Parameter(Mandatory)][string] $Tag,
        [Parameter(Mandatory)][hashtable] $Run
    )

    if (-not (Test-ReleaseTagName $Tag) -or $Tag.Substring(1).Contains('-')) { throw 'Autorização manual alternativa aceita somente versões estáveis; candidatas/previews usam aprovação nativa.' }
    $repositoryInfo = Invoke-GhJson -Path "repos/$Repository"
    if ($repositoryInfo['private'] -isnot [bool] -or -not $repositoryInfo['private']) { throw 'Autorização manual alternativa só é permitida em repositório privado configurado explicitamente.' }
    $expectedTitle = "Authorize distribution | $Tag | $Commit"
    if ($Run['head_sha'] -cne $Commit -or $Run['event'] -cne 'workflow_dispatch' -or $Run['display_title'] -cne $expectedTitle -or [string]$Run['path'] -cnotmatch '^\.github/workflows/gw-release\.yml(?:@|$)' -or $Run['head_branch'] -cnotin @($repositoryInfo['default_branch'], $Tag)) {
        throw "Autorização manual inválida: execute Release com confirmação explícita da tag $Tag e do SHA $Commit. Push, rerun de evento automático e workflows de preview não autorizam divulgação."
    }
    $actor = $Run['actor']
    $triggering = $Run['triggering_actor']
    $login = [string]$actor['login']
    if ($actor['type'] -cne 'User' -or $triggering['type'] -cne 'User' -or $login -notmatch '^[A-Za-z0-9-]+$' -or $triggering['login'] -cne $login) {
        throw 'Autorização manual exige usuário humano; reexecução por outro ator exige uma nova autorização.'
    }
    $permission = Invoke-GhJson -Path "repos/$Repository/collaborators/$login/permission"
    if ($permission['permission'] -cne 'admin') { throw "Usuário $login não é administrador do repositório; distribuição recusada." }
    Write-Output "Autorização manual auditada: administrador $login confirmou $Tag/$Commit na execução $($Run['id'])."
}

function Assert-DistributionApproval {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string] $Commit,
        [Parameter(Mandatory)][string] $Tag,
        [string] $RunId = $env:GITHUB_RUN_ID
    )

    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $Commit -notmatch '^[0-9a-f]{40}$' -or -not (Test-ReleaseTagName $Tag) -or $RunId -notmatch '^[1-9]\d{0,19}$') {
        throw 'Distribuição bloqueada: repositório, tag, SHA ou execução inválidos.'
    }
    $run = Invoke-GhJson -Path "repos/$Repository/actions/runs/$RunId"
    if ($run['head_sha'] -cne $Commit -or [string]$run['path'] -cnotmatch '^\.github/workflows/(gw-release|gw-preview|publish)\.yml(?:@|$)') {
        throw "Distribuição bloqueada: execução $RunId não corresponde à fonte $Tag/$Commit ou ao workflow de publicação autorizado."
    }
    $mode = Get-DistributionApprovalMode -Config (Read-CiCdConfig)
    if ($mode -ceq 'manual-dispatch') {
        Assert-ManualDistributionApproval -Repository $Repository -Commit $Commit -Tag $Tag -Run $run
    }
    else {
        $reviews = @(Invoke-GhJson -Path "repos/$Repository/actions/runs/$RunId/approvals" | Where-Object { $_ })
        $distribution = @($reviews | Where-Object { @($_['environments'] | Where-Object { $_['name'] -ceq 'distribution' }).Count -gt 0 })
        $approved = @($distribution | Where-Object { $_['state'] -ceq 'approved' -and $_['user']['type'] -ceq 'User' -and $_['user']['login'] -and -not ([string]$_['user']['login']).EndsWith('[bot]', [StringComparison]::Ordinal) })
        if ($approved.Count -eq 0 -or @($distribution | Where-Object { $_['state'] -ceq 'rejected' }).Count -gt 0) {
            throw "Distribuição bloqueada: aprove manualmente o Environment distribution nesta execução para $Tag/$Commit. Ambiente sem revisor, aceite de quality e aprovação de PR não autorizam publicação; rejeição exige nova execução."
        }
    }
    $tagCommit = Invoke-GhJson -Path "repos/$Repository/commits/$Tag"
    if ($tagCommit['sha'] -cne $Commit) { throw "Distribuição bloqueada: tag $Tag mudou depois da autorização do SHA $Commit." }
    Write-Output "Distribuição autorizada: $Tag/$Commit, execução $RunId, modo $mode."
}

function Write-GitHubOutput {
    param(
        [Parameter(Mandatory)][hashtable] $Values,
        [string] $Path = ''
    )

    foreach ($key in @($Values.Keys | Sort-Object)) {
        $value = [string]$Values[$key]
        if ($value -match '[\r\n]') { throw "Saída $key contém quebra de linha." }
        if ($Path) { "$key=$value" | Add-Content -LiteralPath $Path -Encoding utf8 }
        Write-Output "$key=$value"
    }
}

Export-ModuleMember -Function Test-SemVer, Test-ReleaseTagName, Compare-SemVer, Read-CiCdConfig, Get-AttestationPolicy, Get-VersionFromFile,
Get-ProjectVersionFiles, Invoke-GhJson, Assert-ReleaseManifest, Assert-ReleaseEntries, Test-RollbackAllowed,
Find-GitHubRelease, Save-ReleaseAsset, Get-ReleaseAssetText, Write-GitHubOutput, Get-PreviewPolicy, Test-CandidateTag,
Get-CandidateBaseVersion, Get-TagCommit, Find-ValidatedCandidate, Find-PreviewWaiver, Get-ReleasePleaseFiles, Assert-CurrentReleaseSource,
Assert-ReleaseDependencies, Assert-StableCandidateSource, Assert-DistributionApproval, Assert-InternalSnapshotPackage,
Get-DistributionApprovalMode, Assert-ManualDistributionApproval
