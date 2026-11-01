# Governança do PixelPointer

**Português (Brasil)** · [English](GOVERNANCE.en.md) · [Español](GOVERNANCE.es.md)

## Responsabilidade

`@lzocateli` é o proprietário e único mantenedor com autoridade para aceitar
escopo, aprovar contribuições e integrar alterações na `main`. Contribuições
externas chegam por fork e PR, conforme [CONTRIBUTING](CONTRIBUTING.md).
Reviews de terceiros são consultivos: não substituem a decisão do mantenedor.

## Proteção da `main`

As políticas do repositório são um contrato versionado em
[`repository-policy.json`](repository-policy.json), gerado pelo Altivy.Groundwork
e aplicado com `groundwork github --sync` pelo mantenedor (ver
[REPOSITORY-POLICY](REPOSITORY-POLICY.md)). `groundwork github` confere a
configuração remota sem alterá-la e retorna `0` quando ela corresponde ao contrato.

| Ruleset | Controles | Exceções |
| --- | --- | --- |
| `gw-main-integrity` | Bloqueia exclusão e force-push, exige histórico linear e o check `Result` com a branch atualizada | Nenhuma, nem para o proprietário |
| `gw-main-review` | Exige PR com uma aprovação, descarta aprovações após novos commits, exige conversas resolvidas e merge somente por squash | Papel de administrador, somente por PR |
| `gw-release-tags` | Restringe criação, atualização e exclusão de `refs/tags/v*` | Somente o GitHub App `Altivy Release` |

O check `Result` (job do [`ci.yml`](workflows/ci.yml)) agrega higiene do PR
(título Conventional Commit, actionlint e arquivos gerenciados),
`Gitleaks / Full History` e os jobs `Build, tests and Native AOT` de
`linux-x64`, `win-x64` e `osx-arm64`; falha se qualquer um deles não terminar com
sucesso. Novos jobs entram em `needs` do `Result`, sem alterar o ruleset.

O [CODEOWNERS](CODEOWNERS) cobre todos os arquivos com `@lzocateli`. Ele
sozinho não bloqueia merge: a imposição depende dos rulesets ativos no GitHub.

### Tags de release e Environments

Tags `v*` são criadas somente pelo GitHub App `Altivy Release`; nem o
administrador cria tags manualmente. Tags estáveis `vX.Y.Z` continuam sendo
criadas no merge do Release PR aberto pelo release-please. Antes de publicar,
`gw-release.yml` confere a versão e a ancestralidade de `origin/main`.

Em 2026-10-31, o mantenedor autorizou a exceção de previews públicas:
`workflow_dispatch` do `publish.yml` em `main` pode criar `vX.Y.Z-preview.N`
pelo mesmo App, após build, testes e aprovação no Environment `release`.
O job verifica commit, checksums e digests remotos; publica como Pre-release,
sem `Latest`, Homebrew, winget ou deploy. PRs continuam sem secrets e não
publicam previews. A exceção não libera criação manual, alteração ou exclusão
de tags; o ruleset `gw-release-tags` permanece inalterado.
Detalhes e recuperação: [processo de release](../docs/release.md#preview-obrigatória).

O Environment `release` exige aprovação de `@lzocateli` para anexar os assets à
release em rascunho e, de novo, para atualizar o tap Homebrew no
`post-release.yml` (o `HOMEBREW_TAP_TOKEN` existe somente nele). O Environment
`winget` exige aprovação de `@lzocateli` tanto para submissões automáticas quanto
manuais e só permite as referências `main` e `v*`. O `prevent_self_review` está
desativado para permitir que o único mantenedor aprove conscientemente uma
execução que ele próprio iniciou; o GitHub também permite bypass administrativo
(`can_admins_bypass=true`), atualmente limitado ao mesmo mantenedor. PRs e
validações sem `submit=true` não executam o job nem acessam
`WINGET_CREATE_GITHUB_TOKEN`.

### PRs de terceiros

1. O mantenedor autoriza a execução dos workflows de colaboradores externos
   após inspecionar o conteúdo; autorização para rodar CI não é aprovação do PR.
2. O CI deve passar e as conversas devem ser resolvidas.
3. O mantenedor revisa e registra sua aprovação antes de integrar.
4. Somente o mantenedor pode efetuar o merge.

### PRs do próprio mantenedor

O GitHub não permite registrar uma review `Approve` no próprio PR. O mantenedor
pode integrar seu PR pela exceção de revisão de `gw-main-review`,
registrando no PR a decisão e as evidências. A exceção **não** dispensa o
check `Result`, a atualização da branch ou as regras de `gw-main-integrity`;
ela não permite push direto, force-push ou exclusão.

Tecnicamente, a exceção também permite ao mantenedor integrar um PR de terceiro
sem review formal; a política do projeto é registrar a aprovação nesses casos.
Ela não é uma autoaprovação formal nem uma verificação automática de autoria.

### Limites da garantia

A exceção está vinculada ao papel `RepositoryRole: Admin` (ID `5`), e não a
um usuário individual. O repositório foi criado em 2026-10-30 a partir da F0 (spike de viabilidade,
[relatório](../docs/f0-relatorio.md)). Entregas seguintes seguem o fluxo
Issue → fase do [plano](../docs/plano-implementacao.md) → branch → PR.

Publicação e aprovação do Environment `release` são controles separados do
merge de PRs comuns: somente o merge do Release PR inicia uma release estável;
a preview pública segue a exceção aprovada acima. Consulte o
[processo de release](../docs/release.md).

## Verificação e recuperação

Sinais de divergência: push direto aceito, merge por terceiro, checks ausentes
ou exceção administrativa ampliada. Na raiz de um clone, com `gh` autenticado
como proprietário, os comandos abaixo são somente leitura:

```bash
groundwork github
gh api repos/lzocateli/PixelPointer/rulesets
gh api repos/lzocateli/PixelPointer/rules/branches/main
gh api repos/lzocateli/PixelPointer/rules/tags/v0.0.0
gh api repos/lzocateli/PixelPointer/environments/winget
gh api repos/lzocateli/PixelPointer/environments/winget/deployment-branch-policies
gh api repos/lzocateli/PixelPointer/collaborators
gh api repos/lzocateli/PixelPointer/private-vulnerability-reporting
gh api repos/lzocateli/PixelPointer/actions/permissions/fork-pr-contributor-approval
```

Resultado esperado: `groundwork github` com código `0`, os dois rulesets `gw-*` de
branch ativos sobre `refs/heads/main` (o primeiro sem bypass, o segundo com bypass
administrativo somente por PR), o check `Result` vinculado ao GitHub Actions,
apenas o proprietário como administrador, `gw-release-tags` ativo com bypass
somente do App, Environment `winget` com aprovação do mantenedor e
branches/tags `main`/`v*`, relatos privados habilitados e
`all_external_contributors` para workflows.

Se houver divergência, suspenda merges e publicações, remova acessos indevidos,
restaure as regras em **Settings > Rules > Rulesets** e repita as consultas.
Se houver suspeita de comprometimento, revogue tokens e sessões afetados e
acione o proprietário; vulnerabilidades seguem a política de segurança.
Não desative regras para contornar CI quebrado: corrija o workflow ou atualize
o nome do check com uma decisão explícita e rastreável.
