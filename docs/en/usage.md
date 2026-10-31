# Usage guide

[Português (Brasil)](../uso.md) · **English** · [Español](../es/uso.md)

> Translation of the Portuguese reference [docs/uso.md](../uso.md). If they
> differ, the Portuguese version and the implemented behavior prevail.

**Functional reference version:** `v0.1.2`, the first functional version accepted
as stable by the maintainer on 2026-10-30 on Ubuntu 24.04.5/GNOME X11 and Ubuntu
26.04/GNOME Wayland. Acceptance is manual and limited to exercised flows;
Windows/macOS, other compositors and the complete matrix remain pending.
See the [evidence record](plano-e2e-grafico-uv.md). The F0 report is historical;
this acceptance does not change backend limitations described below.

## Commands

| Command | Purpose |
| --- | --- |
| `pixelpointer daemon` | Keeps running, registers the global hotkeys (Ctrl+1 zoom, Ctrl+2 draw) and the local command channel; Ctrl+C or SIGTERM stops it |
| `pixelpointer zoom` | Opens zoom now; with the `daemon` running, sends the command to it |
| `pixelpointer draw` | Opens draw mode without zoom; same rule as `zoom` |
| `pixelpointer status` | Reports whether the daemon is running, without opening an overlay |
| `pixelpointer quit` | Stops the daemon and any active overlay |
| `pixelpointer overlay [--image file.png] [--mode zoom\|draw]` | Opens the overlay on a new capture or on an image |
| `pixelpointer capture [--out file.png]` | Captures the screen and reports the latency |
| `pixelpointer probe` | Diagnostics: OS, session, monitors, portal versions |
| `pixelpointer install-desktop-file` | Linux: installs the `.desktop` file with the app id `io.github.lzocateli.PixelPointer` |
| `pixelpointer uninstall-desktop-file` | Linux: removes per-user resources only when unchanged; preserves modified files |
| `pixelpointer grant` | GNOME/Wayland: requests the capture permission from a focused window |
| `pixelpointer selftest` | Checks the native libraries without a display (used by package tests) |
| `pixelpointer --version` / `--help` | Version and help |

Overlay keys: mouse wheel or arrows to zoom, click and drag to draw,
R/G/B/Y/O/P for colors, Ctrl+wheel for pen width, Ctrl+Z to undo, E to erase,
W/K for whiteboard/blackboard, Ctrl+S to save a PNG to the user's screenshots
folder, Esc or right click to exit.

## Where Ctrl+S saves

| System | Destination |
| --- | --- |
| Windows | The user's special **Pictures** folder plus `Screenshots`, honoring redirects such as OneDrive |
| macOS | The `location` preference in `com.apple.screencapture`; if absent, the user's Desktop |
| Linux (X11 and Wayland) | XDG User Directories `PICTURES` folder plus `Screenshots` |

On Linux, the setting is read from `$XDG_CONFIG_HOME/user-dirs.dirs` or, when
`XDG_CONFIG_HOME` is unset, `~/.config/user-dirs.dirs`. For example,
`XDG_PICTURES_DIR="$HOME/Pictures"` resolves to `~/Pictures/Screenshots`. If the
Pictures folder is missing or disabled, configure it in the desktop preferences
or with the distribution's XDG tool before saving. There is no universal
destination shared by other screenshot tools.

The setting is checked on every Ctrl+S. The app creates the destination when
needed, uses unique names and never overwrites existing captures. On Linux and
macOS, new PNGs are readable and writable only by the user (0600). The final path
or error appears in the overlay and, when available, in the terminal. On error,
the app does not silently save to the working directory or another folder.

The PNG contains the overlay scene and annotations, without the cursor or save
message. In transparent X11 draw mode, the PNG background is transparent and
exports annotations without recapturing the desktop. To save the frozen screen
with annotations, use zoom; W/K explicitly export a white or black board.

## Settings

The `settings.json` file is optional; without it, the defaults below apply. The
application only reads the file, on every overlay, and never creates or rewrites
it. `pixelpointer probe` shows the effective path and whether the file was loaded.

| System | Path |
| --- | --- |
| Windows | `%APPDATA%\PixelPointer\settings.json` |
| macOS | `~/Library/Application Support/PixelPointer/settings.json` |
| Linux | `$XDG_CONFIG_HOME/pixelpointer/settings.json` or `~/.config/pixelpointer/settings.json` |

```json
{
  "version": 1,
  "pen": { "color": "red", "width": 6 },
  "zoom": { "initial": 2 }
}
```

| Field | Values | Default |
| --- | --- | --- |
| `version` | `1` | `1` |
| `pen.color` | `red`, `green`, `blue`, `yellow`, `orange`, `pink` | `red` |
| `pen.width` | 1 to 60 | 6 |
| `zoom.initial` | 1 to 16, magnification when zoom opens | 2 |
| `rendering.gpu` | `true` or `false`; draws with OpenGL | `false` |

