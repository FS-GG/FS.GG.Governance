# Feature Specification: Release-Provenance Host Wiring — `fsgg release` Pack/Version Boundary, the Attestation Sidecar, `release.json` v2, and the `fsgg verify` Release-Readiness Preview

**Feature Branch**: `065-release-provenance-host-wiring`

**Created**: 2026-06-25

**Input**: User description: "work on the next backlog item." — Backlog resolution: the roadmap
(`docs/initial-implementation-plan.md`) carries three deferred host-wiring passes. F27 human-projection host wiring
(`063-human-projection-host-wiring`) is complete; the F25 cost-cache host wiring landed as
`064-cost-cache-host-wiring`. The one remaining deferred pass is the **F26 release host edge** — the Phase 8 host
wiring (`T048–T055`, `T057`, `T062`) explicitly deferred in `specs/061-verify-release-provenance/tasks.md`. This
feature is exactly that pass.

## Overview

F26 (`061-verify-release-provenance`) landed five pure cores and two projections — `PackEvidence` (the
packed-version-is-truth policy: `evaluatePack`, `versionPolicy`, `factContributions`), `Attestation` +
`AttestationJson` (the SLSA/in-toto-**shaped** `AttestationSummary` projected from the F25 provenance audit snapshot,
emitted as the `fsgg.attestation/v1` sidecar), `ReleaseReport` (the immutable, presentation-free release/verify
report objects), `ValidationMatrix` (the scheduled-exhaustive-matrix `decideMatrix`), plus the additive
`ReleaseJson` → `fsgg.release/v2` (`ofReleaseReport`) and the additive `VerifyJson` `releaseReadiness` preview
projection. All are built, packed, and exercised through their public surfaces (117 tests green; five blessed
surface baselines). They are wired into **no command host**: today `fsgg release` evaluates declared release rules
over F54-sensed facts but never **packs** anything, builds no attestation, and writes neither `attestation.json` nor
`release.json` v2; `fsgg verify` emits no release-readiness preview.

This feature wires those cores into the existing `fsgg release` and `fsgg verify` hosts **additively**. At the
`fsgg release` edge the host packs every declared packable project through the existing F51 `GateExecution`
execution port (recording each as a `Pack` command run), builds the real pack evidence (`evaluatePack`), merges its
fact contributions over the F54 sensed facts (packed evidence wins on `VersionBump` / `PackageMetadata` /
`Provenance`), calls `Release.evaluateRelease` **unchanged**, assembles the provenance audit snapshot, the
attestation summary, and the immutable `ReleaseReport`, then writes `attestation.json` (`fsgg.attestation/v1`) and
`release.json` v2 (`ofReleaseReport`) through the host's existing atomic artifact writer. At the `fsgg verify` edge
the host assembles the same evidence advisorily, previews the `ReleaseReport`, records the scheduled matrix as
deferred in the inner loop, and projects the additive `releaseReadiness` block into `verify.json` — never changing
its exit code.

This is a host-edge integration row only: it adds **no** new pure core, **no** new report object, **no** new
release-rule family, and **no** new dependency. It consumes the seven already-built F26 surfaces at the MVU
interpreter edge of two mature host commands, reusing each host's existing gate-execution and artifact-write ports
and the F53/F54/F55/F56 verdict and exit-code machinery verbatim.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every packable project must pack at a bumped version before `fsgg release` passes (Priority: P1) 🎯 MVP

A maintainer runs `fsgg release` to decide whether the product may be published. Before the release verdict can
pass, the host **packs every declared packable project** through the existing execution port — recording each as a
`Pack` command run — and verifies each packed artifact is at a version **bumped** relative to its released baseline,
evaluated against the **packed artifact's** version. A project that fails to pack, or that packs at an
unbumped/downgraded version, **blocks release** with a clear, named reason; the real pack outputs (artifact path,
version, digest) become the package evidence the existing release rules are evaluated against.

