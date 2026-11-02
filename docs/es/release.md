# Proceso de release

[Português (Brasil)](../release.md) · [English](../en/release.md) · **Español**

El proceso común de release (Release PR, versión, etiqueta, `release-manifest.json`,
republicación y corrección de una release defectuosa) lo administra
Altivy.Groundwork y está descrito en el [runbook de release](runbooks/release.md)
y en la [referencia CI/CD (portugués)](../../.github/CI-CD.md). Este documento
cubre los detalles específicos de PixelPointer: [`publish.yml`](../../.github/workflows/publish.yml)
publica paquetes Native AOT para todos los RIDs compatibles y
[`post-release.yml`](../../.github/workflows/post-release.yml) distribuye por
Homebrew y winget. Cada etapa tiene un equivalente local en `tools/scripts/`.

## Preview obligatorio

Cada Release PR `release-please--branches--main` ejecuta `release-preview` dentro
del check obligatorio `Result`. El job crea paquetes `preview.<run_number>` para
los seis RIDs, SBOM, archivos `.deb` Linux y `SHA256SUMS`, ejecuta los smokes de
paquetes y las pruebas de instaladores, y sube artifacts
`pixelpointer-preview-<run_id>-<run_attempt>` durante siete días. El artifact no
incluye `install.sh`/`install.ps1`, no crea etiqueta ni GitHub Release y no ejecuta
`post-release.yml`. Los rulesets impiden integrar el Release PR si la preview
obligatoria falla o sigue pendiente.

Cuando pase el check, abre en Actions la ejecución `CI` del Release PR y descarga
el artifact `pixelpointer-preview-<run_id>-<run_attempt>`; contiene
`pixelpointer-linux-x64.tar.gz`, `.deb`, SBOM y checksums. La retención es de siete días.

También se puede iniciar manualmente una preview de `main`, sin Release PR:

```bash
gh workflow run publish.yml --repo lzocateli/PixelPointer --ref main -f preview=true
gh run list --repo lzocateli/PixelPointer --workflow publish.yml
```

