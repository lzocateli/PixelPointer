# Integridad de la fuente de release

[Português (Brasil)](../release-integrity.md) · [English](../en/release-integrity.md) · [Español](release-integrity.md)

## Contrato implementado

Candidatas, releases, snapshots y paquetes nuevos exigen checkout en el HEAD
remoto actual de la branch predeterminada. `Assert-ReleaseSource.ps1` usa
`Assert-CurrentReleaseSource`: el CI de `ci.yml` disparado por push debe estar
verde en ese SHA exacto, con `Result` de GitHub Actions y de la misma check suite.
CI ausente/fallido o consulta inconclusa bloquean. Se relee HEAD después de las
consultas; la ascendencia no basta. El portugués brasileño es la fuente normativa.

El RC usa la versión del Release PR abierto, cuya base debe coincidir con el SHA.
La guarda valida la fuente y la cabeza del Release PR antes de crear la etiqueta
y el borrador. La divulgación se autoriza en el Environment `distribution`; tras
la espera, fuente, SHA y etiqueta se revalidan antes de cualquier carga pública.
Cuando `distributionAfterValidation` está habilitado, build/pruebas generan
artifacts privados antes de la aprobación y el publisher reutiliza esos mismos
bytes sin recompilar. La finalización revalida antes de hacer pública la GitHub
Release. Los workflows nunca deben eliminar estas guardas.

Preview gate exige cabeza actual del Release PR, dependencias integradas, base
actual y CI verde antes de comprobar aceptación o waiver. La release final
integrada pendiente se valida **antes** de crear la tag por release-please: el
commit de merge debe ser la fuente actual y su padre la candidata aceptada de
esa versión. El diff sólo permite `versionFiles` y archivos configurados de
release-please. Los SHAs RC/final difieren por el bump/merge; los archivos permitidos
no autorizan cambios funcionales disfrazados de metadatos y requieren revisión humana.

Al actualizar sólo el Release PR, release-please usa `skip-github-release: true`;
sólo la salida `release_ready=true` de la guarda autoriza crear release/tag.
Si un Release PR integrado aún marcado `autorelease: pending` pertenece a un SHA
anterior al HEAD actual, la guarda devuelve `release_ready=false` y solo permite que
release-please actualice el Release PR; no crea etiqueta ni publica. La creación se
habilita únicamente cuando exactamente un pending coincide con el SHA actual. Nunca
publique con la etiqueta antigua: resuelva y revise la nueva versión en el HEAD vigente.

## Autorización de distribución

En el modo predeterminado `environment`, estables, RCs, previews manuales y reenvíos de artefactos ausentes exigen aprobación
humana en el nuevo Environment `distribution`, antes de publicar en feeds,
registros o GitHub Releases y antes de Homebrew/winget. Tags y borradores privados
pueden existir antes de autorizar; no equivalen a distribuir el paquete.

El job identifica versión y SHA y espera en **Review deployments**. Tras aprobar,
`Assert-DistributionApproval` consulta la evidencia del mismo run en GitHub y exige
usuario humano, workflow autorizado, SHA correspondiente y tag sin cambios.
Falta de revisión, aprobación sólo de quality, bot, rechazo o API inconclusa bloquean.
Un rechazo exige una ejecución nueva. Se revalidan fuente y CI tras la espera y
antes de publicar; aceptar código antiguo no autoriza avanzar main. Finalización
y publicadores repiten la verificación.

`distribution` autoriza divulgar; `release` mantiene credenciales de empaquetado
y publicación; `quality` acepta pruebas del paquete; `production` verifica la versión
ya publicada. Aprobación de PR, merge o `Preview-Waiver` no autorizan distribución.
Republish conserva su excepción de HEAD histórico, pero exige aprobación para el
nuevo envío. Los snapshots siguen internos: se rechaza paquete GitHub existente
con visibilidad pública/desconocida. Paquetes nuevos usan la visibilidad privada
predeterminada de GitHub Packages. Las guardas no impiden cambios administrativos
de visibilidad fuera del workflow.

Antes de activar los workflows nuevos, configure **Settings > Environments > distribution**
con revisores obligatorios y refs de branch predeterminada/`v*`, conforme al contrato
repository-policy. No necesita secrets. Aplique política por interfaz o
`groundwork github --sync` sólo con autorización administrativa explícita. No hay
fallback a ambiente sin revisores: si el plan de GitHub no soporta la protección,
la publicación queda bloqueada. Un ambiente creado automáticamente sin aprobación
registrada no permite enviar paquetes. Actualizar localmente el catálogo no configura
GitHub ni activa gates remotos.

