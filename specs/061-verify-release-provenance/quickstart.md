# Quickstart: Verify & Release Publication Boundary (F26)

Runnable validation scenarios proving the publication boundary works end-to-end. Each maps to a user story and
its success criteria. Implementation bodies live in `tasks.md`/the implementation phase; this is the run/verify
guide.

## Prerequisites

- .NET `net10.0` SDK; repo builds clean: `dotnet build FS.GG.Governance.sln`.
- The five new projects added to the solution (`PackEvidence`, `Attestation`, `ReleaseReport`,
  `ValidationMatrix`, `AttestationJson`) plus their test projects.
- Pack output location per the constitution: `~/.local/share/nuget-local/`.
- FSI prelude: `dotnet fsi scripts/prelude.fsx` loads the packed public surfaces (Constitution I — exercise the
  API through the same surface a human/script uses; never internals).

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test FS.GG.Governance.sln                       # whole suite
dotnet test tests/FS.GG.Governance.PackEvidence.Tests
dotnet test tests/FS.GG.Governance.Attestation.Tests
dotnet test tests/FS.GG.Governance.ReleaseReport.Tests
dotnet test tests/FS.GG.Governance.ValidationMatrix.Tests
dotnet test tests/FS.GG.Governance.AttestationJson.Tests
dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests   # extended — end-to-end pack + v2 + sidecar
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests    # extended — advisory preview
```

## Scenario 1 — Every packable project must pack at a bumped version (P1, US1, SC-001)

A product with several packable projects + a declared baseline.

1. **Pass:** every project packs at a version above baseline ⇒
   `Pack.factContributions` all `Met` ⇒ `Release.evaluateRelease` is not blocked on packing/versioning ⇒
   `fsgg release` exit `0`. (Story 1.1)
2. **Fail-to-pack:** one project's pack exits non-zero ⇒ `PackOutcome.PackFailed(sentinel)` ⇒ `Unmet` ⇒
   release **blocked** (exit `1`), reason names the project + pack failure, and the failed `Pack` run is in
   `PackEvidenceSet.Runs` with its sentinel. (Story 1.2)
3. **Unbumped/downgraded:** a project packs at a version `<=` baseline ⇒ `VersionVerdict.Unbumped`/`Downgraded`
   ⇒ `Unmet` ⇒ release **blocked**, reason names the project + version. (Story 1.3)
4. **Determinism:** run `fsgg release` twice on identical state ⇒ byte-identical `release.json` (pack duration
   retained only as sensed `durationNanos`, never affecting the verdict or identity). (Story 1.4)

Expected: `dotnet test tests/FS.GG.Governance.PackEvidence.Tests` green (version-bump matrix, failed-pack
sentinel, packed-no-artifact, reorder-invariance); `ReleaseCommand.Tests` green for the end-to-end block.

## Scenario 2 — Publication is a blocking boundary distinct from ship (P1, US2, SC-002)

A product that is **mergeable** (`fsgg ship` passes) but **not releasable** (unbumped version).

1. `fsgg ship` ⇒ exit `0` (mergeable).
2. `fsgg release` ⇒ exit `1` (`Blocked`), a release exit-code basis **distinct** from ship, the `ReleaseReport`
   carrying the failing precondition. (Story 2.1)
3. A fully releasable product ⇒ `fsgg release` exit `0`, `ReleaseExitCodeBasis = Clean`. (Story 2.2)
4. The release verdict, basis, and each unmet precondition are explicit in `release.json` (v2) — never folded
   into the ship verdict. (Story 2.4)

Expected: `ReleaseReport.Tests` green (mergeable-but-not-releasable + fully-releasable fixtures); the release
and ship verdicts are reported independently.

## Scenario 3 — `fsgg verify` advisory release-readiness preview (P1/P2, US2.3, SC-003)

1. `fsgg verify` on a pre-PR scope ⇒ `verify.json` carries `releaseReadiness` with `advisory: true` and the
   same evidence the release boundary would. (Story 2.3)
2. The preview **never** changes verify's exit code — an unreleasable-but-mergeable product still exits per the
   F56 verify scheme (the preview is advisory). 

Expected: `VerifyCommand.Tests` green (preview present + advisory; exit scheme unchanged).

## Scenario 4 — Publish-plan, posture, and template-pin evidence (P2, US4, SC-004)

1. A resolved publish plan ⇒ the `PublishPlan` `PreconditionEvidence` is `Met` and surfaced in the report.
   (Story 4.1)
2. A missing publish plan / unconfigured trusted-publishing posture / drifted template pin ⇒ the relevant
   `PreconditionEvidence` is `Unmet`/`Unrecoverable` ⇒ release **blocked**, reason names the precondition.
   (Story 4.2)
3. Each precondition's satisfied/unmet state + reason appears in `release.json`. (Story 4.3)

Expected: `ReleaseReport.Tests` green against publish-plan, posture, and template-pin-drift fixtures (reusing
the F54 sensed snapshot — no new sensing).

## Scenario 5 — SLSA/in-toto-shaped attestation summary, without overclaiming (P2, US3, SC-005)

From a fixed provenance audit snapshot (packed subjects, builder, materials, command runs):

1. `Attestation.summarize` ⇒ subject / builder / materials / invocation populated in an in-toto-compatible
   shape; `AttestationJson.ofAttestation` ⇒ `attestation.json` (`fsgg.attestation/v1`). (Story 3.1)
2. Run twice ⇒ byte-identical; changing only a duration ⇒ byte-identical `identity` (different `durationNanos`);
   changing a reproducible input (a subject digest, a material) ⇒ a different document. (Story 3.2)
3. The document carries `compliance: compatible-shape-not-formal-compliance` + the note — never overclaims.
   (Story 3.3)
4. A failed-build snapshot ⇒ `subjects: []` (no attested subject); the failed run still appears under
   `invocation.runs`. (FR-008)

Expected: `Attestation.Tests` + `AttestationJson.Tests` green (snapshot fixtures, no-op-input-change stability,
failed-build no-subject, marker present, reorder-invariance).

## Scenario 6 — Scheduled exhaustive validation hooks (P3, US5, SC-006)

1. A declared `Exhaustive` matrix + `MatrixBoundary.InnerLoop` ⇒ `decideMatrix` ⇒
   `Deferred (DeferredToScheduledBoundary …)`; the inner-loop run does **not** run the broad matrix and records
   it deferred. (Story 5.1)
2. The same matrix + `ScheduledOrRelease` ⇒ `RunNow`; the broad matrix runs and gates the verdict. (Story 5.2)
3. No declared matrix ⇒ `NotDeclared`; no matrix is invented at any boundary. (Story 5.3)

Expected: `ValidationMatrix.Tests` green (deferred-in-inner-loop, runs-at-boundary, never-invented).

## Scenario 7 — Safe failure & determinism (cross-cutting, SC-007/SC-008)

1. **No packable projects** ⇒ `NoPackableProjects = true`; the pack precondition is vacuously satisfied and the
   report states "no packable projects" — never a fabricated pack. (edge case, FR-011)
2. **Unreadable pack output / absent provenance input / missing publish plan** ⇒ a clear input signal, the
   release blocks, no hollow attestation, no fabricated pass — distinguished from a tool defect (exit `3`
   input-unavailable vs exit `4` tool-error at the host edge). (SC-008, FR-011)
3. **Determinism under reordering** ⇒ presenting packable projects / publish-plan entries / command runs in a
   different order yields byte-identical evidence, verdict, attestation, and report. (SC-007, edge case)
4. **Report-object parity** ⇒ the JSON projections render from the `ReleaseReport`; an unchanged report yields
   byte-identical JSON, and every existing `route.json`/`ship.json` golden is untouched (FR-015). (SC-007)

## Surface-baseline check (Constitution II, Tier 1)

```bash
dotnet test --filter "SurfaceDrift"     # or the repo's surface-drift test target
```

Confirms the five new `surface/*.surface.txt` baselines match the curated `.fsi`, and the `ReleaseJson` /
`VerifyJson` baselines changed only by the added projection `val`s.
