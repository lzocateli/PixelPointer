# Workflow Flow

[Português (Brasil)](../fluxo-workflows.md) · [English](fluxo-workflows.md) · [Español](../es/fluxo-workflows.md)

## Scope and Status

Review of local code on 2026-11-03, not verification of live GitHub configuration.
This guide supplements the [managed strategy](../estrategia-de-release.md) and
[CI/CD contract](../../.github/CI-CD.md); it does not change those contracts.
Guards were implemented in the catalog and applied locally through managed update.
The [integrity contract](release-integrity.md) documents interfaces, recovery,
migration and limits. Remote activation requires merging these changes.
Brazilian Portuguese is normative.

## Implemented Flow

```mermaid
flowchart TD
    PR["Regular PR: feat, fix, docs and others"] --> CI["ci.yml: hygiene, Gitleaks, build, tests, AOT"]
    CI --> RESULT["Required Result + review + up-to-date base"]
    RESULT --> MERGE["Squash merge into main"]
    MERGE --> MAINCI["ci.yml on main SHA"]
    MERGE --> RP["gw-release.yml: release-please creates or updates Release PR"]
    RP --> RPCI["Release PR CI: includes Preview gate"]
    RP --> MANUAL["Manual gw-preview.yml dispatch on main"]
    MANUAL --> RESOLVE["Get-PreviewVersion: version, dependencies, current HEAD and green CI"]
    MAINCI --> RESOLVE
    RESOLVE --> RELRC["release Environment: approval"]
    RELRC --> RCTAG["GitHub App creates vX.Y.Z-rc.N + draft prerelease"]
    RCTAG --> RCCTX["Validate tag version and ancestry"]
    RCCTX --> PUB["publish.yml: build 6 RIDs, smoke, installers, SBOM"]
    PUB --> ASSETS["gw-github-assets.yml: release Environment, assets and provenance"]
    ASSETS --> RCFINAL["gw-preview.yml: attested manifest + public prerelease, no Latest"]
    RCFINAL --> Q["gw-deploy.yml on tag: quality guard"]
    Q --> QA["deploy.yml: human approval in quality Environment"]
    QA --> QS["Verify assets + linux-x64 package smoke in containers"]
    QS --> ACCEPT["preview-acceptance.json + rerun Release PR CI"]
    ACCEPT --> RPCI
    RPCI --> RPOK["Green Result + human review"]
    RPOK --> RPMERGE["Squash merge Release PR into main"]
    RPMERGE --> FINALGUARD["Guard before tag: current SHA, green CI and accepted RC code"]
    FINALGUARD --> FINALTAG["gw-release.yml: GitHub App creates vX.Y.Z + draft release"]
    FINALTAG --> FINALCTX["Validate tag versions and ancestry"]
    FINALCTX --> FINALPUB["publish.yml: build, smoke, SBOM and assets in release Environment"]
    FINALPUB --> STABLE["gw-release.yml: attested manifest + public stable release"]
    STABLE --> POST["post-release.yml"]
    POST --> BREW["release Environment: update Homebrew + verify installation"]
    POST --> WINGET["winget.yml: validation + submission in winget Environment"]
    STABLE --> AUTOQ["Auto-dispatch gw-deploy.yml to quality"]
    STABLE --> PROD["Manual gw-deploy.yml on stable tag: production"]
    AUTOQ --> VERIFYQ["Guard + quality approval + assets + smoke"]
    PROD --> VERIFYPROD["Guard + production approval + assets + smoke"]
```

Main CI and release-please start from the same push, but there is no cross-workflow
`needs` dependency. The diagram does not imply that main CI finishes before
candidate resolution. Preview gate allows the auditable waiver described in the
strategy; regular PRs, including fixes, do not require this gate.

Human approval in `quality` must represent manual package testing: the Environment
wait occurs **before** asset verification and automated smoke. Subsequent success
records acceptance; smoke does not replace human graphical testing.

Stable packages are rebuilt from the final tag, not promoted by reusing RC binaries.
The strategy requires equivalent code, with only release metadata differences.
`production` **does not gate public publication or distribution**: both have
already started when that manual flow runs. PixelPointer has no deployed server;
deployment verifies published packages.

## Other Paths