### Alternativa explícita para repositorio privado

Para estables privadas sin soporte de revisores de Environment en su plan, existe
el opt-in `distributionApproval.mode = "manual-dispatch"` en `.github/ci-cd.json`.
El parámetro de renderización es `distribution_approval_mode`; `environment` sigue
siendo el valor predeterminado de nuevos consumidores. Groundwork selecciona la
alternativa explícitamente en configuración y parámetro versionados. No hay
detección automática del plan ni fallback después de HTTP 422.

El run usa `distribution-manual`, separado del ambiente nativo. Ese ambiente no
representa aprobación: la evidencia es el dispatch autenticado de un administrador
humano, con título inmutable `Authorize distribution | <tag> | <SHA>`. La guarda
consulta la API y verifica `workflow_dispatch`, `gw-release.yml`, ref permitida,
SHA, actor humano y permiso administrativo actual. Falta de confirmación, bot,
permiso sólo de escritura, otro actor en reejecución, tag/SHA incorrectos o API
inconclusa bloquean. La tag se revisa antes de publicar. HEAD actual, CI/Result,
dependencias, versiones, draft y equivalencia RC siguen obligatorios cuando aplican.

Desde la raíz del repositorio privado, con GitHub CLI autenticado como administrador,
después de integrar Release PR y esperar CI verde, defina `SHA_APROVADO` con el SHA
completo del merge. Sustituya la versión ficticia `v1.2.3` por la integrada:

```bash
gh workflow run gw-release.yml --ref main -f mode=release \
	-f confirm_distribution=true -f authorize_tag=v1.2.3 \
	-f authorize_commit="$SHA_APROVADO"
```

El comando autoriza publicar y puede crear tag/borrador y enviar paquetes; no lo
ejecute sin revisar versión y fuente. El push del merge no publica solo: falta de
confirmación rechaza antes de crear tag. Reejecutar un evento push no lo convierte
en aprobación manual. Otro administrador exige nuevo dispatch. Republish de tag
estable ya pública que contenga este soporte usa `mode=republish` en esa tag con
la misma confirmación explícita, conservando la fuente histórica.

La alternativa no aplica a repositorios públicos, RCs ni previews manuales; esos
flujos mantienen aprobación nativa. Tags/runs anteriores sin soporte no se modifican:
preserve borradores/artefactos y prepare una versión nueva con la corrección; nunca
mueva tags ni libere código antiguo. Administradores capaces de editar workflows
siguen dentro de la frontera de confianza; revisión de branch protegida y logs de
GitHub son parte de la auditoría.

## Integraciones y fixes

Un PR común, incluido fix, no exige aceptación de RC ni se bloquea por otros PRs
abiertos. Para integraciones específicas obligatorias, el operador registra en
la configuración versionada `.github/ci-cd.json`, mediante PR revisado:

```json
"releaseDependencies": [123, 456]
```

El valor predeterminado es `[]`; incluya sólo integraciones obligatorias del alcance
de release. Retire dependencias satisfechas sólo al revisar el alcance siguiente.
La lista versionada sobrevive a regenerar notas. El cuerpo del Release PR puede
complementarla sin editar su branch generada:

```text
Release-Depends-On: #123, #456
```

Las dos listas se unifican y limitan al mismo repositorio. Cada PR debe tener merge en la branch
predeterminada e inclusión del SHA resultante; squash usa `merge_commit_sha`.
Cerrar sin merge no satisface. Se relee la lista tras aprobar y en Preview gate.
Release-please puede regenerar el cuerpo: verifique/preserve el marcador al
actualizar el Release PR. No se infieren dependencias no declaradas de PRs abiertos.
Use la configuración versionada como registro duradero. Eliminar una dependencia
obligatoria cambia el alcance de release y exige revisión
humana; nunca debe ser recuperación automática.

Tras integrar código nuevo, actualice el Release PR y genere/acepte `rc.N+1`.
`Preview-Waiver` sólo dispensa aceptación de candidata con motivo auditable; nunca
permite SHA antiguo, CI fallido ni dependencia obligatoria ausente.

## Recuperación y migración

