# Runbook: deploy, redeploy y rollback

[Português (Brasil)](../../runbooks/deploy-e-rollback.md) · [English](../../en/runbooks/deploy-e-rollback.md) · **Español**

Traducción del archivo gestionado por el Altivy.Groundwork (componente `deploy`); la versión en
portugués es la normativa. Destinos, runner, migraciones y contactos están en
[deploy-projeto.md](deploy-projeto.md).

Repositorio: `lzocateli/PixelPointer` · Environments de deploy: `quality`, `production` ·
auto-deploy: `quality` · revisores: `@lzocateli`.

## 1. Principios

- Deploy solo de una GitHub Release **publicada**: `gw-deploy.yml` se ejecuta en la etiqueta
  `vX.Y.Z` y el guard `Assert-DeployableRelease.ps1` rechaza ramas, borradores, etiquetas fuera de
  `main`, manifiestos ausentes o sin atestación y prereleases en `production`.
- Una prerelease solo se puede desplegar si es una candidata `vX.Y.Z-rc.N` y solo en el
  Environment de preview (`deploy.previewEnvironment`, `quality` por defecto); el éxito allí
  registra la aceptación de la candidata.
- Con `preview.requiredForRelease`, `production` exige la candidata validada de la misma versión,
  sin código distinto de ella, o un commit con `Preview-Waiver:` desde la estable anterior (vea
  [../estrategia-de-release.md](../estrategia-de-release.md)).
- Los Environments de deploy solo aceptan refs `tag:v*`; el propio GitHub rechaza un job de deploy
  en una rama.
- Los artefactos son inmutables y se identifican por digest en el `release-manifest.json`; el
  deploy nunca recompila nada.
- El rollback es el deploy de una release anterior ya publicada. Nunca borre ni mueva etiquetas,
  nunca elimine GitHub Releases y nunca recompile artefactos.
- Los deploys en el mismo Environment no se superponen (`concurrency: deploy-<environment>`).

## 2. Deploy de una nueva release

1. Después del merge del Release PR, `gw-release.yml` publica la release.
2. El job `Auto deploy` inicia automáticamente el deploy en `quality` en la etiqueta. Apruébelo en
   el Environment y siga el smoke test.
3. La promoción a `production` es siempre manual, en la **misma etiqueta** validada, con
   aprobación:

   ```bash
   gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f environment=production
   ```

   En la interfaz: Actions → Deploy → Run workflow → *Use workflow from: Tags / vX.Y.Z*.

## 3. Redeploy de la misma release

Úselo cuando el host se recreó, hubo un fallo transitorio o hay que volver a aplicar la
configuración.

```bash
gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f environment=<environment>
```

El guard valida la release, el Environment pide aprobación y `deploy.yml` aplica los **mismos
digests**. Verifique el smoke test y la versión en ejecución. Corregir el procedimiento de deploy
de una versión exige una nueva release: ejecutar en la etiqueta usa el `gw-deploy.yml` y el
`deploy.yml` de esa release.

## 4. Rollback

1. **Decida entre rollback y roll-forward.** Prefiera el rollback cuando el defecto es grave, la
   causa no está clara y la versión anterior es compatible con los datos actuales. Prefiera el
   roll-forward cuando la corrección es simple o la versión anterior no lee los datos o el
   protocolo actuales.
2. **Identifique la versión actual y la de destino:**

   ```bash
   gh api "repos/lzocateli/PixelPointer/deployments?environment=<environment>" --jq '.[0:5][] | {ref, created_at, creator: .creator.login}'
   pwsh ./tools/scripts/ci/Get-DeployedRelease.ps1 -Environment <environment> -Repository lzocateli/PixelPointer
   gh release list --repo lzocateli/PixelPointer --exclude-drafts
   ```

3. **Revise el bloqueo `rollbackMinimumVersion`** en el manifiesto de la versión actual:

   ```bash
   pwsh ./tools/scripts/ci/Get-ReleaseManifest.ps1 -Tag v<actual> -Repository lzocateli/PixelPointer -OutputPath /tmp/release-manifest.json
   jq -r .rollbackMinimumVersion /tmp/release-manifest.json
   ```

   La versión de destino no puede ser menor que ese valor.
4. **Ejecútelo en la etiqueta anterior:**

   ```bash
   gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref v<anterior> -f environment=<environment>
   ```

5. **Apruebe** en el Environment, siga el smoke test y confirme la versión en ejecución.
6. **Comunique** el rollback, registre el motivo (el historial de deployments registra quién,
   cuándo y qué etiqueta) y abra la corrección como Issue o por el flujo de bugs del proyecto.
7. **Vuelva a la versión corregida con una nueva release** (`fix:` o `revert:` → Release PR →
   deploy). Nunca vuelva a desplegar la release defectuosa.

## 5. Cuando el rollback es rechazado

El guard rechaza destinos por debajo del `rollbackMinimumVersion` registrado en el manifiesto de la
versión desplegada. Eso significa que la versión actual hizo un cambio sin vuelta atrás (migración
*contract*, eliminación del protocolo N-1, formato de datos incompatible) y que la versión anterior
no funcionaría con el estado actual. El workflow no tiene bypass: haga **roll-forward**
(corrección → Release PR → nueva release → deploy). Si el bloqueo es incorrecto, corrija
`rollbackMinimumVersion` en `.github/ci-cd.json` y publique una nueva release.

## 6. Fallos durante el deploy

| Situación | Acción |
| --- | --- |
| Fallo transitorio en el deploy | Vuelva a ejecutar el mismo comando en la misma etiqueta; `deploy.yml` es idempotente |
| La aprobación expiró | Inícielo de nuevo en la etiqueta y apruebe dentro del plazo |
| El smoke test falló | Evalúe rollback (sección 4) o redeploy (sección 3); no cambie el entorno a mano |
| Deploy concurrente bloqueado | Espere a que termine el deploy en curso; no lo cancele a mitad de la aplicación |
| El guard rechazó | Lea el motivo en el resumen del job `Guard`; ramas, borradores, prereleases en `production` y releases sin atestación (`"attestation": "none"`) se rechazan por diseño |

## 7. Republicación de artefactos ausentes

Republicar (`gw-release.yml` con `mode=republish` en la etiqueta) completa una **publicación**
parcial: sube solo los artefactos que faltan y finaliza la release. El redeploy (sección 3) vuelve
a aplicar una release ya publicada en un Environment. Una release en borrador no se puede
desplegar hasta que la republicación termine.

```bash
gh workflow run gw-release.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f mode=republish
```

## 8. Auditoría

- Historial de deployments por Environment (quién, cuándo, qué etiqueta):
  `gh api "repos/lzocateli/PixelPointer/deployments?environment=<environment>"`.
- Logs y resúmenes del workflow `Deploy` (el job `Guard` registra deploy, redeploy o rollback).
- `release-manifest.json` atestado de cada release:
  `gh attestation verify release-manifest.json --repo lzocateli/PixelPointer`.

## 9. Lista rápida de rollback

- [ ] Incidente registrado y responsable definido.
- [ ] Versión actual y de destino identificadas (`Get-DeployedRelease.ps1`, `gh release list`).
- [ ] Destino mayor o igual al `rollbackMinimumVersion` de la versión actual.
- [ ] `gh workflow run gw-deploy.yml --ref v<anterior> -f environment=<environment>` iniciado.
- [ ] Aprobación concedida en el Environment y smoke test en verde.
- [ ] Versión en ejecución confirmada.
- [ ] Comunicación enviada y corrección abierta.
- [ ] Corrección publicada en una nueva release, sin volver a desplegar la versión defectuosa.
