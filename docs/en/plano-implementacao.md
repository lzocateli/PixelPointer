# Implementation Plan — PixelPointer

[Português (Brasil)](../plano-implementacao.md) · **English** · [Español](../es/plano-implementacao.md)

| Field | Value |
| --- | --- |
| Initial status | Approved for F0 (decisions of 2026-10-29) |
| Plan status (2026-11-04) | **Active**. Section 8 is the sole source for implementation, validation, decision and documentation backlog |
| Delivery status (2026-11-04) | v0.3.1 is the latest stable release. F0/F1 are delivered; zoom, annotation, timer, snipping, Live Zoom and GIF are implemented. The 2026-11-04 GNOME/X11 acceptance covers the features available in a local package, but is not a published release or cross-platform validation. See [consolidated pending items](#8-consolidated-pending-items) |
| First functional version | v0.1.2, accepted as stable by the maintainer on 2026-10-30 on Ubuntu 24.04.5/GNOME X11 and Ubuntu 26.04/GNOME Wayland; manual acceptance limited to recorded flows/environments, without completing F2/F3 or validating Windows/macOS |
| Origin | AI-assisted |
| Human reviewer | Lincoln Zocateli |
| Repository | <https://github.com/lzocateli/PixelPointer> |
| License | MIT |

**Additional acceptance on 2026-10-30:** the requester confirmed transparent
drawing, zoom, PNG export with alpha, CapsLock/NumLock and use on both 1920×1080
monitors at scale 1 on Ubuntu 24.04.5/GNOME X11, authorizing those tests to be
considered complete. The [evidence and limitations](plano-e2e-grafico-uv.md#manual-acceptance-of-transparent-drawing-on-gnome-x11)
separate manual acceptance from automated E2E. At the time of that acceptance,
F3 was incomplete; shapes, text and blur were implemented later. Current F3 gaps
are Wayland multi-monitor support and platform/topology validation, as described
in section 8. Additional screenshots from the requester on the same date show annotations
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

**Preview acceptance on 2026-10-31:** the requester tested
`0.1.3-preview.14` on Ubuntu 26.04.1 LTS x64/GNOME Wayland, attached a
ScreenCast consent screenshot and a PipeWire capture log, and explicitly
confirmed that zoom and drawing work. Capture, zoom and drawing have manual
acceptance on this machine. The PNG was 1920×1080; the portal took
20,830 ms including dialog interaction, the first frame 122 ms and the
total 20,999 ms. This does not prove hotkey-to-first-frame < 150 ms, restore
token reuse, indicator duration, physical hotkeys, tray, launcher, Ctrl+S
or live transparency. Other monitors/desktops and Windows/macOS do not
inherit acceptance. [Evidence and limits](plano-e2e-grafico-uv.md#manual-preview-acceptance-on-gnome-wayland).

## 1. Objective

**Maintainer evolution requirement (2026-10-31):** further implementations must
preserve accepted experience and quality and be limited to legitimate improvements
and fixes. Known regressions block integration; experimental changes do not become
defaults without evidence. Each increment must validate its scope and affected
behavior and record what was not tested.

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
| D13 | ScreenCast on Wayland (2026-10-31, maintainer decision) | **On-demand** session per capture, closed after the first frame, so the sharing indicator is not permanent; keeping the session open will only be evaluated with measurements. System `libpipewire-0.3` (MIT) through `LibraryImport`, not redistributed. Restore token in `$XDG_STATE_HOME/pixelpointer` (0600, never logged). Single monitor only; otherwise, or on failure, the Screenshot portal. User cancellation is not bypassed. **Revision 2026-11-01 (maintainer decision, option A):** on GNOME capture always uses the Screenshot portal, because on GNOME 46 the sharing indicator stayed stuck after short ScreenCasts; other compositors keep ScreenCast |

## 3. Architecture

The diagram below records the target architecture. The current implementation
contains `PixelPointer.App`, `PixelPointer.Core` and `PixelPointer.Rendering`;
the `PixelPointer.Platform*` abstractions/projects listed below have not yet
been extracted from the app.

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

**F2 status (2026-11-04):** `src/PixelPointer.Core` holds the annotation
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
before making it the default. On Wayland outside GNOME, capture uses on-demand
ScreenCast + PipeWire (D13): 36 ms on isolated Wayland with sway, against ~550 ms
for Screenshot on GNOME in F0. Capture, zoom and drawing had manual acceptance
on real GNOME on 2026-10-31; the first frame took 122 ms, but portal timing
included human consent; these are not equivalent benchmarks. The main capture,
settings, model, rendering and annotation tools are implemented. Remaining F2
criteria and the decision about platform abstractions are centralized in
[section 8](#8-consolidated-pending-items).

**F2 diagnostics increment (2026-10-31):** capture now reports session creation,
selection, start, authorization persistence, PipeWire remote opening and frame
reading/conversion times. It preserves the legacy `portal` and `primeiro quadro`
totals; the latter includes remote opening, not just reading. It only reports
whether restoration was requested, never the token. Consent, timeout, closure
and fallback flow remain unchanged. Deterministic tests cover formatting; these
times do not include every step from hotkey to presentation.
[Interpretation](usage.md) and real GNOME benchmarking remain separate; this
diagnostic does not complete F2.

**Grouped capability queries (F2, 2026-10-31):** version, source types and cursor
modes are read with `Properties.GetAll`, reducing three individual reads to
one when the response is compatible. Values are not cached. Missing, incomplete
or unsupported replies retain the previous `Get` path on that connection with
the same order and early exits; the initial incompatible probe costs one extra
call. `ScreenCast capacidades` reports timing and the method used. Tests cover
types, missing properties, monitor support, legacy reads and cancellation.
Consent and capture flow remain unchanged; benchmarking and the compatibility
matrix remain pending.

**Controlled-denial compatibility (2026-11-01):** the
`Test-WaylandScreenCast.ps1` orchestrator now accepts `-DenyScreenCastGetAll`,
injecting a mandatory policy only into disposable D-Bus. The normal scenario
passed with `GetAll`; the denial scenario confirmed two bus rejections, `Get`
reads, two captures through real ScreenCast/PipeWire portals and an updated frame.
Production code, portals and consent were not changed. This does not mean
the portal naturally rejects `GetAll`: it is audited fault injection.
GNOME/KDE and naturally incompatible replies remain pending. Repeated capture
on one connection was validated in the isolated scenario below.
[Evidence and command](plano-e2e-grafico-uv.md#getall-compatibility-under-controlled-denial).

**Daemon repetition (2026-11-01):** `-RepeatDaemonCaptures` started one daemon
and exercised drawing and zoom through IPC. A D-Bus monitor restricted to
property calls confirmed fresh capabilities: two `GetAll` calls in control,
or one denied probe followed by six ordered `Get` calls, without a second
`GetAll` attempt. Frame colors changed, the daemon stayed active after the
compositor closed each window, and `quit` exited normally. Production code
did not change in this step. This does not prove physical hotkeys, Esc, tray,
quit with an open overlay or GNOME/KDE coverage.
[Record](plano-e2e-grafico-uv.md#repeated-captures-on-the-daemon-connection).

The same binary is both app and CLI: `pixelpointer zoom|draw|timer <min>|snip`
sends a command through a Unix socket (`$XDG_RUNTIME_DIR` or temporary directory;
AF_UNIX also on Windows 10+) to the running instance.

## 4. Platform Capability Matrix

| Capability | Windows | macOS | Linux X11 | Linux Wayland |
| --- | --- | --- | --- | --- |
| Global hotkey | RegisterHotKey | RegisterEventHotKey | XGrabKey | GlobalShortcuts portal (**validated on GNOME 50**); desktop shortcut + IPC fallback |
| Static capture | GDI | ScreenCaptureKit (Screen Recording permission) | XGetImage/XShm | Screenshot on GNOME; on-demand ScreenCast/PipeWire on other compositors with one monitor; Screenshot for multiple monitors or fallback |
| Full-screen overlay | SDL3 | SDL3 + window level | SDL3 | Native SDL3 Wayland (**validated**, fractional scaling works) |
| Live Zoom | Implemented with Magnification API | Implemented through ScreenCaptureKit | Implemented as a window lens | Best effort through ScreenCast and cursor metadata; one monitor, compatible portal, disabled on GNOME by D13 |
| Recording | GIF implemented | Not implemented | GIF implemented | GIF implemented through a ScreenCast session |
| DemoType | Not implemented | Not implemented | Not implemented | Not implemented; RemoteDesktop/libei remains a possibility, not existing support |

## 5. Phases

The phases below summarize progress; actionable work and validation criteria
are listed only in [section 8](#8-consolidated-pending-items).

| Phase | Status | Summary |
| --- | --- | --- |
| **F0 — Spike** | Complete | Go/no-go report ([pt-BR](../f0-relatorio.md)) |
| F1 — Foundation | Complete | Governance, CI, releases and initial documentation delivered |
| F2 — Core + fast capture | Partial | Core, capture and settings implemented; remaining criteria are in section 8 |
| F3 — Zoom + Drawing | Partial | Main tools implemented; Wayland/platform coverage remains incomplete |
| F4 — Timer + Snipping | Implemented | Local GNOME/X11 manual acceptance on 2026-11-04 |
| F5 — Live Zoom | Partial | Windows/macOS/X11 implemented; Wayland is best effort |
| F6 — Recording | Partial | GIF implemented on Windows and Linux X11/Wayland |
| F7 — DemoType | Not started | No implementation yet |
| F8 — Distribution | Partial | Current channels delivered; additional formats await a scope decision |
| F9 — i18n and documentation | Partial | Current docs in pt-BR/en/es; legacy documents still need translations |

## 6. Risks

Active risks and their next actions are tracked in the single table in
[section 8](#8-consolidated-pending-items); this plan keeps no parallel list.

## 7. Open Questions

Scope decisions and open work are tracked only in
[section 8](#8-consolidated-pending-items).

## 8. Consolidated Pending Items

**Manual X11 test on preview `v0.1.3-preview.25` (2026-11-03):** drawing, zoom, timer, snip, Live Zoom and GIF recording were exercised (evidence attached by the maintainer). The maintainer clarified that starting Live Zoom at the mouse position is already satisfied by centering the captured region on the cursor; the feedback does not require a behavior change. The recording test does not constitute formal acceptance.

This is the single source for implementation, validation, decision and
documentation backlog. E2E plans and acceptance records retain evidence and
historical limits without maintaining a separate task list. Updated 2026-11-04;
historical PRs remain in the repository and graphical results are recorded in the
[E2E plan](plano-e2e-grafico-uv.md).

| ID | Type | Status | Open work and completion criterion |
| --- | --- | --- |
| IMPL-01 | Implementation | Partial | Implement Wayland monitor capture/selection while preserving GNOME Screenshot policy; verify output selection and cropping on a real compositor |
| IMPL-02 | Implementation | Open | Make Wayland recording startup asynchronous; portal consent, waits and errors must not block daemon callbacks |
| IMPL-03 | Implementation | Not implemented | Define and implement macOS GIF, MP4 and audio only after selecting components with verified permissive licenses; FFmpeg is not approved |
| IMPL-04 | Implementation | Not started | Design and implement DemoType per platform, including Wayland input-injection permissions and limits |
| IMPL-05 | Implementation/architecture | Not extracted | Decide whether `PixelPointer.Platform*` abstractions remain necessary; if so, extract them from `PixelPointer.App` without duplicating existing adapters |
| IMPL-06 | Implementation | Implemented; awaiting human review of [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md) and validation on Windows and macOS | Opt-in update checking and installation for self-managed installs (`pixelpointer update`, **Atualizações** submenu); `.deb`, Homebrew and winget only notify and direct users to their package manager. Validated on Linux x64 with unit tests, installers, X11 E2E and a real update from GitHub. Requirements and decisions are in the [update plan](plano-atualizacao.md) |
| DEC-01 | Scope decision | Open | Decide whether `.zip`, `.dmg`, AppImage and Flatpak remain in scope; do not implement automatic startup without explicit approval |
| DOC-01 | Documentation | Open | Create en/es translations of the F0 report and historical issue #3 plan |
| VAL-01 | Performance validation | Target unmet | Measure and address GNOME latency: observed capture 520–531 ms and first frame 585–635 ms versus the < 150 ms target; record a decision if the target changes |
| VAL-02 | Performance validation | Open | Measure GPU rendering on real hardware and at 4K; use results to decide whether it can stop being opt-in |
| VAL-03 | Native validation | Open | Define a run compliant with container rules (or obtain explicit authorization for an exception) and complete the Windows checklist for capture, hotkeys, GIF, Ctrl+S, tray and shutdown; record OS/architecture and evidence |
| VAL-04 | Native validation | Open | Define a run compliant with container rules (or obtain explicit authorization for an exception) and validate macOS capture, Live Zoom, permissions, installation and hotkeys; recording is excluded until implemented |
| VAL-05 | Compatibility/E2E | Partial | Run a Wayland matrix on GNOME/KDE and other compositors: natural portal incompatibility, real consent, physical hotkeys and tray. Isolated Sway already covers ScreenCast, recording, lifecycle, controlled `GetAll` denial and scales 1/1.5/2 |
| VAL-06 | Hardware/E2E | Partial | Validate hotkey conflicts, pen/pressure, multiple displays, scales and GPUs on real hardware; X11/Windows and a two-display GNOME/X11 setup have local evidence, not full-matrix coverage |
| VAL-07 | Compatibility | Open | Run and record Linux arm64 tests on suitable hardware |

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
