# Estrategia de release

[Português (Brasil)](../estrategia-de-release.md) · [English](../en/estrategia-de-release.md) · **Español**

Traducción del archivo gestionado por el Altivy.Groundwork (componente `release-preview`); la
versión en portugués es la normativa. Los pasos operativos están en
[runbooks/release-preview.md](runbooks/release-preview.md) y
[runbooks/release.md](runbooks/release.md).

Repositorio: `lzocateli/PixelPointer` · rama predeterminada: `main` · aceptación de la candidata:
`approval`.

## 1. Resumen

Toda versión estable `vX.Y.Z` va precedida de una **candidata** `vX.Y.Z-rc.N`, publicada con los
mismos workflows, validada en el Environment `quality` y solo entonces promovida. La versión estable
es el mismo código de la candidata validada: entre ambas solo cambian los archivos de versión,
`CHANGELOG.md` y `.release-please-manifest.json`.

```text
PR (squash, Conventional Commit)
  └─▶ main ─▶ release-please actualiza el Release PR (versión + CHANGELOG)
            └─▶ gw-preview.yml (manual) ─▶ etiqueta/borrador ─▶ preflight privado y pruebas
              └─▶ distribution (aprobación después de pruebas) ─▶ publish del mismo artifact ─▶ prerelease atestada
                  └─▶ quality: deploy/smoke + aprobación humana ─▶ preview-acceptance.json
                          └─▶ Preview gate en verde en el Release PR
                  └─▶ merge humano ─▶ etiqueta/borrador ─▶ preflight y pruebas
                    └─▶ distribution (aprobación después de pruebas) ─▶ publish sin recompilar ─▶ release estable
                                          └─▶ production (manual, con aprobación)
```

## 2. Ramas

`main` es la única rama de larga duración. Las candidatas y las versiones estables son
**etiquetas**, no ramas: no hay ramas de homologación (`qas`, `develop`, `release/*`) ni de feed
(`nugettest/*`), y el Environment `quality` recibe la candidata por la etiqueta `vX.Y.Z-rc.N`.

| Rama | Dueño | Vida | Regla |
| --- | --- | --- | --- |
| `main` | humano | permanente | rulesets `gw-*`; solo recibe merge squash de PR con `Result` en verde |
| `release-please--branches--main` | release-please (GitHub App) | hasta el merge del Release PR | generada; nunca la edite a mano |
| `dependabot/*` | Dependabot | hasta el merge o el cierre del PR | generada |
| `feat/*`, `fix/*`, `docs/*`, `ci/*` y similares | autor del PR | hasta el merge | bórrela en el merge (`--delete-branch`) |

Una rama sin PR abierto y con el contenido ya integrado en `main` es un resto y debe borrarse.
Antes, compruébelo con `git merge-base --is-ancestor origin/<branch> origin/main` o, para squash,
`git diff origin/main origin/<branch>` sin diferencias en sus archivos.

## 3. Versiones y canales

| Canal | Versión | Origen | Etiqueta y GitHub Release | Desplegable |
| --- | --- | --- | --- | --- |
| snapshot | `X.Y.Z-preview.N` | cada push en `main` (si `publishSnapshots`) | no | nunca |
| candidata | `X.Y.Z-rc.N` | `gw-preview.yml`, versión del Release PR abierto | etiqueta + prerelease sin *Latest* | solo en `quality` |
| estable | `X.Y.Z` | merge del Release PR | etiqueta + release *Latest* | `quality` y `production` |

- `N` es secuencial por versión: la primera candidata de `X.Y.Z` es `rc.1`; una corrección
  posterior genera `rc.2` en la siguiente ejecución.
- La candidata etiqueta el **commit base del Release PR**, el HEAD de `main` sobre el que
  release-please calculó la versión. Si `main` avanza, el Release PR se actualiza y hace falta una
  nueva candidata.
- Las prereleases que no siguen `vX.Y.Z-rc.N` (por ejemplo `preview.N`) nunca se pueden desplegar
  ni promover.

## 4. CHANGELOG y Conventional Commits

release-please escribe el `CHANGELOG.md` a partir de los títulos de los PR (merge squash). Nadie
edita el CHANGELOG fuera del Release PR.

| Título del PR | Entra en el CHANGELOG | Bump |
| --- | --- | --- |
| `feat` | sí | minor |
| `fix`, `fix(deps)`, `deps`, `perf`, `revert` | sí | patch |
| `!` o pie `BREAKING CHANGE:` | sí | major (antes de 1.0: minor) |
| `docs`, `chore`, `ci`, `build`, `test`, `refactor`, `style` | no | — |

El job `Conventional title` rechaza los títulos fuera del patrón y muestra en el resumen del PR el
impacto en la release. Las notas de la candidata reproducen la sección del Release PR con el aviso
de que no es la versión estable; las notas de la estable reciben la línea "Validada na
`vX.Y.Z-rc.N`" (validada en esa candidata).

