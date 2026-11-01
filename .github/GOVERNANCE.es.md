# Gobernanza de PixelPointer

[Português (Brasil)](GOVERNANCE.md) · [English](GOVERNANCE.en.md) · **Español**

## Responsabilidad

`@lzocateli` es el propietario y único mantenedor autorizado para aceptar
alcance, aprobar contribuciones e integrar cambios en `main`. Las contribuciones
externas llegan mediante fork y PR, según [CONTRIBUTING](CONTRIBUTING.md).
Las reviews de terceros son consultivas y no reemplazan su decisión.

## Protección de main

Las políticas son un contrato versionado en
[`repository-policy.json`](repository-policy.json), generado por
Altivy.Groundwork y aplicado por el mantenedor con `groundwork github --sync`
(véase [REPOSITORY-POLICY](REPOSITORY-POLICY.md)). `groundwork github` verifica
la configuración remota sin modificarla y devuelve `0` si coincide con el contrato.

| Ruleset | Controles | Excepciones |
| --- | --- | --- |
| `gw-main-integrity` | Bloquea eliminación y force-push, exige historial lineal y rama actualizada con el check `Result` | Ninguna, tampoco para el propietario |
| `gw-main-review` | Exige PR con una aprobación, descarta aprobaciones tras nuevos commits, exige conversaciones resueltas y merge por squash | Rol de administrador, solo mediante PR |
| `gw-release-tags` | Restringe creación, actualización y eliminación de `refs/tags/v*` | Solo el GitHub App `Altivy Release` |

`Result` en [`ci.yml`](workflows/ci.yml) agrega higiene del PR (título Conventional
Commit, actionlint y archivos administrados), `Gitleaks / Full History` y
`Build, tests and Native AOT` de `linux-x64`, `win-x64` y `osx-arm64`; falla si
algún job obligatorio no termina correctamente. Los jobs nuevos se incluyen en
`Result.needs` sin cambiar el ruleset.

[CODEOWNERS](CODEOWNERS) cubre todos los archivos con `@lzocateli`. Por sí solo
no bloquea merges; la imposición depende de los rulesets activos de GitHub.

### Etiquetas de release y Environments

Solo el GitHub App `Altivy Release` crea etiquetas `v*`; tampoco el administrador
las crea manualmente. Las estables `vX.Y.Z` siguen creándose al integrar el
Release PR de release-please. Antes de publicar, `gw-release.yml` verifica
versión y ancestralidad de `origin/main`.

El 2026-10-31, el mantenedor autorizó una excepción para previews públicas:
`workflow_dispatch` de `publish.yml` en `main` puede crear `vX.Y.Z-preview.N`
mediante el mismo App, tras build, pruebas y aprobación del Environment
`release`. El job verifica commit, checksums y digests remotos y publica como
Pre-release, sin `Latest`, Homebrew, winget ni deploy. Los PR siguen sin secrets
y no publican previews. La excepción no permite crear, modificar o eliminar
etiquetas manualmente; `gw-release-tags` permanece sin cambios.
Detalles y recuperación: [proceso de release](../docs/es/release.md#preview-obligatorio).

El Environment `release` exige aprobación de `@lzocateli` para adjuntar assets
al borrador y, nuevamente, para actualizar el tap Homebrew en `post-release.yml`
(`HOMEBREW_TAP_TOKEN` existe solo allí). `winget` exige su aprobación para envíos
automáticos y manuales y admite solo referencias `main` y `v*`.
`prevent_self_review` está desactivado para que el único mantenedor pueda aprobar
conscientemente una ejecución que inició. GitHub también permite bypass
administrativo (`can_admins_bypass=true`), actualmente limitado al mismo
mantenedor. Los PR y validaciones sin `submit=true` no ejecutan el job de envío
ni acceden a `WINGET_CREATE_GITHUB_TOKEN`.

### PR de terceros

1. El mantenedor autoriza workflows de colaboradores externos tras inspeccionar
   su contenido; autorizar CI no es aprobar el PR.
2. CI debe pasar y las conversaciones deben resolverse.
3. El mantenedor revisa y registra la aprobación antes de integrar.
4. Solo el mantenedor puede efectuar el merge.

### PR del propio mantenedor

GitHub no permite registrar una review `Approve` en el propio PR. El mantenedor
puede integrar su PR mediante la excepción de revisión de `gw-main-review`,
registrando decisión y evidencias. La excepción no elimina `Result`, la
actualización de la rama ni `gw-main-integrity`; no permite push directo,
force-push ni eliminación.

Técnicamente también permite integrar un PR de terceros sin review formal;
la política exige registrar la aprobación en esos casos. No es una autoaprobación
formal ni una comprobación automática de autoría.

### Límites de la garantía

La excepción está vinculada a `RepositoryRole: Admin` (ID `5`), no a un usuario
individual. El repositorio se creó el 2026-10-30 a partir de F0, el spike de
viabilidad ([informe](../docs/f0-relatorio.md)). Las entregas posteriores siguen
Issue, fase del [plan](../docs/plano-implementacao.md), rama y PR.

La publicación y aprobación del Environment `release` son controles separados
del merge de PR comunes: solo el merge del Release PR inicia una release
estable; la preview pública sigue la excepción autorizada arriba. Véase el
[proceso de release](../docs/es/release.md).

## Verificación y recuperación

Señales de divergencia: push directo aceptado, merge por terceros, checks
ausentes o excepción administrativa ampliada. En la raíz de un clone, con `gh`
autenticado como propietario, estos comandos son solo de lectura:

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

Resultado esperado: `groundwork github` devuelve `0`; ambos rulesets `gw-*`
de rama activos sobre `refs/heads/main` (integridad sin bypass, revisión con
bypass administrativo solo por PR); `Result` vinculado a GitHub Actions; solo
el propietario como administrador; `gw-release-tags` con bypass exclusivo del
App; `winget` con aprobación del mantenedor y referencias `main`/`v*`; reportes
privados habilitados y aprobación `all_external_contributors` para workflows.

Si hay divergencia, suspende merges y publicaciones, elimina accesos indebidos,
restaura reglas en **Settings > Rules > Rulesets** y repite las consultas. Si
sospechas compromiso, revoca tokens y sesiones afectados y contacta al
propietario; las vulnerabilidades siguen la política de seguridad. No desactives
reglas para eludir CI defectuoso: corrige el workflow o cambia el nombre del
check mediante una decisión explícita y rastreable.
