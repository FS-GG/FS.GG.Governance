# Feature Specification: SDD First-Class Reference Integration (Template + Tutorials)

**Feature Branch**: `072-sdd-first-class-integration`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "integrate fs.gg.sdd as a first class citizen. template. tutorials...."

## Overview

Feature `071-runtime-project-templates` shipped the **generic** template-provider
seam — a provider contract, a pure scaffold-orchestration MVU core, a total edge
interpreter, and a deterministic scaffold-manifest projection — as
`FS.GG.Governance.*` libraries. By design that core hardcodes **no** provider name,
package id, target name, or layout, and 071 deliberately left two things outside
this repository: a concrete provider, and host wiring of the seam into
`fsgg-sdd init` (deferred to the sibling `FS.GG.SDD` repo, research D0).

The consequence is that FS.GG.SDD — the lifecycle product this whole ecosystem is
built to serve — exists in this repository only as an abstract "external customer."
There is no end-to-end, runnable demonstration that turns an empty directory into a
buildable, governed SDD product, and no guided material for the three audiences who
need one: a team adopting SDD, an author writing their own template provider, and an
integrator wiring SDD readiness into the Governance loop.

This feature makes SDD a **first-class citizen inside Governance as a reference
integration**: a concrete **reference template provider** (a worked example that
conforms to the 071 contract and lays down a buildable runtime skeleton), a
**layered end-to-end worked example** that goes from an empty directory through the
lifecycle governance skeleton and then the runtime skeleton via the seam, and a set
of **tutorials** for adopters, provider authors, and the SDD↔Governance handoff.

Crucially, "first-class" here means *documented, tested, and reproducible* — not
ownership. The reference provider and its SDD-flavored template content live as a
clearly separated example/sample artifact; the **generic seam core gains no
provider-, package-, target-, or layout-specific knowledge**, preserving the
constitution's genericity operating rule. Production wiring of the seam into
`fsgg-sdd init` remains owned by the sibling `FS.GG.SDD` repository; the deliverable
here is the reference and the tutorials, not a change to `fsgg-sdd init`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Adopter goes from empty directory to a buildable, governed product (Priority: P1)

A team adopting SDD follows the **adopter onboarding tutorial**. Starting from an
empty directory they obtain the lifecycle governance skeleton (the
`.fsgg/`/`work/`/`readiness/` layer, produced by the SDD `init` step), then run the
**reference template provider** through the seam to lay down a runtime project
skeleton (source project, test project, package/manifest, entry point). They finish
with a project that both **builds** with the provider's documented toolchain and is
**ready to be governed**, plus a deterministic manifest recording every generated
path — without hand-writing boilerplate.

**Why this priority**: This is the headline value — "empty governed directory" →
"buildable, governed product" demonstrated end-to-end. It is the proof that the 071
seam works for a real customer, and every other story builds on it.

**Independent Test**: Run the reference example against a temporary directory; confirm
both the lifecycle layer (precondition) and the provider's runtime layer appear, the
runtime skeleton builds on the first attempt with no hand-editing, and the scaffold
manifest lists every generated path. Delivers value even if no other story ships.

**Acceptance Scenarios**:

1. **Given** an empty target directory and the reference provider selected, **When** the operator runs the worked example with scaffolding requested, **Then** the runtime skeleton is created on top of the lifecycle skeleton and a manifest lists every path the provider generated.
2. **Given** scaffolding completed, **When** the operator builds the runtime skeleton with its documented toolchain, **Then** the build succeeds without further hand-editing of boilerplate.
3. **Given** scaffolding completed, **When** the operator inspects the result, **Then** the runtime files are recorded as provider-owned, and the lifecycle/governance tooling claims ownership only of delegation, safety, recording, and reporting — not of the runtime code's internal shape.
4. **Given** the same empty target, **When** the example is run repeatedly, **Then** the scaffold manifest is byte-identical every time.

---

### User Story 2 - Provider author clones the reference to build their own provider (Priority: P2)

An author who needs a runtime stack the reference does not cover follows the
**provider-author tutorial**. They copy the reference provider as a starting point,
adapt the files it describes to their stack, register and select it, and run it
through the **same** seam — learning the 071 contract by working example. No change
to the lifecycle/seam tool is required, and no provider-specific knowledge leaks into
that tool.

**Why this priority**: The boundary rule "runtime ownership stays outside the tool"
is only real if third parties can supply providers. The reference provider is the
canonical thing they clone; without a faithful, contract-conforming starting point
the seam is hard to adopt.

**Independent Test**: Following only the tutorial and the reference provider, produce
a minimal custom provider, register it, and run the example selecting it; confirm the
seam resolves and invokes it through the identical path with no edits to the tool.

**Acceptance Scenarios**:

