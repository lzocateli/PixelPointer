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

## Installation

Packages for `linux-x64`, `linux-arm64` (glibc 2.34+: Ubuntu 22.04, Debian 12,
RHEL/Rocky 9 or newer), `win-x64`, `win-arm64`, `osx-arm64` and `osx-x64`. The
installers check the SHA-256 against the release `SHA256SUMS`, never elevate
themselves and never change shell profiles or `PATH`:

To test a **preview**, choose a version marked **Pre-release** in
[Releases](https://github.com/lzocateli/PixelPointer/releases) and follow the
[preview package guide](docs/en/release.md#using-a-preview-package), with
Linux/Wayland and Windows commands. The installers below are for stable;
previews use portable packages and are not distributed through Homebrew/winget.

```bash
# Linux and macOS: app in ~/.local/lib/pixelpointer, command in ~/.local/bin
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh
```

```powershell
# Windows (PowerShell 5.1+): installs to %LOCALAPPDATA%\Programs\pixelpointer
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1)))
```

On Windows, if the directory is not on `PATH`, `install.ps1` suggests two
options; use **only one**. The recommended one writes the user `PATH` to the
registry, but only new processes see it: in the same PowerShell session,
`pixelpointer` stays "not recognized" until you open another terminal or refresh
the session:

```powershell
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
```

The profile alternative writes `$PROFILE.CurrentUserAllHosts` (`profile.ps1`),
which Windows PowerShell only loads when the execution policy allows scripts.
With the default `Restricted` policy, every new terminal shows "profile.ps1
cannot be loaded because running scripts is disabled on this system". If you
already wrote `PATH` to the registry, remove the `$env:PATH = ...pixelpointer...`
line from the profile; the profile is unnecessary. To keep the profile, allow
local scripts for your user only, knowing that this relaxes the policy (a policy
enforced by Group Policy takes precedence; check with `Get-ExecutionPolicy -List`):

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
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

## Updating

PixelPointer does not contact the network by default. On installations made by
`install.sh` or `install.ps1` in the user's directory, `pixelpointer update install`
downloads the latest stable release, verifies the SHA-256 and the Sigstore
provenance inside the application, runs the new version (`--version` and
`selftest`) and only then swaps the files, keeping the previous version on
failure. With the daemon running, the swap happens when it exits. Automatic
checking and installation are optional and start disabled
(`pixelpointer update auto-check on`, `pixelpointer update auto-install on` or
the tray's **Atualizações** submenu). `.deb`, Homebrew, winget, global
installations and portable copies only notify and show the command for their
channel. Installations made before this feature need to run the installer once
more. Details and limitations are in the [usage guide](docs/en/usage.md#updating).

## Uninstall

For manual removal, stop the daemon first with `pixelpointer quit`. The
installers' `--uninstall` and `-Uninstall` modes attempt to stop it automatically.
Then remove PixelPointer using the same method you used to install it:

| Installation | Command or action |
| --- | --- |
| `install.sh` (Linux/macOS, default prefix) | Run `sh install.sh --uninstall`; for a global install, stop the daemon and remove the desktop integration as the user before running `sudo sh install.sh --uninstall --global` |
| `install.sh --prefix <dir>` | Remove `<dir>/bin/pixelpointer` and `<dir>/lib/pixelpointer`, replacing `<dir>` with the prefix used during installation |
| `install.sh --global` | Run `sudo rm -f -- /usr/local/bin/pixelpointer` and `sudo rm -r -- /usr/local/lib/pixelpointer`; on Linux, remove the desktop entry first using the installed binary |
| `install.ps1` | In PowerShell, run `install.ps1 -Uninstall` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall lzocateli/tap/pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

### Optional Full Cleanup

For user-scope installations made with `install.sh` or `install.ps1`, run the
release installer with the explicit full-cleanup option:

```bash
# Linux
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh -s -- --uninstall --purge
```

```powershell
# Windows PowerShell 5.1+
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1))) -Uninstall -Purge
```

Purge deletes settings, cache and every file in `Screenshots`; on Linux it also
removes only Trash items whose names contain `pixelpointer`. On Windows, the
Recycle Bin is preserved. The script stops the daemon if it is running. This is
permanent: review the captures folder first. Purge requires a non-elevated
user-scope installation with a valid receipt; for older installations, run the
installer once first. On macOS, use `--uninstall` without `--purge`, since
captures may be stored in a user-selected folder. For global installations,
`.deb`, Homebrew or winget, use the corresponding method in the table.

If you manually added the PowerShell startup line to your profile, remove that
line from `$PROFILE.CurrentUserAllHosts`. On Linux, remove the desktop
integration before the executable with `pixelpointer uninstall-desktop-file`,
using the same `XDG_DATA_HOME`; the command preserves modified files. The
standard uninstall does not delete PNG or GIF captures; the optional cleanup can
remove them. It does not remove VS Code attachments or .NET build and test caches.
See the [usage guide](docs/en/usage.md)
for details and limitations by installation method.

## Getting started

```bash
pixelpointer install-desktop-file   # Linux: registers the app id used by the portals
pixelpointer grant                  # Linux: authorize portal screen capture (once)
pixelpointer daemon                 # Ctrl+1 zoom, Ctrl+2 draw, Ctrl+3 timer, Ctrl+4 Live Zoom, Ctrl+5 recording, Ctrl+6 snip
```

The `daemon` command runs in the **foreground**: it keeps hotkeys and IPC active
but does not release the terminal automatically. This is expected, not a hang;
Ctrl+C stops it. On Linux, append `&` to release the prompt:

```bash
pixelpointer daemon &
```

The process may still write to that terminal and may stop when its window closes.
To run without a terminal on Linux, use `pixelpointer install-desktop-file` and
open **PixelPointer** from the applications menu. Exit from the tray's **Sair**
item or with `pixelpointer quit`. This does not enable autostart at login.

On Windows (PowerShell), start the daemon in a separate, windowless process:

```powershell
Start-Process pixelpointer -ArgumentList daemon -WindowStyle Hidden
pixelpointer status
```

The prompt returns immediately and the daemon keeps running after PowerShell is
closed. Exit from the tray's **Sair** item or with `pixelpointer quit`. In this
mode the daemon's messages are not shown; to diagnose hotkey conflicts, run
`pixelpointer daemon` in the foreground.

| Hotkey or key | Action |
| --- | --- |
| **Ctrl+1** | Zoom: freezes the magnified screen; the mouse pans the image |
| **Ctrl+2** | Draw without zoom |
| **Ctrl+3** | Break timer (10 min; arrows or wheel adjust and restart) |
| **Ctrl+4** | Live Zoom: magnifies the live screen following the cursor, without freezing; press again to exit (full screen on Windows; window magnifier on macOS and X11; Wayland best effort when the portal provides cursor metadata; disabled on GNOME) |
| **Ctrl+5** | Start or save a GIF (up to 5 min); X11/Windows: display under the cursor at startup; Wayland: display authorized in the portal |
| **Ctrl+6** | Snip: X11/Windows open on the display under the cursor; drag to copy; hold Shift on release to save a PNG |
| `pixelpointer status` | Check whether the daemon is running |
| `pixelpointer quit` | Stop the daemon and any active overlay |
| Mouse wheel / arrows | Zoom in or out |
| Click and drag | Draw (pen pressure is supported) |
| Shift + drag | Straight line |
| Ctrl + drag | Rectangle |
| Ctrl+Shift + drag | Arrow |
| Tab + drag | Ellipse |
| R G B Y O P | Red, green, blue, yellow, orange, pink |
| Shift + R G B Y O P | Translucent highlighter in that color |
| X / Shift+X | Blur / strong blur (rectangle and ellipse are filled) |
| T | Text at the pointer (Enter adds a line, Esc or click finishes) |
| Ctrl+wheel | Pen width |
| Ctrl+Z / E | Undo / erase all |
| W / K | Whiteboard / blackboard |
| Ctrl+S | Save PNG |
| Ctrl+C | Copy the image to the clipboard |
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
| Capture and hotkeys on X11, Windows and macOS | XGetImage/XGrabKey and Screenshot portal for X11 drawing, GDI/RegisterHotKey, screencapture |
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

## Community and contributions

Contributions through forks and pull requests are welcome. Start with
[bug reports, feature requests or questions](https://github.com/lzocateli/PixelPointer/issues/new/choose)
and wait for scope alignment before implementing.

- [Contribution guide](.github/CONTRIBUTING.md) and [governance](.github/GOVERNANCE.md).
- [Support](.github/SUPPORT.md) and [code of conduct](.github/CODE_OF_CONDUCT.md).
- [Private vulnerability reports](.github/SECURITY.md).
- [Release history](https://github.com/lzocateli/PixelPointer/releases).

The managed infrastructure (the `gw-*` workflows, `.githooks/`,
`tools/scripts/ci/`, templates and policies in `.github/`) was generated by
Altivy.Groundwork; `.groundwork/manifest.json` lists these files and
`groundwork status` checks their integrity. The build, publishing and
distribution workflows/scripts (`ci.yml`, `publish.yml`, `post-release.yml`,
`winget.yml`, `tools/scripts/*.ps1`, `install/`) and governance documents were
adapted from DotnetScope and belong to this project.

## License

[MIT](LICENSE). Redistributed components (SDL3, SkiaSharp/Skia, Tmds.DBus):
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
