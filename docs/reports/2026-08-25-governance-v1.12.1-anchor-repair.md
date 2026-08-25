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
4. Use the reviewed `release_anchor_runner.py`; do not reproduce its sequence
   interactively. It refuses live operation unless both `--apply` and the exact
   `--confirm-target` value are present:

   ```console
   python3 readiness/418-governance-release-anchor-repair/release_anchor_runner.py \
     --apply \
     --confirm-target 388819d0e060c11f53e5ca2df3277a54a13f9e75 \
     --baseline readiness/418-governance-release-anchor-repair/pre-delivery-evidence.json \
     --receipt readiness/418-governance-release-anchor-repair/delivery-receipt.json
   ```

5. The runner requires the live run-ID set to equal the 36 IDs persisted in the
   reviewed baseline. It disables workflow `303994065`, verifies
   `disabled_manually`, pushes only the lightweight ref, and verifies the exact
   target.
6. While the workflow remains disabled, it polls the complete run set for at
   least 60 seconds, requiring at least three unchanged complete samples and no
   ID outside the baseline. The poll is bounded at 180 seconds; unreadable,
   incomplete, growing, or non-convergent observations fail.
7. Every exit after a disable attempt enters cleanup. Enablement is retried up
   to five times with bounded backoff and each attempt is followed by an API
   read requiring `active`. A recovered cleanup disturbance still returns a
   failure receipt; it is not silently converted to success. If primary work and
   cleanup both fail, the receipt and exception preserve the primary failure
   first and append the cleanup failure.
8. After verified re-enable, the runner repeats the same 60/180-second bounded
   unchanged-run-set convergence before success. Its receipt records all step
   facts, both complete run-ID sets, tag target, primary/cleanup errors, and
   `workflowActiveAtExit`. Re-download the four feed artifacts afterward and
   require the pre-delivery nuspec and unsigned-content digests unchanged.

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
`pre-delivery.junit.xml`: 6 passed, 0 failed. `test_verifier_controls.py`
produced `verifier-controls.junit.xml`: 12 passed, 0 failed. Those controls
independently invert all six live gates and additionally exercise known-present
tag/run/package non-vacuity plus unreadable tag, run, and package authorities;
each nested verifier run had exactly its intended one red case.

`test_release_anchor_runner.py` produced `runner-controls.junit.xml`: 13 passed,
0 failed. It injects a failure after every post-disable boundary, proves cleanup
restores and verifies `active`, proves transient enable errors are retried but
still reported, proves a primary error remains first when cleanup also exhausts,
and proves a delayed new run is rejected. These controls use an in-memory
operations adapter and never disable a live workflow, push a tag, or publish.
`run_readonly_checks.py` reruns and combines all three suites into
`all-readonly-checks.junit.xml`: 31 passed, 0 failed; this combined report is
the SDD observed-run authority for the repaired candidate.
