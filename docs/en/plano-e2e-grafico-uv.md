# Isolated Graphical E2E Plan with uv and Video Evidence

[Português (Brasil)](../plano-e2e-grafico-uv.md) · **English** · [Español](../es/plano-e2e-grafico-uv.md)

| Field | Status |
| --- | --- |
| Date | 2026-10-30 |
| Human authorization | Requester, in this conversation: authorization for isolated graphical E2E under the project rules |
| Authorized decision | Allow a separate, isolated automated graphical suite |
| Execution status | X11 executed with 10/10 stability; human video review closed by maintainer decision on 2026-10-31, with no recorded review. ScreenCast/PipeWire validated on isolated Sway with a render node; no-GPU path handled. Capture, zoom and drawing of v0.1.3-preview.14 manually accepted on Ubuntu 26.04.1/GNOME Wayland on 2026-10-31; token, indicator, latency without the dialog and additional matrix remain pending |
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
| Wayland | ScreenCast/PipeWire passed on isolated Sway; no-GPU unavailability handled. v0.1.3-preview.14 manually accepted for capture, zoom and drawing on Ubuntu 26.04.1/GNOME Wayland, with consent observed. Token reuse, indicator duration, latency without the dialog, multi-monitor and other compositors remain pending; no consent mocks or XTEST via XWayland as native evidence |
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

### Manual preview acceptance on GNOME Wayland

On 2026-10-31, the requester reported that the Linux Wayland test passed,
attached a screenshot and capture log, then explicitly authorized recording
zoom and drawing as successful. The test ran on the requester's machine,
not through automation connected to their graphical session.

- Observed environment: Ubuntu 26.04.1 LTS x64, GNOME/Wayland, Native AOT,
  `0.1.3-preview.14` from commit `cdd125e`; `probe` reported ScreenCast v5
  and GlobalShortcuts v1. GNOME version and scale were not explicitly supplied.
- The screenshot shows the sharing dialog with the internal monitor selected,
  plus desktop entry and icon installation. The app registration warning in
  `probe` preceded that installation; no new diagnostic proves it resolved.
- The log confirms **xdg-desktop-portal ScreenCast + PipeWire**, a
  **1920×1080** PNG, portal **20,830 ms**, first frame **122 ms** and
  total **20,999 ms**. Portal timing includes human selection and confirmation
  in the dialog; it does not prove hotkey-to-frame < 150 ms.
- Capture, zoom and drawing are **manually accepted in this environment**
  according to human confirmation. The still screenshot alone does not prove
  interactive zoom/drawing; acceptance of those actions relies on the report.
- This preview still lacks evidence for token reuse, indicator duration,
  repetition without consent, physical hotkeys, tray/launcher actions, Ctrl+S,
  alpha or live content, multi-monitor, other compositors, scales, GPUs and
  Windows/macOS. Confirmation does not complete automated Wayland coverage
  or phases F2/F3.

The attachment remains in the conversation and is not copied to the repository;
the personal screenshot is not published. The personal PNG path is omitted.

### Stage diagnostics validation on 2026-10-31

The local Native AOT binary with the unpublished diagnostics passed
ScreenCast/PipeWire testing on isolated Sway with a render node. The existing
runner verified two captures, colors/format and a fresh frame after scene
changes. nginx stayed isolated without public ports, with smoke and cleanup;
no host graphical session or D-Bus was accessed.

Local evidence: `artifacts/wayland/screencast-20261101T015920989-305aa712`.
First capture: portal 9 ms, remote 3 ms, reading 19 ms, legacy
`primeiro quadro` 23 ms, `restore solicitado nao`. Second: portal 8 ms,
remote 4 ms, reading 16 ms, legacy field 20 ms, `restore solicitado sim`.
Values are rounded independently; they describe this run, not an equivalent
benchmark to GNOME with a dialog or proof of the < 150 ms target.

The local gate passed format, build with no warnings/errors, 139 unit tests,
23 preview governance cases, Native AOT publication and `selftest`.
The ScreenCast class had 23 tests pass after preparing Fontconfig in the
container. Native Windows/macOS graphical tests and manual GNOME acceptance
of the new diagnostics were not repeated. This does not complete F2/F3.

### Grouped query validation on 2026-10-31

