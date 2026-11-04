# ADR 2026-0001: Opt-in updates with Sigstore provenance verified by the application

[Português (Brasil)](../../adr/2026-0001-atualizacao-opt-in.md) · **English** · [Español](../../es/adr/2026-0001-atualizacao-opt-in.md)

**Status:** proposed

**Date:** 2026-11-04  
**Owners:** Lincoln Zocateli  
**Origin:** AI-assisted; options chosen by the requester in the 2026-11-04 session  
**Human reviewer:** pending  
**Related:** [update plan](../plano-atualizacao.md), IMPL-06 in the [implementation plan](../plano-implementacao.md#8-consolidated-pending-items)

## Context

The [update plan](../plano-atualizacao.md) left six open decisions: frequency,
metadata source, runtime provenance proof without `gh`, installation origin
detection, interface and when to apply. Releases already publish `SHA256SUMS`,
SBOM and keyless Sigstore attestations; the standalone installers only check
SHA-256. The application is Native AOT, without reflection, and `settings.json`
is read-only for the application.

## Decision drivers

- never install content without proof of origin or replace third-party channels;
- never request or bypass elevation and never interrupt presentations;
- no new dependency, AOT compatibility and no new CI secret;
- reversible operation that fails closed.

## Considered options

### Provenance

- **Native verification of the Sigstore bundle (chosen):** reuses existing
  attestations with no new key or secret; requires embedding public trust
  material and following rotations.
- **Project ECDSA key signing `SHA256SUMS`:** simple verification, but adds a
  secret, rotation and a new runbook.
- **Postpone installation:** no installation risk, but misses the goal.

### Installation origin

- **Installer receipt plus filesystem checks (chosen):** the receipt alone
  authorizes nothing; it is combined with write access for the current user, no
  group or other write access, a non-elevated process and exclusion of `.deb`,
  Homebrew and winget paths.
- **Path heuristics only:** cannot identify portable installs.

## Decision

1. **Frequency:** only in the daemon, with automatic checking enabled: 5 minutes
   after it starts and then every 24 hours; 1 hour after a failure. Manual checks
   ignore the interval.
2. **Metadata:** `GET /repos/lzocateli/PixelPointer/releases/latest` only
   discovers the stable tag. The package comes from `releases/download/v<version>/`
   and is accepted only with the published size, the SHA-256 from `SHA256SUMS`
   and the attestation; the version is also confirmed by running `--version` on
   the attested binary, which prevents serving another legitimate binary as the
   announced version.
3. **Provenance:** BCL-only C# verification of the bundle returned by
   `GET /repos/.../attestations/sha256:<digest>`: Fulcio chain, CT log SCT, Rekor
   v1 SET and Merkle inclusion proof with signed checkpoint, DSSE signature, log
   body bound to the envelope and certificate, in-toto SLSA v1 statement covering
   the package name and digest, and identity: issuer
   `token.actions.githubusercontent.com`, repository
   `https://github.com/lzocateli/PixelPointer` with numeric IDs `1439215418`
   (repository) and `16601706` (owner), `github-hosted` runner, signer
   `.github/workflows/gw-github-assets.yml` on `refs/heads/main` or `refs/tags/v*`.
4. **Origin:** `install.sh` and `install.ps1` write `install-receipt.json`
   (`installer`, `scope` user/global, `rid`). Only user scope that passes the
   checks above can be replaced; other channels only notify with the package
   manager's command.
5. **Interface:** the `pixelpointer update` CLI and an **Atualizações** (Updates)
   tray submenu; preferences in `update.json`, written by the application with an
   atomic swap that refuses to overwrite an invalid or concurrently changed file.
6. **Apply:** when the user exits the daemon (tray, `quit`, Ctrl+C) and through
   `pixelpointer update apply`/`install` with the daemon stopped. Linux and macOS
   use an atomic directory exchange (`renameat2`/`renamex_np`, falling back to two
   renames with rollback); on Windows, the prepared version completes the swap
   after the previous process exits. No automatic restart.

Revisit this decision if GitHub moves attestations to Rekor v2 or another
issuer, if the release workflow changes or if a preview channel is requested.

## Consequences

### Positive

- User installations update without `gh`, elevation or remote scripts.
- No new dependency, key or secret.

### Negative and trade-offs

- Sigstore trust material and the workflow identity are embedded: a rotation,
  Rekor v2 (Ed25519, not supported by the BCL) or a workflow change makes
  verification fail closed until the user updates with the installer.
- GitHub's anonymous API is rate-limited per IP.
- Installations made before the receipt existed must run the installer once more.

## Security and privacy

Nothing runs before SHA-256 and provenance are accepted; only expected regular
files are extracted; staging stays on the same volume, in a private directory;
helper processes are never elevated and are limited to the directory derived from
their own executable and the marker. No telemetry: only a fixed User-Agent.

## Data, protocol and compatibility

New files: `install-receipt.json` (version 1), `update.json` (version 1) and
`<parent>/.pixelpointer.update/staged.json`. Installers stay compatible with
existing installations.

## Observability and operation

`UPDATE` events in the diagnostic log (no captured content), status in the tray
and in `pixelpointer update status`, and the last apply result on the next daemon
start.

## Validation

- Unit tests with the real v0.3.1 bundle and tampered cases (payload, SET, proof,
  checkpoint, log, identity, digest).
- Real-filesystem tests for preferences, origin, extraction, staging, apply,
  rollback and cleanup.
- E2E in an Ubuntu 24.04 container with a 0.3.0 binary updated from real GitHub to
  the published release; global installation refused.
- Pending: Windows (swap helper, `install.ps1`) and macOS (`renamex_np`).

## Rollout and rollback

Opt-in and disabled by default. To turn it off: `pixelpointer update auto-check off`.
To revert the feature, a release without the command keeps installations working;
a failed update restores the previous version automatically.

## References

- [Update plan](../plano-atualizacao.md) and [usage guide](../usage.md#updating)
- [Sigstore trusted root](https://github.com/sigstore/root-signing/blob/main/targets/trusted_root.json)
- [RFC 6962](https://www.rfc-editor.org/rfc/rfc6962) and [DSSE](https://github.com/secure-systems-lab/dsse)
