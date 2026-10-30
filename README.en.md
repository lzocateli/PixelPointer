# PixelPointer — screen zoom and annotation for presentations

[Português (Brasil)](README.md) · **English** · [Español](README.es.md)

[![CI](https://github.com/lzocateli/PixelPointer/actions/workflows/ci.yml/badge.svg)](https://github.com/lzocateli/PixelPointer/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/lzocateli/PixelPointer?sort=semver)](https://github.com/lzocateli/PixelPointer/releases/latest)
[![MIT License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
![Platforms: Linux, Windows and macOS](https://img.shields.io/badge/platforms-Linux%20%7C%20Windows%20%7C%20macOS-informational)

A Native AOT tool (`pixelpointer`) for people who present, record or teach:
zoom into the screen with the mouse, draw and highlight on top of it and get
back to work with a single key, triggered by **global hotkeys** on Linux (X11
and **Wayland**), Windows and macOS. One package, no .NET installation needed.

**Who it is for:** speakers, teachers, video creators and teams running live
demos who want the ZoomIt (Sysinternals/PowerToys) workflow on any operating
system, including Wayland desktops such as GNOME and KDE. PixelPointer is an
independent project; it is not a Microsoft product and does not use ZoomIt's
code, name or icons.

> **Status:** pre-1.0. Zoom (Ctrl+1) and draw (Ctrl+2) work and were validated
> on GNOME 50/Wayland; Windows and macOS pass CI and await the manual test
> script. Break timer, Live Zoom, snip, recording and DemoType are in the
> [implementation plan](docs/plano-implementacao.md) (Portuguese). Results and
> limitations: [F0 report](docs/f0-relatorio.md) (Portuguese).

## Installation

Packages for `linux-x64`, `linux-arm64` (glibc 2.34+: Ubuntu 22.04, Debian 12,
RHEL/Rocky 9 or newer), `win-x64`, `win-arm64`, `osx-arm64` and `osx-x64`. The
installers check the SHA-256 against the release `SHA256SUMS`, never elevate
themselves and never change shell profiles or `PATH`:

```bash
# Linux and macOS: app in ~/.local/lib/pixelpointer, command in ~/.local/bin
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh
```

```powershell
# Windows (PowerShell 5.1+): installs to %LOCALAPPDATA%\Programs\pixelpointer
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1)))
```

```bash
# Homebrew (macOS and Linux)
brew install lzocateli/tap/pixelpointer

# winget (Windows), once the version passes moderation in microsoft/winget-pkgs
winget install lzocateli.PixelPointer

# Debian/Ubuntu (amd64 or arm64), from the release asset
curl -fsSLO https://github.com/lzocateli/PixelPointer/releases/latest/download/pixelpointer-linux-x64.deb
sudo apt install ./pixelpointer-linux-x64.deb
```

Every package ships an SPDX SBOM and provenance attestations:
`gh attestation verify <file> --repo lzocateli/PixelPointer`
([verification](docs/en/usage.md#verifying-downloads)).

## Getting started

```bash
pixelpointer install-desktop-file   # Linux: registers the app id used by the portals
pixelpointer grant                  # GNOME/Wayland: screen capture permission (once)
pixelpointer daemon                 # Ctrl+1 zoom, Ctrl+2 draw
```

| Hotkey or key | Action |
| --- | --- |
| **Ctrl+1** | Zoom: freezes the magnified screen; the mouse pans the image |
| **Ctrl+2** | Draw without zoom |
| Mouse wheel / arrows | Zoom in or out |
| Click and drag | Draw (pen pressure is supported) |
| R G B Y O P | Red, green, blue, yellow, orange, pink |
| Ctrl+wheel | Pen width |
| Ctrl+Z / E | Undo / erase all |
| W / K | Whiteboard / blackboard |
| Ctrl+S | Save PNG |
| Esc or right click | Exit |

On Wayland, the first `daemon` run asks the desktop to confirm the hotkeys.
Without the shortcuts portal, bind a desktop shortcut to `pixelpointer zoom` or
`pixelpointer draw`. Details, per-OS permissions and troubleshooting:
[usage guide](docs/en/usage.md).

## How it works

| Layer | Technology |
| --- | --- |
| Window, input and pen | SDL3 (native Wayland, X11, Win32, Cocoa) |
| Drawing and zoom | SkiaSharp |
| Capture and hotkeys on Wayland | XDG portals (Screenshot, GlobalShortcuts) over D-Bus |
| Capture and hotkeys on X11, Windows and macOS | XGetImage/XGrabKey, GDI/RegisterHotKey, screencapture |
| Distribution | .NET 10 Native AOT, ~19 MB including native libraries |

## Development

- .NET 10 (SDK from `global.json`) with Native AOT; tests use xUnit v3 on
  Microsoft Testing Platform: `dotnet test --solution PixelPointer.slnx`.
- Validate like CI (PowerShell 7 and Docker or Podman; `-Aot` publishes the
  native `linux-x64` binary and runs its `selftest`):

  ```bash
  pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot
  ```

- On Windows and macOS: `pwsh ./tools/scripts/Invoke-Validation.ps1 -Native -Aot`.
- Release packages, smoke tests on clean distributions and installer tests:
  [release process](docs/release.md) (Portuguese).
- Contribution guides are in Portuguese: [CONTRIBUTING](.github/CONTRIBUTING.md),
  [commit convention](.github/COMMIT_CONVENTIONS.md) and
  [security policy](.github/SECURITY.md). Issues and pull requests in English
  are welcome.

## License

[MIT](LICENSE). Redistributed components (SDL3, SkiaSharp/Skia, Tmds.DBus):
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
