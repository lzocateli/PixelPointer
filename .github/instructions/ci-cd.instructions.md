---
name: "CI e release"
description: "Use ao criar ou alterar GitHub Actions, Dependabot, matriz de build Native AOT, release, checksums, assinatura, SBOM, proveniência ou canais de distribuição do PixelPointer."
applyTo: ".github/workflows/**, .github/dependabot.yml, .github/release.yml"
---

# CI e release

- Fixe actions por SHA e registre a versão humana em comentário.
- Use `permissions: {}` no workflow e permissões mínimas por job.
- Pull requests não recebem secrets; chaves de assinatura ficam em Environment protegido `release`.
- Nenhum secret, token ou chave privada é versionado em código, configuração, documentação ou workflow.
- Separe restore, format, build, testes, publicação AOT, smoke de pacote e testes de instaladores.
- Publique Native AOT por RID no próprio SO: `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`. Não há `linux-musl` (SDL3-CS sem binário musl); o mínimo Linux é glibc 2.34, exigido pela libSDL3.
- Publicação AOT falha com qualquer aviso de trimming ou AOT.
- O smoke de cada RID roda `--version`, `--help` e `selftest` do pacote em ambiente limpo, sem .NET, também por link simbólico.
- Pacotes levam o executável, SDL3, libSkiaSharp, `LICENSE` e `THIRD-PARTY-NOTICES.md`; release gera `tar.gz`/`zip`, `.deb`, instaladores, `SHA256SUMS`, SBOM e atestações Sigstore keyless de proveniência e SBOM. Authenticode e Developer ID ficam fora da v1 (D5).
- Artefatos publicados devem ser imutáveis; a limitação atual de reexecução está registrada no [processo de release](../../docs/release.md#limitações-conhecidas).
- Versões seguem SemVer e são calculadas pelo release-please a partir de Conventional Commits; a tag `vX.Y.Z` é criada pelo GitHub App de release no merge humano do Release PR, e a publicação ocorre somente após aprovação no Environment `release` (ver [CI-CD](../CI-CD.md)).
- Workflows `gw-*` e scripts de `tools/scripts/ci/` são gerenciados pelo Altivy.Groundwork: não os edite; `publish.yml`, `post-release.yml`, `ci.yml` e `winget.yml` pertencem ao projeto.
- Cacheie somente conteúdo reprodutível; restore usa lock file em modo travado.
- Não adicione workflow antes de existir comando local equivalente e validado em `tools/scripts/`.
- Testes que instalam pacotes usam apenas containers ou runners efêmeros.
- Falha de segurança crítica ou alta bloqueia release.
