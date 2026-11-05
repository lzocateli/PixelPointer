<#
.SYNOPSIS
Testa o codigo de saida final do runner de instaladores sem pacotes ou HTTP.

.DESCRIPTION
Executa o bloco final real de Test-Installers.ps1 em processos PowerShell
descartaveis, com codigos nativos anteriores 0 e 1 e com/sem falhas de cenario.
Reproduz o chamador do workflow, que encerra com LASTEXITCODE.
Nao executa instaladores nem modifica configuracoes do usuario.

.PARAMETER RemainingArguments
Aceita somente --help; outros valores retornam erro de uso.

.EXAMPLE
./tools/scripts/Test-InstallerExitCode.ps1

.NOTES
Requer PowerShell 7+. Usa somente arquivos temporarios e processos locais.
Codigos: 0 sucesso; 1 regressao; 2 erro de uso.
#>
#requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments)]
    [string[]] $RemainingArguments
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

$runner = Join-Path $PSScriptRoot 'Test-Installers.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "Sintaxe invalida em Test-Installers.ps1: $($parseErrors -join '; ')"
}
$failureChecks = @($ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.IfStatementAst] -and
    $_.Clauses[0].Item1.Extent.Text -eq '$failures.Count -gt 0'
})
if ($failureChecks.Count -ne 1) {
    throw 'Bloco final de verificacao das falhas nao encontrado de forma univoca.'
}
$footer = $ast.Extent.Text.Substring($failureChecks[0].Extent.StartOffset)
$shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$work = Join-Path ([IO.Path]::GetTempPath()) "pixelpointer-exit-code-$([Guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $work
try {
    $fixture = Join-Path $work 'runner.ps1'
    $caller = Join-Path $work 'caller.ps1'
    [IO.File]::WriteAllText($fixture, @'
param([string] $Shell, [int] $NativeExitCode, [int] $FailureCount)
$failures = [Collections.Generic.List[string]]::new()
for ($i = 0; $i -lt $FailureCount; $i++) { $failures.Add('cenario reprovado') }
& $Shell -NoLogo -NoProfile -Command "exit $NativeExitCode"
'@ + "`n" + $footer)
    [IO.File]::WriteAllText($caller, @'
param([string] $Fixture, [string] $Shell, [int] $NativeExitCode, [int] $FailureCount)
& $Fixture -Shell $Shell -NativeExitCode $NativeExitCode -FailureCount $FailureCount
exit $LASTEXITCODE
'@)
    foreach ($nativeExitCode in @(0, 1)) {
        foreach ($failureCount in @(0, 1)) {
            $output = & $shell -NoLogo -NoProfile -File $caller -Fixture $fixture -Shell $shell `
                -NativeExitCode $nativeExitCode -FailureCount $failureCount 2>&1 | Out-String
            $actual = $LASTEXITCODE
            $expected = if ($failureCount -eq 0) { 0 } else { 1 }
            if ($actual -ne $expected) {
                throw "Codigo nativo=$nativeExitCode, falhas=$failureCount`: esperado $expected, recebido $actual. $output"
            }
            Write-Output "ok: codigo nativo=$nativeExitCode, falhas=$failureCount, saida=$actual"
        }
    }
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force
}
exit 0