Omitted fields use the default; `//` comments and trailing commas are accepted.
An unknown field, an out-of-range value, malformed JSON, a file larger than
64 KiB or one that is not UTF-8 invalidates the whole file: the overlay opens with
the defaults and the reason is shown in the terminal and by `probe`. Keys still
change color and width during the session without writing to the file.

`rendering.gpu` is experimental and off by default. When on, the overlay tries
OpenGL and falls back to CPU rendering, with a terminal notice, if the OpenGL
window or context cannot be created. Transparent X11 drawing and
`SDL_RENDER_DRIVER=software` stay on the CPU. The `quadros` line in the terminal
reports the path used.

## Permissions per system

### Linux on Wayland (GNOME, KDE, Hyprland)

Wayland does not let a program see other programs' screen or keyboard;
PixelPointer uses the desktop's XDG portals.
Draw mode uses the capture authorized by the portal and remains frozen;
transparent live content is not implemented by this backend.

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
The `pixelpointer grant` command is not needed on X11. If another app already
registered Ctrl+1 or Ctrl+2, the daemon reports the conflict and does not claim
that hotkey is available. In draw mode, a detected X11 compositor enables
transparent annotations over current content. Without a compositor, the app
warns and uses a frozen capture; this is not live transparency.

### Running in the background and exiting

Esc or closing the overlay ends only the visual session; the daemon remains
active. Use `pixelpointer status` to check it and `pixelpointer quit` to stop it,
including while an overlay is open. The tray menu offers **Zoom**, **Desenhar**
and **Sair** when SDL and the desktop provide a tray. Ctrl+C or SIGTERM also
stops the daemon. If the tray is unavailable, use `status` and `quit`; GNOME may
require a tray extension.

`Tray registrada` means the backend registered the tray, not that the panel
actually displays it. The daemon should remain running after this message.
When started from a terminal, `pixelpointer daemon` runs in the foreground and
occupies that terminal; Ctrl+C stops it. On Linux, append `&` to release the
prompt:

```bash
pixelpointer daemon &
```

The process may still write to that terminal or stop when its window closes.
To run without a terminal, install the desktop entry and launch
**PixelPointer** from the applications menu. This does not enable autostart.

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

If the daemon is running, stop it first with `pixelpointer quit`. Then remove it
using the same method used to install it:

| Installed with | Command or action |
| --- | --- |
| `install.sh` (Linux/macOS, default prefix) | On Linux, run `pixelpointer uninstall-desktop-file` if you registered the app menu entry; then run `rm -f -- "$HOME/.local/bin/pixelpointer"` and `rm -r -- "$HOME/.local/lib/pixelpointer"` |
| `install.sh --prefix <dir>` | Remove `<dir>/bin/pixelpointer` and `<dir>/lib/pixelpointer`, replacing `<dir>` with the installation prefix |
| `install.sh --global` | Run `sudo rm -f -- /usr/local/bin/pixelpointer` and `sudo rm -r -- /usr/local/lib/pixelpointer`; on Linux, first remove the desktop entry with the installed binary |
| `install.ps1` | In PowerShell, run `Remove-Item -LiteralPath "$env:LOCALAPPDATA\Programs\pixelpointer" -Recurse` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

If you manually added the PowerShell startup line to your profile, remove that
line from `$PROFILE.CurrentUserAllHosts`. On Linux, remove the desktop
integration before the executable with `pixelpointer uninstall-desktop-file`,
using the same `XDG_DATA_HOME`; the command preserves modified files. Uninstalling
does not delete PNG captures. The `.deb` package separately manages resources
installed under `/usr/share`. On GNOME, bound hotkeys can be removed in Settings;
the capture permission with:

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
| App ID Registry registration failed | Check `pixelpointer probe`; an unavailable Registry is reported as a portal limitation, not as a reason to reinstall the desktop entry |
| Hotkeys unavailable on Wayland | Desktop without the GlobalShortcuts portal: use a desktop shortcut (table above) |
| The window opens but gets no keyboard input | Compositor without an activation token; click the window once |
| Tray registered, but no icon appears in GNOME | Confirm `pixelpointer status` responds and the daemon has not crashed. Panel visibility depends on StatusNotifier/AppIndicator support; use the CLI if unavailable. The app does not install extensions. |
| Ctrl+S reports an invalid or inaccessible destination | Check the screenshots-folder setting and permissions; the app does not save to a fallback location |
| Daemon aborts after tray registration with `_dbus_check_is_valid_utf8` | Use a package rebuilt with the SDL 3.5.0 tray-text lifetime fix; an aborted daemon also releases its hotkeys |
| `selftest falhou` on Linux | glibc older than 2.34 or missing `libfontconfig`: use Ubuntu 22.04, Debian 12, RHEL 9 or newer and install `libfontconfig1` |
| ~0.5 s delay when opening on Wayland | Cost of the Screenshot portal; continuous PipeWire capture is planned for F2 |