1. **Given** the reference provider as a starting point, **When** an author follows the provider-author tutorial, **Then** they can produce a custom provider that the seam resolves and invokes through the same path as the reference.
2. **Given** the reference and a custom provider, **When** each is selected in turn, **Then** the tool's behavior differs only in what the provider emits; the same safety, recording, and reporting rules apply to both.
3. **Given** a provider whose declared contract version is incompatible with the seam, **When** it is selected, **Then** the example surfaces the explicit, actionable version-mismatch refusal rather than partially scaffolding.

---

### User Story 3 - Integrator connects scaffolded readiness to the Governance loop (Priority: P3)

An integrator follows the **SDD↔Governance handoff tutorial**. Using the scaffolded
governed product as the worked subject, they learn how the product's SDD readiness /
`governance-handoff.json` outputs are consumed by Governance routing, evidence, and
enforcement — and how each readiness state maps to Governance's tokens (per ADR
0002). They leave understanding where the SDD boundary ends and the Governance loop
begins.

**Why this priority**: It closes the loop from "scaffolded product" to "governed
product," making the integration genuinely first-class rather than stopping at
project creation. It is lower priority because it is explanatory and builds on the
artifacts produced by Stories 1–2.

**Independent Test**: Following the handoff tutorial against the scaffolded product's
readiness outputs, a reader can correctly state which readiness fields Governance
consumes and how each maps to routing/evidence/enforcement, matching ADR 0002.

**Acceptance Scenarios**:

1. **Given** a scaffolded governed product, **When** an integrator follows the handoff tutorial, **Then** they can identify which SDD readiness outputs Governance consumes and how each maps to a Governance routing/evidence/enforcement outcome.
2. **Given** the handoff tutorial's worked mapping, **When** it is compared to ADR 0002, **Then** every documented mapping (including `deferred → skipped`) agrees with the accepted contract.

---

### Edge Cases

- **Collision in the target**: the target already contains a file the reference provider would emit → the seam's existing collision decision applies; the example demonstrates the refusal/decision rather than silently overwriting.
- **Contract-version drift**: the reference (or a cloned) provider declares a contract version the seam does not support → explicit, actionable version-mismatch refusal, no partial tree.
- **Missing toolchain**: the adopter lacks the runtime skeleton's documented toolchain → the tutorial states prerequisites and the build step fails with an actionable prerequisite message, distinguishable from a tool defect.
- **Lifecycle layer skipped**: the adopter runs the runtime scaffold without first obtaining the lifecycle governance skeleton → the tutorial clarifies the ordering and that the lifecycle layer is a sibling-owned precondition.
- **No provider selected**: the example must reproduce today's no-provider behavior with no difference — the reference provider is an additive choice, never a new default.
- **Empty provider output**: a provider that describes zero files → the manifest records an empty generated set and the example completes cleanly.
- **Docs/example drift**: a future change alters the seam or reference provider → the automated end-to-end check fails the build so tutorials cannot silently rot.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST provide a **reference template provider** that conforms to the existing 071 provider contract and describes a minimal but **buildable** runtime project skeleton for an SDD-governed product (at least a source project, a test project, a package/manifest, and an entry point).
- **FR-002**: The reference provider MUST be delivered as a clearly separated example/sample artifact; the **generic seam core libraries MUST gain no provider-, package-, target-, or layout-specific knowledge** as a result of this feature (genericity preserved).
- **FR-003**: The repository MUST provide a **layered end-to-end worked example** that takes an empty target directory through the lifecycle governance skeleton (the documented, sibling-owned precondition) and then the runtime skeleton emitted by the reference provider through the seam, producing a buildable, governed result and a deterministic manifest recording every generated path.
- **FR-004**: The runtime skeleton produced by the reference provider MUST build with its documented toolchain on the first attempt, without further hand-editing of boilerplate.
- **FR-005**: The feature MUST provide an **adopter onboarding tutorial** covering the path from an empty directory through scaffold, govern, verify, and ship. The **scaffold/build/manifest** steps MUST be anchored to the executable reference example (per FR-008/FR-009); the **govern/verify/ship** steps are presented as **cross-references** to the existing Governance surfaces (prior features), not exercised by this feature's end-to-end check, and the tutorial MUST say so plainly so a reader does not mistake them for steps this feature verifies.
- **FR-006**: The feature MUST provide a **provider-author tutorial** showing how to use the reference provider as a starting point to author and register a custom provider against the documented contract, with no change to the lifecycle/seam tool.
- **FR-007**: The feature MUST provide an **SDD↔Governance handoff tutorial** explaining how a scaffolded governed product's SDD readiness / handoff outputs are consumed by Governance routing, evidence, and enforcement, with mappings consistent with ADR 0002.
- **FR-008**: Every tutorial step that this feature's end-to-end check exercises MUST be anchored to the actual, executable reference example, so documented steps match real behavior (no doc/example drift). This covers the **adopter** (US1) and **provider-author** (US2) tutorials' scaffold/build/manifest/clone steps. The **SDD↔Governance handoff** tutorial (US3) is **explanatory** and ships no consumer code (see plan Deferred); its anchor is **ADR 0002** — every row of its readiness→token mapping MUST match the accepted contract (FR-007, SC-008) — rather than the executable example.
- **FR-009**: The reference example MUST be exercised by an automated check that runs it end-to-end against a **real temporary directory** and asserts the runtime skeleton appears, builds, and yields the expected deterministic manifest.
- **FR-010**: With no provider selected, the worked example MUST reproduce the existing no-provider behavior unchanged, demonstrating that the seam remains strictly opt-in.
- **FR-011**: When a selected provider's declared contract version is incompatible with the seam, the worked example MUST surface the seam's explicit, actionable version-mismatch refusal rather than partially scaffolding, demonstrated as a documented failure-path example.
- **FR-012**: The feature MUST document the **ownership boundary**: runtime code emitted by the reference provider is provider-owned; the lifecycle/seam tooling owns only delegation, safety, recording, and reporting; and SDD-specific template content lives only in the example, never in the generic core.
- **FR-013**: The documentation MUST state that **production host wiring** of the seam into `fsgg-sdd init` is owned by the sibling `FS.GG.SDD` repository, so adopters are not misled into believing `fsgg-sdd init` already invokes this provider; the Governance-side artifact is a reference demonstration.

