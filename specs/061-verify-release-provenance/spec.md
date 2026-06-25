# Feature Specification: Verify & Release Publication Boundary — Pack, Version, Publish-Plan, and Provenance Attestation

**Feature Branch**: `061-verify-release-provenance`

**Created**: 2026-06-25

**Status**: Planned

**Input**: User description: "next item in plan" — roadmap **F26 ·
`026-verify-release-provenance`** (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`), the
next unimplemented row after **F25 (`060-cost-cache-command-provenance`)** merged on 2026-06-25. F25 made
expensive evidence governed — bounded by a cost budget and reused only when its freshness key proves it still
applies — and rolled the real commands a governed run performs into a deterministic **provenance audit
snapshot**. This row turns that into a **blocking publication boundary**: before a product may be released, every
packable project must be **packed with a bumped version**, the **publish plan / trusted-publishing posture /
template pins** must check out, and the run must emit **SLSA/in-toto-shaped provenance metadata** that proves
what was built — without overclaiming formal compliance. Per the roadmap: "publication has a blocking governance
boundary distinct from ship."

Three scope decisions are confirmed for this feature (the requester advanced from F25 to the next row, F26,
after F25 merged — confirming the publication-boundary scope over further cost/cache work):

1. **This row completes the publication boundary by adding real pack/version/publish *evidence* and an
   *attestation summary* — it does not re-invent the release-rule families or the host commands.** The six
   release-rule families (version bump, package metadata, template pins, publish plan, trusted publishing,
   provenance) already exist as a pure rules core (F53) sensed from a real repository (F54) and run by the
   `fsgg release` host with a five-code exit basis (F55); the pre-PR `fsgg verify` host (F56) already selects
   and runs profile-appropriate checks behind the same exit-code scheme. F26 reuses **all of that unchanged**.
   What is missing is the **publication evidence those release rules are evaluated against** — actually packing
   every packable project at a bumped version and proving it — and a **publication-grade attestation** of what
   ran.

2. **The release boundary is *distinct from* `fsgg ship`.** `fsgg ship` decides whether a change may merge;
   `fsgg release` (and `fsgg verify`'s release-readiness preview) decides whether the merged product may be
   **published**. These are different verdicts with different bases: a change can be perfectly mergeable yet not
   releasable (unbumped version, no pack output, an unpinned template, a missing publish plan). This row makes
   the publication verdict and its exit-code basis a first-class, blocking boundary that a release run must pass
   before publication — never folded into the merge decision.

3. **Provenance is emitted as *useful metadata first*, and never overclaims formal compliance.** The attestation
   summary is **shaped like** SLSA/in-toto (subject, builder identity, materials, invocation) and is derived
   deterministically from the F25 provenance audit snapshot, but the row makes **no claim of formal
   SLSA-level/in-toto attestation conformance** — it emits compatible, reproducible metadata and reserves any
   formal-compliance claim for an explicit, later verification step (roadmap C7).

## Overview

After F25, a governed run is cost-bounded and produces a deterministic provenance audit snapshot of every
expensive command it performed (build, test, pack, template instantiation, git diff, package inspection, visual
capture). And after F53–F56, the six release-rule families are modelled, sensed from a real repository, and run
by the `fsgg release` and `fsgg verify` hosts. What is still missing is the **publication evidence** those
release rules need to be meaningful, and the **attestation** that makes a published artifact traceable:

- The release rules can *declare* that a version must be bumped and a package must pack, but there is **no
  enforced act of packing every packable project at a bumped version** and feeding the real pack output back
  into the release verdict. Today a `Provenance`/`PackageMetadata` rule can be declared, but nothing guarantees
  the run actually built the artifact it claims is releasable.
- There is a `Provenance` audit snapshot (F25, F033) of what ran, but **no publication-grade attestation
  summary** projected from it — no SLSA/in-toto-shaped record of subject (the packed artifacts), builder
  identity, materials (the inputs), and invocation (the recorded command runs) that a downstream consumer can
  inspect.
- `fsgg ship` and `fsgg release` both exist, but the **publication boundary is not yet first-class**: there is
  no unified release/verify **report object** that carries package evidence, version policy, publish plan, and
  the attestation summary together, and no single **release exit-code basis** that makes "this product may be
  published" a blocking verdict distinct from "this change may merge."
- Broad validation matrices (e.g. packing across every packable project, multi-target validation) are
  expensive; there is **no declared hook** to run an exhaustive validation matrix on a schedule rather than on
  every pre-PR run.

This feature closes that gap, in priority order:

- **Pack-and-version-bump evidence gates release.** Before the release verdict can pass, Governance **packs
  every packable project** (through the existing execution port, recorded as `Pack` command runs) and verifies
  each is at a **bumped version** relative to its released baseline. The real pack outputs become
  **package evidence** the existing release rules (F53 `PackageMetadata` / `VersionBump` / `Provenance`
  families) are evaluated against. A project that fails to pack, or that packs at an unbumped version, **blocks
  release** with a clear, named reason — it is never assumed releasable. This is the central new value and the
  P1 slice.
- **Publication boundary distinct from ship.** The release verdict — and `fsgg verify`'s release-readiness
  preview of it — is a **first-class blocking boundary** with its own **release exit-code basis**, surfaced
  through a unified **release report** (and the verify report that previews it). A product may be perfectly
  mergeable yet not releasable; the two verdicts and their bases are kept distinct.
- **Publish-plan, trusted-publishing, and template-pin evidence.** The declared **publish plan** (what is
  published, where, under what posture), the **trusted-publishing posture**, and the **template pins** are
  validated as evidence the existing release families consume — surfaced in the report so a maintainer sees
  exactly which publication precondition failed and why.
- **SLSA/in-toto-shaped attestation summary.** The F25 provenance audit snapshot is projected into a
  deterministic **attestation summary** — subject (packed artifacts + digests), builder identity, materials
  (rule hash, generator version, base/head, artifact digests, environment class), and invocation (the recorded
  command runs) — emitted as compatible metadata that **never overclaims** formal SLSA/in-toto compliance.
- **Scheduled exhaustive validation hooks.** A run may **declare** an exhaustive validation matrix (e.g. pack
  across every packable project / target) to be executed on a schedule rather than on every pre-PR run, so the
  broad matrix runs without making the inner loop pay for it.

This feature does **not** add a new release-rule family or change how a release finding blocks (F53/F23/F24,
reused), does **not** change the freshness key, the cost budget, or the provenance audit snapshot
(F29/F25/F033, reused), does **not** change the `fsgg verify`/`fsgg release` exit-code scheme (F55/F56, reused),
and adds **no** new dependency. It supplies the enforced pack/version-bump evidence, the publish-plan/posture
evidence, the publication-grade attestation summary, and the unified report objects that make publication a
blocking governance boundary distinct from ship.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every packable project must pack at a bumped version before release passes (Priority: P1)

A maintainer runs `fsgg release` to decide whether the product may be published. Before the release verdict can
pass, Governance **packs every packable project** in the product (recording each as a `Pack` command run through
the existing execution port) and verifies each packed artifact is at a **version bumped** relative to its
released baseline. A project that fails to pack, or that packs at an **unbumped** version, **blocks release**
with a clear, named reason ("release blocked: project X packed at unchanged version 1.2.0"). The real pack
outputs and their digests become the **package evidence** the existing release rules are evaluated against, so
the version-bump and package-metadata verdicts are grounded in what was actually built — never assumed.

**Why this priority**: "Pack every packable project with bumped version number before release gates can pass" is
the row's defining act — it is what turns declared release rules into an enforced publication boundary. Without
it, `fsgg release` can pass on a product that was never actually built or whose version was never bumped. It is
the foundation the attestation and report objects build on, and it delivers value alone: even before
attestation, a maintainer gets a release verdict grounded in real pack evidence.

**Independent Test**: Define a product with several packable projects and a declared version baseline. Confirm
that when every project packs at a bumped version the release verdict passes; that a project failing to pack
blocks release with a named reason; that a project packing at an unbumped (or downgraded) version blocks release
naming the project and version; and that the pack runs are recorded as `Pack` command runs feeding the package
evidence. Confirm the verdict is deterministic for identical repository state.

**Acceptance Scenarios**:

1. **Given** a product whose every packable project packs successfully at a version bumped above its released
   baseline, **When** `fsgg release` runs, **Then** the package-evidence preconditions are satisfied and the
   release verdict is not blocked on packing or versioning.
2. **Given** a packable project that fails to pack (non-zero pack exit), **When** `fsgg release` runs, **Then**
   release is **blocked** with a reason naming the project and the pack failure, and the failing pack is recorded
   as a `Pack` command run (with its sentinel/exit code) in the provenance — never dropped.
3. **Given** a packable project that packs at a version **equal to or below** its released baseline, **When**
   `fsgg release` runs, **Then** release is **blocked** with a reason naming the project and the unbumped
   version — never reported as releasable.
4. **Given** identical repository state, **When** `fsgg release` runs twice, **Then** the package evidence, the
   version-bump verdict, and the release decision are byte-identical (deterministic), with wall-clock pack
   duration kept as sensed metadata that does not affect the verdict.

---

### User Story 2 - Publication is a blocking boundary distinct from ship (Priority: P1)

A maintainer understands that **mergeable** and **releasable** are different verdicts. `fsgg ship` decides
whether a change may merge; `fsgg release` decides whether the merged product may be **published**, and
`fsgg verify` previews the release-readiness verdict before a PR. The release decision is surfaced through a
unified **release report** carrying the package evidence, version policy, publish plan, and attestation summary,
and it resolves to a **release exit-code basis** that is **blocking and distinct** from the ship verdict: a
change can be perfectly mergeable yet not releasable (unbumped version, missing publish plan, unpinned
template). The release boundary is never folded into the merge decision.

**Why this priority**: The roadmap exit criterion is exactly this — "publication has a blocking governance
boundary distinct from ship." Making the release verdict and its exit-code basis first-class (rather than a
side report) is what lets CI gate publication independently of merge. It is P1 alongside the pack evidence
because the evidence is only meaningful if it drives a real blocking boundary.

**Independent Test**: Construct a product that is mergeable (ship would pass) but not releasable (e.g. unbumped
version). Confirm `fsgg release` blocks with a release exit-code basis distinct from ship's, that the release
report carries the failing precondition, and that `fsgg verify` previews the same release-readiness verdict.
Confirm a fully releasable product passes the release boundary.

**Acceptance Scenarios**:

1. **Given** a change that would pass `fsgg ship` (mergeable) but whose product is not releasable (e.g. an
   unbumped version), **When** `fsgg release` runs, **Then** it **blocks** with a release exit-code basis,
   distinct from the ship verdict, naming the unmet publication precondition.
2. **Given** a fully releasable product (every project packed at a bumped version, publish plan present, posture
   and pins satisfied), **When** `fsgg release` runs, **Then** the release boundary **passes** with a clean
   exit-code basis.
3. **Given** a pre-PR run, **When** `fsgg verify` runs, **Then** it surfaces a **release-readiness preview** of
   the same publication verdict (advisory at the pre-PR boundary) using the same evidence, without itself being
   the blocking release gate.
4. **Given** a release run, **When** its report is read, **Then** the release verdict, its exit-code basis, and
   each unmet precondition are explicit in a unified **release report** — never silently merged into the ship
   verdict.

---

### User Story 3 - SLSA/in-toto-shaped attestation summary, without overclaiming (Priority: P2)

A downstream consumer wants to know **what** was built, **by whom**, **from what**, and **how**. From the F25
provenance audit snapshot, Governance projects a deterministic **attestation summary** shaped like SLSA/in-toto:
**subject** (the packed artifacts and their digests), **builder identity**, **materials** (rule hash, generator
version, base/head, artifact digests, environment class), and **invocation** (the recorded command runs). The
summary is emitted as compatible, reproducible metadata and is **byte-identical** for identical inputs. It
**never claims formal SLSA-level or in-toto conformance** — it is useful, inspectable provenance metadata that
reserves any formal-compliance claim for an explicit later verification.

**Why this priority**: The attestation summary makes the publication boundary auditable and the published
artifact traceable, but it builds on the pack evidence (Story 1) and the boundary (Story 2) being the thing
worth attesting. Emitting compatible-shaped metadata without overclaiming is a deliberate safety stance
(roadmap C7). It is independently testable against snapshot fixtures.

**Independent Test**: From a fixed provenance audit snapshot (packed subjects, builder identity, materials, and
recorded command runs), confirm the attestation summary is produced with each SLSA/in-toto-shaped field
populated, is byte-identical for identical inputs, changes only when a reproducible input changes, and carries
**no** formal-compliance claim (an explicit "compatible-shape, not formally attested" marker).

**Acceptance Scenarios**:

1. **Given** a provenance audit snapshot with packed subjects, builder identity, materials, and command runs,
   **When** the attestation summary is produced, **Then** it carries the subject (artifacts + digests), builder
   identity, materials, and invocation in an SLSA/in-toto-compatible shape.
2. **Given** the same snapshot, **When** the attestation summary is produced twice, **Then** it is
   byte-identical (deterministic) and changes only when a reproducible input (a subject digest, a material, an
   invocation) changes.
3. **Given** an attestation summary, **When** it is read, **Then** it explicitly marks itself as
   **compatible-shaped, not a claim of formal SLSA/in-toto compliance** — never overclaiming conformance.

---

### User Story 4 - Publish-plan, trusted-publishing posture, and template-pin evidence (Priority: P2)

The maintainer needs the publication preconditions beyond packing to be visible and enforced. Governance
validates the declared **publish plan** (what artifacts are published, where, under what posture), the
**trusted-publishing posture** (the publishing identity/credentials posture is configured as required), and the
**template pins** (any product templates are pinned, not drifting), surfacing each as evidence the existing
release-rule families consume. A missing publish plan, an unconfigured trusted-publishing posture, or a drifted
template pin **blocks release** with a named reason; a satisfied set passes — so a maintainer sees exactly which
publication precondition failed.

**Why this priority**: Packing proves the artifact exists; the publish plan, posture, and pins prove it can be
published **correctly and safely**. These reuse the existing release families (F53 `PublishPlan` /
`TrustedPublishing` / `TemplatePins`) sensed by F54, so the new value is surfacing them as first-class report
evidence at the publication boundary. It is independently testable against publish-plan and template-pin
fixtures.

**Acceptance Scenarios**:

1. **Given** a declared publish plan that resolves against the packed artifacts, **When** `fsgg release` runs,
   **Then** the publish-plan precondition is satisfied and surfaced as evidence in the report.
2. **Given** a missing or unresolved publish plan, an unconfigured trusted-publishing posture, or a drifted
   template pin, **When** `fsgg release` runs, **Then** release is **blocked** with a reason naming the failing
   precondition — never assumed satisfied.
3. **Given** a release run, **When** the report is read, **Then** the publish-plan, trusted-publishing-posture,
   and template-pin evidence each appear with their satisfied/unmet state and reason.

---

### User Story 5 - Scheduled exhaustive validation hooks for broad matrices (Priority: P3)

Some validation matrices are too broad to run on every pre-PR run — packing across every packable project and
target, or a wide cross-configuration matrix. A run may **declare** such an exhaustive validation matrix to be
executed on a **schedule** (Exhaustive cost, at a release/scheduled boundary) rather than per-PR, so the inner
loop stays fast while the broad matrix still runs and gates publication. The declared schedule and the matrix
it covers are explicit and deterministic; an undeclared matrix simply does not run in the inner loop.

**Why this priority**: Scheduled exhaustive hooks keep the broad, expensive matrices from taxing every pre-PR
run while preserving the publication boundary's strength — but they depend on the boundary and evidence (Stories
1–4) existing. It is a cross-cutting affordance, hence P3, and independently testable: a declared scheduled
matrix runs at the scheduled boundary and is absent from the inner-loop run.

**Acceptance Scenarios**:

1. **Given** a declared exhaustive validation matrix marked for the scheduled/release boundary, **When** an
   inner-loop run executes, **Then** the broad matrix does **not** run (the inner loop stays fast) and the
   report records that it is deferred to the scheduled boundary.
2. **Given** the same declared matrix, **When** the run is at the scheduled/release boundary, **Then** the
   boundary decision **admits** the broad matrix (the boundary's cost ceiling admits its Exhaustive cost — the
   matrix is marked to run). *(Actually invoking the admitted matrix and folding its results into the publication
   verdict is a host/CI concern deferred as a bounded follow-up — this row supplies the declaration surface and
   the pure run/defer decision only; see plan D4.)*
3. **Given** no declared exhaustive matrix, **When** any run executes, **Then** no broad matrix is implied or
   silently invented.

---

### Edge Cases

- **No packable projects**: a product with nothing to pack ⇒ the pack-evidence precondition is vacuously
  satisfied (no project blocks release on packing), and the report states there were no packable projects —
  never a fabricated pack.
- **Pack succeeds but produces no artifact**: a pack that exits zero yet emits no package artifact ⇒ release is
  blocked with a "packed but no artifact produced" reason, not assumed releasable.
- **Version bumped in source but not in the packed artifact** (or vice versa): the version policy is evaluated
  against the **packed artifact's** version (the real evidence), and a mismatch between declared and packed
  version is surfaced — the packed version is the source of truth for releasability.
- **Mergeable but not releasable**: a change that passes `fsgg ship` but fails a publication precondition ⇒ ship
  passes and release blocks; the two verdicts are independent and both reported truthfully (never one masking
  the other).
- **Attestation with a failed build**: when a build/pack failed, the attestation summary still records the
  attempt (the recorded command run with its sentinel exit code) and the release verdict blocks — the
  attestation never asserts a subject that was not actually produced.
- **Empty / absent attestation inputs**: a missing provenance input (no recorded pack, no builder identity)
  surfaces a clear input signal and blocks release rather than emitting a hollow attestation that overclaims.
- **Determinism under reordering**: presenting the packable projects, the publish-plan entries, or the command
  runs in a different order ⇒ the package evidence, the release verdict, the attestation summary, and the report
  are unchanged (order-independent, byte-identical).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Before the release verdict may pass, Governance MUST **pack every packable project** in the
  product through the existing execution port, recording each as a `Pack` command run (F25 `CommandKind.Pack`),
  and MUST collect the real pack outputs (artifact path + digest) as **package evidence**; a project that fails
  to pack MUST **block release** with a named reason and MUST still record the failed pack run (sentinel/exit
  code) — never drop it.
- **FR-002**: Governance MUST verify each packed artifact is at a **version bumped** relative to its released
  baseline, evaluated against the **packed artifact's** version; a project packed at a version equal to or below
  its baseline MUST **block release** with a reason naming the project and the version — never reported as
  releasable.
- **FR-003**: Governance MUST evaluate the packed **package evidence** through the existing release-rule families
  (F53 `VersionBump` / `PackageMetadata` / `Provenance`) **unchanged** — it MUST NOT introduce a new release-rule
  family or change how a release finding maps to a blocking verdict (F53/F23/F24, reused).
- **FR-004**: The release verdict MUST be a **first-class blocking boundary distinct from `fsgg ship`**, with a
  **release exit-code basis** surfaced through a unified **release report**; a product that is mergeable MUST be
  able to be **not releasable**, and the publication verdict MUST NOT be folded into the merge decision.
- **FR-005**: `fsgg verify` MUST surface a **release-readiness preview** of the publication verdict using the
  same evidence, **advisory** at the pre-PR boundary (it MUST NOT itself be the blocking release gate), and MUST
  reuse the existing F56 verify exit-code scheme unchanged.
- **FR-006**: Governance MUST validate the declared **publish plan**, **trusted-publishing posture**, and
  **template pins** as evidence consumed by the existing release families (F53 `PublishPlan` /
  `TrustedPublishing` / `TemplatePins`, sensed by F54), surfacing each precondition's satisfied/unmet state and
  reason in the report; a missing/unresolved publish plan, unconfigured posture, or drifted pin MUST block
  release with a named reason.
- **FR-007**: Governance MUST project the F25 **provenance audit snapshot** (F033/F25) into a deterministic
  **attestation summary** shaped like SLSA/in-toto — **subject** (packed artifacts + digests), **builder
  identity**, **materials** (rule hash, generator version, base/head, artifact digests, environment class), and
  **invocation** (recorded command runs) — that is byte-identical for identical inputs and changes only when a
  reproducible input changes.
- **FR-008**: The attestation summary MUST **not overclaim** formal compliance — it MUST mark itself as
  **compatible-shaped, not a formal SLSA-level/in-toto attestation**, reserving any formal-compliance claim for
  an explicit later verification (roadmap C7); it MUST NOT assert a subject that was not actually produced (a
  failed build/pack MUST NOT yield an attested subject).
- **FR-009**: Governance MUST allow a run to **declare** an **exhaustive validation matrix** to be executed at a
  **scheduled/release boundary** (Exhaustive cost) rather than in the inner loop; an inner-loop run MUST NOT run
  the declared broad matrix (recording it as deferred to the scheduled boundary), and a run MUST NOT invent an
  exhaustive matrix that was not declared.
- **FR-010**: The release verdict, the verify release-readiness preview, the package/version/publish evidence,
  and the attestation summary MUST all be **deterministic** — stable ordering, normalized paths, no wall-clock /
  username / environment dependence (pack duration retained as sensed metadata only) — so identical repository
  state yields byte-identical output.
- **FR-011**: Publication evaluation MUST distinguish a missing/malformed **input** (no packable project, an
  unreadable pack output, an absent provenance/attestation input, a missing publish plan) from a **tool defect**,
  naming the offending source, with no swallowed errors and **no fabricated pack, no hollow attestation, and no
  fabricated pass** (safe-failure, as F14–F60).
- **FR-012**: The release and verify report objects (the **release report** and **verify report**) MUST be
  **immutable report values** that carry the verdict, exit-code basis, evidence, and attestation summary, so
  downstream JSON projections and later human projections (F27) render from a single source of truth — the
  report object MUST be presentation-free.
- **FR-013**: The publication boundary MUST be runnable **standalone** — without monorepo access — using only the
  product's own packable projects, version baselines, declared publish plan/pins/posture, and recorded
  provenance, consistent with the standalone-governance guarantee (F23/F24/F25); no release or attestation step
  may require a monorepo-only path or a network/registry call from the pure cores.
- **FR-014**: The pack/version/publish evaluation and the attestation projection MUST be **pure, total
  functions over already-sensed inputs** (packed artifacts + digests, sensed versions, declared plan/pins/posture,
  the provenance audit snapshot); the only I/O — packing the projects and reading the pack outputs / provenance
  sources — MUST live at the host edge through the existing ports (F50/F51 execution, F54 sensing), with no
  filesystem / process / registry dependency in the pure cores.
- **FR-015**: The new JSON surfaces (the attestation summary projection and any extension of `release.json` /
  `verify.json`) MUST follow the existing deterministic-JSON, `schemaVersion`-headed precedent (F25/F42/F55/F56)
  — additive, byte-identical when empty, leaving every existing `route.json` / `ship.json` / `verify.json` /
  `release.json` / `provenance.json` golden byte-identical except where a new field is explicitly added under a
  bumped schema version.

### Key Entities *(include if data involved)*

- **Package evidence**: the real output of packing a packable project — artifact path, version, and digest —
  collected through the existing execution port as `Pack` command runs and fed to the existing release families;
  the proof that a releasable artifact was actually built.
- **Version policy**: the rule that each packed artifact's version is bumped relative to its released baseline,
  evaluated against the **packed** version; the basis of the version-bump verdict.
- **Publish plan** (reused/sensed, F53/F54): the declared description of what is published, where, and under what
  posture — validated as a publication precondition.
- **Trusted-publishing posture / template pins** (reused, F53/F54): the publishing-identity posture and product
  template pins validated as publication preconditions.
- **Attestation summary**: the SLSA/in-toto-**shaped** projection of the F25 provenance audit snapshot — subject
  (artifacts + digests), builder identity, materials, invocation — emitted as compatible metadata that never
  claims formal compliance.
- **Release report / verify report**: the immutable, presentation-free report objects carrying the publication
  verdict, its exit-code basis, the package/version/publish evidence, and the attestation summary — the single
  source of truth for JSON and later human projections.
- **Release exit-code basis** (reused/extended, F53/F55): the basis for the release process exit code that makes
  publication a blocking boundary distinct from ship.
- **Provenance audit snapshot** (reused, F25/F033): the deterministic roll-up of provenance inputs and recorded
  command runs that the attestation summary is projected from.
- **Scheduled exhaustive validation matrix**: a declared broad validation matrix marked for the
  scheduled/release boundary (Exhaustive cost) rather than the inner loop.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A product whose every packable project packs at a bumped version passes the pack/version
  preconditions; a project that fails to pack, or packs at an unbumped/downgraded version, **blocks release**
  with a named reason — verified by a version-bump matrix and a pack-evidence fixture across multiple packable
  projects.
- **SC-002**: A change that passes `fsgg ship` (mergeable) but fails a publication precondition **blocks**
  `fsgg release` with a release exit-code basis distinct from ship, while a fully releasable product passes the
  boundary — verified by a mergeable-but-not-releasable fixture and a fully-releasable fixture.
- **SC-003**: `fsgg verify` surfaces a release-readiness preview of the publication verdict using the same
  evidence, advisory at the pre-PR boundary and never itself the blocking release gate — verified by a
  verify-preview fixture.
- **SC-004**: The publish-plan, trusted-publishing-posture, and template-pin preconditions each surface their
  satisfied/unmet state and reason, blocking release on any unmet precondition — verified by publish-plan,
  posture, and template-pin-drift fixtures.
- **SC-005**: The attestation summary carries subject / builder / materials / invocation in an SLSA/in-toto
  compatible shape, is **byte-identical** for identical inputs, changes only when a reproducible input changes,
  and carries an explicit "compatible-shape, not formal compliance" marker — verified by attestation-summary
  snapshot fixtures (including a no-op-input-change stability check and a failed-build no-attested-subject case).
- **SC-006**: A declared exhaustive validation matrix is **decided to run** at the scheduled/release boundary
  (the boundary's cost ceiling admits its Exhaustive cost) and is **deferred** (not run) at the inner-loop
  boundary, and no matrix is decided for a run that was not declared — verified by a scheduled-matrix decision
  fixture (`decideMatrix`). *(Actually invoking the admitted matrix and gating the verdict on it is a host/CI
  follow-up out of this row's scope — plan D4.)*
- **SC-007**: The release and verify report objects are immutable and presentation-free, and the JSON
  projections render from them deterministically (byte-identical for identical state, every existing golden
  untouched except for explicitly-versioned additive fields) — verified by report-object parity and
  determinism/reordering tests.
- **SC-008**: Publication evaluation distinguishes a missing/malformed input (no packable project, unreadable
  pack output, absent provenance/attestation input, missing publish plan) from a tool defect, naming the source,
  with no fabricated pack, hollow attestation, or fabricated pass — verified by safe-failure fixtures.

## Assumptions

- **Next-item resolution**: "next item in plan" is roadmap **F26 · `026-verify-release-provenance`**, the next
  unimplemented row after F25 (`060-cost-cache-command-provenance`) merged on 2026-06-25. F26's roadmap
  dependency (F25) is implemented, as are the release/verify stacks it extends (F53 release rules, F54 release
  facts sensing, F55 `fsgg release`, F56 `fsgg verify`). The new spec directory is `061-verify-release-provenance`
  (sequential), independent of the roadmap's `026-` row id.
- **The release-rule families and host commands are reused unchanged**: the six release families (version bump,
  package metadata, template pins, publish plan, trusted publishing, provenance) are F53, sensed by F54 and run
  by F55; the pre-PR `fsgg verify` host is F56 with its five-code exit scheme. This row adds the **enforced
  publication evidence** (real pack + version-bump) those rules are evaluated against, the **attestation
  summary**, and the **unified report objects** — it does **not** add a release-rule family, change a finding's
  severity mapping (F23/F24), or change the verify/release exit-code scheme.
- **The provenance audit snapshot and command-kind taxonomy are reused unchanged**: the F25 `Provenance` audit
  snapshot and `CommandKind` taxonomy (including `Pack`) model what ran; the attestation summary is a new
  **projection** of that snapshot, not a new provenance identity. Pack duration remains sensed metadata excluded
  from identity (F25/F032).
- **What is genuinely new**: the enforced act of **packing every packable project at a bumped version** and
  feeding the real pack outputs (package evidence) to the existing release rules; the **release-readiness preview**
  in `fsgg verify`; the **publish-plan / posture / template-pin evidence** surfaced first-class in the report;
  the **SLSA/in-toto-shaped attestation summary** projected from the F25 provenance snapshot without
  overclaiming; the unified, immutable **release/verify report objects**; and the **scheduled exhaustive
  validation hook**. The precise attestation field shape, whether the attestation surfaces as its own sidecar
  artifact or embeds in `release.json`, and the exact mechanism for declaring/triggering the scheduled matrix
  are planning decisions deferred to `/speckit-plan`.
- **Pure decision + edge sensing split**: the pack/version/publish evaluation and the attestation projection are
  pure, total functions over already-sensed inputs (the F53/F54/F25 leaf precedent); packing the projects and
  reading the pack outputs / provenance happen only at the host edge through the existing execution (F50/F51) and
  sensing (F54) ports, with no filesystem/process/registry dependency in the pure cores (FR-014).
- **Provenance emits useful metadata first, claims compliance only after verification**: per roadmap C7 and the
  M9 milestone note, the attestation summary is SLSA/in-toto-**shaped** and reproducible but explicitly **does
  not** assert formal SLSA-level or in-toto conformance; a formal-compliance claim is reserved for an explicit
  later verification step (FR-008, SC-005).
- **Distinct from ship**: the publication boundary is a separate verdict from the F24/`fsgg ship` merge verdict;
  a change may merge yet not be releasable. The two are reported independently and neither masks the other
  (FR-004, SC-002).
- **Determinism is mandatory**: every verdict, evidence, attestation, and report normalizes ordering and paths
  and avoids wall-clock/username/environment dependence so identical repository state yields byte-identical
  output (FR-010, SC-005, SC-007), matching the byte-identical discipline F42–F60 hold.
- **Standalone preserved**: the publication boundary runs against a product checked out standalone using only its
  own packable projects, version baselines, declared plan/pins/posture, and recorded provenance (FR-013),
  consistent with the F23/F24/F25 standalone guarantee; no release or attestation step requires monorepo-only
  access or a network/registry call from the cores.
</content>