The unpublished local Native AOT binary passed again on isolated Sway with a
render node, isolated nginx without public ports and container cleanup.
Evidence: `artifacts/wayland/screencast-20261101T022349873-b4ed0c2d`.
Both captures, colors/format and a fresh frame after scene changes passed;
both reported `ScreenCast capacidades: 0 ms; consulta GetAll`, confirming
the grouped path. The rounded value does not mean zero cost and is not a
GNOME benchmark.

The gate passed format, build without warnings/errors, 156 unit tests,
23 preview governance cases, Native AOT and `selftest`. The portal client's
24 tests cover types/properties, monitor support and the old sequence with
early exits and cancellation. Incompatible replies and legacy reads were
tested as synthetic logic, not against a real portal rejecting `GetAll`.
GNOME/KDE/other portal coverage and complete benchmarking remain pending;
this run does not complete F2/F3.

### GetAll compatibility under controlled denial

On 2026-11-01, normal and `GetAll`-denied scenarios passed with the same local
Native AOT package and existing Wayland image. nginx was isolated without
public ports, with smoke and cleanup. No host graphical session, D-Bus or
personal directories were accessed; only the previously authorized render node
was exposed to the rootless container.

- Control: `artifacts/wayland/screencast-20261101T121050665-764bf12c`,
  two ScreenCast/PipeWire captures with an updated frame, without added policy.
- Denial: `artifacts/wayland/screencast-20261101T121052886-6dcf124c`,
  `result.json` with `passed=true`, `mode=getall-denied` and two rejections.
  Both captures still used ScreenCast/PipeWire and reported `consulta Get`.
- D-Bus configuration was derived from the image through XML parsing with DTD
  and external resolution disabled, created without overwriting and mounted
  read-only. The policy blocks `Properties.GetAll` for
  `org.freedesktop.portal.Desktop` in the container, without examining call
  arguments. `Get`, session creation/start and frame reading remain allowed.
- Bus logs prove the rejections. Tests require both `Get` diagnostics and
  correct captures; post-check failure writes `passed=false` rather than retaining
  the runner's positive result. The portal and its consent were not replaced.

Additional command from the clone root, with the current package in
`artifacts/aot/linux-x64`, rootless Podman, the Wayland image available and
isolated nginx already running under E2E rules:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -DenyScreenCastGetAll
```

Requires the new capability diagnostic, not the older published preview package.
Without `RenderNode`, the mode is rejected before starting the scenario; help
and unknown arguments were tested in a container. Production code did not change
in this step; the normal scenario was also repeated successfully. Each `capture`
command opens a new connection, so this does not verify repeated captures using
the `Get` preference on one daemon connection. Naturally incompatible portals,
GNOME/KDE, cross-platform coverage and the latency target remain pending.
Fault injection does not complete this plan.

### Repeated captures on the daemon connection

On 2026-11-01, optional `-RepeatDaemonCaptures` passed in control and with
`-DenyScreenCastGetAll` using the same local AOT package. The Python driver uses
only the standard library and tools already in the image; it runs the existing
baseline and then a real daemon on disposable D-Bus/Sway. A monitor records
only portal property calls, without replies, tokens or screen content.
Visual captures inspect a synthetic scene. nginx stayed isolated without
public ports, with smoke and cleanup.

| Scenario | Local evidence | Result |
| --- | --- | --- |
| Control | `artifacts/wayland/screencast-20261101T123222089-3df3f7ae` | One daemon, two sessions, two `GetAll` calls, no rejections |
| Denial | `artifacts/wayland/screencast-20261101T123225959-89b40b94` | One daemon, one denied `GetAll`, six ordered `Get` calls for version/sources/cursor across two sessions |

The driver identifies the D-Bus connection by daemon PID. Drawing and zoom use
IPC; one window is required and a portal capture checks frame color, with a
different color in the second session. `swaymsg` closes the window, the process
must remain active according to `status`, and `quit` must exit with code zero.
`daemon-result.json` records calls, colors and lifecycle; the orchestrator
requires a complete result and propagates failures to `result.json`.
Incomplete startup or verification is not success.

From the clone root, with image, current package, authorized render node,
rootless Podman and isolated nginx running under the previous rules:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures
```

Add `-DenyScreenCastGetAll` to repeat under denial. The driver is mounted
read-only; the option requires a render node and leaves the normal scenario
unchanged. The orchestrator remains PowerShell 7; Python runs only inside the
container. Production code did not change. This isolated acceptance covers
connection reuse and fresh queries, not changing properties in a naturally
incompatible portal, physical hotkeys, Esc, tray or quit with a window still
open. GNOME/KDE and the latency target remain pending.

