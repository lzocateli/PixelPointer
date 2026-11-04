# Isolated X11 graphical E2E

[Português (Brasil)](README.md) · **English** · [Español](README.es.md)

Implementation of the first target in the [plan](../../docs/en/plano-e2e-grafico-uv.md).
It exercises the Native AOT package through XTEST input, desktop pixels, public
IPC and a synthetic StatusNotifier panel. It does not access the developer's
graphical session.

## Run

Run from the repository root on Linux x64 with PowerShell 7 and Podman or Docker.
Builds need access to the registry, NuGet, PyPI and the Debian snapshot. The
graphical test itself runs without external access. Docker is supported by the
interface, but this implementation has only been verified with rootless Podman.

```powershell
pwsh -NoProfile -File tools/scripts/Publish-Aot.ps1 -Rid linux-x64 -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -Build -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -Repeat 10 -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -NoCompositor -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -NoTray -Engine podman
```

`Publish-Aot.ps1` produces `artifacts/package/linux-x64`. `-Package` accepts
only a directory inside `artifacts`, without symbolic links. `-Build` builds the
local image; otherwise the image must already exist. Each invocation pins the
inspected image ID across all attempts, without intermediate rebuilds.
`--help` does not start containers. Do not run the runner directly on the host.

The normal result is exit code zero. Failures, missing evidence or timeouts
return a non-zero code; the first failed attempt stops the sequence. To verify
failure detection:

```powershell
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -InjectFailure -Engine podman
```

This command **must fail** with code 1: the runner kills only its child daemon
and requires it to remain available at the next check. The report must record
`Daemon interrompido: -9`, never convert that failure into a successful scenario.

## Coverage and references

