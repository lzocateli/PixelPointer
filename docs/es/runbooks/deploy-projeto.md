# Deploy del PixelPointer

[Português (Brasil)](../../runbooks/deploy-projeto.md) · [English](../../en/runbooks/deploy-projeto.md) · **Español**

Semilla del Altivy.Groundwork (componente `deploy`): este archivo pertenece al proyecto
y complementa el procedimiento estándar de [deploy-e-rollback.md](deploy-e-rollback.md).

PixelPointer es una aplicación distribuida por GitHub Releases, Homebrew y winget, sin
servidor. Por eso, el "deploy" no instala nada en un host: comprueba que la release
publicada es exactamente la del manifiesto atestado y ejecuta el paquete publicado. Es
la aceptación de la candidata `vX.Y.Z-rc.N` en `quality`
([release-preview](release-preview.md)).

## Destinos

| Environment | Destino | Runner | Revisores |
| --- | --- | --- | --- |
| `quality` | Assets de la GitHub Release (candidata o estable) y máquinas de prueba del mantenedor | `ubuntu-24.04` de GitHub | `@lzocateli` (obligatorio: aceptación `approval`) |
| `production` | Assets de la GitHub Release estable; Homebrew y winget requieren dispatch independiente con versión explícita | `ubuntu-24.04` de GitHub | `@lzocateli` |

## Ejecución

`.github/workflows/deploy.yml`, llamado por `gw-deploy.yml` después del guard:

1. **Aprobación del Environment.** En `quality`, apruebe solo después de probar la
   candidata en las máquinas de prueba ([proceso de release](../release.md)).
2. **`Apply release`:** descarga todos los assets de la etiqueta y compara el SHA-256
   de cada uno con el `release-manifest.json` validado y atestado. No modifica nada;
   repetirlo da el mismo resultado. Usa solo el `GITHUB_TOKEN` con `contents: read`; no
   hay credencial de Environment.
3. **`Smoke test`:** ejecuta `tools/scripts/Test-AotPackage.ps1 -Rid linux-x64` sobre
   el paquete **publicado** (no un build nuevo), en contenedores limpios sin .NET:
   `--version` con la versión de la release, `--help`, `selftest`, SBOM y `.deb`.

Si termina bien en `quality` para una candidata, `gw-deploy.yml` adjunta
`preview-acceptance.json` a la prerelease y vuelve a ejecutar el CI del Release PR.

## Migraciones y compatibilidad

No hay migraciones de datos ni de protocolo entre versiones; `rollbackMinimumVersion`
sigue en `0.0.0`. El rollback consiste en instalar una release anterior publicada.

## Smoke test y verificaciones después del deploy

El smoke automatizado cubre solo `linux-x64`. Windows, macOS, Wayland y el uso real
(zoom, anotación, grabación y atajos globales) los verifica a mano el mantenedor antes
de aprobar `quality`, según [uso](../uso.md).

## Contactos

Aprobación, operación e incidentes: `@lzocateli`.
