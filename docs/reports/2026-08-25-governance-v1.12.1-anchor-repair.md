# Governance v1.12.1 source-anchor repair

Issue FS-GG.Governance#418 restores one missing source anchor. It is not a
package release: `FS.GG.Governance.Cli` and
`FS.GG.Governance.FSharpSurfaceCommand` 1.12.1 are already available on both
feeds, and every package names commit
`388819d0e060c11f53e5ca2df3277a54a13f9e75`.

## Why a direct tag push is unsafe

The `publish` workflow at both current `main` and the artifact commit subscribes
to `push.tags: ['v*']`. Creating `v1.12.1` while the workflow is active would
therefore start the publisher from the historical commit. `--skip-duplicate`
protects immutable feed versions, but it does not prevent a workflow run,
packing, authentication, or attempted publication. That violates this repair's
no-publication boundary.

## Authorized delivery recipe

Run only after the evidence PR has passed independent review and the exact
target has host authorization.

1. Fetch `origin`, prove commit `388819d0e060c11f53e5ca2df3277a54a13f9e75`
   exists, enumerate the complete remote tag namespace, require `v1.12.1`
   absent, and observe known-present lightweight tag `v1.12.0` as the positive
   control.
2. Download both 1.12.1 package identities from GitHub Packages and nuget.org.
   Require every nuspec repository commit to equal the target. Compare every
   archive entry and byte after excluding only nuget.org's `.signature.p7s`.
3. Read Actions workflow id `303994065`; require its state to be `active`.
   Enumerate all pages of its runs and save the complete set of run ids. The
   authority's `total_count` must equal the enumerated count.
4. Establish a cleanup handler that runs `gh workflow enable publish.yml --repo
   FS-GG/FS.GG.Governance` on every exit after the next step, including failure.
5. Run `gh workflow disable publish.yml --repo FS-GG/FS.GG.Governance`, then
   re-read workflow id `303994065` and require state `disabled_manually`.
6. Push only the lightweight ref with `git push origin
   388819d0e060c11f53e5ca2df3277a54a13f9e75:refs/tags/v1.12.1`. Do not create
   a GitHub Release.
7. Read `refs/tags/v1.12.1` from the remote and require it to resolve exactly to
   the target commit. Re-enumerate workflow runs while the workflow remains
   disabled and require no new run id and no `head_branch=v1.12.1` run.
8. Re-enable `publish.yml`, require workflow state `active`, and repeat the
   complete run census. Re-download the four feed artifacts and require the
   pre-delivery nuspec and unsigned-content digests unchanged.

If tag creation fails, restore the workflow and leave the item incomplete. If
tag creation succeeds but any later check fails, restore the workflow, retain
the immutable tag, and report the failed postcondition for recovery; never
delete or retarget the tag and never attempt a compensating publication.

## Pre-delivery evidence

The machine-readable companion is
`readiness/418-governance-release-anchor-repair/pre-delivery-evidence.json`.
At candidate base `b20edb3c6b4b19d658ab7ee1208356972d8728cf`, it records:

- 20 complete remote tag refs; `v1.12.0` present and `v1.12.1` absent;
- 36 of 36 paginated `publish` workflow runs, with the sole run at the target
  commit identified as the original successful `workflow_dispatch` run
  `31563788761`, and zero runs for branch/tag `v1.12.1`;
- workflow id `303994065` in `active` state;
- identical unsigned package trees across feeds: 174 entries for the CLI and
  38 for the surface command.

The tracked read-only verifier
`readiness/418-governance-release-anchor-repair/verify_pre_delivery.py` produced
`pre-delivery.junit.xml`: 6 passed, 0 failed. A bounded mutation changed the
complete remote-tag baseline from 20 to 21 in a temporary copy of that verifier;
the same command produced `pre-delivery-inversion.junit.xml` with exactly one
failure, “expected complete 20-ref baseline, got 20”. This proves the census
gate rejects the measured drift it is designed to catch; the mutation is not in
the candidate source.