### Quit with an open overlay and restart

On 2026-11-01, `test/wayland-daemon-lifecycle` added opt-in
`-CheckDaemonLifecycle`, requiring `-RepeatDaemonCaptures`. Production code
and dependencies did not change in this step.

The driver requires an open overlay and verifies its color through the portal
before `quit`. The process must exit zero, the window disappear and `status`
report no daemon. It then starts another process, requires a new D-Bus identity,
opens zoom through IPC, verifies the updated frame and quits again. JSON records
each assertion; the orchestrator rejects incomplete lifecycle results.

| Scenario | Local evidence | Result |
| --- | --- | --- |
| Previous test without the new option | `artifacts/wayland/screencast-20261101T130137628-161f4830` | Passed; existing behavior preserved |
| Normal lifecycle | `artifacts/wayland/screencast-20261101T130141551-ca85f33c` | Open-overlay quit, stopped status, new connection and post-restart capture passed |
| Lifecycle with `GetAll` denied | `artifacts/wayland/screencast-20261101T130145567-e269262d` | Same checks passed with the denial policy |

From the clone root, with current AOT package, rootless Podman, authorized render
node and isolated nginx under the preceding prerequisites:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle
```

Add `-DenyScreenCastGetAll` to repeat under denial. nginx had no public ports
and passed its smoke check; containers were removed. No host graphical session,
D-Bus or personal data was accessed. The package was stage 1's validated AOT.
This is isolated Sway/IPC coverage, not physical hotkeys, Esc, tray, launcher
or native GNOME/Windows/macOS acceptance.

### Multi-monitor policy and Screenshot errors

On 2026-11-01, `test/wayland-multi-monitor-policy` validated the existing policy:
ScreenCast only with exactly one monitor. Five unit cases cover negative, zero,
one, two and three counts. Screenshot fallback D-Bus errors are now controlled
errors, retaining cancellation and the earlier fallback's unavailable message.

Opt-in `-CheckMultiMonitorPolicy` runs the capture baseline and creates two
active virtual Sway outputs, verified through JSON. The image has no Screenshot;
`capture` must exit 1, without a PNG, ScreenCast message or unhandled exception.
This proves policy and safe unavailability, not successful multi-monitor capture.

| Scenario | Local evidence | Result |
| --- | --- | --- |
| No render node | `artifacts/wayland/screencast-20261101T131754378-903d3b19` | Unavailability and previous message preserved |
| One monitor and lifecycle | `artifacts/wayland/screencast-20261101T131758088-7c112c2c` | Capture, quit and restart still pass |
| Two outputs without Screenshot | `artifacts/wayland/screencast-20261101T131802565-e28d49cf` | `passed=true`, 2 outputs, exit 1, no file, controlled error |

From clone root with current AOT package, existing image, rootless Podman,
authorized render node and isolated nginx under previous prerequisites:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -CheckMultiMonitorPolicy
```

Cannot combine with `-RepeatDaemonCaptures`. The gate passed format, build
without warnings/errors, 161 tests, 23 governance cases, AOT and `selftest`.
nginx had no public ports; containers were removed. No host graphical session,
D-Bus or personal capture was used. Real multi-monitor capture with Screenshot,
monitor selection, HiDPI, GNOME/KDE and other architectures/platforms remain pending.

### Source identity after fallback and recovery

On 2026-11-01, `fix/capture-backend-recovery` separated native capture from
fallback selection through existing `IScreenCapture`. The name changes only
after a successful capture: returns to ScreenCast after recovery and does not
change to Screenshot when fallback fails. Cancellation still has no fallback;
native session, consent, timeout and closure remain unchanged.

Three focused tests use controlled sources for success → fallback → recovery,
failed fallback and cancellation. The real gate passed format, build without
warnings/errors, 164 tests, 23 governance cases, AOT and `selftest`.

| Native scenario | Local evidence | Result |
| --- | --- | --- |
| No render node | `artifacts/wayland/screencast-20261101T174558524-1a07a163` | Controlled error, no file |
| Capture and lifecycle | `artifacts/wayland/screencast-20261101T174601492-fccadfc0` | IPC draw/zoom, status, quit and restart passed |
| `GetAll` denial | `artifacts/wayland/screencast-20261101T174606129-0bb81304` | Get reads and real captures preserved |