**Why this priority**: This is the feature's core value and the row's defining act — it turns declared release rules
into an enforced publication boundary grounded in what was actually built. Without it `fsgg release` can pass on a
product that was never packed or whose version was never bumped, and the F26 cores stay inert. It is the smallest
slice that delivers an observable behavioral change: a real `dotnet pack` runs and an unbumped version now blocks.

**Independent Test**: Run `fsgg release` standalone over a product with several declared packable projects and a
version baseline; assert that when every project packs at a bumped version the release verdict is not blocked on
packing/versioning; that a project failing to pack blocks release with a named reason and its failed `Pack` run is
recorded with its sentinel exit code; and that a project packing at an unbumped/downgraded version blocks release
naming the project and version. Assert the verdict is deterministic for identical repository state.

**Acceptance Scenarios**:

1. **Given** a product whose every declared packable project packs successfully at a version bumped above its
   released baseline, **When** `fsgg release` runs, **Then** the pack/version preconditions are `Met`, each pack is
   recorded as a `Pack` command run, and the release verdict is not blocked on packing or versioning.
2. **Given** a declared packable project that fails to pack (non-zero pack exit), **When** `fsgg release` runs,
   **Then** release is **blocked** with a reason naming the project and the pack failure, and the failing pack is
   recorded as a `Pack` run with its sentinel/exit code in the provenance snapshot — never dropped.
3. **Given** a declared packable project that packs at a version equal to or below its released baseline, **When**
   `fsgg release` runs, **Then** release is **blocked** with a reason naming the project and the unbumped/downgraded
   version — never reported as releasable.
4. **Given** identical repository state, **When** `fsgg release` runs twice, **Then** the package evidence, the
   version-bump verdict, and the release decision are byte-identical, with wall-clock pack duration kept as sensed
   metadata only (excluded from identity).

---

### User Story 2 - `fsgg release` writes `release.json` v2 + the attestation sidecar and blocks distinctly from ship (Priority: P1)

After a release run, the host surfaces the publication decision through the immutable `ReleaseReport` and writes two
artifacts: `release.json` bumped additively to `fsgg.release/v2` (carrying the package evidence, version policy, and
attestation summary appended) and the new `attestation.json` (`fsgg.attestation/v1`) sidecar — the SLSA/in-toto-shaped
projection of the F25 provenance audit snapshot, carrying its always-present "compatible-shape, not formal
compliance" marker and never asserting a subject that was not actually packed. The release verdict resolves to its
own **release exit-code basis**, blocking and **distinct** from the ship verdict: a change can be perfectly mergeable
yet not releasable. Both new outputs are deterministic, and every existing `route.json` / `ship.json` golden stays
**byte-identical**.

**Why this priority**: The roadmap exit criterion is exactly this — "publication has a blocking governance boundary
distinct from ship." The attestation sidecar and the `release.json` v2 report are the durable, auditable record of
what was built and why it may (or may not) publish. Byte-identity of the existing goldens is the non-negotiable
safety anchor proving the wiring is purely additive. This must land with US1 for the boundary to be coherent.

**Independent Test**: Construct a product that is mergeable (`fsgg ship` exits 0) but not releasable (unbumped
version / missing publish plan / drifted pin); assert `fsgg release` exits with its release exit-code basis distinct
from ship, the `release.json` v2 report carries the failing precondition, and the `attestation.json` sidecar carries
the compatible-shape marker. Run a fully-releasable product twice and assert both new artifacts are byte-identical
across runs, and that the existing `route.json` / `ship.json` goldens are byte-identical to a pre-wiring baseline.

**Acceptance Scenarios**:

1. **Given** a completed `fsgg release` run, **When** the host persists its outputs, **Then** `release.json`
   (`fsgg.release/v2`) and `attestation.json` (`fsgg.attestation/v1`) are written beside the existing artifacts, the
   former carrying the package evidence / version policy / attestation summary, the latter carrying subject / builder
   / materials / invocation in SLSA/in-toto-compatible shape.
