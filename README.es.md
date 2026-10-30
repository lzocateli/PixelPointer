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

> **Estado:** pre-1.0. El zoom (Ctrl+1) y el dibujo (Ctrl+2) funcionan y se
> validaron en GNOME 50/Wayland; Windows y macOS pasan el CI y esperan el guion
> de prueba manual. Temporizador, Live Zoom, recorte, grabación y DemoType están
> en el [plan de implementación](docs/plano-implementacao.md) (en portugués).
> Resultados y limitaciones: [informe de la F0](docs/f0-relatorio.md) (en portugués).

## Instalación

Paquetes para `linux-x64`, `linux-arm64` (glibc 2.34+: Ubuntu 22.04, Debian 12,
RHEL/Rocky 9 o posteriores), `win-x64`, `win-arm64`, `osx-arm64` y `osx-x64`.
Los instaladores verifican el SHA-256 con el `SHA256SUMS` de la versión, nunca
se autoelevan y no modifican perfiles ni el `PATH`:

```bash
# Linux y macOS: aplicación en ~/.local/lib/pixelpointer, comando en ~/.local/bin
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh
```

```powershell
# Windows (PowerShell 5.1+): instala en %LOCALAPPDATA%\Programs\pixelpointer
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1)))
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

## Primeros pasos

```bash
pixelpointer install-desktop-file   # Linux: registra el app id que usan los portales
pixelpointer grant                  # GNOME/Wayland: permiso de captura (una vez)
pixelpointer daemon                 # Ctrl+1 zoom, Ctrl+2 dibujo
```

| Atajo o tecla | Acción |
| --- | --- |
| **Ctrl+1** | Zoom: congela la pantalla ampliada; el mouse recorre la imagen |
| **Ctrl+2** | Dibujo sin zoom |
| Rueda del mouse / flechas | Más o menos zoom |
| Clic y arrastrar | Dibujar (se admite la presión del lápiz) |
| R G B Y O P | Rojo, verde, azul, amarillo, naranja, rosa |
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

## Licencia

[MIT](LICENSE). Componentes redistribuidos (SDL3, SkiaSharp/Skia, Tmds.DBus):
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
