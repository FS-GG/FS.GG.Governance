# Feature Specification: Publish a Populated Reference `.fsgg` Gate Set

**Feature Branch**: `079-reference-gate-set`

**Created**: 2026-06-27

**Status**: Draft

**Change Classification**: Tier 2 (data + test + docs). Publishes a YAML data artifact, an adopter doc, and a regression-guard test project; introduces **no new public F# API surface**, **no `.fsi`** and **no surface-area baseline change**, and **no new dependency** (see plan.md Constitution Check / research D7). Public API impact: none.

**Input**: Coordination board item — *P3 · governance — Publish populated reference `.fsgg` gate set (build/test + EvidenceGraph/EvidenceAudit)*: "Make `checks:`/`commands:` non-empty with a populated reference gate set (build/test + in-process EvidenceGraph/EvidenceAudit from build.fsx). Keep the `light` policy profile as the no-blocking default for first-touch — populated ≠ blocking by default." (FS-GG Coordination board, Phase P3 Governance; Epic: "Governance actually fires, and the handoff is consumed".)

## Why This Feature *(context)*

Today the only populated `.fsgg` configurations in the repository are minimal test fixtures (`tests/.../valid-complete/.fsgg`, `tests/golden-fixture/.fsgg`) — one toy `build` check, or an empty check list. There is **no curated, production-shaped gate set** an adopter (or a generated SDD product) can copy to make governance actually *do* something. As a direct consequence the downstream Templates work (Coordination board P4 — "Populate fs-gg-governance overlay (real gates from P3)") is **Blocked**: it has no real gates to populate the overlay from.

This feature closes that gap by publishing a single, curated, **populated** reference gate set: meaningful first-touch checks (build, test, and an in-process evidence-integrity check) declared so that governance fires real gates — while keeping the `light` policy profile as the non-blocking default so that *populated does not mean blocking* on first adoption.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An adopter gets gates that actually fire (Priority: P1)

A team adopting FS-GG governance on an SDD-lifecycle F#/.NET product wants governance to evaluate real checks, not an empty placeholder. They copy the published reference `.fsgg` gate set into their project. When governance runs over a change, it selects and reports concrete build/test/evidence gates for the paths those gates govern — instead of finding nothing to do.

**Why this priority**: This is the core deliverable. Without a populated, loadable, routable gate set, every downstream consumer (adopters, the P4 Templates overlay) has nothing to build on, and the P3 Epic's promise that "Governance actually fires" is unmet.

**Independent Test**: Load the published reference `.fsgg` through the existing configuration pipeline and route a representative change through it; confirm it validates with zero errors and that real build/test/evidence gates are selected for the governed paths. Delivers value as a standalone, copyable artifact even before any downstream consumption.

**Acceptance Scenarios**:

1. **Given** the published reference `.fsgg`, **When** it is loaded through the governance configuration pipeline, **Then** it validates with zero configuration errors and zero unknown/unrecognized-config findings.
2. **Given** the published reference `.fsgg`, **When** the declared checks are assembled into the gate registry, **Then** every declared check resolves to exactly one gate-registry entry and references a command that is declared in the tooling section (no dangling command references, no orphan checks).
3. **Given** a representative change touching a governed path, **When** governance routes it against the reference gate set, **Then** the build, test, and evidence gates appropriate to that path are selected and reported.

---

### User Story 2 - First-touch is non-blocking by default (Priority: P2)

A team enabling the reference gate set for the first time wants to *see* governance fire without it immediately blocking their pull requests or ship. With the default `light` profile, the populated gates are reported as advisory, giving the team visibility and a path to ratchet up strictness deliberately later — not a wall on day one.

**Why this priority**: "Populated ≠ blocking by default" is an explicit constraint from the board item and the architecture posture (preserve light-by-default). Getting populated gates without this would regress first-touch adopters from "nothing happens" straight to "everything is blocked," which would discourage adoption.

**Independent Test**: Route a representative change against the reference gate set under the default profile and confirm every resulting gate's effective severity is advisory (no blocking outcomes), then confirm that switching to a stricter profile *does* change the posture — proving the non-blocking result is a deliberate default, not an inability to block.

**Acceptance Scenarios**:

