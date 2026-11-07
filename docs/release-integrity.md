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
O guard valida fonte e cabeça do Release PR antes de criar a tag/rascunho. A
autorização de divulgação ocorre no Environment `distribution`; depois da espera,
fonte, SHA e tag são revalidados antes de qualquer upload público. Quando
`distributionAfterValidation` está habilitado, o build/teste gera artifacts privados
antes da aprovação e o publisher reutiliza esses mesmos bytes sem rebuild. A
finalização revalida antes de tornar a GitHub Release pública. Nenhum workflow pode
remover essas guardas.

O Preview gate exige cabeça atual do Release PR, dependências integradas, base
atual e CI verde antes de verificar aceite ou waiver. A release final integrada
pendente é validada **antes** do release-please criar a tag: o commit de merge
precisa ser a fonte atual e seu pai precisa ser a candidata aceita da versão.
O diff só admite `versionFiles` e arquivos de release-please configurados.
O RC e a final têm SHAs diferentes devido ao bump/merge; arquivos permitidos não
autorizam alterações funcionais disfarçadas de metadados e exigem revisão humana.

Quando só atualiza o Release PR, o release-please usa `skip-github-release: true`;
somente `release_ready=true` da guarda libera criação de release/tag.
Se um Release PR integrado ainda marcado `autorelease: pending` pertencer a um SHA
anterior ao HEAD atual, a guarda retorna `release_ready=false` para permitir somente
que release-please atualize o Release PR; não cria tag nem publica. A autorização de
criação só é liberada quando exatamente um pending corresponde ao SHA atual. Nunca
publique pela tag antiga: resolva e revise a nova versão no HEAD vigente.

## Autorização da distribuição

No modo padrão `environment`, estáveis, RCs, previews manuais e reenvios de artefatos ausentes exigem aprovação
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

### Alternativa explícita para repositório privado

Para estáveis privadas cujo plano não oferece revisores de Environment, existe o
modo opt-in `distributionApproval.mode = "manual-dispatch"` em `.github/ci-cd.json`.
O parâmetro de renderização correspondente é `distribution_approval_mode`;
`environment` continua sendo o padrão de novos consumidores. No próprio Groundwork,
a configuração e o parâmetro versionados selecionam explicitamente a alternativa.
Não há detecção automática do plano nem fallback após HTTP 422.

A execução usa `distribution-manual`, separado do ambiente nativo. Esse ambiente
não representa aprovação: a evidência é o dispatch autenticado por administrador
humano, cujo título imutável registra `Authorize distribution | <tag> | <SHA>`.
A guarda consulta o run pela API, verifica evento `workflow_dispatch`, workflow
`gw-release.yml`, ref permitida, SHA, ator humano e permissão administrativa atual.
Confirmação ausente, bot, permissão somente de escrita, outro ator na reexecução,
tag/SHA divergentes ou API inconclusiva recusam a distribuição. A tag é conferida
novamente antes de publicar; as guardas de HEAD atual, CI/Result, dependências,
versões, draft e equivalência com RC continuam obrigatórias quando aplicáveis.

Na raiz do repositório privado, com GitHub CLI autenticado como administrador,
após integrar o Release PR e aguardar CI verde, defina `SHA_APROVADO` como o SHA
completo daquele merge. Substitua a versão fictícia `v1.2.3` pela versão integrada:

```bash
gh workflow run gw-release.yml --ref main -f mode=release \
	-f confirm_distribution=true -f authorize_tag=v1.2.3 \
	-f authorize_commit="$SHA_APROVADO"
```

O comando é autorização de publicação e pode criar tag, rascunho e enviar pacotes;
nunca o execute sem conferir versão e fonte. O push do merge não publica sozinho:
ele é recusado antes da tag enquanto faltar confirmação. Reexecutar um run de push
não o transforma em autorização manual. Reexecução por outro administrador exige
novo dispatch. Republish de tag estável já pública que contenha este suporte usa
`mode=republish` na tag com a mesma confirmação explícita, preservando a fonte histórica.

A alternativa não se aplica a repositórios públicos, RCs ou previews manuais.
Nesses fluxos a aprovação nativa continua obrigatória. Tags/runs anteriores que não
contêm o suporte não são alterados: preserve rascunhos e artefatos e prepare uma
nova versão com a correção integrada; não mova tags nem libere código antigo.
Administradores capazes de alterar workflows continuam dentro da fronteira de
confiança; revisão da branch protegida e logs do GitHub são parte da auditoria.

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
checkout correspondente e artefatos ausentes. Para assets da GitHub Release, antes
de qualquer upload o publicador exige entrada correspondente no manifesto imutável
e digest reconstruído idêntico; asset sem entrada ou divergente é recusado antes de
se tornar órfão na release. Redeploy/rollback continuam usando tags históricas
publicadas e as políticas de deploy/`rollbackMinimumVersion`.

Ao atualizar consumidores, execute `groundwork update --dry-run`, revise e aplique
`groundwork update` com o catálogo local ou distribuído autorizado. Sementes não
são migradas automaticamente. Em `ci.yml`, conceda `pull-requests`, `actions` e
`checks: read` ao chamador de higiene; snapshot depende de `result`, mas não entra
nos `needs` de Result. Em `publish.yml`, propague essas permissões e `republish`
a todos os publicadores. Repasse permissões também nas chamadas aninhadas. Em
`release-please-config.json`, adicione `"always-update": true`: sem isso, um commit
que não muda as notas (`ci`, `docs`, `chore`) deixa o Release PR atrás da branch
padrão, e `gw-preview.yml` recusa gerar a candidata.
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