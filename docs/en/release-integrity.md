# Release Source Integrity

[Português (Brasil)](../release-integrity.md) · [English](release-integrity.md) · [Español](../es/release-integrity.md)

## Implemented Contract

New candidates, releases, snapshots and packages require a checkout at the
current remote HEAD of the default branch. `Assert-ReleaseSource.ps1` uses
`Assert-CurrentReleaseSource`: push CI for `ci.yml` on that branch must be green
on the exact SHA, including `Result` from GitHub Actions in the same check suite.
Missing/failing CI or inconclusive queries reject publication. HEAD is reread
after CI queries; ancestry alone is insufficient. Brazilian Portuguese is normative.

The RC uses the open Release PR version and its base must match the source SHA.
After `release` approval, resolution is repeated with the expected Release PR
head and tag before tag/draft creation. Asset/package publication also validates
source after approval; finalization revalidates before making the GitHub Release
public. Workflows must never remove these guards.

Preview gate requires the current Release PR head, integrated dependencies,
current base and green CI before acceptance or waiver. A pending merged final
release is validated **before** release-please creates its tag: the merge commit
must be current source and its parent the accepted candidate for that version.
Only configured `versionFiles` and release-please files may differ. RC/final SHAs
differ because of the version bump/merge. Allowed files do not authorize disguised
functional changes; human review remains mandatory.

When only updating the Release PR, release-please uses `skip-github-release: true`;
only guard output `release_ready=true` authorizes release/tag creation.

## Integrations and Fixes

Regular PRs, including fixes, require no RC acceptance and are not blocked by
unrelated open PRs. For specific required integrations, the operator records in
the versioned `.github/ci-cd.json`, through a reviewed PR:

```json
"releaseDependencies": [123, 456]
```

Default is `[]`; include only mandatory integrations for this release scope.
Remove satisfied dependencies only when reviewing the next scope. This versioned
list survives note regeneration. The Release PR body may supplement it without
editing its generated branch:

```text
Release-Depends-On: #123, #456
```

The two lists are combined and local to the repository. Each PR must be merged into the default
branch and its resulting SHA included in approved source; squash is checked using
`merge_commit_sha`. Closing without merging is insufficient. Dependencies are
reread after approval and in Preview gate. Release-please may regenerate the body:
verify/preserve the marker when updating the Release PR. Undeclared dependencies
are not inferred from open PRs; use versioned configuration as the durable record.
Removing required dependencies changes release
scope and requires human review, never automatic recovery.

After new code merges, update the Release PR and generate/accept `rc.N+1`.
`Preview-Waiver` only provides an auditable exception to candidate acceptance;
it never permits stale SHAs, failing CI or missing mandatory dependencies.

## Recovery and Migration

Signals include stale source, pending CI/integrations or RC/final code differences.
Wait for integration/CI, update the Release PR and generate a fresh run/candidate.
Never move existing tags or overwrite assets. If main CI was still running when
the release push triggered, resume after it turns green using authenticated CLI
from the repository root:

```bash
gh workflow run gw-release.yml --ref main -f mode=release
```

Replace `main` with the configured default branch. This command may create tags
and releases and requires explicit operational authorization. Interrupted drafts
require revalidated source, not historical republish. Escalate to the maintainer
if main advanced past a merged Release PR still awaiting its tag.

Republish is exempt from current HEAD only for an immutable stable tag of an
**already public** release, matching checkout and missing artifacts. Redeploy/
rollback retain historical published tags and deployment/`rollbackMinimumVersion` policy.

Update consumers using `groundwork update --dry-run`, review, then apply
`groundwork update` with an authorized local/distributed catalog. Seeds are not
automatically migrated. In `ci.yml`, grant hygiene callers `pull-requests`,
`actions`, `checks: read`; snapshots depend on `result` but never enter Result's
`needs`. In `publish.yml`, propagate those permissions and `republish` to every
publisher, including nested calls. Preserve customization; never manually change
hashes to hide drift.

Local catalog fixes are not a distributed version: merge and publish the catalog
through the authorized process, then update/merge consumers. Do not reapply an
older catalog lacking these guards over the validated local changes. Local tests
do not activate remote workflow changes.

## Security and Limits

Covered threats: approval of old source, CI from a PR/wrong SHA/app/suite, omitted
integrations, RC/final differences, inconclusive APIs and drafts disguised as
republish. Guards use read queries and reject before mutations; tokens are not
printed. Reviewers protect metadata, configuration and dependencies.

GitHub offers no atomic transaction spanning HEAD, checks and tag creation/upload.
Revalidation reduces the window but does not lock concurrent server merges.
Tags/artifacts remain immutable; a later detected change leaves the draft blocked,
never silently promoted. Historical public packages are not invalidated merely
because main advances. Local fake-API tests do not verify live GitHub permissions/
protections or human graphical acceptance.

Validate `ReleasePreviewTests`, catalog contracts and rendered workflows with
actionlint in containers. Essential cases: main advances during approval/query,
wrong/pending/failing CI, squash/missing dependencies, RC/final code differences,
waiver and historical republish.