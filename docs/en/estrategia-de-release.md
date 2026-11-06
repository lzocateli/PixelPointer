# Release strategy

[Português (Brasil)](../estrategia-de-release.md) · **English** · [Español](../es/estrategia-de-release.md)

Translation of the file managed by Altivy.Groundwork (the `release-preview` component); the
Portuguese version is normative. The operational steps are in
[runbooks/release-preview.md](runbooks/release-preview.md) and
[runbooks/release.md](runbooks/release.md).

Repository: `lzocateli/PixelPointer` · default branch: `main` · candidate acceptance: `approval`.

## 1. Summary

Every stable version `vX.Y.Z` is preceded by a **candidate** `vX.Y.Z-rc.N`, published by the same
workflows, validated in the `quality` Environment and only then promoted. The stable release is the
same code as the validated candidate: between them only version files, `CHANGELOG.md` and
`.release-please-manifest.json` change.

```text
PR (squash, Conventional Commit)
  └─▶ main ─▶ release-please updates the Release PR (version + CHANGELOG)
            └─▶ gw-preview.yml (manual) ─▶ tag/draft ─▶ private preflight and tests
              └─▶ distribution (approval after tests) ─▶ publish same artifact ─▶ attested prerelease
                  └─▶ quality: deploy/smoke + human approval ─▶ preview-acceptance.json
                          └─▶ Preview gate green on the Release PR
                  └─▶ human merge ─▶ tag/draft ─▶ preflight and tests
                    └─▶ distribution (approval after tests) ─▶ publish without rebuild ─▶ stable release
                                          └─▶ production (manual, with approval)
```

## 2. Branches

`main` is the only long-lived branch. Candidates and stable releases are **tags**, not branches:
there are no staging branches (`qas`, `develop`, `release/*`) or feed branches (`nugettest/*`), and
the `quality` Environment receives the candidate through the `vX.Y.Z-rc.N` tag.

| Branch | Owner | Lifetime | Rule |
| --- | --- | --- | --- |
| `main` | human | permanent | `gw-*` rulesets; only squash merges of PRs with a green `Result` |
| `release-please--branches--main` | release-please (GitHub App) | until the Release PR merge | generated; never edit by hand |
| `dependabot/*` | Dependabot | until the PR is merged or closed | generated |
| `feat/*`, `fix/*`, `docs/*`, `ci/*` and similar | PR author | until the merge | delete on merge (`--delete-branch`) |

A branch without an open PR whose content is already in `main` is leftover and should be deleted.
Check first with `git merge-base --is-ancestor origin/<branch> origin/main` or, for squash merges,
`git diff origin/main origin/<branch>` with no difference in its files.

## 3. Versions and channels

| Channel | Version | Source | Tag and GitHub Release | Deployable |
| --- | --- | --- | --- | --- |
| snapshot | `X.Y.Z-preview.N` | each push to `main` (if `publishSnapshots`) | no | never |
| candidate | `X.Y.Z-rc.N` | `gw-preview.yml`, version of the open Release PR | tag + prerelease without *Latest* | only in `quality` |
| stable | `X.Y.Z` | Release PR merge | tag + *Latest* release | `quality` and `production` |

- `N` is sequential per version: the first candidate of `X.Y.Z` is `rc.1`; a fix after it produces
  `rc.2` on the next run.
- The candidate tags the **Release PR base commit**, the `main` HEAD on which release-please computed
  the version. If `main` moves, the Release PR is updated and a new candidate is required.
- Prereleases that do not follow `vX.Y.Z-rc.N` (for example `preview.N`) are never deployable or
  promotable.

## 4. CHANGELOG and Conventional Commits

release-please writes `CHANGELOG.md` from PR titles (squash merge). No human edits the CHANGELOG
outside the Release PR.

| PR title | Goes into the CHANGELOG | Bump |
| --- | --- | --- |
| `feat` | yes | minor |
| `fix`, `fix(deps)`, `deps`, `perf`, `revert` | yes | patch |
| `!` or `BREAKING CHANGE:` footer | yes | major (before 1.0: minor) |
| `docs`, `chore`, `ci`, `build`, `test`, `refactor`, `style` | no | — |