Program messages are currently in Portuguese. To report a problem, include the
output of `pixelpointer probe` and follow [support](../../.github/SUPPORT.md).

### Recovering after a daemon abort

A native abort can leave the socket on disk even though the process and X11
grabs have ended. For safety, the next daemon refuses that path and does not
remove it automatically. Run `pixelpointer status` and check your processes with
the system monitor. Only after confirming that no daemon is active and that the
path belongs to your user, remove **only** `$XDG_RUNTIME_DIR/pixelpointer.sock`
(when `XDG_RUNTIME_DIR` is set) or
`~/.cache/pixelpointer/run/pixelpointer.sock` (fallback). Do not remove
directories or sockets belonging to other apps. Restart using the corrected
package.

## Current limitations

- Break timer (Ctrl+3), Live Zoom (Ctrl+4), recording (Ctrl+5), snip (Ctrl+6)
  and DemoType (Ctrl+7) do not exist yet; see the [plan](../plano-implementacao.md).
- With several monitors on Wayland, the compositor picks the window's monitor.
- macOS has no native global hotkeys and no signature in version 1.

## Manual X11 acceptance test

Use a **real X11 session**, not `SDL_VIDEODRIVER=dummy`, with test content that
contains no personal data. The test requires the locally built Native AOT
`linux-x64` binary (build the matching RID for another architecture). From the
repository root, build and validate in containers:

```bash
pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot -Engine podman
pwsh ./tools/scripts/Publish-Aot.ps1 -Rid linux-x64 -Engine podman
pwsh ./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64 -Engine podman
```

Expected: build and tests pass and the package is valid. These commands create
local artifacts; they do not install the app on the host or validate graphics.
Docker can replace Podman by setting `-Engine docker`.

In the user's graphical session, from the repository root, use the generated
package without `sudo`:

```bash
./artifacts/publish/linux-x64/pixelpointer probe
./artifacts/publish/linux-x64/pixelpointer grant
./artifacts/publish/linux-x64/pixelpointer daemon
```

1. **Diagnostics:** `probe` should identify X11 and report whether a compositor
  is present. `grant` should explain that native X11 capture needs no portal
  permission. Missing Registry must not recommend reinstalling the desktop
  entry; real registration errors must remain distinguishable.
2. **Hotkeys and capture:** press Ctrl+1, annotate a known image, zoom and close
  with Esc. The daemon should remain active. Compare with
  `./artifacts/publish/linux-x64/pixelpointer zoom` in another terminal. Repeat
  Ctrl+1 and Ctrl+2 with all four CapsLock/NumLock combinations.
3. **Live drawing:** with a compositor, show a clock or animation and press
  Ctrl+2. Content should continue moving under the strokes, without an opaque
  background. W/K should change only explicitly selected boards. Without a
  compositor, expect a warning and frozen capture, not transparency.
4. **PNG:** press Ctrl+S twice; check the message, two distinct files and the XDG
  destination. In zoom, the PNG should contain the capture and strokes; in
  transparent draw mode, it should preserve alpha. With an unwritable
  destination, expect an overlay error and no file in another folder. Use an
  unprivileged user for this test.
5. **Tray and exit:** open and close Zoom/Desenhar from the tray. Choose **Sair**
  with and without an overlay open. If the tray is unavailable, confirm the
  warning and run `./artifacts/publish/linux-x64/pixelpointer quit` in another
  terminal. Check `status` before and after; after exit it should return 1 and
  no daemon should remain.
6. **Single instance and restart:** with the daemon active, run `daemon` from
  another terminal. It should fail without affecting the first instance. Stop
  and restart the first; hotkeys should return without stale grabs.
7. **Menu without a terminal:** stop the daemon, run `install-desktop-file` with
  the same binary and open **PixelPointer** from the applications menu. Check
  the icon, Ctrl+S feedback and exit through tray/CLI, without a terminal. If an
  existing entry conflicts, preserve it and review the message; do not delete
  files to force installation. Undo only this integration with
  `uninstall-desktop-file` from the same binary.
8. **Conflicts and monitors:** in a test session, reserve Ctrl+1 for another app
  before starting the daemon; confirm the conflict and that Ctrl+2 still works
  if available. Repeat capture, stroke alignment and saving across monitors
  with different scales/resolutions and negative positions.

Record OS, architecture, desktop/compositor, PixelPointer version, scale,
monitors and each step's result. Do not publish sensitive captures, tokens or
full logs. Compositor, tray, physical hotkeys and multiple monitors require
manual acceptance; SDL dummy does not validate them.