1. **Given** the reference `.fsgg`, **When** its policy is inspected, **Then** the default profile is `light`.
2. **Given** the reference gate set under its default profile, **When** a change is routed on the everyday inner/verify loop (`RunMode.Verify` and below — including a change that fails a declared check), **Then** no gate produces a blocking effective severity — all reported outcomes are advisory. (The ship/release gate at `Gate`/`Release` modes is the deliberate ratchet where `block-on-ship` checks do escalate even under `light`; it is not covered by this scenario — see plan research D5.)
3. **Given** the same reference under a stricter profile, **When** the same change is routed, **Then** at least one gate's effective severity becomes blocking — demonstrating the gates *can* block and that `light` is the chosen non-blocking default.

---

### User Story 3 - Evidence integrity is a declared, first-class gate (Priority: P3)

The reference gate set demonstrates how a generated product surfaces its build-time evidence state to governance: it declares an in-process evidence-integrity check (the EvidenceGraph/EvidenceAudit step the product's own build performs) as a normal governed gate, so adopters can see the intended shape of "the build reports its evidence state and governance gates on it."

**Why this priority**: The board item names EvidenceGraph/EvidenceAudit explicitly. It is the distinguishing example beyond plain build/test, but the gate set still delivers value with build/test alone, so it is sequenced last.

**Independent Test**: Inspect the reference gate set and confirm it declares an evidence-integrity check bound to a real command, with complete metadata, that loads and routes like any other gate; confirm that on first touch (no real evidence yet) the check surfaces as advisory/not-yet-satisfied rather than blocking.

**Acceptance Scenarios**:

1. **Given** the reference `.fsgg`, **When** its checks are listed, **Then** an evidence-integrity (EvidenceGraph/EvidenceAudit) check is present, bound to a declared command, with complete and valid metadata.
2. **Given** a first-touch product with no real evidence recorded yet, **When** the evidence-integrity gate is routed under the default profile, **Then** it reports advisory (not blocking).

---

### Edge Cases

- **Dangling command reference**: a check naming a command that is not declared in the tooling section must be a validation failure — the published reference must contain none.
- **Orphan check / unreachable domain**: a check whose domain is not a declared domain, or a domain no path-map entry can route to, must be detectable as an orphan — the published reference must contain none.
- **Profile drift to blocking**: if the default profile were ever changed away from `light`, first-touch adopters would be blocked; a guard must catch this.
- **Reference rots to empty**: if the populated checks/commands were ever emptied, the gate set would silently stop firing; a guard must catch this so the reference cannot regress to the prior empty state.
- **Evidence not yet present**: on first touch the evidence-integrity check has no real evidence; it must surface as advisory/pending under the default profile, never as a blocking failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST publish a single curated, populated reference `.fsgg` gate set (the full project / policy / capabilities / tooling set) at a stable, discoverable location, alongside the existing SDD reference worked-example.
- **FR-002**: The reference's check list MUST be non-empty and MUST include, at minimum, a build check and a test check.
- **FR-003**: The reference MUST declare an in-process evidence-integrity check (representing the generated product's build-time EvidenceGraph/EvidenceAudit step) as a first-class governed check.
- **FR-004**: Every declared command referenced by a check MUST be declared in the tooling section — the reference MUST contain no dangling command references.
- **FR-005**: Every declared check MUST carry complete, valid metadata (owning domain, command, owner, cost, environment class, maturity), and every check's domain MUST be a declared domain reachable by at least one path-map entry — the reference MUST contain no orphan checks or unreachable domains.
- **FR-006**: The reference policy's default profile MUST be `light`, and under that default the populated gates MUST remain advisory on the everyday inner/verify loop (`RunMode.Verify` and below; never escalated to blocking there), so that *populated ≠ blocking* on first touch. (Escalation to blocking is reserved for the deliberate ship/release ratchet at `Gate`/`Release` modes and for the stricter profiles — research D5.)
- **FR-007**: The reference gate set MUST load and validate through the existing governance configuration pipeline with zero validation errors and zero unknown/unrecognized-config findings.
- **FR-008**: The reference's declared checks MUST assemble into the gate registry and MUST be selectable (routable) for the paths the reference's path-map governs.
- **FR-009**: The reference gate set MUST be usable, unedited, as the source the downstream Templates overlay (Coordination board P4) copies to populate `fs-gg-governance` — i.e. a downstream consumer can adopt it without modification.
- **FR-010**: An automated regression guard MUST assert that the reference loads, routes, keeps a non-empty build/test/evidence check set, and stays non-blocking under the default profile — so the reference cannot silently rot back to empty or flip to blocking.
- **FR-011**: The reference MUST be accompanied by adopter-facing documentation explaining each declared gate and the non-blocking-by-default (`light`) posture, including how to ratchet strictness up deliberately.

### Key Entities *(include if feature involves data)*

- **Reference gate set**: the curated, populated `.fsgg` (project + policy + capabilities + tooling) published as the canonical first-touch example; the unit downstream consumers copy.
- **Check**: a declared governed verification (build, test, evidence-integrity) with a domain, a bound command, and metadata (owner, cost, environment, maturity).
- **Command**: a declared tooling invocation a check binds to (e.g. the build, test, and in-process evidence step).
- **Domain / Path-map**: the capability classification and the globs that route changed paths to domains, determining which checks fire for which paths.
- **Policy profile**: the strictness setting controlling whether checks escalate to blocking; `light` is the non-blocking default declared by the reference.
- **Evidence-integrity check**: the declared gate representing the product's in-process EvidenceGraph/EvidenceAudit step.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The reference declares at least 3 checks (build, test, evidence-integrity) with 0 dangling command references — up from the prior state of 0 curated production-shaped checks.
- **SC-002**: Loading the reference through the configuration pipeline yields 0 validation errors and 0 unknown/unrecognized-config findings.
- **SC-003**: Routing a representative change (including one that fails a declared check) against the reference under its default `light` profile on the everyday inner/verify loop (`RunMode.Verify` and below) yields 0 blocking gate outcomes — every reported gate is advisory. (The deliberate ship/release ratchet at `Gate`/`Release` modes is out of scope for this criterion; the guard exercises `RunMode.Verify` — research D5.)
- **SC-004**: 100% of the reference's declared checks resolve to a gate-registry entry and are selectable by at least one governed path (0 orphan checks, 0 orphan commands, 0 unreachable domains).
- **SC-005**: A downstream consumer can populate its overlay by copying the reference with 0 edits required for it to load and route — confirming it is reusable as published.
- **SC-006**: Switching the reference from its default profile to a stricter profile produces at least 1 blocking outcome on the same failing change — proving the non-blocking result is a deliberate default, not an inability to block.
- **SC-007**: The regression guard fails if the check set is emptied, a command reference is broken, or the default profile is changed away from `light`.

## Assumptions

- The reference targets an SDD-lifecycle F#/.NET product — the shape produced by `fsgg-sdd scaffold` / the existing SDD reference worked-example (feature 072) — so its build/test/evidence commands match that runtime skeleton.
- "In-process EvidenceGraph/EvidenceAudit from build.fsx" refers to the **generated product's own build** computing and reporting its evidence state in-process (the FAKE-style `build.fsx` lives in the generated product, not in this Governance repo). Governance declares this as a normal check; this feature does **not** re-introduce the deleted Spec Kit evidence-audit extension or any DAG/merge-gate machinery into this repository.
- The semantics of the `light` profile and effective-severity derivation are unchanged from the existing enforcement core; this feature does not add per-class strictness dials — it only *uses* `light` as the declared default.
- This feature delivers the **producer** side only: a published, validated reference gate set. Enforcement of the SDD→Governance handoff is the separate P3 board item ("Ship the handoff CONSUMER") and is out of scope here.

## Out of Scope

- Implementing or wiring the handoff consumer / enforcing `governance-handoff.json` (separate P3 item).
- Making any gate blocking by default, or changing profile/enforcement/severity semantics.
- Building the downstream P4 Templates overlay itself — this feature only publishes the artifact that overlay will copy.
- Adding new check kinds, commands, or config schema fields beyond what the existing schema already supports.

## Dependencies

- Existing `.fsgg` configuration schema and loader, gate registry assembly, and routing/gate-selection pipeline (already shipped).
- The existing SDD reference worked-example / runtime skeleton (feature 072), which the reference gate set is shaped to govern.
- The existing enforcement core and `light` profile semantics (used unchanged).
- Unblocks: Coordination board P4 — "Populate fs-gg-governance overlay (real gates from P3)".