## 5. Environments

| Environment | Refs aceptadas | Revisores | Uso |
| --- | --- | --- | --- |
| `release` | `main`, `v*` | `@lzocateli` | se conserva para operaciones heredadas; no autoriza el flujo AOT actual |
| `release` | `main`, `v*` | `@lzocateli` | credenciales de componentes que aún usan este Environment; no sustituye a `distribution` |
| `distribution` | `main`, `v*` | `@lzocateli` | autoriza la divulgación; con `distributionAfterValidation=true`, ocurre tras el preflight privado |
| `quality` | `v*` | obligatorios | deploy y aceptación de la candidata; también acepta estables |
| `production` | `v*` | obligatorios | solo estables con candidata validada |
| `preview` | `main` | — | snapshots en el feed interno |

Los Environments, las políticas de refs y los revisores vienen de `.github/repository-policy.json`
y se aplican con `groundwork github --sync`.

## 6. Aceptación de la candidata

Modo `approval` (aplicación): el deploy en `quality` ejecuta el smoke automatizado del `deploy.yml`
y espera la **aprobación humana** en el Environment `quality`. Aprobar significa: "probé la
candidata y puede convertirse en la versión estable".

Si el deploy termina bien, el job `Record candidate acceptance` del `gw-deploy.yml`:

1. adjunta a la prerelease el asset `preview-acceptance.json` (`tag`, `version`, `commit`,
   `environment`, `result`, `acceptedAt`, `actor`, `run`);
2. vuelve a ejecutar el CI del Release PR abierto, para que el `Preview gate` se evalúe de nuevo.

## 7. Gates

| Gate | Dónde | Regla |
| --- | --- | --- |
| `Preview gate` | job del `gw-pr-hygiene.yml`, dentro de `Result` | el Release PR solo pasa con una candidata `vX.Y.Z-rc.N` publicada en el commit base y aceptada en `quality`, o con waiver |
| Guard de deploy | `Assert-DeployableRelease.ps1` | candidata solo en `quality`; `production` exige la candidata validada de la misma versión, con diff limitado a los archivos de versión y al CHANGELOG, o waiver |
| Etiqueta | `Assert-ReleaseTag.ps1` | candidata con versión mayor que la registrada; estable igual a los archivos de versión |
| Firma | `release-manifest.json` atestado | candidata firmada por `gw-preview.yml`; estable por `gw-release.yml` |

### Waiver

En una emergencia (corrección de seguridad, indisponibilidad), un commit desde la última versión
estable puede omitir la candidata con el pie:

```text
fix: corrija a falha de autenticação

Preview-Waiver: corrección de seguridad publicada sin candidata porque quality no está disponible
```

El waiver libera el `Preview gate` y el guard de `production`, y aparece en el resumen del deploy y
en las notas de la release. Es una excepción auditable, no un atajo: registre el motivo real.

## 8. Automático y humano

| Etapa | Automático | Humano |
| --- | --- | --- |
| PR común | CI, título, Gitleaks, snapshot | revisión y merge |
| Release PR | versión y CHANGELOG | — |
| Candidata | N, etiqueta/borrador, preflight privado, build/pruebas, SBOM, publish del mismo artifact | iniciar `gw-preview.yml`; probar el artifact privado y aprobar `distribution` |
| `quality` | deploy, smoke y registro de la aceptación | probar y aprobar `quality` |
| `Preview gate` | verificación completa | waiver, si corresponde |
| Estable | etiqueta/borrador, preflight privado, validaciones, publish sin recompilar, release y notas de la candidata | merge del Release PR; aprobar `distribution` tras las validaciones |
| `production` | guard | iniciar y aprobar `production` |
| Repositorio | Environments, variables y rulesets (`groundwork github --sync`) | secrets ausentes (`--secrets`) |

## 9. Fallos y rollback

- **Candidata rechazada en `quality`:** corrija con un PR común; release-please actualiza el
  Release PR y la siguiente ejecución genera `rc.N+1`. La candidata rechazada queda como
  prerelease, sin aceptación.
- **`Preview gate` en rojo:** no hay candidata aceptada en el commit base actual (`main` avanzó, la
  aceptación falló o el Environment no coincide). Genere y valide una nueva candidata.
- **`production` rechazado:** la estable difiere de la candidata más allá de los archivos de
  versión (se integró código después de la validación). Publique una nueva versión con candidata.
- **Rollback:** deploy de una estable anterior publicada, con el bloqueo `rollbackMinimumVersion`;
  nunca borre etiquetas ni releases (vea [runbooks/deploy-e-rollback.md](runbooks/deploy-e-rollback.md)).
