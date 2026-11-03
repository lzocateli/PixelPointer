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
Tras aprobar `release`, se repite la resolución con cabeza del Release PR y tag
esperadas antes de crear tag/borrador. La publicación de assets/paquetes valida
la fuente tras aprobar, y la finalización revalida antes de hacer pública la
GitHub Release. Los workflows nunca deben eliminar estas guardas.

Preview gate exige cabeza actual del Release PR, dependencias integradas, base
actual y CI verde antes de comprobar aceptación o waiver. La release final
integrada pendiente se valida **antes** de crear la tag por release-please: el
commit de merge debe ser la fuente actual y su padre la candidata aceptada de
esa versión. El diff sólo permite `versionFiles` y archivos configurados de
release-please. Los SHAs RC/final difieren por el bump/merge; los archivos permitidos
no autorizan cambios funcionales disfrazados de metadatos y requieren revisión humana.

Al actualizar sólo el Release PR, release-please usa `skip-github-release: true`;
sólo la salida `release_ready=true` de la guarda autoriza crear release/tag.

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
**ya pública**, checkout correspondiente y artefactos ausentes. Redeploy/rollback
conservan tags históricas y políticas de deploy/`rollbackMinimumVersion`.

Actualice consumidores con `groundwork update --dry-run`, revise y aplique
`groundwork update` con catálogo local/distribuido autorizado. Las semillas no
se migran automáticamente. En `ci.yml`, conceda `pull-requests`, `actions` y
`checks: read` al llamador de higiene; snapshot depende de `result`, sin entrar
en los `needs` de Result. En `publish.yml`, propague permisos y `republish` a todos
los publicadores y llamadas anidadas. Preserve personalizaciones; nunca cambie
hashes manualmente para ocultar desviaciones.

Las correcciones locales del catálogo aún no son una versión distribuida: integre
y publique el catálogo por el proceso autorizado, después actualice/integre los
consumidores. No reaplique un catálogo antiguo sin estas guardas sobre el conjunto
local validado. Los tests locales no activan cambios de workflows remotos.

## Seguridad y límites

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