# Runbook: release

[Português (Brasil)](../../runbooks/release.md) · **English** · [Español](../../es/runbooks/release.md)

Managed file from Altivy.Groundwork (the `release` component). Do not edit the
Portuguese source; changes to that runbook go through the catalog. Project-specific
procedures belong outside this file.

Repository: `lzocateli/PixelPointer` · default branch: `main`.

## 1. Merge is not a release

The squash PR title decides whether a change **counts** toward the next release;
a human decides **when** the release happens by merging the Release PR.

| Title type | Releasable | Bump |
| --- | --- | --- |
| `feat` | yes | minor |
| `fix`, `fix(deps)`, `deps` | yes | patch |
| `perf`, `revert` | yes | patch |
| Any type with `!` or `BREAKING CHANGE:` footer | yes | major (before 1.0: minor) |
| `docs`, `chore`, `ci`, `build`, `test`, `refactor`, `style` | no | — |

- Releasable commits only create or update the Release PR; merging a regular PR
  does not create a tag, release, publication or deployment.
- The `Conventional title` job in `gw-pr-hygiene.yml` shows in the PR summary
  whether the change is releasable and which bump it causes. Check the type before
  approval: a behavior change classified as `refactor` should be `fix` or `feat`.
- `X.Y.Z-preview.N` snapshots from the default branch (when enabled) are not
  releases: they create no tag or GitHub Release and are never accepted by deploy.

## 2. Publish a version

1. Find the Release PR `chore(main): release X.Y.Z` created by the GitHub App:

   ```bash
   gh pr list --repo lzocateli/PixelPointer --head release-please--branches--main
   ```

2. Review `CHANGELOG.md` and the version; wait for the `Result` check.
3. Merge (squash). The `gw-release.yml` workflow:
   - creates tag `vX.Y.Z` and a draft GitHub Release;
   - calls `publish.yml` in the `release` Environment (approve when requested);
   - generates, attests and attaches `release-manifest.json`;
   - publishes the GitHub Release only if all preceding steps succeed;
   - calls `post-release.yml` with the release already public (project distribution, when configured).
4. Confirm:

   ```bash
   gh release view vX.Y.Z --repo lzocateli/PixelPointer
   gh attestation verify release-manifest.json --repo lzocateli/PixelPointer
   ```

## 3. Release without a releasable commit

To force a version (for example, for operational reasons), merge a PR whose
commit has the footer below. This opens a Release PR, which still requires a
human merge.

```text
chore: request operational release

Release-As: X.Y.Z
```

## 4. Republish missing artifacts

If publishing partially fails, the release remains a draft. After fixing the
cause (credentials or unavailable registry), rerun on the **tag**:

```bash
gh workflow run gw-release.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f mode=republish
```

`republish` validates the tag, reruns `publish.yml` idempotently (uploads only
missing artifacts and never overwrites published ones), finalizes the release
and runs `post-release.yml` again with `republish=true`. If `release-manifest.json`
already exists, it is not changed: any digest mismatch makes the workflow fail.

## 5. Fix a defective release

Never delete or move tags, delete GitHub Releases, or rebuild artifacts for a
published version.

1. Open a fix issue (or use the project's bug-report flow) and merge a `fix:` or
   `revert:` PR; both are releasable and cause a patch bump.
2. Merge the resulting Release PR to publish the fixed version.
3. Mark the defective version when applicable:
   - GitHub Release: edit the notes with a warning and link to the fixed version
     (`gh release edit vX.Y.Z --notes-file notes.md`);
   - NuGet.org: mark the version as *deprecated* in the portal and identify its replacement;
   - images: keep the published tag and communicate the fixed version.

## 6. Common failures

| Symptom | Likely cause | Action |
| --- | --- | --- |
| `Require release GitHub App` fails | `vars.RELEASE_APP_ID` or `RELEASE_APP_PRIVATE_KEY` is missing | Configure them manually; never use a personal PAT |
| `Release` run has `startup_failure` and no jobs | Repository (or organization) Actions policy blocks `googleapis/release-please-action` | Run `groundwork github --sync` to allow the SHA in `allowedActions`; if the organization restricts it, allow it there |
| Release PR has no checks | PR was created with `GITHUB_TOKEN` | Confirm the App is installed; the App token triggers checks |
| Tag is rejected | `gw-release-tags` ruleset does not include the App in bypass | Run `groundwork github` and review `release_app_id` |
| `Assert-ReleaseTag.ps1` fails | Version differs between the tag and version files | Fix it in the Release PR; do not create tags manually |
| Release remains a draft | Publish or manifest failed | Fix the cause and use `republish` |
| `Failed to persist attestation: Feature not available` | Attestation is unavailable on the plan for a private repository | Set `"attestations": false` in `.github/ci-cd.json` (see `.github/CI-CD.md`); releases without it cannot be deployed |
| Release PR is blocked by a failed `Result` while another run is green | CI run was canceled on the same commit; the ruleset considers every run of the check | Rerun the canceled run (`gh run rerun <id>`); current `ci.yml` does not cancel PR runs |
