# Usage guide

[Português (Brasil)](../uso.md) · **English** · [Español](../es/uso.md)

> Translation of the Portuguese reference [docs/uso.md](../uso.md). If they
> differ, the Portuguese version and the implemented behavior prevail.

## Commands

| Command | Purpose |
| --- | --- |
| `pixelpointer daemon` | Keeps running, registers the global hotkeys (Ctrl+1 zoom, Ctrl+2 draw) and the local command channel; Ctrl+C or SIGTERM stops it |
| `pixelpointer zoom` | Opens zoom now; with the `daemon` running, sends the command to it |
| `pixelpointer draw` | Opens draw mode without zoom; same rule as `zoom` |
| `pixelpointer overlay [--image file.png] [--mode zoom\|draw]` | Opens the overlay on a new capture or on an image |
| `pixelpointer capture [--out file.png]` | Captures the screen and reports the latency |
| `pixelpointer probe` | Diagnostics: OS, session, monitors, portal versions |
| `pixelpointer install-desktop-file` | Linux: installs the `.desktop` file with the app id `io.github.lzocateli.PixelPointer` |
| `pixelpointer grant` | GNOME/Wayland: requests the capture permission from a focused window |
| `pixelpointer selftest` | Checks the native libraries without a display (used by package tests) |
| `pixelpointer --version` / `--help` | Version and help |

Overlay keys: mouse wheel or arrows to zoom, click and drag to draw,
R/G/B/Y/O/P for colors, Ctrl+wheel for pen width, Ctrl+Z to undo, E to erase,
W/K for whiteboard/blackboard, Ctrl+S to save a PNG in the current directory,
Esc or right click to exit.

## Permissions per system

### Linux on Wayland (GNOME, KDE, Hyprland)

Wayland does not let a program see other programs' screen or keyboard;
PixelPointer uses the desktop's XDG portals.

1. `pixelpointer install-desktop-file`: without the `.desktop` file the desktop
   cannot associate permissions and hotkeys with PixelPointer. The `.deb`
   package already installs it.
2. `pixelpointer grant` (GNOME): opens a PixelPointer window; press Enter and
   click **Allow**. GNOME only shows this dialog to the focused application,
   so it cannot be requested from the terminal.
3. `pixelpointer daemon`: the first time, the desktop shows the proposed hotkeys
   (Ctrl+1 and Ctrl+2) for you to confirm or change. Afterwards binding is
   immediate.

If the desktop has no GlobalShortcuts portal (Sway, GNOME older than 48), create
desktop shortcuts that run the command:

| Desktop | Setting |
| --- | --- |
| GNOME | Settings → Keyboard → Custom Shortcuts: command `pixelpointer zoom`, shortcut Ctrl+1 |
| KDE Plasma | System Settings → Shortcuts → Add Command: `pixelpointer zoom` |
| Sway | `bindsym Ctrl+1 exec pixelpointer zoom` in `~/.config/sway/config` |
| Hyprland | `bind = CTRL, 1, exec, pixelpointer zoom` in `hyprland.conf` |

With the `daemon` running, `pixelpointer zoom` only sends the command through
the local socket (`$XDG_RUNTIME_DIR/pixelpointer.sock`) and exits in milliseconds.

### Linux on X11

Hotkeys and capture work without extra permissions (XGrabKey and XGetImage).

### macOS

On the first capture, macOS asks for the **Screen Recording** permission in
System Settings → Privacy & Security. Version 1 is not signed with a Developer
ID, so macOS may ask again after each update. Native global hotkeys arrive in a
later version; until then, bind `pixelpointer zoom` to a system shortcut
(Shortcuts/Automator) or a launcher such as Raycast.

### Windows

No extra permissions. Ctrl+1 and Ctrl+2 are registered with `RegisterHotKey`;
if another program already uses the combination, the `daemon` reports it.

## Verifying downloads

```bash
sha256sum --check --ignore-missing SHA256SUMS
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer \
  --predicate-type https://spdx.dev/Document/v2.2
```

## Uninstalling

| Installed with | Removal |
| --- | --- |
| `install.sh` | `rm -rf ~/.local/lib/pixelpointer ~/.local/bin/pixelpointer` (or the `--prefix` used) |
| `install.ps1` | delete `%LOCALAPPDATA%\Programs\pixelpointer` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

On Linux, also remove `~/.local/share/applications/io.github.lzocateli.PixelPointer.desktop`
if you used `install-desktop-file`. On GNOME, bound hotkeys can be removed in
Settings; the capture permission with:

```bash
gdbus call --session --dest org.freedesktop.impl.portal.PermissionStore \
  --object-path /org/freedesktop/impl/portal/PermissionStore \
  --method org.freedesktop.impl.portal.PermissionStore.DeletePermission \
  screenshot screenshot io.github.lzocateli.PixelPointer
```

## Troubleshooting

| Symptom | Likely cause and fix |
| --- | --- |
| `erro: O portal recusou a solicitação (código 2)` | Missing capture permission on GNOME: run `pixelpointer grant` |
| `aviso: registro do app id falhou` | Missing `.desktop` file: run `pixelpointer install-desktop-file` |
| Hotkeys unavailable on Wayland | Desktop without the GlobalShortcuts portal: use a desktop shortcut (table above) |
| The window opens but gets no keyboard input | Compositor without an activation token; click the window once |
| `selftest falhou` on Linux | glibc older than 2.34 or missing `libfontconfig`: use Ubuntu 22.04, Debian 12, RHEL 9 or newer and install `libfontconfig1` |
| ~0.5 s delay when opening on Wayland | Cost of the Screenshot portal; continuous PipeWire capture is planned for F2 |

Program messages are currently in Portuguese. To report a problem, include the
output of `pixelpointer probe` and follow [support](../../.github/SUPPORT.md).

## Current limitations

- Break timer (Ctrl+3), Live Zoom (Ctrl+4), recording (Ctrl+5), snip (Ctrl+6)
  and DemoType (Ctrl+7) do not exist yet; see the [plan](../plano-implementacao.md).
- With several monitors on Wayland, the compositor picks the window's monitor.
- macOS has no native global hotkeys and no signature in version 1.