2. **Given** a change that passes `fsgg ship` (mergeable) but is not releasable, **When** `fsgg release` runs,
   **Then** it **blocks** with a release exit-code basis distinct from the ship verdict, naming the unmet publication
   precondition; a fully releasable product passes the boundary with a clean basis.
3. **Given** two `fsgg release` runs over the same inputs, **When** the new outputs are compared, **Then**
   `release.json` v2 and `attestation.json` are byte-identical (stable ordering, normalized paths, no wall-clock /
   username / environment dependence; pack duration retained only as sensed metadata).
4. **Given** the wiring is active, **When** the existing `route.json` / `ship.json` goldens are compared to their
   pre-wiring baselines, **Then** they are byte-identical (`attestation.json` is a new artifact; `release.json` v1→v2
   is an additive schema bump that is byte-identical when the appended fields are empty).
5. **Given** a failed build/pack, **When** the attestation summary is produced, **Then** it records the attempt (the
   recorded command run with its sentinel exit code) but **never** asserts a subject that was not produced, and the
   release verdict blocks.

---

### User Story 3 - `fsgg verify` previews release readiness advisorily, and defers the broad matrix (Priority: P2)

A maintainer running `fsgg verify` on a pre-PR scope sees a **release-readiness preview** of the publication verdict
using the same evidence the release boundary would, surfaced as an advisory `releaseReadiness` block in
`verify.json`. The preview is **advisory only**: it never changes the verify exit code and is never itself the
blocking release gate. When the product declares an exhaustive validation matrix, the verify (inner-loop) run records
it as **deferred** to the scheduled/release boundary rather than running it, so the inner loop stays fast; an
undeclared matrix is never invented.

**Why this priority**: Previewing release readiness before a PR lets maintainers fix publication gaps early without
making verify a blocking release gate, and deferring the broad matrix keeps the inner loop fast. It depends on the
release evidence and report assembly (US1/US2) existing, so it follows them. The existing `verify.json` exit-code
scheme and goldens are untouched.

**Independent Test**: Run `fsgg verify` over a pre-PR scope and assert `verify.json` carries a `releaseReadiness`
block (`advisory: true`) with the same evidence the release boundary would; assert an unreleasable-but-mergeable
product still exits per the unchanged F56 verify scheme; assert a declared exhaustive matrix is recorded deferred and
does not run; and assert a `verify.json` run with no release declaration stays byte-identical to its pre-wiring
golden (no schema bump when the preview is absent).

**Acceptance Scenarios**:

1. **Given** a pre-PR `fsgg verify` run on a product with a release declaration, **When** verify runs, **Then**
   `verify.json` carries an advisory `releaseReadiness` block previewing the publication verdict from the same
   evidence, and the verify exit code is unchanged by the preview.
2. **Given** an unreleasable-but-mergeable product, **When** `fsgg verify` runs, **Then** the preview reports the
   unmet precondition but verify still exits per the unchanged F56 five-code scheme (the preview never blocks).
3. **Given** a product that declares an exhaustive validation matrix, **When** `fsgg verify` runs in the inner loop,
   **Then** the matrix is recorded **deferred** to the scheduled/release boundary and does not run; **and Given** no
   declared matrix, **Then** no broad matrix is implied or invented.
4. **Given** a `fsgg verify` run on a product with **no** release declaration, **When** `verify.json` is compared to
   its pre-wiring golden, **Then** it is byte-identical (the `releaseReadiness` block is absent, no schema bump).

---

### User Story 4 - The publication boundary runs standalone and fails safely (Priority: P2)

A generated product checked out on its own (no monorepo) runs `fsgg release`. The pack/version/publish evaluation and
the attestation projection use only the product's own declared packable projects, version baselines, publish
plan/pins/posture, and recorded provenance — no monorepo path is required. A product with **no** packable projects
satisfies the pack precondition vacuously and says so (never a fabricated pack). A missing/unreadable pack output, an
absent provenance/attestation input, or a missing publish plan surfaces a clear input diagnostic naming the offending
source — distinct from a tool defect — blocks release, and emits **no** hollow attestation and **no** fabricated pass.

