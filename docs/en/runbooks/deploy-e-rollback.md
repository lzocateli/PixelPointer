# Runbook: deployment, redeployment and rollback

[Português (Brasil)](../../runbooks/deploy-e-rollback.md) · **English** · [Español](../../es/runbooks/deploy-e-rollback.md)

Translation of the file managed by Altivy.Groundwork (the `deploy` component); the Portuguese
version is normative. Targets, runner, migrations and contacts are in
[deploy-projeto.md](deploy-projeto.md).

Repository: `lzocateli/PixelPointer` · deployment Environments: `quality`, `production` ·
auto-deploy: `quality` · reviewers: `@lzocateli`.

## 1. Principles

- Deploy only a **published** GitHub Release: `gw-deploy.yml` runs on the `vX.Y.Z` tag and the
  `Assert-DeployableRelease.ps1` guard refuses branches, drafts, tags outside `main`, missing or
  unattested manifests and prereleases in `production`.
- A prerelease is deployable only if it is a `vX.Y.Z-rc.N` candidate and only in the preview
  Environment (`deploy.previewEnvironment`, `quality` by default); success there records the
  candidate acceptance.
- With `preview.requiredForRelease`, `production` requires the validated candidate of the same
  version, with no code different from it, or a commit with `Preview-Waiver:` since the previous
  stable release (see [../estrategia-de-release.md](../estrategia-de-release.md)).
- Deployment Environments accept only `tag:v*` refs; GitHub itself refuses a deployment job on a
  branch.
- Artifacts are immutable and identified by digest in `release-manifest.json`; deployment never
  rebuilds anything.
- Rollback is the deployment of an earlier, already published release. Never delete or move tags,
  never delete GitHub Releases and never rebuild artifacts.
- Deployments to the same Environment do not overlap (`concurrency: deploy-<environment>`).

## 2. Deploying a new release

1. After the Release PR merge, `gw-release.yml` publishes the release.
2. The `Auto deploy` job automatically starts the deployment to `quality` on the tag. Approve it
   in the Environment and follow the smoke test.
3. Promotion to `production` is always manual, on the **same validated tag**, with approval:

   ```bash
   gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f environment=production
   ```

   In the UI: Actions → Deploy → Run workflow → *Use workflow from: Tags / vX.Y.Z*.

## 3. Redeploying the same release

Use it when the host was recreated, after a transient failure or to reapply the configuration.

```bash
gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f environment=<environment>
```

The guard validates the release, the Environment asks for approval and `deploy.yml` applies the
**same digests**. Check the smoke test and the running version. Fixing the deployment procedure of
a version requires a new release: running on the tag uses that release's `gw-deploy.yml` and
`deploy.yml`.

## 4. Rollback

1. **Choose between rollback and roll-forward.** Prefer rollback when the defect is severe, the
   cause is unclear and the earlier version is compatible with the current data. Prefer
   roll-forward when the fix is simple or the earlier version cannot read the current
   data/protocol.
2. **Identify the current and target versions:**

   ```bash
   gh api "repos/lzocateli/PixelPointer/deployments?environment=<environment>" --jq '.[0:5][] | {ref, created_at, creator: .creator.login}'
   pwsh ./tools/scripts/ci/Get-DeployedRelease.ps1 -Environment <environment> -Repository lzocateli/PixelPointer
   gh release list --repo lzocateli/PixelPointer --exclude-drafts
   ```

3. **Check the `rollbackMinimumVersion` lock** in the manifest of the current version:

   ```bash
   pwsh ./tools/scripts/ci/Get-ReleaseManifest.ps1 -Tag v<current> -Repository lzocateli/PixelPointer -OutputPath /tmp/release-manifest.json
   jq -r .rollbackMinimumVersion /tmp/release-manifest.json
   ```

   The target version cannot be lower than that value.
4. **Run it on the earlier tag:**

   ```bash
   gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref v<earlier> -f environment=<environment>
   ```

5. **Approve** in the Environment, follow the smoke test and confirm the running version.
6. **Communicate** the rollback, record the reason (the deployment history records who, when and
   which tag) and open the fix as an Issue or through the project's bug flow.
7. **Return to the fixed version through a new release** (`fix:` or `revert:` → Release PR →
   deployment). Never redeploy the defective release.

## 5. When the rollback is refused

The guard refuses targets below the `rollbackMinimumVersion` recorded in the manifest of the
deployed version. This means the current version made an irreversible change (*contract*
migration, removal of the N-1 protocol, incompatible data format) and the earlier version would
not work with the current state. The workflow has no bypass: **roll forward** (fix → Release PR →
new release → deployment). If the lock is wrong, fix `rollbackMinimumVersion` in
`.github/ci-cd.json` and publish a new release.

## 6. Failures during deployment

| Situation | Action |
| --- | --- |
| Transient deployment failure | Run the same command on the same tag again; `deploy.yml` is idempotent |
| Approval expired | Start it again on the tag and approve in time |
| Smoke test failed | Consider rollback (section 4) or redeployment (section 3); do not change the environment by hand |
| Concurrent deployment blocked | Wait for the running deployment to finish; do not cancel it midway |
| Guard refused | Read the reason in the `Guard` job summary; branches, drafts, prereleases in `production` and releases without attestation (`"attestation": "none"`) are refused by design |

## 7. Republishing missing artifacts

Republishing (`gw-release.yml` with `mode=republish` on the tag) completes a partial
**publication**: it uploads only the missing artifacts and finalizes the release. Redeployment
(section 3) reapplies an already published release to an Environment. A draft release is not
deployable until the republication finishes.

```bash
gh workflow run gw-release.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f mode=republish
```

## 8. Audit

- Deployment history per Environment (who, when, which tag):
  `gh api "repos/lzocateli/PixelPointer/deployments?environment=<environment>"`.
- Logs and summaries of the `Deploy` workflow (the `Guard` job records deployment, redeployment or
  rollback).
- Attested `release-manifest.json` of each release:
  `gh attestation verify release-manifest.json --repo lzocateli/PixelPointer`.

## 9. Quick rollback checklist

- [ ] Incident recorded and owner assigned.
- [ ] Current and target versions identified (`Get-DeployedRelease.ps1`, `gh release list`).
- [ ] Target greater than or equal to the current version's `rollbackMinimumVersion`.
- [ ] `gh workflow run gw-deploy.yml --ref v<earlier> -f environment=<environment>` started.
- [ ] Approval granted in the Environment and smoke test green.
- [ ] Running version confirmed.
- [ ] Communication sent and fix opened.
- [ ] Fix shipped in a new release, without redeploying the defective version.