Señales: fuente desactualizada, CI/integración pendiente o diferencias RC/final.
Espere integración/CI, actualice Release PR y genere una ejecución/candidata nueva.
Nunca mueva tags existentes ni sobrescriba assets. Si el CI de main seguía en
ejecución cuando el push disparó release, retome cuando esté verde mediante CLI
autenticada desde la raíz del repositorio:

```bash
gh workflow run gw-release.yml --ref main -f mode=release
```

Sustituya `main` por la branch predeterminada. El comando puede crear tags y releases
y exige autorización operacional explícita. Borradores interrumpidos requieren
fuente revalidada; no son republish histórico. Escale al mantenedor si main avanzó
después de un Release PR integrado aún pendiente de tag.

Republish sólo tiene excepción de HEAD para tag estable inmutable de release
**ya pública**, checkout correspondiente y artefactos ausentes. Antes de subir
assets a GitHub Release, el publicador exige una entrada correspondiente en el
manifiesto inmutable y un digest reconstruido idéntico; rechaza assets no listados
o divergentes antes de que queden huérfanos en la release. Redeploy/rollback
conservan tags históricas y políticas de deploy/`rollbackMinimumVersion`.

Actualice consumidores con `groundwork update --dry-run`, revise y aplique
`groundwork update` con catálogo local/distribuido autorizado. Las semillas no
se migran automáticamente. En `ci.yml`, conceda `pull-requests`, `actions` y
`checks: read` al llamador de higiene; snapshot depende de `result`, sin entrar
en los `needs` de Result. En `publish.yml`, propague permisos y `republish` a todos
los publicadores y llamadas anidadas. En `release-please-config.json`, agregue
`"always-update": true`: sin esto, un commit que no cambia las notas (`ci`, `docs`,
`chore`) deja el Release PR detrás de la rama predeterminada y `gw-preview.yml`
rechaza generar la candidata. Preserve personalizaciones; nunca cambie
hashes manualmente para ocultar desviaciones.

Las correcciones locales del catálogo aún no son una versión distribuida: integre
y publique el catálogo por el proceso autorizado, después actualice/integre los
consumidores. No reaplique un catálogo antiguo sin estas guardas sobre el conjunto
local validado. Los tests locales no activan cambios de workflows remotos.

## Seguridad y límites

El publicador NuGet se ejecuta como root en container Linux, mientras el checkout
puede pertenecer a otro UID. Después del checkout y antes de leer el SHA, la etapa
`Trust checked-out workspace` registra sólo `$GITHUB_WORKSPACE` en `safe.directory`
del Git del container. No use `safe.directory=*`: otro repositorio con ownership
diferente debe seguir bloqueado. La configuración es efímera; no cambia el Git
del desarrollador ni omite las guardas de fuente/CI.

La imagen SDK no necesita incluir GitHub CLI. En `ubuntu-24.04`, el publicador
monta sólo `/usr/bin/gh` del runner en `/usr/local/bin/gh:ro`; no monta HOME,
credenciales ni socket Docker. El token de permisos mínimos viene del proceso
del workflow. Runners alternativos deben proporcionar ese binario en esa ruta.
`NuGetFeedTests` reproduce ownership diferente con Git real, verifica el orden de
etapas e impide ampliar la confianza/mount a otros directorios.

Amenazas cubiertas: aprobación de SHA antiguo, CI de PR/otro SHA/app/suite,
integración omitida, diferencias RC/final, API inconclusa y borrador usado como
republish. Las guardas consultan con lectura y rechazan antes de mutaciones;
no imprimen tokens. Los revisores protegen metadatos, configuración y dependencias.

GitHub no ofrece transacción atómica entre HEAD, checks y creación/upload de tags.
La revalidación reduce la ventana, pero no bloquea merges concurrentes en el
servidor. Tags/artefactos siguen inmutables; cambios detectados posteriormente
dejan el borrador bloqueado, sin promoción silenciosa. Un paquete histórico público
no se invalida sólo porque main avance. Tests locales con API simulada no verifican
permisos/protecciones reales en GitHub ni aceptación gráfica humana.

Valide `ReleasePreviewTests`, contratos y workflows renderizados con actionlint
en containers. Casos esenciales: avance de main durante aprobación/consulta,
CI errado/pendiente/fallido, dependencia squash/ausente, diferencias RC/final,
waiver y republish histórico.