nginx stayed isolated without public ports; no host session or D-Bus was accessed.
Identity recovery is controlled-logic coverage, not a real portal changing
availability. Real capture was tested on Sway; GNOME/KDE and hardware coverage pending.

### Blocked fractional-scale investigation

Historical record of the initial blockage; subsequent validation is in the next section.

On 2026-11-01, `test/wayland-output-scale` attempted tests at scales 1, 1.5 and 2.
The 1.5 test did not pass: Sway's logical output was `533×400`, but the window
stayed `1×1` without fullscreen. Evidence:
`artifacts/wayland/screencast-20261101T134318812-3740613a/window-geometry.json`.
This isolated result does not invalidate earlier human GNOME acceptance.

A disposable SDL probe compared zero initial size, explicit size and fullscreen
requested after creation. Local fix candidates passed build, 161 tests,
AOT/selftest and X11 E2E but not the Wayland matrix: the second session had a
small window, followed by inspection capture failure on static content.
They were not classified as accepted fixes.

Production and compositor-damage experiments were withdrawn; `OverlaySession.cs`
matches `main` again. Experimental AOT was preserved at
`artifacts/investigation/fullscreen-experimental-536b8551f28b4f36af044186905856f3`;
do not use it as a validated implementation. Rebuild from source before new tests.
Failure evidence and scale tests remain local without commit/push/PR. This
increment is **blocked**, not complete. Scales 1.5 and 2 were not accepted by
this test; geometry, composition and frame inspection in the fixture require
investigation before delivery.

### Isolated HiDPI after separating inspection

On 2026-11-01, the investigation separated two problems: a zero-sized initial
Wayland window/unapplied fullscreen, and inspecting a static overlay through
a second ScreenCast session that can receive no damage on wlroots. The fix
uses logical display dimensions for Wayland creation and requests fullscreen
after renderer creation only if its flag was not applied. It does not block
with `SDL_SyncWindow` or change X11/Windows/macOS, consent, capture timeout or GPU opt-in.

`localhost/pixelpointer-wayland:screencast-v2` pins `grim=1.4.0+ds-2+b1`
from the existing Debian snapshot. Exact-package Expat/MIT licensing, including
`debian/*`, was verified by extracting copyright from the signed `.deb` without
running package scripts; the package is supplied by maintained Debian trixie.
Upstream/web metadata sites blocked automated access; version came from
snapshot APT, not assumptions about a website. Native links: wayland-client/
libffi and pixman (MIT), libpng, zlib and distribution runtime, already in the
image; versions are inventoried in `/opt/native-packages.tsv`. This is test-only,
not part of PixelPointer packages or production capture.

The baseline still requires real ScreenCast/PipeWire. The driver inspects
synthetic scene pixels through the compositor with grim, avoiding another
portal session; this does not prove consent, which remains covered by the
baseline. It requires fullscreen/logical bounds through Sway JSON, physical
800×600 size, color, density and lifecycle. Old failures were not reclassified.

| Scale | Local evidence | Result |
| --- | --- | --- |
| 1 | `artifacts/wayland/screencast-20261101T143030541-59f79fcb` | Drawing, zoom, status, quit and restart passed |
| 1.5 | `artifacts/wayland/screencast-20261101T143034841-b65dc44a` | Logical 533×400, pixels 800×600, density 1.5; both sessions and lifecycle passed |
| 2 | `artifacts/wayland/screencast-20261101T143038653-a3d322d0` | Logical 400×300, pixels 800×600, density 2; both sessions and lifecycle passed |

Format, warning/error-free build, 161 tests, 23 governance cases, AOT and
`selftest` passed. X11 E2E also passed at
`artifacts/e2e/x11-20261101T143148779-500ce3d3/attempt-01`. nginx stayed isolated
without public ports; no host socket or personal graphical data was used.

With v2 image built, current AOT package, authorized render node and isolated
nginx under the preceding prerequisites:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Build -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle -OutputScale 1.5
```

`OutputScale` accepts 1/1.5/2; non-1 values require `RepeatDaemonCaptures`.
Compositor captures do not prove physical hotkeys, pen/pressure, GPU parity,
multi-monitor or native GNOME/KDE/Windows/macOS acceptance. Those slices and
latency benchmarking remain pending; F2/F3 are not complete.

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