The `Conventional title` job rejects titles outside the pattern and shows the release impact in the
PR summary. The candidate notes reproduce the Release PR section with a warning that it is not the
stable version; the stable notes get the line "Validada na `vX.Y.Z-rc.N`" (validated in that
candidate).

## 5. Environments

| Environment | Accepted refs | Reviewers | Use |
| --- | --- | --- | --- |
| `release` | `main`, `v*` | `@lzocateli` | retained for legacy operations; not used to approve the current AOT flow |
| `release` | `main`, `v*` | `@lzocateli` | credentials for components that still use this Environment; does not replace `distribution` |
| `distribution` | `main`, `v*` | `@lzocateli` | authorizes public release; with `distributionAfterValidation=true`, occurs after private preflight |
| `quality` | `v*` | required | candidate deployment and acceptance; also accepts stable releases |
| `production` | `v*` | required | only stable releases with a validated candidate |
| `preview` | `main` | — | snapshots in the internal feed |

The Environments, ref policies and reviewers come from `.github/repository-policy.json` and are
applied by `groundwork github --sync`.

## 6. Candidate acceptance

`approval` mode (application): the deployment to `quality` runs the automated smoke test of
`deploy.yml` and waits for **human approval** in the `quality` Environment. Approving means: "I have
tested the candidate and it can become the stable release".

After a successful deployment, the `Record candidate acceptance` job of `gw-deploy.yml`:

1. attaches the `preview-acceptance.json` asset to the prerelease (`tag`, `version`, `commit`,
   `environment`, `result`, `acceptedAt`, `actor`, `run`);
2. reruns the CI of the open Release PR so that `Preview gate` is evaluated again.

## 7. Gates

| Gate | Where | Rule |
| --- | --- | --- |
| `Preview gate` | `gw-pr-hygiene.yml` job, part of `Result` | the Release PR passes only with a `vX.Y.Z-rc.N` candidate published on the base commit and accepted in `quality`, or with a waiver |
| Deployment guard | `Assert-DeployableRelease.ps1` | candidates only in `quality`; `production` requires the validated candidate of the same version, with a diff limited to version files and the CHANGELOG, or a waiver |
| Tag | `Assert-ReleaseTag.ps1` | candidate with a version greater than the recorded one; stable equal to the version files |
| Signature | attested `release-manifest.json` | candidate signed by `gw-preview.yml`; stable by `gw-release.yml` |

### Waiver

In an emergency (security fix, unavailability), a commit since the last stable release can skip the
candidate with the footer:

```text
fix: corrija a falha de autenticação

Preview-Waiver: security fix published without a candidate because quality is unavailable
```

The waiver clears `Preview gate` and the `production` guard, and appears in the deployment summary
and the release notes. It is an auditable exception, not a shortcut: record the real reason.

## 8. Automated and human steps

| Step | Automated | Human |
| --- | --- | --- |
| Regular PR | CI, title, Gitleaks, snapshot | review and merge |
| Release PR | version and CHANGELOG | — |
| Candidate | N, tag/draft, private preflight, build/tests, SBOM, publish the same artifact | start `gw-preview.yml`; test the private artifact and approve `distribution` |
| `quality` | deployment, smoke test and acceptance record | test and approve `quality` |
| `Preview gate` | full check | waiver, when needed |
| Stable | tag/draft, private preflight, validation, publish without rebuild, release and candidate notes | merge the Release PR; approve `distribution` after validation |
| `production` | guard | start and approve `production` |
| Repository | Environments, variables and rulesets (`groundwork github --sync`) | missing secrets (`--secrets`) |

## 9. Failures and rollback

- **Candidate rejected in `quality`:** fix it with a regular PR; release-please updates the Release
  PR and the next run produces `rc.N+1`. The rejected candidate stays as a prerelease, without
  acceptance.
- **`Preview gate` red:** there is no accepted candidate on the current base commit (`main` moved,
  the acceptance failed or the Environment does not match). Create and validate a new candidate.
- **`production` refused:** the stable release differs from the candidate beyond version files
  (code was merged after the validation). Ship a new version with a candidate.
- **Rollback:** deploy an earlier published stable release, subject to the `rollbackMinimumVersion`
  lock; never delete tags or releases (see [runbooks/deploy-e-rollback.md](runbooks/deploy-e-rollback.md)).
