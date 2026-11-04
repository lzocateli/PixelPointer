# PixelPointer Update Plan

[Português (Brasil)](../plano-atualizacao.md) · **English** · [Español](../es/plano-atualizacao.md)

| Field | Status |
| --- | --- |
| Plan status | **Implemented** on 2026-11-04; awaiting human review and validation on Windows and macOS |
| Feature status | Implemented: `pixelpointer update` and the tray's **Atualizações** submenu; swap validated end to end only on Linux x64 |
| Official tracking | [IMPL-06 in section 8 of the implementation plan](plano-implementacao.md#8-consolidated-pending-items) |
| Date | 2026-11-04 |
| Origin | Requester proposal, detailed with AI assistance |
| Technical decisions | Chosen by the requester on 2026-11-04 and recorded in [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md) (`proposed`) |

## 1. Goal and current state

Define opt-in updates for self-managed PixelPointer installations without
interrupting a presentation or bypassing operating-system permissions. The
[README](../../README.en.md#installation) and [release guide](release.md#distribution-channels)
describe current behavior: the standalone installers can be run again, while
`.deb`, Homebrew and winget use their respective update flows. Before this
implementation, the application did not check for or install updates; the
implemented behavior is in the [usage guide](usage.md#updating).

This document proposes requirements and work sequence. Execution status is
tracked only by IMPL-06 in the [main plan](plano-implementacao.md#8-consolidated-pending-items).

## 2. Proposed behavior

User-facing preferences:

| Option | Default | Behavior |
| --- | --- | --- |
| `Check for updates automatically` | Off | Periodically checks for a compatible release and informs the user. Manual checks remain available. |
| `Install updates automatically` | Off | When enabled together with automatic checks, downloads, validates and stages an update without further confirmation. It does not authorize interrupting an active session. |

Automatic installation depends on automatic checking: turning off the first
option also suspends automatic installation. The installation preference must
be unavailable or turned off while checking is disabled.

Rules by installation source:

- Per-user installations managed by `install.sh` or `install.ps1` may update
  automatically if the current user can write to the directory and the
  installation source is identified reliably.
- `.deb`, Homebrew and winget may check and notify about a new release, but must
  not install it automatically. The notification should point to the relevant
  package-manager flow in the [release guide](release.md#distribution-channels).
- Global installs, directories requiring elevation, unknown sources and
  unregistered portable installs must not be replaced by the updater. Do not
  request or bypass elevation.
- Stable is the default channel. Pre-releases are checked only after an explicit
  channel choice; preview and stable versions must not be mixed.

## 3. Automatic update flow

1. Check release metadata over HTTPS, respecting channel, platform, architecture
   and a bounded frequency. A manual check bypasses the interval. The request
   must not send personal identifiers or telemetry.
2. If a compatible release exists, show the installed and available versions.
   When automatic installation is enabled, fetch the package for the
   self-managed installation source; do not download or execute remote installer
   scripts.
3. Download to a restricted temporary area and validate version, RID, size,
   integrity and provenance before changing the installation. The runtime
   provenance-authentication mechanism beyond `SHA256SUMS` must be decided before
   implementation; an unauthenticated checksum alone is not sufficient proof
   of origin.
4. Stage files on the same volume as the installation, run `--version` and
   `selftest` on the new version, and leave the current version intact if any
   step fails.
5. Apply the swap only after the daemon and windows have exited naturally, with
   no active overlay, recording or other session. If a helper process is needed
   to wait for shutdown, it must be platform-specific, restricted to the
   authorized directory and unprivileged.
6. Perform the swap with recovery of the previous version if installation
   fails. Do not restart the application automatically; the next launch uses the
   updated version and can report the outcome.

If an update is downloaded but cannot yet be applied, tell the user that it
will complete when PixelPointer exits. If validation, swapping or recovery
fails, preserve existing files and show an actionable message; never delete the
previous version to free space before confirming success.

## 4. Proposed stages

1. **Finalize requirements and decisions:** choose check interval, metadata
   source, preference experience, channel policy and provenance validation.
2. **Identify installation and preferences:** define safe metadata to distinguish
   per-user, package-manager, global and unknown installations; persist
   per-user preferences without storing secrets.
3. **Implement checking:** compatible-version discovery, manual and automatic
   checks, notifications and suppression of repeated requests.
4. **Implement staged installation:** download, validation, self-test, deferred
   apply, recovery and status messages for Windows, Linux and macOS, respecting
   which channels permit self-updates.
5. **Integrate and document:** update the product's available interface,
   installers, installation/release documentation and channel support.

## 5. Completion criteria

- Both preferences persist per user, are off by default and enforce the
  dependency between automatic checking and installation.
- Manual checks work with automatic checking disabled; releases incompatible
  with the selected channel, OS or architecture are ignored.
- Invalid, interrupted, untrusted or unauthorized updates leave the current
  installation unchanged and never execute unvalidated content.
- Automatic updates do not interrupt an active overlay, recording or daemon, do
  not request elevation and preserve the previous version on failure.
- `.deb`, Homebrew and winget are never replaced by the updater; the user gets
  guidance for the detected channel.
- Tests cover preference policy, source identification, integrity, staging,
  deferral, failure/rollback and notifications, as well as supported OS and
  architecture combinations. Tests that run the application follow the
  repository's container and nginx rules.

## 6. Decisions

The decisions below were chosen by the requester on 2026-11-04 and are detailed
in [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md), still `proposed` until a
recorded human review.

- **Frequency:** daemon only; 5 minutes after it starts and then every 24 hours
  (1 hour after a failure).
- **Metadata:** `releases/latest` only discovers the stable tag; identity comes
  from the tag's assets, `SHA256SUMS`, the attestation and `--version` of the
  attested binary.
- **Provenance:** native verification of the GitHub attestation's Sigstore bundle,
  without `gh` or a new dependency.
- **Origin:** an `install-receipt.json` receipt written by the installers, combined
  with write-access, permission and process-privilege checks.
- **Interface:** the `pixelpointer update` CLI and the tray's **Atualizações**
  submenu; preferences in `update.json`.
- **Apply:** when the user exits the daemon and through `pixelpointer update apply`
  with the daemon stopped; no automatic restart.
- **Preview channel:** not offered in this version; only stable releases are queried.

## 7. Out of scope

- Silent updates for `.deb`, Homebrew, winget or installs requiring elevation.
- Forced restart, automatic downgrade, destructive data migration or executing
  scripts fetched from the network.
- Telemetry, collection of personal identifiers and automatic build-dependency
  updates.
