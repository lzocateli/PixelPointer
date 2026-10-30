# Runbook: release

[Português (Brasil)](../../runbooks/release.md) · [English](../../en/runbooks/release.md) · **Español**

Archivo administrado por Altivy.Groundwork (componente `release`). No edites la
fuente en portugués; los cambios a ese runbook se tramitan mediante el catálogo.
Los procedimientos específicos del proyecto deben estar fuera de este archivo.

Repositorio: `lzocateli/PixelPointer` · rama predeterminada: `main`.

## 1. Integrar no es publicar una versión

El título del PR (squash) decide si el cambio **cuenta** para la siguiente versión;
una persona decide **cuándo** se publica, al integrar el Release PR.

| Tipo del título | Publicable | Incremento |
| --- | --- | --- |
| `feat` | sí | minor |
| `fix`, `fix(deps)`, `deps` | sí | patch |
| `perf`, `revert` | sí | patch |
| Cualquier tipo con `!` o pie `BREAKING CHANGE:` | sí | major (antes de 1.0: minor) |
| `docs`, `chore`, `ci`, `build`, `test`, `refactor`, `style` | no | — |

- Los commits publicables solo crean o actualizan el Release PR; integrar un PR
  normal no crea una etiqueta, release, publicación ni despliegue.
- El job `Conventional title` de `gw-pr-hygiene.yml` muestra en el resumen del PR
  si el cambio es publicable y qué incremento causará. Comprueba el tipo antes de
  aprobar: un cambio de comportamiento clasificado como `refactor` debería ser
  `fix` o `feat`.
- Las snapshots `X.Y.Z-preview.N` de la rama predeterminada (si están habilitadas)
  no son releases: no crean etiqueta ni GitHub Release y nunca se aceptan para deploy.

## 2. Publicar una versión

1. Busca el Release PR `chore(main): release X.Y.Z` creado por la GitHub App:

   ```bash
   gh pr list --repo lzocateli/PixelPointer --head release-please--branches--main
   ```

2. Revisa `CHANGELOG.md` y la versión; espera el check `Result`.
3. Integra (squash). El workflow `gw-release.yml`:
   - crea la etiqueta `vX.Y.Z` y una GitHub Release en borrador;
   - llama a `publish.yml` en el Environment `release` (aprueba cuando se solicite);
   - genera, atesta y adjunta `release-manifest.json`;
   - publica la GitHub Release solo si todos los pasos anteriores tuvieron éxito;
   - llama a `post-release.yml` con la release ya pública (distribución del proyecto, si está configurada).
4. Confirma:

   ```bash
   gh release view vX.Y.Z --repo lzocateli/PixelPointer
   gh attestation verify release-manifest.json --repo lzocateli/PixelPointer
   ```

## 3. Publicar sin un commit que habilite release

Para forzar una versión (por ejemplo, por motivos operativos), integra un PR
cuyo commit tenga el pie siguiente. Esto abre un Release PR, que aún requiere
integración humana.

```text
chore: solicitar una release operativa

Release-As: X.Y.Z
```

## 4. Volver a publicar artefactos ausentes

Si la publicación falla parcialmente, la release permanece en borrador. Después
de corregir la causa (credenciales o registro no disponible), vuelve a ejecutarla
en la **etiqueta**:

```bash
gh workflow run gw-release.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f mode=republish
```

`republish` valida la etiqueta, vuelve a ejecutar `publish.yml` de forma
idempotente (solo carga lo que falta y nunca sobrescribe artefactos publicados),
finaliza la release y ejecuta `post-release.yml` otra vez con `republish=true`. Si
`release-manifest.json` ya existe, no se modifica: cualquier divergencia de
resumen criptográfico hace fallar el workflow.

## 5. Corregir una release defectuosa

Nunca borres ni muevas etiquetas, nunca elimines GitHub Releases y nunca
recompiles artefactos de una versión publicada.

1. Abre una incidencia de corrección (o usa el flujo de errores del proyecto) e
   integra un PR `fix:` o `revert:`; ambos habilitan release y aumentan el patch.
2. Integra el Release PR resultante para publicar la versión corregida.
3. Señala la versión defectuosa cuando corresponda:
   - GitHub Release: edita las notas con una advertencia y el enlace a la versión
     corregida (`gh release edit vX.Y.Z --notes-file notes.md`);
   - NuGet.org: marca la versión como *deprecated* en el portal e indica su sustituta;
   - imágenes: conserva la etiqueta publicada y comunica la versión corregida.

## 6. Fallos comunes

| Síntoma | Causa probable | Acción |
| --- | --- | --- |
| Falla `Require release GitHub App` | Falta `vars.RELEASE_APP_ID` o `RELEASE_APP_PRIVATE_KEY` | Configúralos manualmente; nunca uses un PAT personal |
| La ejecución `Release` tiene `startup_failure` y ningún job | La política de Actions del repositorio (o la organización) bloquea `googleapis/release-please-action` | Ejecuta `groundwork github --sync` para permitir el SHA en `allowedActions`; si la organización lo restringe, habilítalo allí |
| El Release PR no tiene checks | Se creó con `GITHUB_TOKEN` | Confirma que la App esté instalada; el token de la App activa los checks |
| Se rechaza la etiqueta | El ruleset `gw-release-tags` no incluye la App en bypass | Ejecuta `groundwork github` y revisa `release_app_id` |
| Falla `Assert-ReleaseTag.ps1` | La versión difiere entre la etiqueta y los archivos de versión | Corrígelo en el Release PR; no crees etiquetas manualmente |
| La release sigue en borrador | Falló la publicación o el manifiesto | Corrige la causa y usa `republish` |
| `Failed to persist attestation: Feature not available` | El plan no ofrece atestaciones para un repositorio privado | Define `"attestations": false` en `.github/ci-cd.json` (consulta `.github/CI-CD.md`); esas releases no se pueden desplegar |
| El Release PR está bloqueado por `Result` fallido mientras otra ejecución está verde | Se canceló una ejecución de CI en el mismo commit; el ruleset considera todas las ejecuciones del check | Vuelve a ejecutar la ejecución cancelada (`gh run rerun <id>`); el `ci.yml` actual no cancela ejecuciones de PR |
