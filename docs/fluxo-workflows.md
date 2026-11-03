# Fluxo dos workflows

[Português (Brasil)](fluxo-workflows.md) · [English](en/fluxo-workflows.md) · [Español](es/fluxo-workflows.md)

## Escopo e estado

Levantamento do código local em 2026-11-03, não uma comprovação da configuração
ativa no GitHub. Este guia complementa a
[estratégia gerenciada](estrategia-de-release.md) e o
[contrato de CI/CD](../.github/CI-CD.md); não altera esses contratos.
As guardas foram implementadas no catálogo e aplicadas localmente por atualização
gerenciada. O [contrato de integridade](release-integrity.md) documenta interfaces,
recuperação, migração e limites; ativação remota depende de integrar estas mudanças.

## Fluxo implementado

```mermaid
flowchart TD
    PR["PR comum: feat, fix, docs e outros"] --> CI["ci.yml: higiene, Gitleaks, build, testes, AOT"]
    CI --> RESULT["Result obrigatório + revisão + base atualizada"]
    RESULT --> MERGE["Merge squash na main"]
    MERGE --> MAINCI["ci.yml no SHA da main"]
    MERGE --> RP["gw-release.yml: release-please cria ou atualiza Release PR"]
    RP --> RPCI["CI do Release PR: inclui Preview gate"]
    RP --> MANUAL["Disparo manual gw-preview.yml na main"]
   MANUAL --> RESOLVE["Get-PreviewVersion: versão, dependências, HEAD atual e CI verde"]
   MAINCI --> RESOLVE
    RESOLVE --> RELRC["Environment release: aprovação"]
    RELRC --> RCTAG["GitHub App cria vX.Y.Z-rc.N + prerelease em rascunho"]
    RCTAG --> RCCTX["Valida versão e ancestralidade da tag"]
    RCCTX --> PUB["publish.yml: build 6 RIDs, smoke, instaladores, SBOM"]
    PUB --> ASSETS["gw-github-assets.yml: Environment release, assets e proveniência"]
    ASSETS --> RCFINAL["gw-preview.yml: manifesto atestado + prerelease pública, sem Latest"]
    RCFINAL --> Q["gw-deploy.yml na tag: guard quality"]
    Q --> QA["deploy.yml: aprovação humana no Environment quality"]
    QA --> QS["Confere assets + smoke do pacote linux-x64 em containers"]
    QS --> ACCEPT["preview-acceptance.json + reexecução do CI do Release PR"]
    ACCEPT --> RPCI
    RPCI --> RPOK["Result verde + revisão humana"]
    RPOK --> RPMERGE["Merge squash do Release PR na main"]
   RPMERGE --> FINALGUARD["Guard antes da tag: SHA atual, CI verde e código do RC aceito"]
   FINALGUARD --> FINALTAG["gw-release.yml: GitHub App cria vX.Y.Z + release em rascunho"]
    FINALTAG --> FINALCTX["Valida versões e ancestralidade da tag"]
    FINALCTX --> FINALPUB["publish.yml: build, smoke, SBOM e assets no Environment release"]
    FINALPUB --> STABLE["gw-release.yml: manifesto atestado + release estável pública"]
    STABLE --> POST["post-release.yml"]
    POST --> BREW["Environment release: atualiza Homebrew + verifica instalação"]
    POST --> WINGET["winget.yml: validação + submissão no Environment winget"]
    STABLE --> AUTOQ["Auto-disparo gw-deploy.yml em quality"]
    STABLE --> PROD["Disparo manual gw-deploy.yml na tag estável: production"]
    AUTOQ --> VERIFYQ["Guard + aprovação quality + assets + smoke"]
    PROD --> VERIFYPROD["Guard + aprovação production + assets + smoke"]
```

O CI da `main` e o release-please partem do mesmo push, mas não há dependência
`needs` entre esses workflows. O desenho não implica que o CI da `main` terminou
antes da resolução da candidata. O Preview gate admite o waiver auditável
descrito na estratégia; PRs comuns, inclusive fixes, não exigem esse gate.

O aceite humano em `quality` deve representar teste manual do pacote: a espera
do Environment ocorre **antes** da conferência de assets e do smoke automatizado.
O sucesso posterior registra o aceite; o smoke não substitui o teste gráfico humano.

A estável é reconstruída a partir da tag final, não promovida por reutilização dos
binários do RC. A estratégia exige equivalência do código, com diferenças apenas
de metadados de release. `production` **não bloqueia a publicação pública nem a
distribuição**: ambas já começaram quando esse fluxo manual é executado.
PixelPointer não tem servidor implantado; o deploy verifica pacotes publicados.

## Outros caminhos