| Entry | Path and outcome | Environments |
| --- | --- | --- |
| `publish.yml`, dispatch on `main` with preview | Build and validation; creates `vX.Y.Z-preview.N` and GitHub Pre-release without Latest; neither promotable nor deployable | `release` |
| `publish.yml`, PR changing release chain | Build, smoke and installers; no tag or publication | None |
| `gw-release.yml`, dispatch on tag with `mode=republish` | Validates tag and existing release; restores missing artifacts, finalizes and reruns distribution; no new version or auto-deploy | `release`, `winget` for submission |
| `gw-deploy.yml`, historical tag dispatch | Redeploy/rollback using the same guard and smoke; respects `rollbackMinimumVersion`; no recreated tag or release | `quality` or `production` |
| `winget.yml`, dispatch | Validates supplied version; optional, idempotent submission | `winget` if submitting |
| `scheduled-smoke.yml`, Monday 09:00 UTC or dispatch | Rebuilds and tests AOT/packages; no publication | None |
| `gw-gitleaks.yml` | Called by CI; also accepts dispatch; scanning without publication | None |
| `gw-pr-hygiene.yml` | Called by CI: title, actionlint, managed files and Release PR Preview gate | None |
| `gw-github-assets.yml` | Called by publisher: digest-based immutable uploads and provenance | `release` |
| `gw-dotnet.yml`, `gw-node.yml` | Available reusable workflows; not called by current project CI | None in this flow |

`publishSnapshots: true` enables manual preview, but current CI does not call
automatic snapshot publication on each push. The `preview` Environment mentioned
in the strategy is absent from the local Environment policy. `winget` is referenced
by its workflow but is also absent from that policy. Their actual existence and
protections on GitHub have not been verified.

## Corrected Gaps

These items describe behavior before the guards introduced in this change.

- RC resolution compares the Release PR base to dispatch `github.sha`; it does
  not reread remote HEAD immediately before tag creation after `release` approval.
- `Assert-ReleaseTag.ps1` requires ancestry, not equality with current HEAD.
  This is appropriate for historical tags but insufficient for a fresh RC guarantee.
- An open feature PR is not detected as an RC dependency. A package can correctly
  match `main` yet lack the change the operator expected to test.
- Release-please creates the final tag before context and publication checks.
  A publisher-only gate cannot prevent tag/draft creation.

## Integrity Controls

The plan below was implemented **in the Groundwork catalog** and applied locally.
`Get-PreviewVersion.ps1`, `Assert-PreviewGate.ps1`, `Assert-ReleaseSource.ps1` and
publishers execute the guards. The literal marker
`Release-Depends-On: #123, #456` in the Release PR body supplements the durable
`releaseDependencies` array in `.github/ci-cd.json`. Open PRs do not imply
dependencies; verify preservation when release-please regenerates notes. The
points below record policy; the linked contract details its limits.

1. Pin candidate identity: main SHA, Release PR SHA/base, version and expected
   dependencies. Reread remote HEAD after approval and before tag creation;
   require the same base and green `Result` on the exact main SHA. Pending CI,
   a changed base or inconclusive queries must reject, never reuse stale data.
2. Do not block on every open PR. If a candidate must contain a specific PR,
   declare that dependency explicitly in the Release PR scope, using a validated
  format validated by the catalog. Require merge into main and inclusion of the
   resulting commit, accounting for squash; closing without merging is insufficient.
3. Regular fixes remain mergeable through normal CI. After integrating code changes,
   update the Release PR and generate/accept `rc.N+1`; never reuse acceptance for
   previous code. Show pending PR, expected/current SHA and recovery action.
4. Before merging the Release PR, require acceptance matching the current base and
   a diff restricted to allowed files. Before creating the final tag, revalidate
   the merged Release PR commit and code equivalence with the candidate; do not
   require literal RC/final SHA equality, since the bump produces another commit.
5. Main changes during publication approval must reject new releases and require
   fresh resolution. Keep republish/redeploy/rollback separate: immutable historical
   tags must not be required to track current HEAD.
6. An emergency waiver may skip RC acceptance with an auditable reason, but must
   never authorize stale source, missing mandatory dependencies or failing CI.

Primary discriminating test: resolve at SHA A, advance main to B during approval,
and reject **before** creating a tag or draft. Also cover pending/squash-merged
dependencies, pending/failing CI, a fix followed by a new RC, RC/final code
differences and permitted historical republish/rollback.

## Validation and Next Step

Local container tests cover source/CI, dependencies, RC/final differences and
preview without improper mutations. No tags/releases were created, workflows
dispatched, policies applied or remote checks/protections verified. Managed files
came from catalog update; seeds and customization were preserved. The diagram
shows functional stages; guards now precede tags and uploads.
