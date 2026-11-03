# Flujo de los workflows

[Português (Brasil)](../fluxo-workflows.md) · [English](../en/fluxo-workflows.md) · [Español](fluxo-workflows.md)

## Alcance y estado

Revisión del código local del 2026-11-03, no verificación de la configuración
activa en GitHub. Esta guía complementa la
[estrategia gestionada](../estrategia-de-release.md) y el
[contrato de CI/CD](../../.github/CI-CD.md); no modifica esos contratos.
Las guardas se implementaron en el catálogo y se aplicaron localmente mediante
actualización gestionada. El [contrato de integridad](release-integrity.md) documenta
interfaces, recuperación, migración y límites. Activación remota requiere integrar
estos cambios. El portugués brasileño es la fuente normativa.

## Flujo implementado

```mermaid
flowchart TD
    PR["PR común: feat, fix, docs y otros"] --> CI["ci.yml: higiene, Gitleaks, build, pruebas, AOT"]
    CI --> RESULT["Result obligatorio + revisión + base actualizada"]
    RESULT --> MERGE["Merge squash en main"]
    MERGE --> MAINCI["ci.yml en el SHA de main"]
    MERGE --> RP["gw-release.yml: release-please crea o actualiza Release PR"]
    RP --> RPCI["CI del Release PR: incluye Preview gate"]
    RP --> MANUAL["Disparo manual de gw-preview.yml en main"]
    MANUAL --> RESOLVE["Get-PreviewVersion: versión, dependencias, HEAD actual y CI verde"]
    MAINCI --> RESOLVE
    RESOLVE --> RELRC["Environment release: aprobación"]
    RELRC --> RCTAG["GitHub App crea vX.Y.Z-rc.N + prerelease en borrador"]
    RCTAG --> RCCTX["Valida versión y ascendencia de la tag"]
    RCCTX --> PUB["publish.yml: build de 6 RIDs, smoke, instaladores, SBOM"]
    PUB --> ASSETS["gw-github-assets.yml: Environment release, assets y procedencia"]
    ASSETS --> RCFINAL["gw-preview.yml: manifiesto atestado + prerelease pública, sin Latest"]
    RCFINAL --> Q["gw-deploy.yml en la tag: guard quality"]
    Q --> QA["deploy.yml: aprobación humana en Environment quality"]
    QA --> QS["Comprueba assets + smoke del paquete linux-x64 en containers"]
    QS --> ACCEPT["preview-acceptance.json + repetición del CI del Release PR"]
    ACCEPT --> RPCI
    RPCI --> RPOK["Result verde + revisión humana"]
    RPOK --> RPMERGE["Merge squash del Release PR en main"]
    RPMERGE --> FINALGUARD["Guard antes de tag: SHA actual, CI verde y código del RC aceptado"]
    FINALGUARD --> FINALTAG["gw-release.yml: GitHub App crea vX.Y.Z + release en borrador"]
    FINALTAG --> FINALCTX["Valida versiones y ascendencia de la tag"]
    FINALCTX --> FINALPUB["publish.yml: build, smoke, SBOM y assets en Environment release"]
    FINALPUB --> STABLE["gw-release.yml: manifiesto atestado + release estable pública"]
    STABLE --> POST["post-release.yml"]
    POST --> BREW["Environment release: actualiza Homebrew + verifica instalación"]
    POST --> WINGET["winget.yml: validación + envío en Environment winget"]
    STABLE --> AUTOQ["Disparo automático de gw-deploy.yml en quality"]
    STABLE --> PROD["Disparo manual de gw-deploy.yml en tag estable: production"]
    AUTOQ --> VERIFYQ["Guard + aprobación quality + assets + smoke"]
    PROD --> VERIFYPROD["Guard + aprobación production + assets + smoke"]
```

El CI de main y release-please parten del mismo push, pero no existe dependencia
`needs` entre esos workflows. El dibujo no implica que el CI de main termine
antes de resolver la candidata. Preview gate admite el waiver auditable descrito
en la estrategia; los PRs comunes, incluidos fixes, no requieren ese gate.

La aprobación humana en `quality` debe representar una prueba manual del paquete:
la espera del Environment ocurre **antes** de comprobar assets y ejecutar el smoke
automatizado. El éxito posterior registra la aceptación; el smoke no sustituye
la prueba gráfica humana.

La estable se reconstruye desde la tag final, sin reutilizar los binarios del RC.
La estrategia exige equivalencia del código, con diferencias únicamente de
metadatos de release. `production` **no bloquea la publicación pública ni la
distribución**: ambas ya comenzaron cuando se ejecuta ese flujo manual.
PixelPointer no tiene servidor desplegado; el deploy verifica paquetes publicados.

## Otros caminos

