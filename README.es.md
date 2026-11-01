# PixelPointer — zoom y anotación de pantalla para presentaciones

[Português (Brasil)](README.md) · [English](README.en.md) · **Español**

[![CI](https://github.com/lzocateli/PixelPointer/actions/workflows/ci.yml/badge.svg)](https://github.com/lzocateli/PixelPointer/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/lzocateli/PixelPointer?sort=semver)](https://github.com/lzocateli/PixelPointer/releases/latest)
[![Licencia MIT](https://img.shields.io/badge/licencia-MIT-blue)](LICENSE)
![Plataformas: Linux, Windows y macOS](https://img.shields.io/badge/plataformas-Linux%20%7C%20Windows%20%7C%20macOS-informational)

Herramienta Native AOT (`pixelpointer`) para quienes presentan, graban o
enseñan: amplía la pantalla con el mouse, dibuja y resalta encima y vuelve al
trabajo con una sola tecla, activada por **atajos globales** en Linux (X11 y
**Wayland**), Windows y macOS. Un solo paquete, sin necesidad de instalar .NET.

**Para quién es:** ponentes, docentes, creadores de video y equipos que hacen
demostraciones en vivo y quieren el flujo de ZoomIt (Sysinternals/PowerToys) en
cualquier sistema operativo, incluidos escritorios Wayland como GNOME y KDE.
PixelPointer es un proyecto independiente; no es un producto de Microsoft y no
usa el código, la marca ni los íconos de ZoomIt.

> **Estado:** `v0.1.2` es la primera versión funcional, aceptada como estable por
> el mantenedor en Linux/GNOME: Ubuntu 24.04.5/X11 y Ubuntu 26.04/Wayland, el
> 2026-10-30. Zoom y dibujo tienen aceptación manual en los entornos probados,
> incluido dibujo transparente según el relato y las capturas del usuario.
> El producto sigue pre-1.0; Windows y macOS pasan CI, pero esperan aceptación
> manual. Otros compositores/configuraciones no están incluidos en esta aceptación.
> Evidencias y límites: [plan E2E](docs/es/plano-e2e-grafico-uv.md).
> Temporizador, Live Zoom, recorte, grabación y DemoType siguen en el
> [plan de implementación](docs/es/plano-implementacao.md); el
> [informe F0](docs/f0-relatorio.md) (portugués) conserva resultados históricos del spike.

## Instalación

Paquetes para `linux-x64`, `linux-arm64` (glibc 2.34+: Ubuntu 22.04, Debian 12,
RHEL/Rocky 9 o posteriores), `win-x64`, `win-arm64`, `osx-arm64` y `osx-x64`.
Los instaladores verifican el SHA-256 con el `SHA256SUMS` de la versión, nunca
se autoelevan y no modifican perfiles ni el `PATH`:

Para probar una **preview**, elige una versión marcada **Pre-release** en
[Releases](https://github.com/lzocateli/PixelPointer/releases) y sigue la
[guía de paquetes preview](docs/es/release.md#usar-un-paquete-preview), con
comandos para Linux/Wayland y Windows. Los instaladores siguientes son de la
estable; las previews usan paquetes portátiles y no se distribuyen por Homebrew/winget.

```bash
# Linux y macOS: aplicación en ~/.local/lib/pixelpointer, comando en ~/.local/bin
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh
```

```powershell
# Windows (PowerShell 5.1+): instala en %LOCALAPPDATA%\Programs\pixelpointer
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1)))
```

En Windows, si el directorio no está en el `PATH`, `install.ps1` sugiere dos
opciones; usa **solo una**. La recomendada graba el `PATH` del usuario en el
registro, pero solo la ven los procesos nuevos: en la misma sesión de PowerShell,
`pixelpointer` sigue "no reconocido" hasta abrir otra terminal o actualizar la
sesión:

```powershell
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
```

La alternativa por perfil graba `$PROFILE.CurrentUserAllHosts` (`profile.ps1`),
que Windows PowerShell solo carga cuando la directiva de ejecución permite
scripts. Con la directiva predeterminada `Restricted`, cada terminal nueva
muestra "profile.ps1 no se puede cargar porque la ejecución de scripts está
deshabilitada". Si ya grabaste el `PATH` en el registro, elimina del perfil la
línea `$env:PATH = ...pixelpointer...`; el perfil es innecesario. Para conservar
el perfil, permite scripts locales solo para tu usuario, sabiendo que esto relaja
la directiva (una directiva impuesta por GPO prevalece; verifícala con
`Get-ExecutionPolicy -List`):

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

```bash
# Homebrew (macOS y Linux)
brew install lzocateli/tap/pixelpointer

# winget (Windows), tras la moderación de la versión en microsoft/winget-pkgs
winget install lzocateli.PixelPointer

# Debian/Ubuntu (amd64 o arm64), desde el archivo de la versión
curl -fsSLO https://github.com/lzocateli/PixelPointer/releases/latest/download/pixelpointer-linux-x64.deb
sudo apt install ./pixelpointer-linux-x64.deb
```

Cada paquete incluye el SBOM SPDX y atestaciones de procedencia:
`gh attestation verify <archivo> --repo lzocateli/PixelPointer`
([verificación](docs/es/uso.md#verificar-las-descargas)).

## Desinstalar

Si el daemon está en ejecución, detenlo primero con `pixelpointer quit`. Después,
desinstala PixelPointer con el mismo método que usaste para instalarlo:

| Instalación | Comando o acción |
| --- | --- |
| `install.sh` (Linux/macOS, prefijo predeterminado) | En Linux, ejecuta `pixelpointer uninstall-desktop-file` si registraste la entrada del menú; luego ejecuta `rm -f -- "$HOME/.local/bin/pixelpointer"` y `rm -r -- "$HOME/.local/lib/pixelpointer"` |
| `install.sh --prefix <dir>` | Elimina `<dir>/bin/pixelpointer` y `<dir>/lib/pixelpointer`, sustituyendo `<dir>` por el prefijo usado durante la instalación |
| `install.sh --global` | Ejecuta `sudo rm -f -- /usr/local/bin/pixelpointer` y `sudo rm -r -- /usr/local/lib/pixelpointer`; en Linux, elimina primero la entrada del escritorio con el binario instalado |
| `install.ps1` | En PowerShell, ejecuta `Remove-Item -LiteralPath "$env:LOCALAPPDATA\Programs\pixelpointer" -Recurse` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall lzocateli/tap/pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

Si agregaste manualmente la línea de inicio de PowerShell al perfil, elimínala
de `$PROFILE.CurrentUserAllHosts`. En Linux, elimina la integración del escritorio
antes del ejecutable con `pixelpointer uninstall-desktop-file`, usando el mismo
`XDG_DATA_HOME`; el comando conserva los archivos modificados. La desinstalación
no borra las capturas PNG. Consulta la [guía de uso](docs/es/uso.md#desinstalar)
para ver detalles y límites de cada método.

## Primeros pasos

```bash
pixelpointer install-desktop-file   # Linux: registra el app id que usan los portales
pixelpointer grant                  # GNOME/Wayland: permiso de captura (una vez)
pixelpointer daemon                 # Ctrl+1 zoom, Ctrl+2 dibujo
```

El comando `daemon` se ejecuta en **primer plano**: mantiene activos los atajos y
el IPC, pero no libera la terminal automáticamente. Es lo esperado, no un bloqueo;
Ctrl+C lo detiene. En Linux, agrega `&` para liberar el indicador:

```bash
pixelpointer daemon &
```

El proceso aún puede escribir en esa terminal y puede detenerse al cerrar su
ventana. Para usarlo sin terminal en Linux, ejecuta
`pixelpointer install-desktop-file` y abre **PixelPointer** desde el menú de
aplicaciones. Sal con **Sair** en la bandeja o mediante `pixelpointer quit`. Esto
no activa el
inicio automático de sesión.

En Windows (PowerShell), inicia el daemon en un proceso separado y sin ventana:

```powershell
Start-Process pixelpointer -ArgumentList daemon -WindowStyle Hidden
pixelpointer status
```

El indicador se libera de inmediato y el daemon sigue activo incluso después de
cerrar PowerShell. Sal con **Sair** en la bandeja o mediante `pixelpointer quit`.
En este modo los mensajes del daemon no se muestran; para diagnosticar conflictos
de atajos, ejecuta `pixelpointer daemon` en primer plano.

| Atajo o tecla | Acción |
| --- | --- |
| **Ctrl+1** | Zoom: congela la pantalla ampliada; el mouse recorre la imagen |
| **Ctrl+2** | Dibujo sin zoom |
| `pixelpointer status` | Consulta si el daemon está en ejecución |
| `pixelpointer quit` | Detiene el daemon y la sobreposición activa |
| Rueda del mouse / flechas | Más o menos zoom |
| Clic y arrastrar | Dibujar (se admite la presión del lápiz) |
| Shift + arrastrar | Línea recta |
| Ctrl + arrastrar | Rectángulo |
| Ctrl+Shift + arrastrar | Flecha |
| Tab + arrastrar | Elipse |
| R G B Y O P | Rojo, verde, azul, amarillo, naranja, rosa |
| Shift + R G B Y O P | Marcador translúcido en ese color |
| Ctrl+rueda | Grosor del lápiz |
| Ctrl+Z / E | Deshacer / borrar todo |
| W / K | Pizarra blanca / pizarra negra |
| Ctrl+S | Guardar PNG |
| Esc o clic derecho | Salir |

En Wayland, el primer `daemon` pide al escritorio que confirme los atajos. Sin
el portal de atajos, configura un atajo del propio escritorio que ejecute
`pixelpointer zoom` o `pixelpointer draw`. Detalles, permisos por sistema y
solución de problemas: [guía de uso](docs/es/uso.md).

## Cómo funciona

| Capa | Tecnología |
| --- | --- |
| Ventana, entrada y lápiz | SDL3 (Wayland nativo, X11, Win32, Cocoa) |
| Dibujo y zoom | SkiaSharp |
| Captura y atajos en Wayland | Portales XDG (Screenshot, GlobalShortcuts) por D-Bus |
| Captura y atajos en X11, Windows y macOS | XGetImage/XGrabKey, GDI/RegisterHotKey, screencapture |
| Distribución | .NET 10 Native AOT, ~19 MB con las bibliotecas nativas |

## Desarrollo

- .NET 10 (SDK del `global.json`) con Native AOT; pruebas con xUnit v3 sobre
  Microsoft Testing Platform: `dotnet test --solution PixelPointer.slnx`.
- Valida como en el CI (PowerShell 7 y Docker o Podman; `-Aot` publica el
  binario nativo `linux-x64` y ejecuta su `selftest`):

  ```bash
  pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot
  ```

- En Windows y macOS: `pwsh ./tools/scripts/Invoke-Validation.ps1 -Native -Aot`.
- Paquetes, pruebas en distribuciones limpias y pruebas de los instaladores:
  [proceso de release](docs/release.md) (en portugués).
- Las guías de contribución están en portugués: [CONTRIBUTING](.github/CONTRIBUTING.md),
  [convención de commits](.github/COMMIT_CONVENTIONS.md) y
  [política de seguridad](.github/SECURITY.md). Issues y pull requests en
  español son bienvenidos.

## Comunidad y contribuciones

Las contribuciones mediante forks y pull requests son bienvenidas. Empieza por
[informes de errores, solicitudes de funciones o preguntas](https://github.com/lzocateli/PixelPointer/issues/new/choose)
y espera a que se acuerde el alcance antes de implementar.

- [Guía de contribución](.github/CONTRIBUTING.md) y [gobernanza](.github/GOVERNANCE.md).
- [Soporte](.github/SUPPORT.md) y [código de conducta](.github/CODE_OF_CONDUCT.md).
- [Informes privados de vulnerabilidades](.github/SECURITY.md).
- [Historial de versiones](https://github.com/lzocateli/PixelPointer/releases).

La infraestructura administrada (workflows `gw-*`, `.githooks/`,
`tools/scripts/ci/`, plantillas y políticas de `.github/`) fue generada por
Altivy.Groundwork; `.groundwork/manifest.json` enumera estos archivos y
`groundwork status` verifica su integridad. Los workflows y scripts de
compilación, publicación y distribución (`ci.yml`, `publish.yml`,
`post-release.yml`, `winget.yml`, `tools/scripts/*.ps1`, `install/`) y los
documentos de gobernanza se adaptaron de DotnetScope y pertenecen al proyecto.

## Licencia

[MIT](LICENSE). Componentes redistribuidos (SDL3, SkiaSharp/Skia, Tmds.DBus):
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
