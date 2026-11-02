# Runbook: release preview

[Português (Brasil)](../../runbooks/release-preview.md) · [English](../../en/runbooks/release-preview.md) · **Español**

Traducción del archivo gestionado por el Altivy.Groundwork (componente `release-preview`); la
versión en portugués es la normativa. La estrategia completa está en
[estrategia-de-release](../estrategia-de-release.md).

Repositorio: `lzocateli/PixelPointer` · rama predeterminada: `main` · aceptación: `approval`.

## 1. Generar la candidata

1. Confirme que el Release PR está abierto y actualizado:

   ```bash
   gh pr list --repo lzocateli/PixelPointer --head release-please--branches--main
   ```

2. Inicie el workflow en la rama predeterminada:

   ```bash
   gh workflow run gw-preview.yml --repo lzocateli/PixelPointer --ref main
   ```

3. Apruebe el Environment `release` cuando se le solicite. El workflow calcula `vX.Y.Z-rc.N`, crea
   la etiqueta con la GitHub App, llama a `publish.yml`, atesta el `release-manifest.json` y publica
   la prerelease.
4. Compruébelo:

   ```bash
   gh release view vX.Y.Z-rc.N --repo lzocateli/PixelPointer
   ```

## 2. Validar en quality

El job `Dispatch preview environment` de `gw-preview.yml` inicia `gw-deploy.yml` en la etiqueta de
la candidata cuando `deploy.autoDeployEnvironment` es `quality` o la aceptación es `approval`. Si
no, inícielo usted:

```bash
gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z-rc.N -f environment=quality
```

Pruebe la candidata y **apruebe el Environment** solo cuando pueda convertirse en la versión
estable.

Si termina bien, el job `Record candidate acceptance` adjunta `preview-acceptance.json` a la
prerelease y vuelve a ejecutar el CI del Release PR.

## 3. Promover

1. Revise el check `Preview gate` del Release PR; muestra la candidata validada.
2. Integre el Release PR (squash) y siga el [runbook de release](release.md).
3. Despliegue en `production` con la etiqueta estable:

   ```bash
   gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f environment=production
   ```

   El guard comprueba la candidata validada y el diff entre ella y la versión estable.

## 4. Fallos comunes

| Síntoma | Causa probable | Acción |
| --- | --- | --- |
| `Nenhum Release PR aberto` (no hay Release PR abierto) | no hay commit releasable desde la última versión estable | integre un `feat`/`fix` o use `Release-As:` |
| `O Release PR #N não está sobre <sha>` (el Release PR no está sobre el commit) | release-please todavía no actualizó el Release PR | espere `gw-release.yml` en `main` y vuelva a iniciarlo |
| `Preview gate` en rojo después de la aceptación | `main` avanzó y el Release PR cambió de commit base | genere una nueva candidata (`rc.N+1`) y valídela |
| `Preview gate` en rojo sin nueva integración | el CI del Release PR no se volvió a ejecutar | `gh run rerun <id>` en la ejecución del CI del Release PR |
| Deploy de la candidata rechazado | Environment distinto de `quality` o etiqueta fuera del patrón `-rc.N` | use `quality`; las prereleases `preview.N` nunca se pueden desplegar |
| `production` rechazado por el diff | código integrado después de la candidata | nueva versión con nueva candidata |
| Emergencia sin `quality` disponible | — | commit con el pie `Preview-Waiver: <motivo>` desde la última versión estable |