Después del build, smokes de paquetes, pruebas de instaladores y atestaciones
de SBOM, `Publish GitHub Pre-release` espera tu aprobación en el Environment
`release`. El GitHub App `Altivy Release` crea `vX.Y.Z-preview.N` en el commit
validado, prepara una release en borrador, atesta la procedencia y adjunta los
14 assets más `SHA256SUMS`. Luego publica con `prerelease=true` y
`make_latest=false`. La versión aparece en
[Releases](https://github.com/lzocateli/PixelPointer/releases) como **Pre-release**,
con descargas públicas sin login; no caduca tras siete días como los artifacts
de Actions. La estable sigue siendo `Latest`.

Esta excepción del canal preview fue autorizada por el mantenedor el 2026-10-31.
No cambia el flujo estable de Groundwork, no ejecuta `post-release.yml` ni
actualiza Homebrew/winget. La preview obligatoria de PR sigue siendo solo un
artifact, sin secrets ni publicación. La preview manual usa el siguiente patch
de la versión registrada y `preview.<run_number>`; el prefijo se aplica solo
en los checkouts de build y smoke, sin editar la versión en Git. Esto no implica
que release-please elija esa versión como próxima release estable.

En la otra máquina, descarga el paquete del RID y `SHA256SUMS`, verifica SHA-256
y extrae el `.zip`/`.tar.gz` manteniendo las bibliotecas nativas junto al ejecutable,
o instala el `.deb` Linux. Ejecuta `pixelpointer --version` para verificar el
sufijo. Las previews no incluyen `install.sh`/`install.ps1`, no se sirven mediante
`releases/latest` y no son aceptadas por el deploy de Groundwork. Su aprobación
no reemplaza la integración humana del Release PR ni la aprobación de producción.

### Usar un paquete preview

Abre [Releases](https://github.com/lzocateli/PixelPointer/releases) y elige una
versión marcada **Pre-release**, no `releases/latest`. Los ejemplos usan
`0.1.3-preview.14`; sustituye `version` por la versión elegida sin el `v` inicial.
No necesitas .NET ni clonar el repositorio. Las previews no incluyen instaladores;
Homebrew y winget siguen apuntando a versiones estables.

Elige el RID de la máquina que ejecutará la aplicación, no de la de descarga:

| Máquina | RID | Paquete portátil |
| --- | --- | --- |
| Linux Intel/AMD 64 bits (`uname -m`: `x86_64`) | `linux-x64` | `.tar.gz` |
| Linux ARM 64 bits (`uname -m`: `aarch64`) | `linux-arm64` | `.tar.gz` |
| Windows Intel/AMD 64 bits | `win-x64` | `.zip` |
| Windows ARM 64 bits | `win-arm64` | `.zip` |

Si la estable está ejecutándose, detenla primero con `pixelpointer quit`: ambas
versiones comparten identidad e IPC y un comando de preview podría llegar al
daemon anterior. Usa siempre la ruta del ejecutable extraído, no un
`pixelpointer` anterior encontrado en `PATH`. Los ejemplos crean una carpeta
nueva sin sustituir la estable ni modificar perfiles, registro o `PATH`.
El `.deb` es otra opción en Debian/Ubuntu, pero instala el mismo paquete de
sistema y puede sustituir la estable; prefiere `.tar.gz` para probar los binarios
por separado. Configuraciones y permisos del usuario siguen compartidos;
no es una sandbox.

#### Linux con Wayland

Ejecuta en la máquina de destino, en una terminal de la sesión gráfica, no por
SSH. `echo "$XDG_SESSION_TYPE"` debe mostrar `wayland`. Se necesitan glibc 2.34+,
Fontconfig, libstdc++, libX11 y portales XDG con el backend del escritorio;
ScreenCast también usa `libpipewire-0.3` del sistema. No debes instalar SDL3/Skia
por separado: están incluidas en el paquete.

Descarga y verifica en una carpeta nueva dentro de tu carpeta personal.
Para ARM, cambia `rid` por `linux-arm64`:

```bash
version="0.1.3-preview.14"
rid="linux-x64"
base="https://github.com/lzocateli/PixelPointer/releases/download/v$version"
workdir="$(mktemp -d "$HOME/pixelpointer-preview.XXXXXX")" &&
cd "$workdir" &&
curl -fSLO "$base/pixelpointer-$rid.tar.gz" &&
curl -fSLO "$base/SHA256SUMS" &&
sha256sum --check --ignore-missing SHA256SUMS &&
tar -xzf "pixelpointer-$rid.tar.gz"
```

**Continúa solo si la descarga y verificación terminan correctamente con `OK`.**
Si fallan, no ejecutes el paquete. Mantén las bibliotecas junto al ejecutable:

```bash
./pixelpointer --version
./pixelpointer selftest
./pixelpointer probe
```

La versión debe incluir el sufijo elegido y `selftest` debe pasar; no comprueba
captura, atajos ni interfaz gráfica. Si la aplicación aún no está registrada
en el escritorio, ejecuta `./pixelpointer install-desktop-file`. No fuerces
la sustitución de una entrada existente: los conflictos se conservan. Esta
etapa registra la aplicación en el menú sin sustituir sus binarios.

Si el registro muestra `App info not found` seguido de `An app id is required`,
el portal no encontró la identidad de la aplicación; no indica un checksum
inválido. Ejecuta `./pixelpointer install-desktop-file` desde esta carpeta y
reinicia el daemon solo cuando el registro termine sin conflictos. Registrar la
identidad no concede permiso de captura. El daemon trata rechazos D-Bus de los
atajos con un aviso y mantiene el IPC; previews anteriores a esta corrección
pueden terminar con una excepción. Sin atajos globales, usa `zoom`/`draw`;
la captura sigue requiriendo consentimiento del portal.

En GNOME, ejecuta `./pixelpointer grant`, pulsa Enter en la ventana y autoriza
la captura. Luego prueba la captura e inicia el daemon en primer plano para
observar posibles errores:

```bash
./pixelpointer capture --out preview.png
./pixelpointer daemon
```

Elige y autoriza el monitor en la solicitud del portal. Fuera de GNOME, con un
monitor, ScreenCast/PipeWire lee un cuadro y cierra la sesión; el indicador de
compartir es transitorio. En GNOME la captura usa el portal Screenshot, sin
indicador. Cancelar el diálogo cancela la operación. En GNOME anterior
al 48 y escritorios sin GlobalShortcuts, configura atajos del escritorio con
la ruta absoluta de esta preview y `zoom`/`draw`. Detalles:
[permisos Wayland](uso.md). En Wayland se dibuja sobre una captura congelada,
no sobre contenido en vivo. Prueba primero con un monitor; multi-monitor y otros
escritorios tienen límites y no heredan automáticamente la aceptación de una
máquina probada.

#### Windows

Ejecuta en Windows PowerShell 5.1+ o PowerShell 7 de la máquina de destino.
Para ARM, cambia `rid` por `win-arm64`. El ejemplo crea una carpeta nueva en
`LOCALAPPDATA`, verifica exactamente la entrada ZIP de `SHA256SUMS` y después extrae:

```powershell
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
$version = '0.1.3-preview.14'
$rid = 'win-x64'
$base = "https://github.com/lzocateli/PixelPointer/releases/download/v$version"
$dir = Join-Path $env:LOCALAPPDATA ("PixelPointerPreview\" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $dir | Out-Null
$zip = "pixelpointer-$rid.zip"
Invoke-WebRequest "$base/$zip" -UseBasicParsing -OutFile "$dir\$zip"
Invoke-WebRequest "$base/SHA256SUMS" -UseBasicParsing -OutFile "$dir\SHA256SUMS"
$pattern = '^[0-9a-fA-F]{64}  ' + [regex]::Escape($zip) + '$'
$entries = @(Get-Content "$dir\SHA256SUMS" | Where-Object { $_ -match $pattern })
if ($entries.Count -ne 1) { throw 'SHA256SUMS: entrada ausente, invalida o duplicada.' }
$expected = ($entries[0] -split '\s+')[0]
if ((Get-FileHash "$dir\$zip" -Algorithm SHA256).Hash -ne $expected) {
      throw 'Checksum invalido; no ejecutes el paquete.'
}
Expand-Archive "$dir\$zip" -DestinationPath "$dir\app"
Set-Location "$dir\app"
```

Ante un error, detente: no ejecutes ni extraigas manualmente el archivo rechazado.
Tras verificar y extraer, mantén `SDL3.dll` y `libSkiaSharp.dll` junto al ejecutable
y ejecuta:

```powershell
.\pixelpointer.exe --version
.\pixelpointer.exe selftest
.\pixelpointer.exe probe
.\pixelpointer.exe daemon
```

El daemon queda en primer plano; Ctrl+C lo detiene. Después de detenerlo,
para iniciar sin bloquear PowerShell:

```powershell
Start-Process "$PWD\pixelpointer.exe" -ArgumentList daemon -WindowStyle Hidden
```

Luego consulta `.\pixelpointer.exe status` y detén con `.\pixelpointer.exe quit`.
Si un atajo está ocupado, ejecuta en primer plano para ver el conflicto.
Los binarios no tienen firma Authenticode; no desactives las protecciones de
Windows para eludir alertas.

#### Pruebas y retorno a la estable

- Confirma versión elegida, `selftest` y sesión/portales en `probe`.
- Prueba Ctrl+1, rueda del ratón, Ctrl+2, colores, marcador (Shift+color), desenfoque (X y Shift+X), formas (Shift, Ctrl, Ctrl+Shift y Tab al arrastrar), Ctrl+C y Ctrl+Z.
- Prueba Ctrl+S: PNG en Imágenes/`Screenshots` de XDG en Linux o en la carpeta
   especial Imágenes/`Screenshots` de Windows, respetando OneDrive.
- Esc cierra la sobreposición; `status` debe confirmar que el daemon sigue
   activo y `quit` debe detenerlo.

Para volver a la estable, detén el daemon de preview con su ejecutable e inicia
el de la instalación estable. Si registraste una entrada de escritorio para
preview, elimínala con `./pixelpointer uninstall-desktop-file` antes de quitar
sus archivos y registra la estable de nuevo si hace falta. Conserva entradas
modificadas y configuraciones; borrar la carpeta portátil no elimina capturas
ni revoca permisos del portal.

Al reportar un fallo, incluye versión, SO, arquitectura, escritorio/versión,
X11/Wayland, número de monitores, pasos y mensaje de error. No compartas
capturas sensibles, tokens ni logs completos. Los ejemplos son una guía manual:
los smokes aprobados en CI no comprueban aceptación gráfica de la preview en
todas las combinaciones de máquina y escritorio.

### Seguridad y recuperación de la preview

| Riesgo | Control |
| --- | --- |
| Publicación desde un PR o una rama no aprobada | Job solo por dispatch de `main`, aprobación del mantenedor en el Environment `release`, checkout del commit de la ejecución y ancestralidad de `origin/main` |
| Token personal para crear etiquetas protegidas | Solo el App autorizado, con token limitado a `contents: write`; tokens y claves privadas no entran en artifacts ni en la salida de scripts |
| Paquetes incompletos, alterados o de otra ejecución | Misma versión en build/smoke, atestaciones de SBOM/procedencia, conjunto exacto de assets, SHA-256 local y digest remoto verificados antes de publicar |
| Reejecución sobrescribe release o promueve preview a estable | Borrador debe coincidir con etiqueta/commit; nunca mover etiquetas ni sobrescribir assets; `prerelease=true`, `make_latest=false`, sin post-release |

Si falla tras crear la etiqueta o el borrador, se conservan y la preview no se
hace pública. No borres ni muevas etiquetas para eludir un fallo. Reejecutar la
misma ejecución solo finaliza con todos los digests idénticos; los builds pueden
no ser reproducibles byte a byte. Ante una divergencia, inicia otro dispatch
con un nuevo `preview.N`. Una etiqueta existente sin borrador requiere
investigación del mantenedor; el script rechaza reutilizarla. La reejecución
rechaza modificar una preview ya pública; publica otra preview para corregirla.

El equivalente local es `tools/scripts/Set-PreviewRelease.ps1`: sin `-Publish`,
valida archivos y emite el payload sin acceder a GitHub; `-Publish` se reserva
al job aprobado con el App. `tools/scripts/Test-PreviewRelease.ps1`, incluido
en `Invoke-Validation.ps1`, prueba filesystem real y API sintética en container.
Estas pruebas no reemplazan la primera publicación real de los seis RIDs en
Actions ni la aceptación gráfica en las máquinas de destino.

## Primera versión funcional

`v0.1.2` es la primera versión funcional aceptada como estable por el mantenedor
el 2026-10-30 en Ubuntu 24.04.5/GNOME X11 y Ubuntu 26.04/GNOME Wayland. El
[plan E2E](plano-e2e-grafico-uv.md) registra aceptación manual y límites;
no significa validación nativa Windows/macOS ni finalización de F2/F3.

La release `v0.1.1` incluye el SVG en el paquete, pero su instalador no lo copia.
La `v0.1.2` incluye la corrección del PR #8: `install.sh` e `install.ps1`
exigen y copian `pixelpointer.svg`. No sobrescriba assets ni mueva etiquetas
anteriores. Para reproducir la versión aceptada, desde cualquier directorio
Linux/macOS con `curl`, `sh` y dependencias nativas del README disponibles:

```bash
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/download/v0.1.2/install.sh | sh -s -- --version 0.1.2
```

El instalador verifica SHA-256 e instala/sustituye paquete en el prefijo por defecto,
sin cambiar perfiles ni PATH. Compruebe `pixelpointer --version` y en Linux ejecute
`pixelpointer install-desktop-file`. Una entrada desktop anterior modificada
se conserva y exige migración explícita, no sobrescritura silenciosa.

Publicación GitHub y envío winget son etapas distintas. Un job winget esperando
aprobación puede ocupar `release-main` tras la publicación; si el mantenedor
aplaza el envío, rechace solo la aprobación `winget` de la ejecución anterior
para liberar el siguiente Release PR. No apruebe winget para desbloquear una release.
La corrección estructural de concurrencia corresponde al catálogo Groundwork,
no a modificaciones locales de `gw-*`.

## Artefactos

| Archivo | Contenido |
| --- | --- |
| `pixelpointer-<rid>.tar.gz` | `pixelpointer` (modo 0755), `libSDL3` y `libSkiaSharp` (`.so` o `.dylib`), `pixelpointer.svg`, `LICENSE` y `THIRD-PARTY-NOTICES.md`; propietario `0:0`; Linux y macOS |
| `pixelpointer-<rid>.zip` | `pixelpointer.exe`, `SDL3.dll`, `libSkiaSharp.dll`, `pixelpointer.svg`, `LICENSE` y `THIRD-PARTY-NOTICES.md`; Windows |
| `pixelpointer-<rid>.spdx.json` | SBOM SPDX 2.2 del paquete RID: archivos del paquete y dependencias NuGet de compilación |
| `pixelpointer-linux-x64.deb`, `pixelpointer-linux-arm64.deb` | Aplicación en `/usr/lib/pixelpointer`, enlace `/usr/bin/pixelpointer`, entrada de escritorio en `/usr/share/applications`, icono en `/usr/share/icons/hicolor/scalable/apps` y documentación en `/usr/share/doc/pixelpointer`; propietario `root`; `Depends: libc6 (>= 2.34), libstdc++6, libfontconfig1` |
| `install.sh` | Instalador POSIX para Linux y macOS |
| `install.ps1` | Instalador para Windows PowerShell 5.1+ y PowerShell 7 |
| `SHA256SUMS` | SHA-256 de todos los archivos anteriores, formato `sha256sum` |

Son 16 archivos además de `SHA256SUMS` (6 paquetes, 6 SBOM, 2 archivos `.deb` y
2 instaladores); el job `Prepare release assets` falla si el recuento difiere.
Los nombres no llevan versión para que `releases/latest/download/<archivo>` sea
estable; la versión está en la etiqueta y en `pixelpointer --version`. En las
versiones `.deb`, los sufijos usan `~` (por ejemplo, `0.4.0~rc.1`) para ordenarse
antes de la versión final. No hay paquete `linux-musl`: SDL3-CS no publica un
binario musl y Linux requiere glibc 2.34 o posterior para la `libSDL3` distribuida
(D12 del [plan de implementación](../plano-implementacao.md), en portugués).

Cada archivo tiene una atestación de procedencia de GitHub (`gw-github-assets.yml`).
La release incluye un `release-manifest.json` atestado con el SHA-256 de cada
asset; cada paquete `.tar.gz`, `.zip` y `.deb` también tiene una atestación SBOM
para su RID. Ambas usan Sigstore keyless mediante OIDC. Los binarios no tienen
firma Authenticode ni Developer ID (requieren certificados de pago; D5 del
[plan de implementación](../plano-implementacao.md)).

## Verificar las descargas

```bash
sha256sum --check --ignore-missing SHA256SUMS
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer \
  --predicate-type https://spdx.dev/Document/v2.2
```

## Canales de distribución

| Canal | Comando | Actualización |
| --- | --- | --- |
| Instaladores | `install.sh` / `install.ps1` (consulta el [README](../../README.es.md#instalación)) | `releases/latest` |
| `.deb` (asset de release, sin repositorio APT) | `sudo apt install ./pixelpointer-linux-x64.deb` | Manual, descargando la nueva release |
| Homebrew (macOS y Linux) | `brew install lzocateli/tap/pixelpointer` | `brew upgrade`; fórmula actualizada por `post-release.yml` |
| winget (Windows) | `winget install lzocateli.PixelPointer` | `winget upgrade`; `post-release.yml` abre un PR en `microsoft/winget-pkgs`, publicado tras la moderación de Microsoft |

La fórmula vive en [`lzocateli/homebrew-tap`](https://github.com/lzocateli/homebrew-tap)
y se genera desde `SHA256SUMS` mediante
[`New-HomebrewFormula.ps1`](../../tools/scripts/New-HomebrewFormula.ps1). Instala
en `libexec` (el `.tar.gz` de `osx-arm64`, `osx-x64`, `linux-arm64` y `linux-x64`),
crea el enlace `bin/pixelpointer`, depende de `fontconfig` en Linux y `brew test`
comprueba `--version` y `selftest`.

El manifiesto winget (`lzocateli.PixelPointer`, esquema 1.10.0) se genera desde
`SHA256SUMS` con [`New-WingetManifest.ps1`](../../tools/scripts/New-WingetManifest.ps1).
Usa los `.zip` de `win-x64` y `win-arm64`, con `pixelpointer.exe` portátil
(alias `pixelpointer`) y las DLL nativas en el mismo directorio. La aplicación
resuelve las bibliotecas desde el directorio real del ejecutable, por lo que el
alias funciona; `winget.yml` lo comprueba con `selftest`.

## Compilación por RID

| RID | Host de compilación | Toolchain |
| --- | --- | --- |
| `linux-x64`, `linux-arm64` | Contenedor en host Linux x64 | Imagen oficial de compilación cruzada `azurelinux-3.0-net10.0-cross-*` (sysroot glibc 2.27, clang, lld) |
| `win-x64`, `win-arm64` | `windows-2025`, `windows-11-arm` | SDK de `global.json` y Visual Studio C++ |
| `osx-arm64`, `osx-x64` | `macos-15`, `macos-15-intel` | SDK de `global.json` y Xcode |

Native AOT no compila entre sistemas operativos. En Linux, el SDK oficial se
copia sobre la imagen de compilación cruzada mediante
[`linux-cross.Dockerfile`](../../tools/build/linux-cross.Dockerfile). Las imágenes
se fijan por etiqueta y digest en `Publish-Aot.ps1`; deben actualizarse juntas.

```bash
pwsh -c './tools/scripts/Publish-Aot.ps1 -Rid linux-x64, linux-arm64'
pwsh ./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64   # ubuntu:22.04, rockylinux:9, debian:12 y .deb
pwsh ./tools/scripts/Test-Installers.ps1                   # install.sh con servidor HTTP local
```

El SBOM se genera con `microsoft/sbom-tool` (MIT), fijado como herramienta local
en [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json): en el
contenedor de compilación cruzada para Linux y con el SDK del runner en Windows y
macOS. El `.deb` se monta con `dpkg-deb --root-owner-group` en
`docker.io/library/debian:12`.

La prueba de paquete (`Test-AotPackage.ps1`) verifica SHA-256 y el contenido del
paquete, y ejecuta `--version`, `--help` y `selftest` (Skia codifica un PNG y SDL
se inicializa con el controlador `dummy`) directamente y mediante un enlace
simbólico, en contenedores sin .NET que instalan `libfontconfig`, `libstdc++` y
`libX11`. En Linux también comprueba la instalación de escritorio por usuario
idempotente, preservación de conflictos, eliminación segura, instancia única,
`status`, `quit` y reinicio con SDL dummy. Estas pruebas no validan atajos físicos,
visibilidad de la bandeja ni transparencia del compositor. Si está disponible,
también comprueba el SBOM (SPDX 2.2, nombre `pixelpointer <versión>` y SHA-256
del ejecutable), e instala, ejecuta y elimina el `.deb` con `apt-get` en
`ubuntu:22.04` y `debian:12`. `Test-Installers.ps1` sirve los paquetes en
`127.0.0.1` y prueba ayuda, errores de uso, checksum alterado sin efectos,
descarga fallida, instalación, reinstalación limpia, aviso de `PATH`, destino
predeterminado y `--global`. En Windows, ejecuta `install.ps1` en Windows
PowerShell 5.1 y PowerShell 7, con `-File` y como scriptblock.

## Publicación

Solo el mantenedor publica versiones. La aprobación de un PR, incluida una
excepción para los PR propios, no sustituye la aprobación del Environment
`release`; consulta la [gobernanza](../../.github/GOVERNANCE.md).

### Releases de seguridad y soporte

Las versiones anteriores a 1.0 no tienen soporte de seguridad ni backports. A
partir de `1.0.0`, solo se admite la minor `1.x` más reciente; publicar una nueva
minor termina el soporte de la anterior. El soporte de la línea `1.x` termina
con `2.0.0`. No hay SLA contractual. Las correcciones se publican en la línea
admitida; consulta la [política de versiones compatibles](../../.github/SECURITY.md#versões-suportadas).

### Flujo

1. Los PR `feat`, `fix` (o con `!`) integrados en `main` actualizan el Release PR
   `chore(main): release X.Y.Z`, abierto por release-please con la GitHub App
   `Altivy Release`. release-please calcula la versión y actualiza `VersionPrefix`
   en `Directory.Build.props`, `.release-please-manifest.json` y `CHANGELOG.md`.
   No edites la versión ni crees etiquetas manualmente.
2. El mantenedor integra el Release PR. `gw-release.yml` crea la etiqueta
   `vX.Y.Z` y una GitHub Release en borrador, y comprueba que la etiqueta
   corresponda a las versiones y que el commit sea ancestro de `origin/main`.
3. En la etiqueta, `publish.yml` ejecuta la compilación, las pruebas, publica los
   seis RIDs, realiza smoke tests (`--version`, `--help` y `selftest`, también
   mediante enlace simbólico) y prueba los instaladores en seis runners.
4. El job `attest-sbom` atesta el SBOM de cada RID (sujetos: el paquete y, en
   Linux, el `.deb`) antes de los assets, para que la release nunca se haga
   pública sin estas atestaciones.
5. `Prepare release assets` regenera `SHA256SUMS` con los instaladores y comprueba
   los 16 archivos. `Publish release assets` (`gw-github-assets.yml`) espera la
   aprobación en el Environment `release`, crea atestaciones de procedencia y
   adjunta los archivos a la release en borrador sin sobrescribir assets publicados.
6. `gw-release.yml` genera y atesta `release-manifest.json`, y publica la release.
   Las notas provienen del `CHANGELOG.md` generado por release-please.
7. Cuando la release es pública, `post-release.yml`:
   - ejecuta [`Update-HomebrewTap.ps1`](../../tools/scripts/Update-HomebrewTap.ps1)
     en el Environment `release` (con otra aprobación), usa el `SHA256SUMS`
     publicado y envía el commit `pixelpointer X.Y.Z` al tap. No hace nada si la
     fórmula ya coincide con la versión; si el tap publica una versión superior
     (corrección de una línea anterior), lo conserva y muestra una advertencia;
   - cuando el tap tiene la versión de la release, `verify-homebrew` instala
     desde el tap en `macos-15` y comprueba la versión y `brew test`;
   - llama a [`winget.yml`](../../.github/workflows/winget.yml): genera el
     manifiesto, ejecuta `winget validate`, instala desde el manifiesto local en
     `windows-2025`, comprueba `pixelpointer --version` y `selftest`, desinstala
     y luego envía el PR a `microsoft/winget-pkgs` con `wingetcreate submit`
     (v1.12.13.0, SHA-256 fijado). No lo envía si la versión ya está en
     `winget-pkgs` o hay un PR abierto para ella.

Los pull requests que cambian la cadena de release y `workflow_dispatch` de
`publish.yml` ejecutan compilación, smoke tests y pruebas de instaladores, sin
publicar. Las prereleases (`vX.Y.Z-sufijo`) no forman parte del flujo automático.

Si la publicación falla parcialmente, la release queda en borrador: corrige la
causa y usa `republish`, descrito en el [runbook](runbooks/release.md).

### Winget

`winget.yml` también se ejecuta en pull requests que lo modifican (valida contra
la última release, sin enviar) y mediante `workflow_dispatch` desde `main`, con
`version` y `submit`, para el primer envío o para reintentar tras un fallo:

```bash
gh workflow run winget.yml -f version=0.4.0 -f submit=true
```

El dispatch manual con `submit=true` se rechaza fuera de `main`. En el flujo
automático, el workflow reutilizable recibe el contexto de `gw-release.yml`:
`push` en `main` (post-release) o `workflow_dispatch` en la etiqueta
`vX.Y.Z` correspondiente (republish); cualquier otro origen se rechaza. El
secret `WINGET_CREATE_GITHUB_TOKEN` solo está en el Environment `winget`, con
`@lzocateli` como revisor obligatorio. `prevent_self_review` está desactivado
para permitir que el único mantenedor apruebe conscientemente una ejecución que
inició; GitHub también permite bypass administrativo, actualmente limitado a
ese mantenedor.

`wingetcreate` solo acepta un PAT clásico con alcance `public_repo` (no admite
tokens de alcance granular), que da acceso de escritura a todos los repositorios
públicos de la cuenta. Usa una expiración corta y renueva el secret antes de que
venza. El token se lee de una variable de entorno, nunca de la línea de comandos.
Cada PR pasa por la validación automática y la moderación de Microsoft; la versión
está disponible en winget después de integrarse.

### Seguimiento de envíos a winget

Una versión solo está disponible con `winget install` después de que su PR se
integre en [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs).
Hasta entonces, pasa por la validación automática de Microsoft y la moderación
voluntaria, que puede tardar días. Los comandos siguientes son de solo lectura y
funcionan con cualquier `gh` autenticado:

```bash
# Versiones ya publicadas en el catálogo (una carpeta por versión; HTTP 404 = aún no hay ninguna)
gh api repos/microsoft/winget-pkgs/contents/manifests/l/lzocateli/PixelPointer --jq '.[].name'

# PR enviados, abiertos y cerrados
gh pr list --repo microsoft/winget-pkgs --author lzocateli --state all \
  --search "lzocateli.PixelPointer in:title" --json number,title,state,createdAt,url

# Estado de un PR: etiquetas, checks y comentarios recientes
gh pr view <número> --repo microsoft/winget-pkgs --json state,labels,mergedAt,url
gh pr view <número> --repo microsoft/winget-pkgs --comments | tail -n 40

# Ejecuciones de winget.yml y ejecuciones a la espera de aprobación del Environment
gh run list --repo lzocateli/PixelPointer --workflow winget.yml --limit 5
gh run list --repo lzocateli/PixelPointer --status waiting
```

En Windows, `winget show lzocateli.PixelPointer --versions` enumera las versiones
que ve el cliente; el índice puede tardar unas horas en reflejar una integración.

Etiquetas habituales en el PR (referencias: [Moderation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Moderation.md)
y [Validation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Validation.md)):

| Etiqueta | Significado | Acción |
| --- | --- | --- |
| `Azure-Pipeline-Passed`, `Validation-Completed` | Validación automática aprobada | Esperar la moderación |
| `Needs-CLA` | Falta el Contributor License Agreement de Microsoft | Consulta lo siguiente; ningún PR se integra sin él |
| `Needs-Author-Feedback` | El moderador pidió un cambio o respuesta | Responde en el PR; si cambia el manifiesto, corrige `New-WingetManifest.ps1` y vuelve a enviarlo |
| `Validation-*-Error` (por ejemplo, `Validation-Installation-Error`, `Validation-Hash-Verification-Failed`) | Falló la validación automática | Lee el comentario del bot; hay que corregir el asset o el manifiesto |
| `Moderator-Approved` | Aprobado por el moderador | Esperar la integración automática |
| `New-Package` | Primera versión del paquete en el catálogo | Requiere una revisión manual más detallada |

**Contributor License Agreement (una vez por cuenta).** El primer PR de una
cuenta en `microsoft/winget-pkgs` recibe `Needs-CLA`. El titular de la cuenta
lee el acuerdo publicado por el bot y, si está de acuerdo, responde en ese PR
con `@microsoft-github-policy-service agree` (contribución personal) o con la
variante `company="..."` cuando contribuya en nombre de un empleador. Firmarlo
es una decisión legal del titular: no lo automatices ni delegues. Después de la
firma se elimina la etiqueta y el PR sigue a moderación; los siguientes PR de la
misma cuenta no vuelven a solicitar el CLA.

### Una versión anterior aún pendiente en winget

`winget.yml` no envía una versión que ya esté en el catálogo o que ya tenga un
PR abierto, pero **no** comprueba si una versión anterior sigue esperando
moderación. Mientras la primera versión está pendiente, el PR de la siguiente
también aparece como `New package`, y los moderadores pueden cerrar uno de los
dos. Para evitar PR en competencia, mantén como máximo uno abierto a la vez:

1. Antes de aprobar el Environment `winget` en `post-release.yml`, consulta los
   PR abiertos con el comando `gh pr list` anterior y `--state open`.
2. Si no hay un PR abierto del paquete, aprueba: se enviará la nueva versión.
3. Si hay abierto un PR de una versión anterior, **rechaza** la aprobación del
   Environment `winget` (Actions → ejecución → *Review deployments* → *Reject*).
   La release, Homebrew y los demás canales ya están publicados; solo se rechaza
   `Submit to microsoft/winget-pkgs`, por lo que la ejecución `Release` aparece
   como fallida por ese motivo.
4. Resuelve el PR pendiente: firma el CLA si tiene `Needs-CLA` y responde a
   `Needs-Author-Feedback`. Si el PR anterior se cierra sin integrarse, no lo
   reabras: ya fue reemplazado.
5. Cuando el PR pendiente se integre o cierre, envía solo la última versión
   publicada en GitHub; se pueden omitir versiones intermedias porque
   `winget upgrade` instala siempre la más reciente:

   ```bash
   gh workflow run winget.yml --repo lzocateli/PixelPointer --ref main \
     -f version=X.Y.Z -f submit=true
   ```

   El dispatch en `main` pasa por la misma validación, instalación local y
   aprobación del Environment `winget`.

### Token del tap Homebrew

El secret `HOMEBREW_TAP_TOKEN` solo está en el Environment `release`, usado por
el job del tap en `post-release.yml`. Es un PAT de alcance granular con acceso
únicamente a `lzocateli/homebrew-tap` y permiso `Contents: Read and write`. El
script lo lee de una variable de entorno y se lo pasa a `git` mediante
`GIT_CONFIG_*`, fuera de la línea de comandos. Cuando caduque, crea otro PAT con
el mismo alcance y sustituye el secret; sin él, la release se publica y falla el
paso del tap, que puede volver a ejecutarse después de corregirlo.

Para probar localmente sin publicar (el commit solo se crea en el clon temporal):

```bash
pwsh ./tools/scripts/Update-HomebrewTap.ps1 -Version 0.4.0 -ReleaseDirectory dist
```

## Limitaciones conocidas

- `republish` recompila los paquetes. Los assets ya adjuntos conservan el digest
  publicado, pero un asset ausente se carga desde la nueva compilación y puede no
  reproducir byte por byte el `SHA256SUMS` publicado. Si `SHA256SUMS` ya está
  adjunto, comprueba el resultado con `sha256sum --check` antes de anunciarlo.
- La configuración de GitHub permite bypass administrativo del Environment
  `winget` (`can_admins_bypass=true`). El único administrador considerado es
  `@lzocateli`; los cambios en ese grupo requieren revisar la política.
- El primer envío a `microsoft/winget-pkgs` crea un paquete nuevo y requiere
  moderación manual de Microsoft; hasta entonces,
  `winget install lzocateli.PixelPointer` no encuentra el paquete.
- Validación local el 2026-10-30: publicación, smoke (Ubuntu 22.04, Rocky 9,
  Debian 12 y `.deb`) y pruebas de instaladores aprobados para `linux-x64` con
  Podman. Los demás RIDs se validan en runners de GitHub.
