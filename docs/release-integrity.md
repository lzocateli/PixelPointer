# Integridade da fonte de release

[Português (Brasil)](release-integrity.md) · [English](en/release-integrity.md) · [Español](es/release-integrity.md)

## Contrato implementado

Novas candidatas, releases, snapshots e pacotes exigem checkout no HEAD remoto
atual da branch padrão. `Assert-ReleaseSource.ps1` usa
`Assert-CurrentReleaseSource`: o CI de `ci.yml` disparado por push nessa branch
precisa estar verde no SHA exato, com `Result` do GitHub Actions e da mesma check
suite. Ausência, falha ou consulta inconclusiva bloqueia. O HEAD é relido depois
das consultas de CI; ancestralidade sozinha não basta.

O RC vem da versão do Release PR aberto, cuja base deve coincidir com o SHA.
Depois da aprovação em `release`, a resolução é repetida com cabeça do Release
PR e tag esperadas, antes de criar a tag/rascunho. A publicação de assets/pacotes
também valida a fonte após aprovação, e a finalização revalida antes de tornar
a GitHub Release pública. Nenhum workflow é autorizado a remover essas guardas.

O Preview gate exige cabeça atual do Release PR, dependências integradas, base
atual e CI verde antes de verificar aceite ou waiver. A release final integrada
pendente é validada **antes** do release-please criar a tag: o commit de merge
precisa ser a fonte atual e seu pai precisa ser a candidata aceita da versão.
O diff só admite `versionFiles` e arquivos de release-please configurados.
O RC e a final têm SHAs diferentes devido ao bump/merge; arquivos permitidos não
autorizam alterações funcionais disfarçadas de metadados e exigem revisão humana.

Quando só atualiza o Release PR, o release-please usa `skip-github-release: true`;
somente `release_ready=true` da guarda libera criação de release/tag.

## Autorização da distribuição

Estáveis, RCs, previews manuais e reenvios de artefatos ausentes exigem aprovação
humana no novo Environment `distribution`, antes de publicar pacotes em feeds,
registros ou GitHub Releases e antes de Homebrew/winget. Tags e rascunhos privados
podem existir antes da autorização; não equivalem a distribuir o pacote.

O job identifica a versão e o SHA e espera em **Review deployments**. Depois da
aprovação, `Assert-DistributionApproval` consulta a evidência do próprio run no
GitHub, exige usuário humano, workflow autorizado, SHA correspondente e tag
inalterada. Ausência de revisão, aprovação só de `quality`, bot, rejeição ou API
inconclusiva bloqueiam. Rejeição exige uma nova execução. A fonte e o CI são
revalidados depois da espera e antes da publicação; avançar a main não é autorizado
por um aceite antigo. A finalização e os publicadores repetem a verificação.

`distribution` autoriza divulgação; `release` mantém credenciais de empacotamento
e publicação; `quality` aceita o teste do pacote; `production` verifica a versão
já publicada. Aprovar PR, merge ou `Preview-Waiver` não autoriza distribuição.
Republish conserva a exceção de HEAD histórico, mas exige aprovação para o novo
envio. Snapshots continuam internos: o publicador recusa pacote GitHub existente
com visibilidade pública/desconhecida. Pacotes novos usam a visibilidade privada
padrão do GitHub Packages. Mudanças administrativas de visibilidade fora do workflow
não são impedidas por essas guardas.

Antes de ativar os workflows novos, configure **Settings > Environments > distribution**
com revisores obrigatórios e refs da branch padrão/`v*`, conforme o contrato de
`repository-policy`. O ambiente não precisa receber secrets. Aplique a política
pela interface ou por `groundwork github --sync` somente com autorização administrativa
explícita. Não há fallback para um ambiente sem revisores: se o plano do GitHub
não suportar a proteção, a publicação fica bloqueada. Ambiente criado automaticamente
sem aprovação registrada não libera o envio. A atualização local do catálogo não
configura o GitHub nem ativa esses gates remotamente.

## Integrações e fixes

Um PR comum, inclusive fix, não exige aceite de RC e não é bloqueado por outros
PRs abertos. Para uma candidata que depende de integrações específicas, o operador
registra duravelmente em `.github/ci-cd.json`, por PR revisado:

```json
"releaseDependencies": [123, 456]
```

O padrão é `[]`; somente integrações obrigatórias do escopo da release entram
nessa lista. Remova dependências já satisfeitas apenas com revisão do próximo
escopo. A lista versionada não desaparece ao regenerar notas. O corpo do Release
PR pode complementá-la, sem editar sua branch gerada:

```text
Release-Depends-On: #123, #456
```