**Why this priority**: The standalone-governance guarantee (F23/F24/F25) must extend to the publication boundary, or
the feature would silently regress products that release on their own. Safe failure (input-vs-defect, no fabricated
evidence) is the constitutional discipline every prior row holds. It depends on US1/US2 wiring being in place, so it
follows them.

**Independent Test**: Check out a generated product standalone with its own packable projects and recorded
provenance; run `fsgg release` and assert the decision draws only on product-local sources. Then run a product with
no packable projects and assert the pack precondition is vacuously satisfied with a "no packable projects" statement;
and corrupt/remove a pack output, a provenance input, or the publish plan and assert a clear input diagnostic
(distinct from a tool defect via the host's existing input-unavailable vs tool-error exit codes), a blocked release,
and no hollow attestation.

**Acceptance Scenarios**:

1. **Given** a generated product checked out standalone, **When** `fsgg release` runs, **Then** the pack/version/
   publish evaluation and the attestation projection draw only on product-local sources — no monorepo-only path.
2. **Given** a product with no declared packable projects, **When** `fsgg release` runs, **Then** the pack
   precondition is vacuously satisfied, the report states "no packable projects," and no pack is fabricated.
3. **Given** an unreadable pack output, an absent provenance/attestation input, or a missing publish plan, **When**
   `fsgg release` runs, **Then** a clear input diagnostic names the offending source (distinct from a tool defect),
   release **blocks**, and no hollow attestation and no fabricated pass is emitted.
4. **Given** the packable projects or recorded command runs presented in a different order, **When** `fsgg release`
   runs, **Then** the package evidence, release verdict, attestation summary, and report are byte-identical
   (order-independent determinism).

---

### Edge Cases

- **Pack succeeds but produces no artifact**: a pack that exits zero yet emits no package artifact ⇒ release blocks
  with a "packed but no artifact produced" reason (the `PackedNoArtifact` outcome), not assumed releasable.
- **Version bumped in source but not in the packed artifact**: the version policy is evaluated against the **packed**
  artifact's version, so a source bump that never reaches the artifact still blocks; the packed version is the source
  of truth.
- **Mergeable but not releasable**: a change that passes `fsgg ship` but fails a publication precondition ⇒ ship
  passes and release blocks; the two verdicts are reported independently, neither masking the other.
- **First release (no baseline)**: a packable project with no released-version baseline (`None`) is treated as a
  first release (`NoBaseline`) per the F26 `versionPolicy`, not as a downgrade — never silently blocked.
- **Empty / absent attestation inputs**: a missing provenance input (no recorded pack, no builder identity) surfaces
  a clear input signal and blocks release rather than emitting a hollow attestation that overclaims.
- **Reordered packable projects / command runs**: reordering the declared projects or the recorded runs changes no
  package evidence, verdict, attestation, or output bytes (order-independent determinism).
- **Duration-only difference between two runs**: two pack runs differing only in wall-clock duration share a
  reproducible identity and do not change the attestation/provenance bytes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg release` MUST pack **every declared packable project** through the existing F51 execution port
  before the release verdict may pass, recording each as a `Pack` command run, and MUST collect each real pack
  output (artifact path + packed version + digest) as package evidence; a project that fails to pack MUST **block
  release** with a named reason and MUST still record the failed pack run (sentinel/exit code) — never drop it.
- **FR-002**: `fsgg release` MUST verify each packed artifact is at a version **bumped** relative to its released
  baseline, evaluated against the **packed** artifact's version; a project packed at a version equal to or below its
  baseline MUST **block release** with a reason naming the project and version — never reported as releasable.
- **FR-003**: The host MUST evaluate the packed package evidence by merging the F26 `factContributions` over the F54
  sensed facts (packed evidence winning on `VersionBump` / `PackageMetadata` / `Provenance`) and then calling
  `Release.evaluateRelease` **unchanged** — it MUST NOT add a new release-rule family or change how a release finding
  maps to a blocking verdict (F53/F23/F24, reused), and MUST carry the resulting `ReleaseDecision` and exit-code
  basis into the report verbatim (never re-derived).
- **FR-004**: `fsgg release` MUST write `release.json` bumped additively to `fsgg.release/v2` (via `ofReleaseReport`,
  appending the package evidence / version policy / attestation summary) and the new `attestation.json`
  (`fsgg.attestation/v1`) sidecar through the host's **existing** atomic artifact-writer port, beside its existing
  outputs.
- **FR-005**: The release verdict MUST be a **first-class blocking boundary distinct from `fsgg ship`**, surfaced
  through the immutable `ReleaseReport` with its release exit-code basis; a mergeable product MUST be able to be not
  releasable, and the publication verdict MUST NOT be folded into the merge decision.
- **FR-006**: `fsgg verify` MUST surface an advisory `releaseReadiness` preview of the publication verdict (via
  `Report.preview`) using the same evidence, projected as an additive `verify.json` block; the preview MUST NOT
  change the verify exit code and MUST reuse the existing F56 five-code verify scheme unchanged.
- **FR-007**: The host MUST project the F25 provenance audit snapshot into the `AttestationSummary`
  (`Attestation.summarize`) and emit `attestation.json` deterministically; the summary MUST carry subject / builder
  identity / materials / invocation in SLSA/in-toto-compatible shape, MUST carry the always-present "compatible-shape,
  not formal compliance" marker, and MUST NOT assert a subject that was not actually packed (a failed build/pack
  yields no attested subject).
- **FR-008**: `fsgg release` MUST surface the declared **publish plan**, **trusted-publishing posture**, and
  **template pins** (F53/F54, sensed unchanged) as preconditions in the report with their satisfied/unmet state and
  reason; a missing/unresolved publish plan, unconfigured posture, or drifted pin MUST block release with a named
  reason.
- **FR-009**: A run that declares an **exhaustive validation matrix** MUST have it recorded **deferred** to the
  scheduled/release boundary in the inner-loop (`fsgg verify`) run via `Matrix.decideMatrix` — the inner loop MUST
  NOT run the broad matrix — and a run MUST NOT invent a matrix that was not declared. *(Actually invoking the
  admitted matrix and folding its results into the verdict remains a host/CI follow-up out of this row's scope, per
  F26 plan D4.)*
- **FR-010**: The pure pack/version/publish evaluation, the attestation projection, and the report assembly MUST be
  computed by the existing F26 pure cores in each host's `update`; the only new I/O — packing the projects through
  the execution port, reading each pack output, and writing `attestation.json` / `release.json` v2 — MUST live at the
  host interpreter edge through the **existing** execution and artifact-writer ports, with no filesystem / process /
  registry dependency added to any pure core.
- **FR-011**: Every new output (`release.json` v2, `attestation.json`, the `verify.json` `releaseReadiness` block)
  MUST be **deterministic** — stable ordering, normalized paths, no wall-clock / username / environment dependence
  (pack duration retained as sensed metadata only) — so identical repository state yields byte-identical output and
  reordering the packable projects or command runs changes nothing.
- **FR-012**: Every existing artifact the hosts already write — `route.json`, `ship.json`, and the existing
  `verify.json` fields — MUST stay **byte-identical** for identical repository state; the schema bumps are additive
  (`release.json` v1→v2 byte-identical when the appended fields are empty; the `verify.json` `releaseReadiness` block
  absent ⇒ byte-identical, no schema bump).
- **FR-013**: The publication boundary MUST run **standalone** (no monorepo access) using only the product's own
  packable projects, version baselines, declared publish plan/pins/posture, and recorded provenance, consistent with
  the F23/F24/F25 standalone guarantee; no release or attestation step may require a monorepo-only path or a
  network/registry call from the pure cores.
- **FR-014**: A missing / malformed / unreadable input (no packable projects, an unreadable pack output, an absent
  provenance/attestation input, a missing publish plan) MUST be distinguished from a tool defect, naming the
  offending source, blocking release, with no swallowed error, **no fabricated pack, no hollow attestation, and no
  fabricated pass**.

### Key Entities *(reused from F26 — no new entity introduced)*

- **Package evidence** (F26 `PackEvidence`): the real output of packing each declared packable project — artifact
  path, packed version, digest, and the `Pack` command run — and the per-project pack/version verdict; the proof a
  releasable artifact was actually built.
- **Version policy** (F26 `PackEvidence`): the rule that each packed artifact's version is bumped relative to its
  released baseline, evaluated against the packed version; the basis of the version-bump verdict.
- **Publish plan / trusted-publishing posture / template pins** (F53/F54, sensed unchanged): the publication
  preconditions surfaced first-class in the report.
- **Attestation summary** (F26 `Attestation` / `AttestationJson`): the SLSA/in-toto-shaped projection of the F25
  provenance audit snapshot — subject / builder / materials / invocation — emitted as the `fsgg.attestation/v1`
  sidecar, carrying the compatible-shape-not-formal-compliance marker.
- **Release report / verify release preview** (F26 `ReleaseReport`): the immutable, presentation-free report objects
  carrying the publication verdict, exit-code basis, evidence, and attestation summary — the single source of truth
  for `release.json` v2 and the `verify.json` preview block.
- **Scheduled exhaustive validation matrix** (F26 `ValidationMatrix`): a declared broad matrix `decideMatrix`
  defers in the inner loop and admits at the scheduled/release boundary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A product whose every packable project packs at a bumped version passes the pack/version preconditions;
  a project that fails to pack, or packs at an unbumped/downgraded version, **blocks** `fsgg release` with a named
  reason and its `Pack` run recorded — verified by a real-filesystem (`dotnet pack`) pack-boundary fixture across
  multiple packable projects.
- **SC-002**: A change that passes `fsgg ship` (mergeable) but fails a publication precondition **blocks** `fsgg
  release` with a release exit-code basis distinct from ship, while a fully releasable product passes the boundary —
  verified by a mergeable-but-not-releasable fixture and a fully-releasable fixture.
- **SC-003**: `release.json` (`fsgg.release/v2`) and `attestation.json` (`fsgg.attestation/v1`) are written with
  their declared schema versions and are byte-identical on a re-run with unchanged inputs — verified by a re-run
  determinism fixture.
- **SC-004**: `fsgg verify` surfaces an advisory `releaseReadiness` preview using the same evidence, never changing
  the verify exit code, and a declared exhaustive matrix is recorded deferred (not run) in the inner loop — verified
  by a verify-preview fixture and a matrix-deferral assertion.
- **SC-005**: Every existing `route.json` / `ship.json` golden is byte-identical to its pre-wiring baseline, the
  `release.json` v1→v2 bump is byte-identical when the appended fields are empty, and a `verify.json` run with no
  release declaration is byte-identical to its pre-wiring golden — verified against frozen baselines.
- **SC-006**: A generated product checked out standalone produces the release decision and attestation from
  product-local sources only; a product with no packable projects satisfies the pack precondition vacuously; and a
  missing/unreadable pack output, absent provenance input, or missing publish plan yields a clear input diagnostic
  (distinct from a tool defect) with no fabricated pack, hollow attestation, or fabricated pass — verified by a
  standalone fixture and safe-failure fixtures.
- **SC-007**: The full-solution build + test sweep is green with all existing goldens byte-identical and the new
  outputs deterministic — verified by the full-suite gate.

## Assumptions

- **Backlog resolution**: per the roadmap currency in `docs/initial-implementation-plan.md` and `064`'s spec, the
  three deferred host-wiring passes were F27 (landed as `063`), F25 cost-cache (landed as `064`), and the F26 release
  host edge. This row is the F26 release host edge — the Phase 8 tasks (`T048–T055`, `T057`, `T062`) explicitly
  deferred in `specs/061-verify-release-provenance/tasks.md`. The spec directory is `065-release-provenance-host-wiring`
  (sequential).
- **F26 cores are reused verbatim**: `PackEvidence`, `Attestation`, `AttestationJson` (`fsgg.attestation/v1`),
  `ReleaseReport`, `ValidationMatrix`, the additive `ReleaseJson` (`fsgg.release/v2`, `ofReleaseReport`), and the
  additive `VerifyJson` `releaseReadiness` projection are built, packed, green, and surface-baselined (117 tests).
  This row adds **no** new library, report object, verdict, exit-code scheme, release-rule family, JSON schema, or
  dependency — it only consumes their public surfaces at the host edges.
- **Scope is exactly F26 Phase 8**: this feature is the host-edge slice deferred in
  `specs/061-verify-release-provenance/tasks.md` Phase 8. The pure-core stories (US1–US5 of that feature) are already
  complete; nothing in the seven cores/projections changes here.
- **Two hosts, additive only**: only `fsgg release` (`FS.GG.Governance.ReleaseCommand`) and `fsgg verify`
  (`FS.GG.Governance.VerifyCommand`) are wired. The pack step, evidence merge, report/attestation assembly, and
  sidecar writes are layered onto each host's existing F51 execution and artifact-write ports; no new port is
  introduced. The `.fsgg/release.yml` declaration is extended additively to carry the packable projects (surface id,
  pack `GateCommand`, version baseline) and the optional exhaustive matrix.
- **JSON is the contract; the bumps are additive**: existing persisted `*.json` outputs stay the deterministic,
  byte-identical contract; `attestation.json` is a new deterministic contract written beside them, and the
  `release.json` v1→v2 and `verify.json` `releaseReadiness` additions are additive — byte-identical when their new
  fields are empty/absent (FR-012, SC-005).
- **Packed version is the source of truth**: the version policy is evaluated against the packed artifact's version,
  so a source bump that never reaches the artifact still blocks (F26 D1).
- **MVU discipline preserved**: the pure pack/version/publish/report/attestation decisions live in each host's
  `update`; packing through the execution port, reading pack outputs, and writing the sidecar/v2 are effects at the
  interpreter edge through the existing ports — no I/O enters any pure core (FR-010), consistent with Constitution IV
  and the F029/F041/F051 leaf-plus-sensor precedent.
- **Safe failure preserved**: a missing/unreadable pack output, absent provenance input, or missing publish plan
  surfaces a clear input signal distinct from a tool defect, with no crash, no fabricated pack, no hollow
  attestation, and no fabricated pass (FR-014), consistent with F14–F064.
- **Provenance emits useful metadata first, claims compliance only after verification**: per roadmap C7, the
  attestation summary is SLSA/in-toto-**shaped** and reproducible but explicitly does not assert formal SLSA-level or
  in-toto conformance; a formal-compliance claim is reserved for an explicit later verification step (FR-007).
- **Tier**: **Tier 1 (contracted change).** It introduces one new deterministic JSON contract (`attestation.json`
  `fsgg.attestation/v1`), an additive `release.json` v1→v2 bump and an additive `verify.json` `releaseReadiness`
  block, and changes the two wired hosts' public effect/model/declaration surface (re-blessing their surface
  baselines), while leaving every existing `route.json` / `ship.json` golden byte-identical and introducing no new
  dependency. The full chain applies: spec, plan, host `.fsi` updates, re-blessed surface baselines, real
  `dotnet pack` E2E test evidence, and docs (including flipping F26 Phase 8 to complete in
  `specs/061-verify-release-provenance/tasks.md` and updating the roadmap's "Remaining" note).
</content>
</invoke>