| Entrada | Percurso e resultado | Ambientes |
| --- | --- | --- |
| `publish.yml`, dispatch na `main` com preview | Build e validação; cria `vX.Y.Z-preview.N` e GitHub Pre-release sem Latest; não promovível nem implantável | `release` |
| `publish.yml`, PR que altera cadeia de release | Build, smoke e instaladores; sem tag nem publicação | Nenhum |
| `gw-release.yml`, dispatch na tag com `mode=republish` | Valida tag e release existente; recompõe artefatos ausentes, finaliza e reexecuta distribuição; não cria nova versão nem faz auto-deploy | `release`, `winget` na submissão |
| `gw-deploy.yml`, dispatch de tag histórica | Redeploy/rollback pelo mesmo guard e smoke; respeita `rollbackMinimumVersion`; não recria tag nem release | `quality` ou `production` |
| `winget.yml`, dispatch | Valida versão informada; submissão opcional e idempotente | `winget` se submeter |
| `scheduled-smoke.yml`, segunda-feira 09:00 UTC ou dispatch | Recompila e testa AOT/pacotes; não publica | Nenhum |
| `gw-gitleaks.yml` | Chamado pelo CI; também aceita dispatch; varredura sem publicação | Nenhum |
| `gw-pr-hygiene.yml` | Chamado pelo CI: título, actionlint, arquivos gerenciados e Preview gate do Release PR | Nenhum |
| `gw-github-assets.yml` | Chamado pelo publicador: uploads imutáveis por digest e proveniência | `release` |
| `gw-dotnet.yml`, `gw-node.yml` | Reutilizáveis disponíveis; não chamados pelo CI atual do projeto | Nenhum neste fluxo |

`publishSnapshots: true` permite a preview manual, mas o CI atual não chama
publicação automática de snapshots em cada push. O Environment `preview` citado
na estratégia não está na política local de Environments. `winget` é referenciado
pelo workflow, mas também não consta nessa política. A existência e proteção
efetivas desses ambientes no GitHub não foram verificadas.

## Lacunas corrigidas

Os itens abaixo descrevem o comportamento anterior às guardas desta mudança.

- A resolução do RC compara a base do Release PR com `github.sha` do disparo;
  não consulta novamente o HEAD remoto imediatamente antes de criar a tag,
  depois da espera pela aprovação em `release`.
- `Assert-ReleaseTag.ps1` exige ancestralidade, não igualdade com o HEAD atual.
  Isso é correto para tags históricas, mas insuficiente para garantir RC novo atualizado.
- Um PR de funcionalidade ainda aberto não é detectado como dependência do RC.
  Um pacote pode legitimamente ser atual em relação à `main`, mas não conter
  a mudança que o operador esperava testar.
- A criação da tag final ocorre pelo release-please antes das validações de contexto
  e publicação. Um bloqueio apenas no publicador não impede criar a tag/rascunho.

## Controles de integridade

O plano abaixo foi implementado **no catálogo Groundwork** e aplicado localmente.
`Get-PreviewVersion.ps1`, `Assert-PreviewGate.ps1`, `Assert-ReleaseSource.ps1` e
os publicadores executam as guardas. O marcador literal
`Release-Depends-On: #123, #456` no corpo do Release PR complementa o array durável
`releaseDependencies` de `.github/ci-cd.json`. A lista não é inferida
de PRs abertos; confira sua preservação quando o release-please regenerar notas.
Os pontos abaixo registram a política; o contrato vinculado detalha os limites.

1. Fixar a identidade da candidata: SHA da `main`, SHA/base do Release PR, versão
   e dependências esperadas. Antes da tag, reler o HEAD remoto após a aprovação;
   exigir a mesma base e `Result` verde no SHA exato da `main`. CI pendente,
   mudança de base ou consulta inconclusiva deve recusar, não usar dados antigos.
2. Não bloquear por qualquer PR aberto. Se uma candidata precisa conter um PR
   específico, registrar sua dependência explicitamente no escopo do Release PR,
   no formato validado pelo catálogo. Exigir merge na `main` e inclusão
   do commit resultante, considerando squash; fechamento sem merge não satisfaz.
3. Fixes comuns continuam integráveis pelo CI normal. Depois de integrar mudança
   de código, atualizar Release PR e gerar/aceitar `rc.N+1`; não reutilizar aceite
   do código anterior. Exibir PR pendente, SHA esperado/atual e ação de recuperação.
4. Antes do merge do Release PR, exigir candidata aceita correspondente à base
   atual e diff restrito aos arquivos permitidos. Antes de criar a tag final,
   revalidar o commit integrado do Release PR e sua equivalência com a candidata;
   não exigir igualdade literal RC/final, pois o bump gera outro commit.
5. Mudança da `main` durante uma espera de publicação deve causar recusa e nova
   resolução para releases novas. Manter republish/redeploy/rollback separados:
   tags históricas imutáveis não devem ser obrigadas a acompanhar o HEAD atual.
6. Waiver de emergência pode dispensar o aceite do RC com motivo auditável,
   mas nunca autorizar fonte desatualizada, dependência obrigatória ausente ou CI falho.

Teste discriminante principal: resolver no SHA A, avançar a `main` para B durante
a aprovação e exigir recusa **antes** de criar tag ou rascunho. Cobrir também
dependência pendente/integrada por squash, CI pendente/falho, fix seguido de novo
RC, diferença de código RC/final e republish/rollback históricos permitidos.

## Validação e próxima etapa

Testes locais em containers cobrem fonte/CI, dependências, diferença RC/final e
preview sem mutações indevidas. Não foram criadas tags/releases, feitos dispatches,
aplicadas políticas ou verificados checks/proteções remotos. Arquivos gerenciados
vieram da atualização do catálogo; sementes e personalizações foram preservadas.
O diagrama descreve as etapas funcionais; as guardas agora precedem tags e uploads.
