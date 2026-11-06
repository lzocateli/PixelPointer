# Runbook: release preview

[Português (Brasil)](../../runbooks/release-preview.md) · **English** · [Español](../../es/runbooks/release-preview.md)

Translation of the file managed by Altivy.Groundwork (the `release-preview` component); the
Portuguese version is normative. The full strategy is in
[estrategia-de-release](../estrategia-de-release.md).

Repository: `lzocateli/PixelPointer` · default branch: `main` · acceptance: `approval`.

## 1. Create the candidate

1. Confirm the Release PR is open and up to date:

   ```bash
   gh pr list --repo lzocateli/PixelPointer --head release-please--branches--main
   ```

2. Start the workflow on the default branch:

   ```bash
   gh workflow run gw-preview.yml --repo lzocateli/PixelPointer --ref main
   ```

3. The workflow computes `vX.Y.Z-rc.N`, creates the tag and private draft, builds/tests the packages,
   and uploads a private `release-assets` artifact to the run. Nothing is public yet.
4. Open the run from the `distribution` Environment link, download `release-assets`, verify
   `SHA256SUMS`, and test the package. Approve `distribution` only afterward; the workflow attaches
   the same artifacts to the prerelease without rebuilding, attests `release-manifest.json`, and
   does not update Homebrew or winget.
5. Check it:

   ```bash
   gh release view vX.Y.Z-rc.N --repo lzocateli/PixelPointer
   ```

## 2. Validate in quality

The `Dispatch preview environment` job of `gw-preview.yml` starts `gw-deploy.yml` on the candidate
tag when `deploy.autoDeployEnvironment` is `quality` or the acceptance is `approval`. Otherwise,
start it yourself:

```bash
gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z-rc.N -f environment=quality
```

Test the candidate and **approve the Environment** only when it can become the stable release.

On success, the `Record candidate acceptance` job attaches `preview-acceptance.json` to the
prerelease and reruns the Release PR CI.

## 3. Promote

1. Check the `Preview gate` check on the Release PR; it shows the validated candidate.
2. Merge the Release PR (squash) and follow the [release runbook](release.md).
3. Deploy to `production` on the stable tag:

   ```bash
   gh workflow run gw-deploy.yml --repo lzocateli/PixelPointer --ref vX.Y.Z -f environment=production
   ```

   The guard checks the validated candidate and the diff between it and the stable release.

## 4. Common failures

| Symptom | Likely cause | Action |
| --- | --- | --- |
| `Nenhum Release PR aberto` (no open Release PR) | no releasable commit since the last stable release | merge a `feat`/`fix` or use `Release-As:` |
| `O Release PR #N não está sobre <sha>` (Release PR not on the commit) | release-please has not updated the Release PR yet | wait for `gw-release.yml` on `main` and start again |
| `Preview gate` red after acceptance | `main` moved and the Release PR changed its base commit | create a new candidate (`rc.N+1`) and validate it |
| `Preview gate` red with no new merge | the Release PR CI was not rerun | `gh run rerun <id>` on the Release PR CI run |
| Candidate deployment refused | Environment other than `quality` or tag outside the `-rc.N` pattern | use `quality`; `preview.N` prereleases are never deployable |
| `production` refused because of the diff | code merged after the candidate | new version with a new candidate |
| Emergency with `quality` unavailable | — | commit with the `Preview-Waiver: <reason>` footer since the last stable release |