| Entrada | Recorrido y resultado | Environments |
| --- | --- | --- |
| `publish.yml`, dispatch en main con preview | Build y validación; crea `vX.Y.Z-preview.N` y GitHub Pre-release sin Latest; no promovible ni desplegable | `release` |
| `publish.yml`, PR que cambia la cadena de release | Build, smoke e instaladores; sin tag ni publicación | Ninguno |
| `gw-release.yml`, dispatch en tag con `mode=republish` | Valida tag y release existente; repone artefactos ausentes, finaliza y repite distribución; no crea versión ni hace auto-deploy | `release`, `winget` al enviar |
| `gw-deploy.yml`, dispatch de tag histórica | Redeploy/rollback con el mismo guard y smoke; respeta `rollbackMinimumVersion`; no recrea tag ni release | `quality` o `production` |
| `winget.yml`, dispatch | Valida versión indicada; envío opcional e idempotente | `winget` si se envía |
| `scheduled-smoke.yml`, lunes 09:00 UTC o dispatch | Recompila y prueba AOT/paquetes; no publica | Ninguno |
| `gw-gitleaks.yml` | Llamado por CI; también acepta dispatch; análisis sin publicación | Ninguno |
| `gw-pr-hygiene.yml` | Llamado por CI: título, actionlint, archivos gestionados y Preview gate del Release PR | Ninguno |
| `gw-github-assets.yml` | Llamado por el publicador: uploads inmutables por digest y procedencia | `release` |
| `gw-dotnet.yml`, `gw-node.yml` | Reutilizables disponibles; no llamados por el CI actual del proyecto | Ninguno en este flujo |

`publishSnapshots: true` permite la preview manual, pero el CI actual no llama la
publicación automática de snapshots en cada push. El Environment `preview` citado
en la estrategia no aparece en la política local de Environments. `winget` está
referenciado por su workflow, pero tampoco aparece en esa política. No se verificaron
su existencia ni sus protecciones efectivas en GitHub.

## Lagunas corregidas

Estos puntos describen el comportamiento anterior a las guardas de este cambio.

- La resolución del RC compara la base del Release PR con `github.sha` del disparo;
  no consulta nuevamente el HEAD remoto justo antes de crear la tag, después
  de esperar la aprobación en `release`.
- `Assert-ReleaseTag.ps1` exige ascendencia, no igualdad con el HEAD actual.
  Es correcto para tags históricas, pero insuficiente para garantizar un RC nuevo actualizado.
- Un PR de funcionalidad abierto no se detecta como dependencia del RC. El paquete
  puede corresponder a main y no contener el cambio que el operador esperaba probar.
- Release-please crea la tag final antes de las validaciones de contexto y publicación.
  Un bloqueo solamente en el publicador no evita crear la tag o el borrador.

## Controles de integridad

El plan siguiente se implementó **en el catálogo Groundwork** y se aplicó localmente.
`Get-PreviewVersion.ps1`, `Assert-PreviewGate.ps1`, `Assert-ReleaseSource.ps1` y los
publicadores ejecutan las guardas. El marcador literal
`Release-Depends-On: #123, #456` en el cuerpo del Release PR complementa el array
duradero `releaseDependencies` de `.github/ci-cd.json`; no se infieren de PRs
abiertos. Verifique su preservación cuando release-please regenere las notas.
Los puntos siguientes registran política; el contrato vinculado detalla sus límites.

1. Fijar la identidad de la candidata: SHA de main, SHA/base del Release PR,
   versión y dependencias esperadas. Antes de la tag, releer el HEAD remoto tras
   la aprobación; exigir la misma base y `Result` verde en el SHA exacto de main.
   CI pendiente, cambio de base o consulta inconclusa deben rechazar, sin datos antiguos.
2. No bloquear por cualquier PR abierto. Si una candidata debe incluir un PR
   concreto, declarar esa dependencia explícitamente en el alcance del Release PR,
  con formato validado por el catálogo. Exigir merge en main e inclusión
   del commit resultante, considerando squash; cerrar sin merge no satisface.
3. Los fixes comunes siguen integrándose por el CI normal. Tras integrar cambios
   de código, actualizar Release PR y generar/aceptar `rc.N+1`; no reutilizar la
   aceptación del código anterior. Mostrar PR pendiente, SHA esperado/actual y recuperación.
4. Antes del merge del Release PR, exigir candidata aceptada que corresponda a la
   base actual y diff limitado a archivos permitidos. Antes de crear la tag final,
   revalidar el commit integrado del Release PR y su equivalencia con la candidata;
   no exigir igualdad literal RC/final, pues el bump genera otro commit.
5. Un cambio de main durante la espera de publicación debe rechazar releases nuevas
   y exigir nueva resolución. Separar republish/redeploy/rollback: tags históricas
   inmutables no deben estar obligadas a seguir el HEAD actual.
6. Un waiver de emergencia puede dispensar la aceptación del RC con motivo auditable,
   pero nunca autorizar fuente desactualizada, dependencia obligatoria ausente o CI fallido.

Prueba discriminante principal: resolver en SHA A, avanzar main a B durante la
aprobación y exigir rechazo **antes** de crear tag o borrador. Cubrir también
dependencia pendiente/integrada por squash, CI pendiente/fallido, fix seguido de
nuevo RC, diferencias de código RC/final y republish/rollback históricos permitidos.

## Validación y siguiente paso

Tests locales en containers cubren fuente/CI, dependencias, diferencias RC/final
y preview sin mutaciones indebidas. No se crearon tags/releases, dispararon
workflows, aplicaron políticas ni verificaron checks/protecciones remotos. Los
archivos gestionados llegaron por actualización del catálogo; se preservaron
semillas y personalizaciones. El diagrama muestra etapas funcionales; las guardas
ahora preceden tags y uploads.
