# Isolated Graphical E2E Plan with uv and Video Evidence

[Português (Brasil)](../plano-e2e-grafico-uv.md) · **English** · [Español](../es/plano-e2e-grafico-uv.md)

| Field | Status |
| --- | --- |
| Date | 2026-10-30 |
| Human authorization | Requester, in this conversation: authorization for isolated graphical E2E under the project rules |
| Authorized decision | Allow a separate, isolated automated graphical suite |
| Execution status | First X11 increment implemented and executed; 10/10 stability; human video review closed by maintainer decision on 2026-10-31, with no recorded review. Additional manual Ubuntu 26.04/GNOME Wayland acceptance recorded below; not automated E2E |
| Functional reference version | v0.1.2, first functional version accepted as stable by the maintainer on 2026-10-30 on recorded GNOME X11/Wayland environments; not approval of the complete cross-platform matrix |
| First target | Linux x64, X11 in a disposable desktop |
| Expansion | GNOME X11, per-compositor Wayland and other systems after feasibility is established |

## 1. Objective and Limits

Exercise the Native AOT package as a user: start it, trigger global hotkeys,
draw, save, interact with the tray and quit. Produce verifiable results, video
and enough data to repeat a run and diagnose regressions.

The [README](../../README.en.md) defines the product; the [usage guide](usage.md)
defines commands and behavior. The [testing rules, pt-BR](../../.github/instructions/testing.instructions.md)
record authorization. This plan does not replace these contracts or the
[issue #3 plan, pt-BR](../plano-correcao-issue-3.md).

Do not implement future features merely to test them. Timer, Live Zoom,
recording by the product itself, snipping and DemoType are outside initial
coverage. Recording in this plan belongs to the test environment, not PixelPointer.

## 2. Hypotheses and Falsifying Checks

| Hypothesis | How to falsify |
| --- | --- |
| Simulated X11 input exercises the real global path | With another window focused, send Ctrl+1/Ctrl+2 through the X server and require the overlay to open; IPC is not a substitute |
| Virtual environment supports transparent drawing | Animate a background region, keep a stroke over it and verify background changes with the stroke preserved; still images or SDL `dummy` do not prove this |
| Tray works from the user's perspective | Locate its panel icon and physically click entries; isolated D-Bus queries/events prove only the protocol |
| Video documents the entire interaction | Record before the first action; check decodable frames, duration and final steps |
| Suite detects regressions | Cause a controlled failure, such as stopping the daemon, and require failure with preserved evidence |
| Execution is repeatable | Repeat from a clean environment with the same versions/references, without manual intervention |

## 3. Environment and Isolation

Architecture implemented in the first increment:

```text
Docker/Podman on host: orchestration only
├── Test desktop in container
│   ├── Virtual X11 server
│   ├── Window manager, compositor and StatusNotifier panel
│   ├── Private D-Bus and disposable configuration
│   ├── Native AOT package and synthetic visual scene
│   ├── Python runner executed with uv
│   └── Screen capture and video encoding
└── nginx in container: exclusive edge for any HTTP service
```

- Do not mount host X11/Wayland sockets, D-Bus, home directories, secrets or input
  devices. Do not use `--privileged`, host networking or global host injection.
- Use an unprivileged user, private network, resource limits and overall deadline.
  Mount the package read-only; limit writes to the run directory.
- Isolate `HOME`, XDG settings, runtime, captures and preferences per scenario.
  Cleanup must not delete another run's resources, including after failure.
- Xvfb is a candidate X11 server, not a complete desktop. Confirm required
  composition/rendering features before approving the image.
- Keep nginx in runs with the application. Remote viewing or HTTP helpers are
  accessed only through that edge, without direct ports. Native automation and
  D-Bus use private channels, not HTTP.
- Do not require HTTP, a browser or Playwright to control SDL. Remote viewing is
  optional and does not gate the first delivery.

## 4. Runner, Dependencies and Reproducibility

The Python project is in `tests/e2e/`, with shared preparation/evidence collection.
The [runbook, pt-BR](../../tests/e2e/README.md) records commands, versions, licenses,
threats and recovery. Python, manifest and lockfile are pinned; build uses
`uv sync --locked`, offline runtime uses `uv run --locked --no-sync`, without
automatic dependency updates.

Before selecting each component:

1. Verify the exact version's permissive license and transitive dependencies,
   maintenance and MIT distribution compatibility.
2. Pin uv, Python, native tools and image digest.
3. Record sources, licenses and versions; reject components without verified licenses.

Do not assume any FFmpeg distribution, codec or automation library is permitted.
Choose recorder and codec only after verification. uv resolves Python dependencies,
but does not by itself pin the graphical server, fonts or codecs.

On X11 prefer XTEST events through the graphical server. Internal calls or injected
SDL events do not replace global hotkeys. Use coordinates relative to known
windows/regions within controlled geometry. Pin resolution, scale, theme, fonts,
locale, timezone and rendering. Use a deterministic synthetic scene with shapes,
colors and an animated region, without personal data. Wait for conditions with
deadlines; avoid sequences relying only on `sleep`.

## 5. Current Feature Scenario Matrix

| Group | Actions | Required assertions |
| --- | --- | --- |
| Daemon and IPC | Start, status, attempt second instance | Availability, duplication rejected, original process preserved |
| Global hotkeys | Ctrl+1/Ctrl+2 with external focus, CapsLock/NumLock toggled | Expected window/mode; daemon preserved |
| Conflict | Another test client registers a combination before daemon | Explicit error; unavailable grab not reported as success |
| Zoom | Wheel, arrows, mouse movement | Capture scale/region change according to action |
| Annotations | Drag; R/G/B/Y/O/P; Ctrl+wheel | Geometry, colors and thickness within defined tolerances |
| Editing | Ctrl+Z and E | Last stroke/all strokes removed, respectively |
| Boards | W and K | White/black background and annotations according to contract |
| Transparency | Draw over animated scene with compositor | Background keeps changing; strokes persist; PNG has alpha |
| No compositor | Draw without compositor | Explicit warning and frozen capture; no transparency claim |
| Saving | Repeated Ctrl+S, custom XDG destination with spaces/accents | Decodable PNG, correct folder, unique names, existing files preserved |
| Settings | `settings.json` with color, width and initial zoom; then an invalid file | `probe` loads the file; 4x zoom, thick blue stroke; the invalid file produces a warning, uses defaults and stays untouched |
| Save failure | Invalid configuration or inaccessible destination | Visible error, no alternative destination, usable process |
| Overlay exit | Esc and right click where applicable | Contractual transitions, reopening possible, daemon preserved |
| Graphical tray | Click icon and Zoom/Desenhar/Sair entries | Visible icon, correct menu/action, clean shutdown |
| Unavailable tray | Start without StatusNotifier host | Explicit limitation; status/quit still work |
| Lifecycle | Quit with overlay open, restart, desktop menu launch | Resources released, working restart, no-terminal launch |
| CLI and desktop | Help, version, probe, capture, per-user install/removal | Consistent output/file, product identity, conflicts preserved |

Reuse filesystem, IPC, package and PNG tests without duplicating them wholesale.
E2E adds observable interaction, including on-screen messages. Grant and portal
permissions belong to Wayland expansion, not X11 acceptance. Pen pressure,
fractional scaling and multiple monitors require separate scenarios; mouse and
a single virtual monitor do not validate them.

## 6. Evidence and Acceptance Criteria

Each run needs a unique id and artifact directory, without overwriting earlier
results, containing scenario video captured from the desktop (not assembled from
product PNGs), before/after screenshots and exported PNGs, a readable report and
JUnit XML, a structured action/monotonic-time/check timeline, a package-hash/image-
digest/version/graphics manifest and sanitized test-scoped diagnostics, not full
logs or environment dumps.

Start recording before actions and verify readiness. Stop properly even on failure,
preserving partial video when recoverable. Required recording failure fails the run;
empty video or success without evidence is unacceptable. No audio by default.

Pin and version these first-increment criteria:

- Zero exit only when all required assertions and evidence pass.
- Timeout, crash or skipped mandatory functionality cannot produce success.
- Visual checkpoints with explicit regions, tolerances and references, without
  automatic reference updates.
- PNG dimension, color/alpha and content checks, not existence alone.
- Decodable video covering actions, with duration tolerance based on frame rate.
- Ten clean consecutive proof-of-concept runs without intermittent failure.
- Controlled failure detected, failed report and useful evidence preserved.

Retries must not hide the first failure. Record each attempt; do not relabel unstable
execution as stable acceptance. Define time budgets/video retention during
implementation; preserve failures until analysis and do not publish recordings automatically.

## 7. Steps and Deliverables

1. **Inventory contracts and choose components.** Verify licenses, versions, input
   backend, capture and compositor/panel capability before adding dependencies.
2. **Build the minimum environment.** Disposable desktop, synthetic scene and uv
   runner; image build/smoke, isolation and error cleanup checks.
3. **Vertical proof of concept.** Daemon → Ctrl+1 → drawing → Ctrl+S → Esc →
   icon/menu → Sair. Deliver video, assertions and clean repeatable execution.
4. **Demonstrate regression detection.** Force a failure in a run subprocess;
   verify nonzero exit, video/report. If feasible, use a known-broken previous
   package without modifying/reverting the current worktree.
5. **Expand X11.** Section 5 matrix, with/without compositor and tray, conflicts,
   modifiers and save errors.
6. **Integrate execution and evidence.** One command from repository root,
   prerequisites, results and recovery; host orchestrates containers only.
7. **Expand compatibility.** Real isolated GNOME X11, per-compositor Wayland,
   monitors/scaling and additional architectures after support is established.

Commands, image preparation, locked execution and result inspection are in the
[runbook, pt-BR](../../tests/e2e/README.md). Do not edit `gw-*` or `tools/scripts/ci/`:
those changes belong to the Groundwork catalog. CI integration uses authorized
project-owned surfaces only after local container proof.

## 8. Compatibility, Risks and Pending Acceptance

| Environment | Plan and limit |
| --- | --- |
| Linux x64, minimum virtual X11 | Executed: 10/10 consecutive with compositor/tray; variants without compositor and without tray passed |
| GNOME X11 | Manual acceptance: transparent drawing, automatic visual selection, zoom, PNG, CapsLock/NumLock, both monitors and Sair shutdown; screenshots confirm monitor 1, tray icon/menu and no-daemon status after exit; Zoom/Draw through tray, launcher and full matrix not individually proven |
| Wayland | Manual Ubuntu 26.04/GNOME acceptance after v0.1.2 installation: working exercised flows reported, screenshots of drawing, boards, launcher and tray. Isolated per-compositor E2E, libei/EIS and real portals pending; no consent mocks or XTEST via XWayland as native-path evidence |
| Linux arm64 | Execute on appropriate architecture before claiming coverage |
| Windows/macOS | Native backends and disposable graphical sessions; strategy depends on container compatibility and specific authorization for any exception |
| Pen/GPU/physical monitors | Use on both 1920×1080 monitors at scale 1 manually confirmed; pen, other GPUs, scales/topologies pending |

Main risks: rendering/focus/timing differences, panel without SNI, incomplete virtual
compositor, incomplete video and incompatible dependencies. Distinguish infrastructure
from product failure; both block required scenarios. Do not disable transparency
to pass a scenario promising to verify it.

Final acceptance requires the executed matrix with explicit limits, reproducible
evidence and human review of initial videos/references. Authorization allows building
the suite, not preapproval of tools, security exceptions, cross-platform support or
results that do not exist yet.

### Local Results on 2026-10-30

Steps 1–6 implemented for the first target; step 7 remains conditional expansion,
not declared coverage. Rootless Podman, no access to the requester's real desktop,
no managed-workflow changes.

Final image: `16ddca00a969716ee1db935ea42c0a9200a82051b49a9718345ca0ad6ca513cb`.
Tested executable SHA-256: `eae12d3d269670a7da1914ea028fb7cc478f9aa8e0416b1849134b2338f1eb31`.
Manifests also record library and package-file hashes.

| Check | Result and local directory under `artifacts/e2e/` |
| --- | --- |
| Stability, ten clean desktops | 10/10; `x11-20261030T175407362-09a7e896/attempt-01` through `attempt-10` |
| Deliberate daemon kill | Exit 1, `Daemon interrompido: -9`, failed JUnit and preserved video; `x11-20261030T175704158-6db4df5b/attempt-01` |
| No compositor | Passed with warning and opaque PNG; `x11-20261030T175751758-897e6ec5/attempt-01` |
| No StatusNotifier host | Passed with explicit limitation and control preserved; `x11-20261030T175842166-cb50e0dc/attempt-01` |
| Evidence integrity | 13 directories, 750 decoded JPEG frames; resolution, count, JUnit, JSON and final action coverage verified |
| Collector | 3 tests passed: truncation, empty video, counts and failure preservation |
| .NET quality | Formatting/build without warnings/errors; 59 tests, Native AOT and selftest passed |

E2E exposed a real defect: XGetImage BGRX pixels could have zero in the unused byte,
preserved as zero alpha by SKImage. Zoom appeared black/transparent despite correct
direct capture. Copying now normalizes that byte to 255. A Skia test reproduced
the failure before the fix and passed afterward, as did E2E zoom/transparent drawing.

With a compositor, a 32-bit TrueColor visual is explicitly selected through the
public SDL hint, without GLX. Panel/launcher are synthetic and receive real XTEST
clicks; this does not prove GNOME integration or automatic visual selection.
No remote-viewing server or Playwright: the product is native SDL; nginx is isolated
with an HTTP smoke and no public ports. Human video/reference review and other
environments remain pending.

### Manual Acceptance of Transparent Drawing on GNOME X11

On 2026-10-30 the requester ran the package in their own session, confirmed success
and supplied a drawing PNG. The user performed the test; automation did not access
their graphical session.

- Environment from `probe`: Ubuntu 24.04.5 LTS x64, GNOME/X11, compositor present,
  two 1920×1080 monitors at scale 1.
- Daemon started without `SDL_VIDEO_X11_WINDOW_VISUALID`, `SDL_VIDEODRIVER` or
  `SDL_RENDER_DRIVER`; SDL automatically chose X11 and OpenGL rendering.
- Human confirmation: transparent drawing worked, followed by the requested alpha
  export test. Logs also recorded 3840×1080 zoom capture and two saves in that mode.
- Additional explicit human acceptance on 2026-10-30 covered CapsLock/NumLock and
  use on other monitors; those checks are complete for the reported configuration.
- Three additional screenshots on that date show monitor 1: product panel icon,
  open Zoom/Desenhar/Sair menu, red annotations including handwritten monitor id.
  Icon visibility/menu opening verified. Attachments stay in the conversation,
  not the repository. Displayed backgrounds do not prove or disprove PNG alpha.
- Requester confirmed Sair shutdown and supplied a screenshot showing return to
  the prompt and `status` reporting `Daemon não está em execução`. The action is
  the human report; prompt/status are visual evidence. Tray → Sair accepted on
  GNOME/X11, without proof of exit code, resource-by-resource inspection, both
  open/closed-overlay variants or launcher startup.
- Drawing PNG inspected separately in container: RGBA, 1920×1080; 2,054,243 pixels
  with zero alpha, 13,881 with alpha 255, 5,476 intermediate. Proves transparent
  background and opaque/partially transparent content in the file; live desktop
  behavior is the human report, not inferred from PNG.
- Evidence SHA-256: `564c0d4658dafc50d081ecd63c35bd482e1433b4b5d4da8b65595926aa72344d`.
  Personal file stayed outside the repository, not copied/published; only the
  explicitly designated file was mounted read-only for analysis.

This resolves automatic visual selection for that environment and confirms transparent
export, CapsLock/NumLock, both tested monitors, monitor 1 and GNOME tray icon/menu.
Sair shutdown has human acceptance/no-daemon status. It does not prove Zoom/Draw
tray activation, launcher, physical app conflicts, fractional scaling, other GPUs/
topologies, Wayland or the complete GNOME matrix, nor replace automated-video review.

### Manual Acceptance on Ubuntu 26.04/GNOME Wayland

On 2026-10-30, after release `v0.1.2` installation directed in this conversation,
the requester reported that tests on Ubuntu 26.04/Wayland all worked and supplied
screenshots. This is human acceptance of exercised flows, not automated execution
or individual confirmation of every matrix item.

| Slice | Evidence and result |
| --- | --- |
| Environment | Ubuntu 26.04, GNOME/Wayland, according to report/context; reference package linux-x64 v0.1.2. Screenshots do not show `--version`, `probe`, scaling or monitor topology |
| Drawing/colors | Red/blue strokes over the desktop image; a later screenshot shows a new blue stroke |
| Boards/restoration | White/black backgrounds preserve strokes; another screenshot returns to the desktop image with annotations |
| Desktop identity | PixelPointer product icon/name in launcher; panel icon and Zoom/Desenhar/Sair menu visible |
| Visual annotation exit | Screenshots without strokes show the desktop again; do not prove daemon shutdown or which command was used |
| Human result | General functionality confirmed in that session; zoom, hotkeys, permissions and menu actions are not individually demonstrated in attachments |

Six attachments remain evidence in the conversation, without copying, hashing,
publishing or inspecting personal files. Automation did not access the user's
desktop. The published v0.1.2 installer was separately checked in this conversation:
it requires/copies `pixelpointer.svg`; screenshots corroborate desktop visual
identity but do not replace package/installer tests.

**Additional transparent drawing acceptance on 2026-10-30:** the requester
provided two more screenshots in this conversation, explicitly identifying
them as evidence of transparent drawing. The first shows a red stroke over
a browser video image; the second shows a red circular annotation over different
video content. Browser content remains visible beneath the drawing area.
Transparent drawing is complete by human acceptance on this Ubuntu 26.04/GNOME
Wayland session, visually corroborated by the overlay. This record now has eight
attachments, all kept only in the conversation. No original PNG analysis,
session recording or automation was performed. Since annotations and content
differ between screenshots, they do not prove continuous background movement
beneath the same persistent stroke; that objective check and exported PNG alpha
analysis remain pending.

Limits: still images do not prove live transparency, PNG alpha, latency, native
Wayland versus XWayland, ScreenCast/PipeWire or the portal used. No-terminal launch,
hotkey conflicts, CapsLock/NumLock on this Wayland session, permission failure/denial,
other monitors, HiDPI, pen, other compositors and Windows/macOS were not detailed
in this retest. Isolated per-compositor Wayland E2E and human X11 video review remain
pending. Acceptance does not complete all F2/F3.

### ScreenCast + PipeWire validation on isolated Wayland

`tools/scripts/Test-WaylandScreenCast.ps1` runs the package in a container with
headless sway, PipeWire, WirePlumber, xdg-desktop-portal and
xdg-desktop-portal-wlr (pinned Debian snapshot), without network and without the
host session, D-Bus or directories. The xdg-desktop-portal-wlr chooser is
configured for the single virtual output; there is no GNOME dialog in this
environment.

| Mode | Check | Result on 2026-10-31 |
| --- | --- | --- |
| Default, no GPU | xdg-desktop-portal-wlr 0.7 requires DMA-BUF and rejects the pixman renderer ([#289](https://github.com/emersion/xdg-desktop-portal-wlr/issues/289)); `capture` must fail with exit code 1, a Screenshot fallback notice and a clear error, without an unhandled exception or file | Passed |
| `-RenderNode /dev/dri/renderD128` (rootless Podman, `keep-id`) | Two 800x600 ScreenCast captures, exact background color, 0600 restore token reused and a fresh frame after changing the background | Passed: portal 7–8 ms, first frame 19–26 ms, 36 ms total |

The GPU mode optionally exposes only the render node. The timings come from the
virtual environment without a dialog; they do not replace measurement on real
GNOME, where the target remains < 150 ms. On wlroots a frame only arrives after
screen damage; without damage, capture falls back to Screenshot after 2 s.

## 9. Technical References

- [uv scripts and lockfiles](https://docs.astral.sh/uv/guides/scripts/).
- [Xvfb virtual server](https://www.x.org/releases/current/doc/man/man1/Xvfb.1.xhtml).
- [libei/EIS emulated input](https://libinput.pages.freedesktop.org/libei/).
- [F0 manual checklist, pt-BR](../f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma).
- [X11 manual checklist](usage.md#manual-x11-acceptance-test).