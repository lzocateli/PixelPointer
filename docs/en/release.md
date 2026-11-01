# Release process

[Português (Brasil)](../release.md) · **English** · [Español](../es/release.md)

The common release process (Release PR, version, tag, `release-manifest.json`,
republishing and fixing a defective release) is managed by Altivy.Groundwork and
described in the [release runbook](runbooks/release.md) and
[CI/CD reference (Portuguese)](../../.github/CI-CD.md). This document covers
PixelPointer-specific details: [`publish.yml`](../../.github/workflows/publish.yml)
builds Native AOT packages for all supported RIDs, and
[`post-release.yml`](../../.github/workflows/post-release.yml) distributes them
through Homebrew and winget. Every step has a local equivalent under
`tools/scripts/`.

## Required Preview

Every `release-please--branches--main` Release PR runs `release-preview` as part
of the required `Result` check. The job builds `preview.<run_number>` packages for
all six RIDs, SBOMs, Linux `.deb` files and `SHA256SUMS`, runs package smoke tests
and installer tests, then uploads `pixelpointer-preview-<run_id>-<run_attempt>`
artifacts for seven days. The artifact has no `install.sh`/`install.ps1`, creates
no tag or GitHub Release and does not run `post-release.yml`. Rulesets block
merging the Release PR while its required preview is failing or pending.

After the check passes, open the Release PR's `CI` run in Actions and download
the `pixelpointer-preview-<run_id>-<run_attempt>` artifact; it contains
`pixelpointer-linux-x64.tar.gz`, `.deb`, SBOM and checksums. Retention is seven days.

A manual preview of `main`, without a Release PR, can also be started:

```bash
gh workflow run publish.yml --repo lzocateli/PixelPointer --ref main -f preview=true
gh run list --repo lzocateli/PixelPointer --workflow publish.yml
```