As duas listas são unificadas e restritas ao mesmo repositório. Cada PR precisa ter merge na branch
padrão e o SHA resultante precisa estar incluído na fonte aprovada; squash é
considerado pelo `merge_commit_sha`. PR fechado sem merge não satisfaz. A lista
é relida após aprovação e no Preview gate. O release-please pode regenerar o
corpo: confira/preserve o marcador ao atualizar o Release PR; dependências não
declaradas não são inferidas de PRs abertos; use a configuração versionada como
registro durável. A remoção indevida é uma alteração
do escopo de release e exige revisão humana, nunca uma recuperação automática.

Após integrar código novo, atualize o Release PR e gere/aceite `rc.N+1`.
`Preview-Waiver` é exceção auditável somente para aceite de candidata; não libera
SHA antigo, CI falho ou dependência obrigatória ausente.

## Recuperação e migração

Sinais: mensagem `Fonte desatualizada`, `CI ... pendente`, `Integração obrigatória
pendente` ou `Release difere do RC`. Aguarde a integração/CI, atualize o Release
PR e gere nova execução/candidata. Nunca mova tags existentes ou substitua assets.
Se o CI da main ainda não terminou quando o push disparou a release, depois de
ficar verde retome pela CLI autenticada, na raiz do repositório:

```bash
gh workflow run gw-release.yml --ref main -f mode=release
```

Substitua `main` pela branch padrão configurada. O comando pode criar tag e
release e exige autorização operacional explícita. Um rascunho interrompido
exige fonte revalidada; não é republish histórico. Escale ao mantenedor se a
main já avançou sobre um Release PR integrado ainda pendente de tag.

Republish só é exceção de HEAD para tag estável de release **já pública**,
checkout correspondente e artefatos ausentes. Redeploy/rollback continuam usando
tags históricas publicadas e as políticas de deploy/`rollbackMinimumVersion`.

Ao atualizar consumidores, execute `groundwork update --dry-run`, revise e aplique
`groundwork update` com o catálogo local ou distribuído autorizado. Sementes não
são migradas automaticamente. Em `ci.yml`, conceda `pull-requests`, `actions` e
`checks: read` ao chamador de higiene; snapshot depende de `result`, mas não entra
nos `needs` de Result. Em `publish.yml`, propague essas permissões e `republish`
a todos os publicadores. Repasse permissões também nas chamadas aninhadas.
Não sobrescreva personalizações nem altere hashes manualmente para esconder desvios.

Correções locais do catálogo ainda não são uma versão distribuída: integre e
publique o catálogo pelo processo autorizado, depois atualize/integre consumidores.
Não reaplique um catálogo antigo que não contenha estas guardas sobre o conjunto
local validado. Testes locais não ativam mudanças nos workflows remotos.

## Segurança e limites

O publicador NuGet executa em container Linux como root, enquanto o checkout pode
pertencer a outro UID. Após o checkout e antes de ler o SHA, a etapa
`Trust checked-out workspace` registra somente `$GITHUB_WORKSPACE` em
`safe.directory` no Git do container. Não use `safe.directory=*`: outro repositório
com ownership diferente deve continuar bloqueado. Essa configuração é efêmera;
não altera o Git do desenvolvedor nem ignora a guarda de fonte/CI.

A imagem SDK não precisa incluir GitHub CLI. O publicador em `ubuntu-24.04`
monta apenas `/usr/bin/gh` do runner em `/usr/local/bin/gh:ro`; não monta HOME,
credenciais ou socket Docker. O token de permissões mínimas vem do processo do
workflow. Runners alternativos precisam fornecer esse binário nesse caminho.
`NuGetFeedTests` reproduz ownership diferente com Git real, verifica a ordem das
etapas e impede ampliar a confiança/mount para outros diretórios.

Ameaças cobertas: aprovação de SHA antigo, CI de PR/outro SHA/app/check suite,
integração omitida, diferença RC/final, API inconclusiva e uso de rascunho como
republish. Guardas usam consultas de leitura e recusam antes das mutações;
tokens não são impressos. Revisores protegem metadados, configuração e dependências.

GitHub não oferece transação atômica entre HEAD, checks e criação/upload de tags.
As revalidações reduzem a janela, mas não bloqueiam merges concorrentes no servidor.
Tags e artefatos ficam imutáveis; uma mudança detectada posteriormente deixa o
rascunho bloqueado, não promove silenciosamente. Nenhum pacote histórico já
público é invalidado só porque a main avançou. Testes locais com API simulada
não comprovam permissões/proteções efetivas no GitHub nem aceite gráfico humano.

Verifique testes `ReleasePreviewTests`, contratos do catálogo e workflows
renderizados com actionlint em containers. Casos essenciais: main avança durante
aprovação/consulta, CI errado/pendente/falho, dependência squash/ausente,
RC/final com código diferente, waiver e republish histórico.