### Key Entities *(include if feature involves data)*

- **Reference Template Provider**: a concrete, contract-conforming example provider that *describes* the target-relative files of a buildable SDD-governed runtime skeleton and writes nothing itself; the canonical thing provider authors clone.
- **Layered Worked Example (Scaffold Run)**: the runnable end-to-end demonstration that takes an empty directory through the lifecycle layer (precondition) and the runtime layer (via the seam), producing files plus a deterministic manifest.
- **Runtime Project Skeleton**: the buildable source/test/package/entry-point output owned by the provider.
- **Lifecycle Governance Skeleton**: the `.fsgg/`/`work/`/`readiness/` layer the example layers on top of, owned by the sibling SDD `init` step.
- **Scaffold Manifest**: the deterministic, byte-stable record (from 071) of every generated path that the example asserts on.
- **Tutorial Set**: three audience-targeted guides — adopter onboarding, provider author, and SDD↔Governance handoff — each anchored to the executable example.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A newcomer following the adopter onboarding tutorial reaches a buildable, governed product from an empty directory in under 15 minutes and without hand-writing any boilerplate.
- **SC-002**: Running the reference example against an empty temporary directory produces a runtime skeleton that builds successfully on the first attempt with no hand-editing, on 100% of runs.
- **SC-003**: Re-running the reference example over the same empty target yields a byte-identical scaffold manifest every time.
- **SC-004**: A provider author, following only the provider-author tutorial and starting from the reference provider, can produce a working custom provider that runs through the seam with zero changes to the lifecycle/seam tool.
- **SC-005**: 100% of the **executable** steps shown in the adopter (US1) and provider-author (US2) tutorials — scaffold, build, manifest, clone — are covered by the automated end-to-end check, so any doc/example drift is caught by the build. The handoff tutorial's (US3) mapping is instead guarded by the ADR-0002 row-agreement check (SC-008), and the adopter tutorial's govern/verify/ship cross-references are out of this check's scope by FR-005.
- **SC-006**: The generic seam core's public surface is unchanged by this feature — the surface-drift check reports no delta on the core libraries — confirming genericity is preserved.
- **SC-007**: With no provider selected, the example reproduces today's no-provider output with zero difference.
- **SC-008**: A reader of the handoff tutorial can correctly map every scaffolded-product readiness state to its Governance routing/evidence/enforcement outcome, and the documented mapping matches ADR 0002 on 100% of rows.

## Assumptions

- The 071 generic seam (provider contract, scaffold MVU core, edge interpreter, manifest projection) is the integration point and is treated as stable and available; this feature builds on it **without changing its public surface**.
- Production host wiring of the seam into `fsgg-sdd init` is owned by the sibling `FS.GG.SDD` repo (071 research D0); the Governance-side deliverable is a reference example plus tutorials, not a modification of `fsgg-sdd init` (which this repository does not own).
- The reference provider targets an F#/.NET runtime skeleton (the repository's exclusive stack) as the concrete worked example; the provider **contract** itself remains stack-agnostic.
- The lifecycle governance skeleton layer (`.fsgg/`/`work/`/`readiness/`) is demonstrated as a documented precondition (sibling-owned), while the runtime layer is the live, tested portion delivered in this repository.
- "First-class citizen" means a documented, tested, reproducible reference integration inside Governance — not Governance taking ownership of SDD identity, which the constitution's genericity operating rule forbids.
- Tutorials are delivered as repository documentation pages alongside the existing quickstart/migration docs.
- **Change classification: Tier 1** — the feature adds a new, contract-conforming reference-provider surface and a new executable worked-example/test artifact, even though the generic core surface is unchanged. The plan confirms whether the reference provider carries a packed/public `.fsi` surface or ships purely as an example project; either way the generic core baselines stay untouched (SC-006).
