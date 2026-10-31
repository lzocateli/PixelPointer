# Implementation Plan — PixelPointer

[Português (Brasil)](../plano-implementacao.md) · **English** · [Español](../es/plano-implementacao.md)

| Field | Value |
| --- | --- |
| Initial status | Approved for F0 (decisions of 2026-10-29) |
| Delivery status | F0 validated on Linux/GNOME Wayland, including Native AOT; Windows has partial manual evidence from 2026-10-31 and macOS awaits the manual checklist ([report, pt-BR](../f0-relatorio.md), [pending items](#8-consolidated-pending-items)). F1 (repository, governance, CI/CD and distribution foundation) delivered on 2026-10-30 |
| First functional version | v0.1.2, accepted as stable by the maintainer on 2026-10-30 on Ubuntu 24.04.5/GNOME X11 and Ubuntu 26.04/GNOME Wayland; manual acceptance limited to recorded flows/environments, without completing F2/F3 or validating Windows/macOS |
| Origin | AI-assisted |
| Human reviewer | Lincoln Zocateli |
| Repository | <https://github.com/lzocateli/PixelPointer> |
| License | MIT |

**Additional acceptance on 2026-10-30:** the requester confirmed transparent
drawing, zoom, PNG export with alpha, CapsLock/NumLock and use on both 1920×1080
monitors at scale 1 on Ubuntu 24.04.5/GNOME X11, authorizing those tests to be
considered complete. The [evidence and limitations](plano-e2e-grafico-uv.md#manual-acceptance-of-transparent-drawing-on-gnome-x11)
separate manual acceptance from automated E2E. This does not complete all of F3:
shapes, text, blur, HiDPI and cross-platform parity remain future scope, alongside
real acceptance of Zoom/Draw through the tray, the launcher and review of automated
videos. Additional screenshots from the requester on the same date show annotations
on monitor 1 and the visible GNOME tray icon with the Zoom/Desenhar/Sair menu open;
that visual slice is verified. The requester also confirmed quitting through Sair
and supplied a screenshot showing return to the prompt and `status` reporting no
daemon. That flow has manual acceptance on the same GNOME/X11 session, without
proving startup without a terminal.

**Manual acceptance on Ubuntu 26.04/GNOME Wayland, 2026-10-30:** after installing
release `v0.1.2`, the requester reported that everything worked and attached six
screenshots to this conversation. Acceptance covers the flows exercised in that
session according to the human report. Screenshots show annotations over the
desktop image, white and black boards preserving the same strokes, restoration
of the captured background, the product icon in the launcher and the
Zoom/Desenhar/Sair tray menu. The [detailed record](plano-e2e-grafico-uv.md#manual-acceptance-on-ubuntu-2604gnome-wayland)
separates visual observations from the report: still images do not prove a live
background, PNG alpha, launcher/tray activation, latency or the capture backend.
This does not complete F2/F3 or extend acceptance to other compositors, monitors,
scales, Windows or macOS. Automated Wayland E2E remains pending. Attachments stay
in the conversation and are not copied into the repository.

**Additional transparency evidence on the same date:** the requester attached
two more screenshots, identifying them as evidence of transparent drawing.
Both show red strokes over a browser video, with different content. Transparent
drawing has manual acceptance on this Ubuntu 26.04/GNOME Wayland session,
supported by the human report and visual evidence of the overlay. Screenshots
do not demonstrate continuous playback beneath the same stroke or PNG alpha;
these objective checks remain separate from manual acceptance.

## 1. Objective

PixelPointer is a Native AOT presentation tool (`pixelpointer`) for Linux,
Windows and macOS, with ZoomIt (Sysinternals/PowerToys) functionality: screen
zoom, annotation, break timer, Live Zoom, snipping, recording and DemoType,
triggered by global hotkeys.

Reproducing the ZoomIt brand, icons or interface is not a goal. Its PowerToys
code is MIT and may serve as a behavioral reference, with attribution for adaptations.

## 2. Recorded Decisions

| # | Decision | Value |
| --- | --- | --- |
| D1 | Product and binary name | PixelPointer / `pixelpointer`; app id `io.github.lzocateli.PixelPointer` |
| D2 | License | MIT |
| D3 | Stack | .NET 10 Native AOT; SDL3 (ppy.SDL3-CS) for windows and input; SkiaSharp for drawing; Tmds.DBus.Protocol for XDG portals. Avalonia rejected: it runs through XWayland on Linux, and the product is mostly an overlay window |
| D4 | Wayland | Native X11 → GlobalShortcuts portal → `pixelpointer hotkeys install` (desktop shortcut calling CLI through IPC). Live Zoom and DemoType are best effort |
| D5 | macOS | First version unsigned; Developer ID signing and notarization when available |
| D6 | MP4 on Linux | System `ffmpeg` when available (not bundled); native C# GIF on all three platforms |
| D7 | Location | `~/projetos/PixelPointer`, process and governance following DotnetScope |
| D8 | Threading (F0) | Synchronous `Main`; SDL always on the main thread; asynchronous D-Bus/IPC work on other threads, delivered through a queue. A window loop in an `await` continuation blocks the D-Bus reading thread |
| D9 | First use on GNOME (F0) | Capture permission and hotkey binding require a focused PixelPointer window and the `.desktop` installed with the app id; the installer and welcome window handle this |
| D10 | Signals (F0) | `SDL_HINT_NO_SIGNAL_HANDLERS=1`; SIGINT/SIGTERM handled through `PosixSignalRegistration` |
| D11 | Documentation languages (2026-10-30) | pt-BR is authoritative (`README.md`, `docs/*.md`); translations in `README.en.md`, `README.es.md`, `docs/en/` and `docs/es/`. The current rule in `.github/instructions/documentation.instructions.md` also includes plans and reports, replacing the previous scope limited to onboarding and usage |
| D12 | Packages and platforms (2026-10-30) | Packages include executable, SDL3, libSkiaSharp, SVG icon, `LICENSE` and `THIRD-PARTY-NOTICES.md`; installers create the app directory and command link. Linux minimum glibc 2.34 (required by bundled libSDL3); no `linux-musl` (SDL3-CS provides no musl binary) |
| D13 | ScreenCast on Wayland (2026-10-31, maintainer decision) | **On-demand** session per capture, closed after the first frame, so the sharing indicator is not permanent; keeping the session open will only be evaluated with measurements. System `libpipewire-0.3` (MIT) through `LibraryImport`, not redistributed. Restore token in `$XDG_STATE_HOME/pixelpointer` (0600, never logged). Single monitor only; otherwise, or on failure, the Screenshot portal. User cancellation is not bypassed |

## 3. Architecture

```text
src/
  PixelPointer.Core/              annotation model, tools, undo/redo, settings (JSON source-gen), commands
  PixelPointer.Rendering/         Skia: strokes, shapes, text, blur, zoom/pan, timer; CPU and GPU (GL)
  PixelPointer.Platform/          interfaces: IHotkeyService, IScreenCapture, ILiveCapture, IClipboard, IInputInjector, IRecorder
  PixelPointer.Platform.Windows/  Win32 RegisterHotKey, DXGI/Windows.Graphics.Capture, Media Foundation, SendInput
  PixelPointer.Platform.MacOS/    Carbon RegisterEventHotKey, ScreenCaptureKit, AVFoundation, CGEventPost
  PixelPointer.Platform.Linux/    X11 (XGrabKey/XShm/XTest), portals (GlobalShortcuts/Screenshot/ScreenCast/RemoteDesktop), PipeWire
  PixelPointer.App/               AOT executable: tray, overlay, settings, CLI/IPC (currently holds code promoted from the spike)
tests/
  PixelPointer.UnitTests/         xunit.v3 + Microsoft.Testing.Platform
  PixelPointer.IntegrationTests/  deterministic rendering compared with reference images
```

**F2 in progress (2026-10-31):** `src/PixelPointer.Core` now holds the annotation
model (`AnnotationDocument`: strokes, color, width, board, undo and clear), without
SDL or Skia, covered by unit tests. The overlay uses this model without changing
hotkeys or appearance. Read-only settings in `settings.json` (initial color and
width, initial zoom; JSON with a source generator) are described in the
[usage guide](usage.md#settings). `src/PixelPointer.Rendering` (`SceneRenderer`)
draws background, board and strokes independently of the window, with CPU image
tests; the overlay keeps only the cursor and messages. GPU rendering (Skia on
SDL's OpenGL) exists as an experimental opt-in (`rendering.gpu`) with automatic
CPU fallback. In a container with Xvfb and Mesa llvmpipe (GLX), OpenGL zoom
produced pixels identical to the CPU path; Skia's native OpenGL interface may be
unavailable with EGL (Wayland), in which case the CPU is used. Manual acceptance
on real hardware and 4K measurement are required
before making it the default. On Wayland with one monitor, capture uses on-demand
ScreenCast + PipeWire (D13): 36 ms on isolated Wayland with sway, against ~550 ms
for Screenshot on GNOME in F0; measurement on real GNOME is pending. Shapes, text, configurable hotkeys and the other
modules above remain pending.

The same binary is both app and CLI: `pixelpointer zoom|draw|timer <min>|snip`
sends a command through a Unix socket (`$XDG_RUNTIME_DIR` or temporary directory;
AF_UNIX also on Windows 10+) to the running instance.

## 4. Platform Capability Matrix

| Capability | Windows | macOS | Linux X11 | Linux Wayland |
| --- | --- | --- | --- | --- |
| Global hotkey | RegisterHotKey | RegisterEventHotKey | XGrabKey | GlobalShortcuts portal (**validated on GNOME 50**); desktop shortcut + IPC fallback |
| Static capture | DXGI/WGC | ScreenCaptureKit (Screen Recording permission) | XShm | Screenshot portal (**validated**, ~550 ms) → continuous ScreenCast/PipeWire |
| Full-screen overlay | SDL3 | SDL3 + window level | SDL3 | Native SDL3 Wayland (**validated**, fractional scaling works) |
| Live Zoom | Magnification API or WGC | ScreenCaptureKit excluding its own window | XComposite (complex) | ScreenCast + layer-shell where available; GNOME best effort |
| Recording | Media Foundation | AVFoundation | System ffmpeg / GIF | ScreenCast + ffmpeg / GIF |
| DemoType | SendInput | CGEventPost (Accessibility) | XTest | RemoteDesktop portal/libei, best effort |

## 5. Phases

| Phase | Delivery | Exit criterion |
| --- | --- | --- |
| **F0 — Spike** | SDL3+Skia+AOT, capture, hotkeys, per-platform IPC | Go/no-go report ([pt-BR](../f0-relatorio.md)) |
| F1 — Foundation | Public repository, Altivy.Groundwork (governance, release-please, gitleaks, policy), CI matrix, publish all 6 RIDs with SBOM and attestations, `.deb`, installers, Homebrew and winget, pt/en/es docs; spike becomes `src/PixelPointer.App` | Green CI on all 3 OSs |
| F2 — Core + fast capture | Annotation model, Skia **GPU** rendering, settings, platform abstractions, IPC/CLI, **ScreenCast/PipeWire on Wayland** (target: hotkey → first frame < 150 ms), ScreenCaptureKit on macOS, welcome window (D9) | Unit and image tests; measured latency |
| F3 — Zoom + Drawing | Complete Ctrl+1/Ctrl+2: shapes (line, arrow, rectangle, ellipse), highlighter, blur, text, multi-monitor, HiDPI, copy/save | Manually validated functional parity |
| F4 — Timer + Snipping | Ctrl+3 and Ctrl+6 (clipboard and file) | — |
| F5 — Live Zoom | Windows → macOS → X11; Wayland best effort | Documented matrix |
| F6 — Recording | Native GIF; per-platform MP4; optional Linux ffmpeg; audio | — |
| F7 — DemoType | Windows, macOS, X11; Wayland through portal | — |
| F8 — Distribution | winget/zip, `.dmg` (unsigned in v1) + Homebrew cask, `.deb`/AppImage/Flatpak, autostart, `.desktop` | Clean installation on all 3 OSs |
| F9 — i18n and documentation | pt-BR and en, per-OS permission guide | — |

## 6. Risks

| Risk | Mitigation |
| --- | --- |
| Wayland capture latency (~550 ms through Screenshot portal) | ScreenCast with PipeWire and restore token in F2 |
| CPU drawing insufficient at 4K | Skia over OpenGL (GRContext) in F2; CPU fallback |
| GNOME requires a focused window for permission dialogs | Welcome window (D9) |
| Unsigned macOS: TCC permissions lost between versions | Document; sign when Developer ID is available (D5) |
| Different portal behavior on KDE/Hyprland/Sway | Test in F2; IPC fallback always available |
| Native SDL3/libSkiaSharp libraries beside executable | Bundle; assess static linking in F8 |

## 7. Open Questions

1. Wayland multi-monitor: capture covers the whole desktop, while the compositor
   chooses the full-screen monitor; selecting the cursor monitor requires
   per-monitor ScreenCast (F2).
2. AOT size on Windows and macOS (Linux: 19.2 MB including SDL3 and Skia; startup < 10 ms).
3. Windows and macOS behavior (awaiting section 6 of the manual report).

## 8. Consolidated Pending Items

Updated on 2026-10-31. The [issue #3 plan, pt-BR](../plano-correcao-issue-3.md)
was closed together with the issue and remains as a historical record. Its open
items and those of the [E2E plan](plano-e2e-grafico-uv.md) are tracked here, in
addition to phases F2 to F9 in section 5.

| Pending item | Source | Status |
| --- | --- | --- |
| Manual acceptance of Zoom and Draw triggered from the tray on GNOME X11 | Issue #3, 3.5 | Icon, menu and Sair accepted; both actions still unconfirmed |
| Launch from the GNOME applications menu without a terminal | Issue #3, 3.6 | Passed in E2E with a synthetic launcher; real acceptance missing |
| Wayland matrix per compositor: portals with and without Registry, denied permissions, tray and isolated E2E with libei/EIS | Issue #3, 3.1, 3.3 and 5; E2E, 8 | Manual acceptance only on Ubuntu 26.04/GNOME |
| Native Ctrl+S: Windows Known Folder, including OneDrive, and macOS preference, with a real filesystem per OS | Issue #3, 3.7 | Closed by maintainer decision on 2026-10-31; logic covered by tests, no recorded native validation |
| Full manual checklist on Windows | F0; issue #3 | Partial evidence on 2026-10-31, described below |
| Full manual checklist on macOS | F0; issue #3 | No native evidence |
| Human review of X11 E2E videos and references | E2E, 6 and 8 | Closed by maintainer decision on 2026-10-31, with no recorded review |
| Physical hotkey conflict with another application, fractional scaling, other topologies and GPUs, pen | Issue #3, 3.2 and 3.3; E2E, 8 | Pending |
| Linux arm64 on suitable hardware | E2E, 8 | Pending |

**Partial Windows evidence on 2026-10-31:** the requester attached four
screenshots in this conversation. They show installation through `install.ps1`
with SHA-256 verification, the daemon registering Ctrl+1 and Ctrl+2, the tray
menu with Zoom, Desenhar and Sair, and annotations on the screen. They also show
two guidance problems, already documented in the [README](../../README.en.md#installation):
a stale `PATH` in the current PowerShell session and `profile.ps1` blocked by the
`Restricted` execution policy. The screenshots identify neither the Windows
version nor the architecture, and do not prove zoom, Ctrl+S with the Known
Folder, exit through Sair or background execution. The attachments remain in the
conversation and were not copied into the repository.