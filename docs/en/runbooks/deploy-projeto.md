# PixelPointer Deployment

[Português (Brasil)](../../runbooks/deploy-projeto.md) · **English** · [Español](../../es/runbooks/deploy-projeto.md)

Altivy.Groundwork seed (`deploy` component): this file belongs to the project and
complements the standard procedure in [deploy-e-rollback.md](deploy-e-rollback.md).

PixelPointer is an application distributed through GitHub Releases, Homebrew and
winget, without a server. The "deployment" therefore installs nothing on a host: it
checks that the published release is exactly the one in the attested manifest and runs
the published package. It is the acceptance of the `vX.Y.Z-rc.N` candidate in
`quality` ([release-preview](release-preview.md)).

## Targets

| Environment | Target | Runner | Reviewers |
| --- | --- | --- | --- |
| `quality` | GitHub Release assets (candidate or stable) and the maintainer's test machines | GitHub `ubuntu-24.04` | `@lzocateli` (required: `approval` acceptance) |
| `production` | Stable GitHub Release assets; Homebrew and winget require independent dispatches with an explicit version | GitHub `ubuntu-24.04` | `@lzocateli` |

## Execution

`.github/workflows/deploy.yml`, called by `gw-deploy.yml` after the guard:

1. **Environment approval.** In `quality`, approve only after testing the candidate on
   the test machines ([release process](../release.md)).
2. **`Apply release`:** downloads every asset of the tag and compares each SHA-256 with
   the validated, attested `release-manifest.json`. It changes nothing; running it
   again gives the same result. It uses only `GITHUB_TOKEN` with `contents: read`; there
   is no Environment credential.
3. **`Smoke test`:** runs `tools/scripts/Test-AotPackage.ps1 -Rid linux-x64` on the
   **published** package (not a new build), in clean containers without .NET:
   `--version` with the release version, `--help`, `selftest`, SBOM and `.deb`.

When it succeeds in `quality` for a candidate, `gw-deploy.yml` attaches
`preview-acceptance.json` to the prerelease and reruns the Release PR CI.

## Migrations and compatibility

There are no data or protocol migrations between versions; `rollbackMinimumVersion`
stays `0.0.0`. Rollback means installing an earlier published release.

## Smoke test and post-deployment checks

The automated smoke test covers only `linux-x64`. Windows, macOS, Wayland and real use
(zoom, annotation, recording and global hotkeys) are checked by hand by the maintainer
before approving `quality`, as described in [usage](../usage.md).

## Contacts

Approval, operation and incidents: `@lzocateli`.