After build, package smoke tests, installer tests and SBOM attestations pass,
`Publish GitHub Pre-release` waits for your approval in the `release` Environment.
The `Altivy Release` GitHub App creates `vX.Y.Z-preview.N` at the validated commit,
prepares a draft release, attests provenance and uploads the 14 assets plus
`SHA256SUMS`. It then publishes with `prerelease=true` and `make_latest=false`.
The version appears in [Releases](https://github.com/lzocateli/PixelPointer/releases)
as **Pre-release**, with public downloads requiring no login; unlike Actions
artifacts, it does not expire after seven days. Stable remains `Latest`.

This preview-channel exception was authorized by the maintainer on 2026-10-31.
It does not change Groundwork's stable flow, run `post-release.yml` or update
Homebrew/winget. The required PR preview remains an artifact-only operation,
without secrets or publication. Manual previews use the next patch of the
recorded version and `preview.<run_number>`; the prefix is applied only to build
and smoke checkouts without editing the version in Git. This does not imply
that release-please will choose that version for the next stable release.

On the other machine, download the RID package and `SHA256SUMS`, verify SHA-256
and extract the `.zip`/`.tar.gz` keeping native libraries beside the executable,
or install the Linux `.deb`. Run `pixelpointer --version` to check the suffix.
Previews do not include `install.sh`/`install.ps1`, are not served by
`releases/latest` and are not accepted by Groundwork deploy. Preview approval
does not replace the human Release PR merge or production approval.

### Using a preview package

Open [Releases](https://github.com/lzocateli/PixelPointer/releases) and choose
a version marked **Pre-release**, not `releases/latest`. These examples use
`0.1.3-preview.14`; replace `version` with your chosen version without its
initial `v`. Neither .NET nor a repository clone is needed. Previews do not
include installers; Homebrew and winget still point to stable versions.

Choose the RID of the machine that will run the app, not the download machine:

| Machine | RID | Portable package |
| --- | --- | --- |
| Linux Intel/AMD 64-bit (`uname -m`: `x86_64`) | `linux-x64` | `.tar.gz` |
| Linux ARM 64-bit (`uname -m`: `aarch64`) | `linux-arm64` | `.tar.gz` |
| Windows Intel/AMD 64-bit | `win-x64` | `.zip` |
| Windows ARM 64-bit | `win-arm64` | `.zip` |

If stable is running, stop it first with `pixelpointer quit`: both versions
share an identity and IPC, so a preview command could reach the old daemon.
Always use the extracted executable path, not an old `pixelpointer` found
through `PATH`. The examples create a new directory without replacing stable
or changing profiles, registry or `PATH`. The `.deb` is another option on
Debian/Ubuntu, but installs the same system package and may replace stable;
prefer `.tar.gz` for a separate binary test. Per-user settings and permissions
remain shared; this is not a sandbox.

#### Linux on Wayland

Run on the target machine in a terminal of the graphical session, not over SSH.
`echo "$XDG_SESSION_TYPE"` must report `wayland`. glibc 2.34+, Fontconfig,
libstdc++, libX11 and XDG portals with your desktop backend are required;
ScreenCast also uses the system `libpipewire-0.3`. SDL3/Skia do not need a
separate installation: they are bundled.

Download and verify in a new directory under your home. For ARM, change
`rid` to `linux-arm64`:

```bash
version="0.1.3-preview.14"
rid="linux-x64"
base="https://github.com/lzocateli/PixelPointer/releases/download/v$version"
workdir="$(mktemp -d "$HOME/pixelpointer-preview.XXXXXX")" &&
cd "$workdir" &&
curl -fSLO "$base/pixelpointer-$rid.tar.gz" &&
curl -fSLO "$base/SHA256SUMS" &&
sha256sum --check --ignore-missing SHA256SUMS &&
tar -xzf "pixelpointer-$rid.tar.gz"
```

**Continue only if download and verification succeed and report `OK`.** If
they fail, do not run the package. Keep libraries beside the executable:

```bash
./pixelpointer --version
./pixelpointer selftest
./pixelpointer probe
```

The version must include the selected suffix and `selftest` must pass; it does
not prove capture, hotkeys or graphical behavior. If the app is not registered
with the desktop yet, run `./pixelpointer install-desktop-file`. Do not force
replacement of an existing entry: conflicts are preserved. This step registers
the application in the menu without replacing its binaries.

If registration reports `App info not found` followed by `An app id is required`,
the portal could not find the application identity; this does not indicate an
invalid checksum. Run `./pixelpointer install-desktop-file` from this directory
and restart the daemon only after registration succeeds without conflicts.
Registering the identity does not grant capture permission. The daemon handles
D-Bus hotkey refusals with a warning and keeps IPC running; previews before this
fix may terminate with an exception. Without global hotkeys, use `zoom`/`draw`;
capture still requires portal consent.

On GNOME, run `./pixelpointer grant`, press Enter in the window and authorize
capture. Then test capture and start the daemon in the foreground to observe
possible errors:

```bash
./pixelpointer capture --out preview.png
./pixelpointer daemon
```

Choose and authorize the monitor in the portal prompt. With one monitor,
ScreenCast/PipeWire reads a frame and closes the session; the sharing indicator
is transient. Canceling the dialog cancels the operation. On GNOME before 48
and desktops without GlobalShortcuts, configure desktop hotkeys using the
absolute preview executable path with `zoom` and `draw`. Details:
[Wayland permissions](usage.md). Wayland drawing stays over a frozen capture,
not live content. Test with one monitor first; multi-monitor and other desktops
have limitations and do not automatically inherit acceptance from a tested machine.

#### Windows

Run in Windows PowerShell 5.1+ or PowerShell 7 on the target machine. For ARM,
change `rid` to `win-arm64`. This example creates a new directory in
`LOCALAPPDATA`, verifies exactly the ZIP entry in `SHA256SUMS`, then extracts:

```powershell
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
$version = '0.1.3-preview.14'
$rid = 'win-x64'
$base = "https://github.com/lzocateli/PixelPointer/releases/download/v$version"
$dir = Join-Path $env:LOCALAPPDATA ("PixelPointerPreview\" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $dir | Out-Null
$zip = "pixelpointer-$rid.zip"
Invoke-WebRequest "$base/$zip" -UseBasicParsing -OutFile "$dir\$zip"
Invoke-WebRequest "$base/SHA256SUMS" -UseBasicParsing -OutFile "$dir\SHA256SUMS"
$pattern = '^[0-9a-fA-F]{64}  ' + [regex]::Escape($zip) + '$'
$entries = @(Get-Content "$dir\SHA256SUMS" | Where-Object { $_ -match $pattern })
if ($entries.Count -ne 1) { throw 'SHA256SUMS: missing, invalid or duplicate entry.' }
$expected = ($entries[0] -split '\s+')[0]
if ((Get-FileHash "$dir\$zip" -Algorithm SHA256).Hash -ne $expected) {
      throw 'Invalid checksum; do not run the package.'
}
Expand-Archive "$dir\$zip" -DestinationPath "$dir\app"
Set-Location "$dir\app"
```

Stop on any error: do not run or manually extract the rejected file. After
verification and extraction, keep `SDL3.dll` and `libSkiaSharp.dll` beside the
executable and run:

```powershell
.\pixelpointer.exe --version
.\pixelpointer.exe selftest
.\pixelpointer.exe probe
.\pixelpointer.exe daemon
```

The daemon runs in the foreground; Ctrl+C stops it. After stopping it, start
without blocking PowerShell with:

```powershell
Start-Process "$PWD\pixelpointer.exe" -ArgumentList daemon -WindowStyle Hidden
```

Then check `.\pixelpointer.exe status` and stop with `.\pixelpointer.exe quit`.
If a hotkey is occupied, run in the foreground to see the conflict. Binaries
are not Authenticode-signed; do not disable Windows protections to bypass warnings.

#### Test checklist and returning to stable

- Confirm the chosen version, `selftest`, and session/portals in `probe`.
- Test Ctrl+1, mouse wheel, Ctrl+2, colors and Ctrl+Z.
- Test Ctrl+S: PNG under XDG Pictures/`Screenshots` on Linux or the special
   Pictures/`Screenshots` folder on Windows, respecting OneDrive.
- Esc closes the overlay; `status` must confirm the daemon remains active
   and `quit` must stop it.

To return to stable, stop the preview daemon using its executable and start
the stable installation daemon. If you registered a desktop entry for preview,
remove it using `./pixelpointer uninstall-desktop-file` before removing its
files and register stable again if needed. Preserve modified entries and
settings; deleting the portable directory does not remove captures or revoke
portal permissions.

When reporting a failure, include version, OS, architecture, desktop/version,
X11/Wayland, monitor count, steps and error message. Do not share sensitive
captures, tokens or complete logs. These examples are a manual test guide:
passing CI smoke tests does not prove graphical acceptance on every machine
and desktop combination.

### Preview security and recovery

| Risk | Control |
| --- | --- |
| Publication from a PR or unapproved branch | Job only on a `main` dispatch, maintainer approval in the `release` Environment, checkout of the run commit and ancestry of `origin/main` |
| Personal token used to create protected tags | Only the authorized App, with a token limited to `contents: write`; tokens and private keys never enter artifacts or script output |
| Incomplete, tampered or mismatched packages | Same build/smoke version, SBOM/provenance attestations, exact asset set, local SHA-256 and remote digest verification before publication |
| Rerun overwrites a release or promotes preview to stable | Draft must match tag/commit; never move tags or overwrite assets; `prerelease=true`, `make_latest=false`, no post-release |

If the run fails after creating the tag or draft, they are preserved and the
preview stays unpublished. Do not delete or move tags to bypass a failure.
A rerun of the same run only completes when all digests are identical; builds
may not be byte-for-byte reproducible. If they differ, start a new dispatch
with another `preview.N`. An existing tag without a draft requires maintainer
investigation; the script refuses to reuse it. A rerun refuses to modify an
already public preview; publish a new preview to correct it.

The local equivalent is `tools/scripts/Set-PreviewRelease.ps1`: without
`-Publish`, it validates files and emits the payload without accessing GitHub;
`-Publish` is reserved for the approved App-backed job.
`tools/scripts/Test-PreviewRelease.ps1`, included in `Invoke-Validation.ps1`,
tests a real filesystem and a synthetic API in a container. These tests do not
replace the first actual six-RID publication in Actions or graphical acceptance
on the target machines.

## First Functional Version

`v0.1.2` is the first functional version accepted as stable by the maintainer on
2026-10-30 on Ubuntu 24.04.5/GNOME X11 and Ubuntu 26.04/GNOME Wayland. The
[E2E plan](plano-e2e-grafico-uv.md) records manual acceptance and its limits;
it does not mean native Windows/macOS validation or completion of F2/F3.

Release `v0.1.1` bundles the SVG, but its installer does not copy it. Version
`v0.1.2` includes the PR #8 fix: `install.sh` and `install.ps1` require and
copy `pixelpointer.svg`. Do not overwrite older assets or move their tags.
To reproduce the accepted version, from any Linux/macOS directory with
`curl`, `sh` and the native dependencies listed in the README available:

```bash
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/download/v0.1.2/install.sh | sh -s -- --version 0.1.2
```

The installer verifies SHA-256 and installs/replaces the package in the default
prefix, without changing profiles or PATH. Check `pixelpointer --version` and
on Linux run `pixelpointer install-desktop-file`. A modified older desktop entry
is preserved and requires explicit migration, not silent overwriting.

GitHub publication and winget submission are separate stages. A winget job
awaiting approval may occupy `release-main` even after public release; if the
maintainer defers submission, reject only the older run's `winget` approval
to unblock the next Release PR. Do not approve winget just to unblock a release.
The structural concurrency fix belongs to the Groundwork catalog, not local `gw-*` edits.

## Artifacts

| File | Contents |
| --- | --- |
| `pixelpointer-<rid>.tar.gz` | `pixelpointer` (mode 0755), `libSDL3` and `libSkiaSharp` (`.so` or `.dylib`), `pixelpointer.svg`, `LICENSE`, `THIRD-PARTY-NOTICES.md`; owner `0:0`; Linux and macOS |
| `pixelpointer-<rid>.zip` | `pixelpointer.exe`, `SDL3.dll`, `libSkiaSharp.dll`, `pixelpointer.svg`, `LICENSE` and `THIRD-PARTY-NOTICES.md`; Windows |
| `pixelpointer-<rid>.spdx.json` | SPDX 2.2 SBOM for the RID package: package files and NuGet build dependencies |
| `pixelpointer-linux-x64.deb`, `pixelpointer-linux-arm64.deb` | App in `/usr/lib/pixelpointer`, link `/usr/bin/pixelpointer`, desktop entry in `/usr/share/applications`, icon in `/usr/share/icons/hicolor/scalable/apps` and documentation in `/usr/share/doc/pixelpointer`; owner `root`; `Depends: libc6 (>= 2.34), libstdc++6, libfontconfig1` |
| `install.sh` | POSIX installer for Linux and macOS |
| `install.ps1` | Windows PowerShell 5.1+ and PowerShell 7 installer |
| `SHA256SUMS` | SHA-256 checksums for all files above, in `sha256sum` format |

There are 16 files besides `SHA256SUMS` (6 packages, 6 SBOMs, 2 `.deb` files and
2 installers); the `Prepare release assets` job fails if the count differs.
Filenames have no version suffix so `releases/latest/download/<file>` stays
stable; the version is in the tag and `pixelpointer --version`. In `.deb`
versions, suffixes use `~` (for example `0.4.0~rc.1`) so they sort before the
final version. There is no `linux-musl` package: SDL3-CS does not publish a musl
binary, and Linux requires glibc 2.34 or newer for the distributed `libSDL3`
(D12 in the [implementation plan](../plano-implementacao.md), Portuguese).

Every file has a GitHub provenance attestation (`gw-github-assets.yml`). The
release has an attested `release-manifest.json` containing each asset's SHA-256;
each `.tar.gz`, `.zip` and `.deb` also has an SBOM attestation for its RID. Both
use keyless Sigstore through OIDC. The binaries are not signed with Authenticode
or Developer ID (these require paid certificates; D5 in the
[implementation plan](../plano-implementacao.md)).

## Verify downloads

```bash
sha256sum --check --ignore-missing SHA256SUMS
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer \
  --predicate-type https://spdx.dev/Document/v2.2
```

## Distribution channels

| Channel | Command | Updates |
| --- | --- | --- |
| Installers | `install.sh` / `install.ps1` (see [README](../../README.en.md#installation)) | `releases/latest` |
| `.deb` (release asset, no APT repository) | `sudo apt install ./pixelpointer-linux-x64.deb` | Manual; download the new release |
| Homebrew (macOS and Linux) | `brew install lzocateli/tap/pixelpointer` | `brew upgrade`; formula updated by `post-release.yml` |
| winget (Windows) | `winget install lzocateli.PixelPointer` | `winget upgrade`; `post-release.yml` opens a PR to `microsoft/winget-pkgs`, published after Microsoft moderation |

The formula lives in [`lzocateli/homebrew-tap`](https://github.com/lzocateli/homebrew-tap)
and is generated by [`New-HomebrewFormula.ps1`](../../tools/scripts/New-HomebrewFormula.ps1)
from `SHA256SUMS`. It installs into `libexec` (the `.tar.gz` for `osx-arm64`,
`osx-x64`, `linux-arm64` and `linux-x64`), links `bin/pixelpointer`, depends on
`fontconfig` on Linux, and `brew test` checks `--version` and `selftest`.

The winget manifest (`lzocateli.PixelPointer`, schema 1.10.0) is generated by
[`New-WingetManifest.ps1`](../../tools/scripts/New-WingetManifest.ps1) from
`SHA256SUMS`. It uses the `win-x64` and `win-arm64` `.zip` packages with portable
`pixelpointer.exe` (alias `pixelpointer`) and native DLLs in the same directory.
The app resolves libraries from the executable's real directory, so the alias
works; `winget.yml` verifies this with `selftest`.

## Build by RID

| RID | Build host | Toolchain |
| --- | --- | --- |
| `linux-x64`, `linux-arm64` | Container on Linux x64 host | Official `azurelinux-3.0-net10.0-cross-*` cross-build image (glibc 2.27 sysroot, clang, lld) |
| `win-x64`, `win-arm64` | `windows-2025`, `windows-11-arm` | SDK from `global.json` and Visual Studio C++ |
| `osx-arm64`, `osx-x64` | `macos-15`, `macos-15-intel` | SDK from `global.json` and Xcode |

Native AOT cannot cross-compile between operating systems. On Linux, the official
SDK is copied over the cross-build image by
[`linux-cross.Dockerfile`](../../tools/build/linux-cross.Dockerfile). Images are
pinned by tag and digest in `Publish-Aot.ps1`; update them together.

```bash
pwsh -c './tools/scripts/Publish-Aot.ps1 -Rid linux-x64, linux-arm64'
pwsh ./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64   # ubuntu:22.04, rockylinux:9, debian:12 and .deb
pwsh ./tools/scripts/Test-Installers.ps1                   # install.sh with a local HTTP server
```

The SBOM is generated by `microsoft/sbom-tool` (MIT), pinned as a local tool in
[`.config/dotnet-tools.json`](../../.config/dotnet-tools.json): in the Linux
cross-build container and with the runner SDK on Windows and macOS. The `.deb`
is assembled with `dpkg-deb --root-owner-group` in `docker.io/library/debian:12`.

The package smoke test (`Test-AotPackage.ps1`) verifies SHA-256 and package
contents, then runs `--version`, `--help` and `selftest` (Skia encodes a PNG and
SDL initializes with the `dummy` driver) directly and through a symbolic link,
in containers without .NET that install `libfontconfig`, `libstdc++` and `libX11`.
On Linux it also checks idempotent per-user desktop installation, conflict
preservation, safe removal, single-instance behavior, `status`, `quit` and
restart with SDL dummy. These tests do not prove physical hotkeys, tray
visibility or compositor transparency. When available, the test also checks the
SBOM (SPDX 2.2, name `pixelpointer <version>` and executable SHA-256), then
installs, runs and removes the `.deb` with `apt-get` on `ubuntu:22.04` and
`debian:12`. `Test-Installers.ps1` serves packages on `127.0.0.1` and checks help,
usage errors, tampered checksums without side effects, failed downloads,
installation, clean reinstall, `PATH` warning, default destination and
`--global`. On Windows, `install.ps1` runs in Windows PowerShell 5.1 and
PowerShell 7, through `-File` and as a scriptblock.

## Publishing

Only the maintainer publishes versions. Approval of a PR, including an
exception for the maintainer's own PR, does not replace approval of the `release`
Environment; see [governance](../../.github/GOVERNANCE.md).

### Security and support releases

Pre-1.0 versions have no security support or backports. Starting with `1.0.0`,
only the latest `1.x` minor is supported; publishing a new minor ends support
for the previous one. Support for the `1.x` line ends with `2.0.0`. There is no
contractual SLA. Fixes are published on the supported line; see the
[supported versions policy](../../.github/SECURITY.md#versões-suportadas).

### Flow

1. PRs titled `feat`, `fix` (or with `!`) merged into `main` update the Release
   PR `chore(main): release X.Y.Z`, opened by release-please through the
   `Altivy Release` GitHub App. release-please calculates the version and updates
   `VersionPrefix` in `Directory.Build.props`, `.release-please-manifest.json`
   and `CHANGELOG.md`. Do not edit the version or create tags manually.
2. The maintainer merges the Release PR. `gw-release.yml` creates tag `vX.Y.Z`
   and a draft GitHub Release, then checks that the tag matches the versions and
   that the commit is an ancestor of `origin/main`.
3. On the tag, `publish.yml` runs the build, tests, publishes all six RIDs,
   performs smoke tests (`--version`, `--help` and `selftest`, also through a
   symbolic link) and installer tests on six runners.
4. The `attest-sbom` job attests each RID's SBOM (subjects: the package and, on
   Linux, the `.deb`) before assets are published, so the release is never made
   public without these attestations.
5. `Prepare release assets` regenerates `SHA256SUMS` with the installers and
   checks all 16 files. `Publish release assets` (`gw-github-assets.yml`) waits
   for approval in the `release` Environment, creates provenance attestations
   and attaches files to the draft release without overwriting published assets.
6. `gw-release.yml` creates and attests `release-manifest.json`, then publishes
   the release. Release notes come from the `CHANGELOG.md` generated by
   release-please.
7. Once the release is public, `post-release.yml`:
   - runs [`Update-HomebrewTap.ps1`](../../tools/scripts/Update-HomebrewTap.ps1)
     in the `release` Environment (with another approval), using the published
     `SHA256SUMS` and pushing commit `pixelpointer X.Y.Z` to the tap. It does
     nothing if the formula already matches the version; if the tap has a newer
     version (a fix for an older line), it leaves the tap unchanged and warns;
   - when the tap is on the released version, `verify-homebrew` installs from the
     tap on `macos-15` and checks the version and `brew test`;
   - calls [`winget.yml`](../../.github/workflows/winget.yml): generates the
     manifest, runs `winget validate`, installs from the local manifest on
     `windows-2025`, checks `pixelpointer --version` and `selftest`, uninstalls,
     then submits to the `microsoft/winget-pkgs` PR using `wingetcreate submit`
     (v1.12.13.0, pinned SHA-256). It does not submit if the version is already
     in `winget-pkgs` or a PR for that version is open.

Pull requests that change the release chain and `workflow_dispatch` of
`publish.yml` run build, smoke and installer tests without publishing.
Prereleases (`vX.Y.Z-suffix`) are not part of the automatic flow.

If publishing partially fails, the release remains a draft: fix the cause and
use `republish` as described in the [runbook](runbooks/release.md).

### Winget

`winget.yml` also runs on pull requests that change it (validates against the
latest release without submitting) and through `workflow_dispatch` from `main`,
with `version` and `submit`, for the first submission or a retry after failure:

```bash
gh workflow run winget.yml -f version=0.4.0 -f submit=true
```

Manual dispatch with `submit=true` is rejected outside `main`. In the automatic
flow, the reusable workflow receives context from `gw-release.yml`: a `push` on
`main` (post-release) or `workflow_dispatch` on the matching `vX.Y.Z` tag
(republish); any other source is rejected. The `WINGET_CREATE_GITHUB_TOKEN`
secret is only in the `winget` Environment, with `@lzocateli` as required
reviewer. `prevent_self_review` is disabled to allow the sole maintainer to
consciously approve a run they initiated; GitHub also permits administrative
bypass, currently limited to that maintainer.

`wingetcreate` accepts only a classic PAT with the `public_repo` scope
(fine-grained tokens are unsupported), which grants write access to every public
repository in the account. Use a short expiration and renew the secret before it
expires. The token is read from an environment variable, never from the command
line. Each PR passes automatic validation and Microsoft moderation; the version
becomes available in winget after merge.

### Tracking winget submissions

A version becomes available through `winget install` only after its PR is merged
into [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs). Until
then, it undergoes Microsoft's automatic validation and volunteer moderation,
which can take days. The commands below are read-only and work with any
authenticated `gh`:

```bash
# Versions already in the catalog (one directory per version; HTTP 404 means none yet)
gh api repos/microsoft/winget-pkgs/contents/manifests/l/lzocateli/PixelPointer --jq '.[].name'

# Submitted PRs, open and closed
gh pr list --repo microsoft/winget-pkgs --author lzocateli --state all \
  --search "lzocateli.PixelPointer in:title" --json number,title,state,createdAt,url

# A PR's state: labels, checks and latest comments
gh pr view <number> --repo microsoft/winget-pkgs --json state,labels,mergedAt,url
gh pr view <number> --repo microsoft/winget-pkgs --comments | tail -n 40

# winget.yml runs and runs waiting for Environment approval
gh run list --repo lzocateli/PixelPointer --workflow winget.yml --limit 5
gh run list --repo lzocateli/PixelPointer --status waiting
```

On Windows, `winget show lzocateli.PixelPointer --versions` lists versions
visible to the client; the index may take hours to reflect a merge.

Common PR labels (see Microsoft's [Moderation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Moderation.md)
and [Validation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Validation.md)):

| Label | Meaning | Action |
| --- | --- | --- |
| `Azure-Pipeline-Passed`, `Validation-Completed` | Automatic validation passed | Wait for moderation |
| `Needs-CLA` | Microsoft's Contributor License Agreement is missing | See below; no PR is merged without it |
| `Needs-Author-Feedback` | The moderator requested a change or response | Reply on the PR; if the manifest changes, fix `New-WingetManifest.ps1` and resubmit |
| `Validation-*-Error` (for example `Validation-Installation-Error`, `Validation-Hash-Verification-Failed`) | Automatic validation failed | Read the bot's comment; the asset or manifest needs a fix |
| `Moderator-Approved` | Approved by the moderator | Wait for automatic merge |
| `New-Package` | First package version in the catalog | Requires a more detailed manual review |

**Contributor License Agreement (once per account).** The first PR from an
account to `microsoft/winget-pkgs` gets `Needs-CLA`. The account owner must read
the agreement posted by the bot and, if they agree, reply to that PR with
`@microsoft-github-policy-service agree` (personal contribution), or the
`company="..."` variant when contributing on behalf of an employer. Signing is a
legal decision by the account owner: do not automate or delegate it. Once signed,
the label is removed and the PR proceeds to moderation; later PRs from the same
account do not need another CLA.

### Earlier version still pending in winget

`winget.yml` does not submit a version already in the catalog or one that already
has an open PR, but it does **not** check whether an earlier version is still
awaiting moderation. While the first version is pending, a later version's PR
also appears as `New package`, and moderators may close one of them. To avoid
competing PRs, keep at most one open at a time:

1. Before approving the `winget` Environment in `post-release.yml`, check open
   PRs with the `gh pr list` command above and `--state open`.
2. If no package PR is open, approve; the new version is submitted.
3. If an earlier-version PR is open, **reject** the `winget` Environment
   approval (Actions → run → *Review deployments* → *Reject*). The release,
   Homebrew and other channels are already published; only `Submit to
   microsoft/winget-pkgs` is rejected, so the `Release` run appears failed for
   that reason.
4. Resolve the pending PR: sign the CLA if it has `Needs-CLA`, and reply to
   `Needs-Author-Feedback`. If the older PR is closed without merge, do not
   reopen it; it has been superseded.
5. Once the pending PR is merged or closed, submit only the latest GitHub
   release; intermediate versions may be skipped because `winget upgrade` always
   installs the newest version:

   ```bash
   gh workflow run winget.yml --repo lzocateli/PixelPointer --ref main \
     -f version=X.Y.Z -f submit=true
   ```

   Dispatch from `main` goes through the same validation, local installation and
   `winget` Environment approval.

### Homebrew tap token

The `HOMEBREW_TAP_TOKEN` secret exists only in the `release` Environment and is
used by the tap job in `post-release.yml`. It is a fine-grained PAT with access
only to `lzocateli/homebrew-tap` and `Contents: Read and write` permission. The
script reads it from an environment variable and passes it to `git` through
`GIT_CONFIG_*`, outside the command line. When it expires, create a replacement
with the same scope and update the secret; without it, the release is published
and the tap step fails, but can be rerun after fixing the secret.

To test locally without publishing (the commit is made only in a temporary
clone):

```bash
pwsh ./tools/scripts/Update-HomebrewTap.ps1 -Version 0.4.0 -ReleaseDirectory dist
```

## Known limitations

- `republish` rebuilds packages. Existing assets keep their published digest,
  but a missing asset is uploaded from the new build and may not reproduce the
  already-published `SHA256SUMS` byte for byte. If `SHA256SUMS` is already
  attached, check with `sha256sum --check` before announcing.
- GitHub settings allow administrative bypass of the `winget` Environment
  (`can_admins_bypass=true`). The only administrator consulted is `@lzocateli`;
  changes to that group require policy review.
- The first submission to `microsoft/winget-pkgs` creates a new package and
  requires Microsoft's manual moderation; until then,
  `winget install lzocateli.PixelPointer` cannot find the package.
- Local validation on 2026-10-30: publish, smoke (Ubuntu 22.04, Rocky 9, Debian
  12 and `.deb`) and installer tests passed for `linux-x64` with Podman. Other
  RIDs are validated by GitHub runners.