The [plan](../../docs/en/plano-e2e-grafico-uv.md#5-matriz-de-cenários-das-funcionalidades-atuais)
defines the functional matrix. The runner checks external focus, hotkeys,
modifiers, real grab conflicts, zoom, colors, thickness, editing, boards,
transparency, saving, visible errors, ordered diagnostic logs for keys/buttons,
tray, restart, quit with an open window, and desktop launcher install/open/removal.
The launcher interprets the installed desktop file but accepts only
`/package/pixelpointer daemon`, without a shell and with `Terminal=false`.

References use explicit regions and colors in the runner, not automatically
updated baselines. Examples: cyan scene RGB `(0,170,204)`, zoom widths
`280/350/438` with a 4-pixel tolerance, annotation colors with 8-channel
tolerance and minimum area, animated background changes with persistent strokes,
and opaque PNGs containing the captured screen and strokes, with or without a
compositor. PNGs are decoded.

Fixed geometry: 800x600, scale 1, SDL software rendering, locale `C.UTF-8`, UTC
and `xcompmgr -n`. The image has no GLX/Mesa. With a compositor, the runner
explicitly selects the 32-bit TrueColor visual through the public
`SDL_VIDEO_X11_WINDOW_VISUALID` hint; without one, it uses the default 24-bit
visual. This proves transparent drawing in this configuration, **not** automatic
visual selection or parity with GNOME, a GPU or a full desktop.

## Evidence and retention

Results are stored without overwriting in
`artifacts/e2e/x11-<UTC>-<id>/attempt-NN`:

| File | Contents |
| --- | --- |
| `desktop.avi` | Real XGetImage desktop capture, MJPEG at 5 FPS, no audio |
| `*.png` | Screenshots, CLI capture and actual Ctrl+S exports |
| `timeline.jsonl` | Actions and checks with monotonically increasing time |
| `result.json`, `junit.xml` | Result, duration and failure reason |
| `manifest.json` | SHA-256 of each package file, image ID, environment and video |
| `native-components.tsv`, `native-packages.tsv` | Compiled sources and exact Debian packages |
| `native-links.txt`, `python-packages.json` | Resolved native links and installed Python dependencies |

Recording starts before the first product action. Each screenshot waits for a
later frame before proceeding; shutdown also requires a frame after the final
action. Every JPEG is decoded, with required resolution and a 0.4-second duration
tolerance. Recording failure fails the result. Video does not replace objective
assertions.

Artifacts are never published, rotated or deleted automatically. Preserve failed
runs until analysis is complete; preserve stability runs and controlled failures
until human review. Later removal is manual and limited to the explicitly chosen
run ID. Do not use global prune.

## Isolation and threats

| Surface | Control and residual risk |
| --- | --- |
| Capture and input | Private Xvfb only; no host display, D-Bus, home directory or input device is mounted |
| Privileges | UID 1000, capabilities dropped, no-new-privileges, read-only filesystem and private tmpfs |
| Network | Internal network, no published ports; separate nginx and HTTP 404 smoke test; no direct HTTP helpers |
| Package | Read-only mount and normalized paths without symlinks; run only a trusted local package |
| Writes | Dedicated evidence volume and disposable temporaries; no secrets or real profiles |
| Processes | Known arguments, no shell in runner/launcher, terminate only child processes and identified resources |
| Resources | Runner: 2 CPUs, 2 GiB, 128 processes and 512 MiB tmpfs; nginx: 128 MiB and 32 processes |
| Diagnostics | Known tokens and test messages only; do not collect full logs or the complete environment |

X11 starts with `-ac`, acceptable only inside this container without an exposed
socket or port. The internal network and sandbox do not make a hostile package
safe to run against the host kernel. The image and package must be trusted. The
evidence volume is only a test output channel, not a place for personal data.
No portal/TCC permission is simulated as real consent.
In the compositor scenario, the disposable desktop provides a local mock of
`org.freedesktop.portal.Screenshot` that responds with an Xvfb PNG capture. This
exercises the D-Bus call, monitor crop and product composition; it does not
verify composed-window capture through GNOME's real portal or the system
authorization flow.

The cooperative deadline is 120 seconds; SIGALRM at 150 seconds raises an error
to finalize evidence; the orchestrator kills the container at 180 seconds.
SIGKILL or engine failure may prevent AVI finalization; that is not approval.

## Diagnostics and recovery

1. Read `result.json` and the last check in `timeline.jsonl`; compare the failure
   PNG and video. Infrastructure failures also fail the run.
2. If the build fails, check access to the digest/snapshot and the native-link
   gate. Do not automatically replace versions or relax licenses to get a pass.
3. If export fails or an external timeout occurs, the script reports the
   preserved volume. Recover it in a read-only auxiliary container using the
   same image, with no network:
   `podman create --name <unique-helper> --network none --read-only --volume
   <reported-volume>:/evidence:ro --entrypoint python
   localhost/pixelpointer-e2e:x11-v1 --version`, then use
   `podman cp <unique-helper>:/evidence/results/. <new-directory>`. Remove only
   that helper and, after checking the copy, the explicitly identified volume.
4. If reports or frames are missing, keep the run failed. A new run gets a new
   ID and does not erase the previous one.
5. Escalate differences between the synthetic and real desktop to the manual
   checklist and backend owner; do not mount the host session to bypass isolation.

## Dependencies and licenses

Test-only tools, not added to the distributed package. Locally verified on
2026-10-30 against exact artifact notices, PyPI metadata and the Debian snapshot.
Python and its manifest/lock are pinned; the build runs `uv sync --locked` and
the offline runtime uses `uv run --locked --no-sync`, without automatic updates
or downloads.

| Component | Pinned version | Selected license and source |
| --- | --- | --- |
| Python | 3.13.17 | PSF-2.0; official image by digest in Containerfile |
| uv | 0.13.6 | MIT, Apache-2.0 alternative; [PyPI release](https://pypi.org/project/uv/0.13.6/) |
| Pillow | 13.0.0 | MIT-CMU and wheel transitive notices; [PyPI release](https://pypi.org/project/pillow/13.0.0/) |
| dbus-fast | 5.2.0 | MIT; [PyPI release](https://pypi.org/project/dbus-fast/5.2.0/) |
| Xvfb | 2:21.1.16-1.3+deb13u4 | MIT/X11 and permissive `COPYING` notices; Debian xorg-server source |
| D-Bus | 1.16.2-2 | AFL-2.1, **not** the GPL alternative; GPL tools disabled |
| twm | 1:1.0.10-1+b1 | MIT/X11/Open Group, notices at `/opt/twm/copyright` |
| xcompmgr | 1.1.8-1 | Permissive XFree86; Debian copyright installed |
| nginx | 1.26.3-3+deb13u10 | BSD-2-Clause, compiled-source `LICENSE` |

Debian sources: [main](https://snapshot.debian.org/archive/debian/20261029T000000Z/)
and [security](https://snapshot.debian.org/archive/debian-security/20261029T000000Z/).
PyPI releases checked: uv on 2026-10-27, Pillow on 2026-10-16 and dbus-fast on
2026-10-02; Xorg/nginx include the pinned security revisions above. Updates
require new maintenance, notice, lock, build and matrix verification.

The wheels add no transitive Python dependencies. The Pillow wheel includes
native libraries and their notices in
`pillow-13.0.0.dist-info/licenses/LICENSE`: AOM/dav1d/libavif (BSD),
Brotli/HarfBuzz (MIT), bzip2, libjpeg (IJG), lcms2 (MIT), liblzma (0BSD),
libpng (libpng), libtiff (libtiff), libwebp/libyuv (BSD), OpenJPEG (BSD),
libraqm (MIT), Xau/XCB/Xdmcp (MIT/X11), zlib and zstd (BSD). FreeType uses FTL,
not GPL; zstd uses BSD, not GPL. The wheel's full notices prevail; FFmpeg and
FriBidi are not included.

Xvfb and D-Bus are built without libsystemd, libaudit, libapparmor, libunwind
and GLX; nginx without auth_basic, gzip, rewrite and PCRE. The `ldd` gate rejects
missing links and incompatible optional families such as nettle, libelf and
libcrypt. X11, pixman, expat, fontconfig/FreeType and OpenSSL use permissive
alternatives; transitive versions are pinned by the snapshot and inventoried in
each run. `/opt/native-notices` stores notices for compiled sources. The Debian
inventory also lists pre-existing base-system tools and components; this does
not mean the entire distribution or toolchain is MIT-licensed. The test image is
not a new PixelPointer distribution artifact.

## Acceptance limits

The automated suite has run only on virtual Linux x64/X11 with twm and a
synthetic panel. The requester separately confirmed transparent drawing,
automatic visual selection, zoom, alpha export, CapsLock/NumLock and use on both
1920x1080 monitors at scale 1 on real GNOME/X11, authorizing those tests to close.
The [manual acceptance record](../../docs/en/plano-e2e-grafico-uv.md#manual-acceptance-of-transparent-drawing-on-gnome-x11)
contains the environment, PNG hash and limits. Full GNOME matrix, Wayland/portals,
arm64, Windows/macOS, other GPUs, pen, fractional scaling and other monitor
topologies remain unverified. Human review of the suite's first videos and
references remains pending; automated results do not grant that acceptance.
