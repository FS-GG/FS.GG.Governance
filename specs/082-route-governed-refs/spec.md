# Feature Specification: Promote `governedReferences` to First-Class Routing Facts

**Feature Branch**: `082-route-governed-refs`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next governance item on the project coordination board." Resolved to ADR-0002 queued consumer work item #3 — the only remaining governance-scoped follow-on after F081 delivered the SDD→Governance handoff consumer. The org Coordination board's Governance lane (P3) and the P4 governance overlay are all Done; this promotes the declared `governedReferences` from optional gate decoration into a real routing input.

## Context & Background

The SDD→Governance handoff document (`readiness/<id>/governance-handoff.json`, contract v1.x) carries a `governedReferences` block: a list of `{ workItem, paths }` entries declaring which governed paths a given SDD work item touches. Feature 081 (`081-sdd-handoff-consumer`) shipped the consumer that turns a handoff into gates, but it left `governedReferences` as **optional decoration only**: the declared paths are attached as synthetic "self-glob" provenance on the handoff's own pre-selected gates (evidence / readiness / integrity), and they do **not** influence which *other* domain gates (build, test, evidence-integrity, etc.) get selected. ADR-0002 records this as queued item #3 ("Optional: fold `governedReferences` into `Routing.route` inputs… correctness is independent of them — FR-010").

The consequence today: a work item can declare it governs `src/PaymentEngine/**`, but if its actual sensed change set is empty or narrow, none of the build/test gates that the declared surface *implies* are selected. The declared surface and the enforced surface diverge. This feature closes that gap by treating the declared governed paths as **first-class routing inputs** — routed through the same path-map → domain → gate-selection machinery as sensed changed paths — so the surface a work item *declares* it governs actually drives which gates fire.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declared governed surface drives gate selection (Priority: P1)

A team runs a Governance verdict command (`route`, `ship`, or `verify`) on a repository that contains an SDD-produced handoff document. The handoff declares, under `governedReferences`, that work item `042` governs paths under `src/PaymentEngine/**` and `tests/PaymentEngine/**`. The team expects the build and test gates that own those domains to be **selected and enforced**, because the work item has declared that surface — not only when a file under it happens to appear in the sensed git diff.

**Why this priority**: This is the entire point of the feature and the ADR-0002 item #3 resolution. Without it, the declared governed surface is inert for routing, and the handoff under-enforces relative to what the work item claims to touch. P1 because the other stories are refinements of this behavior.

**Independent Test**: Load a handoff whose `governedReferences` declare paths in known domains, with an empty (or unrelated) sensed change set, through the real Config→Gates→Routing→Route pipeline; assert the domain gates owning those declared paths now appear in the route's selected gates (and, at ship/verify, contribute to the verdict) — where today they would not.

**Acceptance Scenarios**:

1. **Given** a handoff declaring `governedReferences` paths that the path-map routes to the `build` and `test` domains, **and** an empty sensed change set, **When** `route` runs, **Then** the `build:build` and `test:test` gates appear in the selected gates, each with a selecting-path entry naming the declared path and the real glob it matched.
2. **Given** that same handoff and a failing change under one declared domain, **When** `ship` runs in a mode where that domain's gate is blocking, **Then** the gate appears in the blockers and the ship verdict is non-shippable — driven by the declared surface, not only the diff.
3. **Given** a handoff declaring paths in a domain whose gate is verify-blocking under `Strict`, **When** `verify --strict` runs, **Then** the verdict is blocked by that gate.

---

### User Story 2 - A governed-routed gate is traceable to its declared path (Priority: P2)

A reviewer reading the route trace needs to understand *why* a gate was selected. When a gate is selected because a declared governed path routed to its domain, the trace must show the declared path and the real path-map glob it matched — so the selection is explainable and auditable, not magic.

**Why this priority**: Explainability is a standing governance principle (route output exists to explain verdicts), but the verdict is correct even before the trace is polished, so this is P2 relative to the core selection behavior.

**Independent Test**: With a handoff whose declared paths route to a domain gate, inspect the selected gate's selecting-path list and confirm it records the declared path and the actual matched glob (not the synthetic self-glob F081 used for the handoff's own gates).

**Acceptance Scenarios**:

1. **Given** a declared governed path that the path-map routes to a domain, **When** `route` selects that domain's gate, **Then** the selecting-path entry records the declared path and the real matched glob from the path-map.
2. **Given** the same gate is also selected by a sensed changed path, **When** `route` runs, **Then** the gate carries one merged, de-duplicated set of selecting paths in deterministic order — no double entries.

---

### User Story 3 - Absent / empty / bad handoff stays a byte-identical no-op (Priority: P1)

An existing adopter with no handoff document, or a handoff that declares no `governedReferences`, must see exactly the output they see today. A malformed or version-mismatched handoff must not inject routing candidates either. This preserves the F081 no-op guarantee and protects every existing golden.

**Why this priority**: P1 because it is a hard safety boundary — the feature must not change behavior for anyone who has not opted into declaring governed references, and must not let a bad document widen enforcement. A regression here breaks every existing route/ship/verify golden across three hosts.

**Independent Test**: Run route/ship/verify in (a) an empty repository with no handoff, (b) a repo with a handoff that declares an empty `governedReferences`, and (c) a repo with a malformed/major-version-mismatched handoff; assert the route/ship/verify output is byte-identical to the pre-feature baseline in (a) and (b), and that in (c) the blocking integrity gate still fires but no routing candidates are contributed.

**Acceptance Scenarios**:

1. **Given** no handoff document, **When** route/ship/verify run, **Then** output is byte-identical to current behavior.
2. **Given** a handoff with an empty `governedReferences` list, **When** route/ship/verify run, **Then** output is byte-identical to current behavior.
3. **Given** a malformed or major-version-mismatched handoff, **When** route/ship/verify run, **Then** the document's blocking integrity gate fires (as in F081) and none of its `governedReferences` contribute routing candidates.

---

### Edge Cases

- **Declared path outside the governed root**: treated as out-of-scope and contributes nothing to selection — identical to how a sensed changed path outside the root is handled.
- **Declared path inside the root but unmatched by the path-map**: surfaces the same unknown-governed-path finding that a sensed changed path in the same position would (a declared surface that maps to no domain is a real governance signal). See Assumptions for the alternative considered.
- **Declared path identical to a sensed changed path**: de-duplicated — routed once, one selecting-path entry per gate, cost counted once.
- **Two work items declaring overlapping paths**: the union is de-duplicated before routing.
- **Handoff present but every document declares empty `governedReferences`**: identical to the absent case (no-op).
- **Declared paths that route only to domains with no gates**: no gate is selected from them; this is not an error.
- **Behavior across the three hosts** (`route` / `ship` / `verify`): identical contribution of declared paths to candidates in all three; no host-specific divergence.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When an SDD→Governance handoff document declares `governedReferences`, the system MUST contribute the declared paths as routing candidates to `Routing.route`, alongside the sensed changed paths, in all three verdict commands (`route`, `ship`, `verify`).
- **FR-002**: A declared governed path MUST be routed through the SAME path-map → domain → precedence machinery as a sensed changed path. No special-case routing logic is introduced for declared paths; they route, match, and lose/win precedence identically.
- **FR-003**: A gate selected because a declared governed path routed to its domain MUST appear in the route result's selected gates with a selecting-path entry that records the declared path and the **real** path-map glob it matched (not the synthetic self-glob F081 uses for the handoff's own pre-selected gates).
- **FR-004**: The promotion MUST be additive-only with respect to selection: contributing declared paths can cause MORE gates to be selected, but MUST NOT remove any gate that would otherwise be selected, nor drop any selecting-path that would otherwise be recorded.
- **FR-005**: When no handoff document is present, or no document declares `governedReferences` (or all declare an empty list), the route/ship/verify output MUST be byte-identical to the current (pre-feature) behavior — the F081 no-op guarantee is preserved and extended.
- **FR-006**: When the same path is contributed by both the sensed changed set and `governedReferences`, the system MUST de-duplicate it: the path is routed once, recorded once per selected gate, and counted once in any cost roll-up.
- **FR-007**: A declared governed path outside the governed root MUST contribute nothing (out-of-scope); a declared path inside the root but unmatched by the path-map MUST surface the same unknown-governed-path finding that a sensed changed path in that position would.
- **FR-008**: `governedReferences` from a malformed, major-version-mismatched, or otherwise non-consumable handoff document MUST NOT contribute routing candidates, consistent with F081's rule that a bad document yields no mapped gates; the document's blocking integrity gate MUST still fire.
- **FR-009**: The handoff's own evidence / readiness / integrity gates MUST remain pre-selected regardless of `governedReferences` (relevance = the declared work item), unchanged from F081. This feature ADDS domain-gate selection from declared paths; it does not replace or weaken the existing pre-selection.
- **FR-010**: Selection output MUST remain deterministic and independent of handoff-document order and path source — selected gates ordered by gate id, selecting paths by normalized-path ordinal — so the same inputs always produce the same route/ship/verify bytes.
- **FR-011**: Any new public surface introduced to expose declared paths as routing candidates MUST be `.fsi`-curated, and the affected surface-area baselines MUST be updated additively (Tier 1 change classification), with no production core (`Routing` / `Route` selection algorithm) semantics altered for the no-handoff path.
- **FR-012**: ADR-0002 MUST be updated to record item #3 as resolved (from "Optional: fold… or ignore" to "`governedReferences` are first-class routing candidates"), and the handoff tutorial / worked example MUST be updated in lockstep to show declared paths driving gate selection.

### Key Entities *(include if feature involves data)*

- **Governed reference**: a declared association of one SDD work item to the set of governed paths it touches (`{ workItem, paths }`), carried in the handoff document. The unit of declared governed surface.
- **Routing candidate**: a governed path submitted to the router for domain matching. Today sourced from the sensed change set; this feature adds declared governed paths as a second source, merged and de-duplicated.
- **Selecting path**: the provenance recorded against a selected gate — the path that caused selection plus the glob it matched. For declared-path-driven selection this records the real path-map glob.
- **Selected gate**: a gate chosen for a run together with its selecting paths; the unit that participates in severity resolution and ship/verify roll-up.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a handoff that declares governed paths in known domains and an empty sensed change set, the route selects 100% of the domain gates owning those declared paths — gates that the pre-feature pipeline selects 0 of.
- **SC-002**: For an absent handoff and for a handoff declaring empty `governedReferences`, route/ship/verify output is byte-identical to the pre-feature baseline across all three hosts (every existing golden and snapshot unchanged).
- **SC-003**: A declared governed path that coincides with a sensed changed path produces exactly one selecting-path entry on each gate it selects and is counted once in the cost roll-up (zero double-counting).
- **SC-004**: At least one failing-evidence scenario shows a ship/verify verdict flip — from shippable/clean to blocked — caused solely by a declared governed path selecting a blocking gate the sensed diff did not, demonstrating the declared surface now enforces.
- **SC-005**: A malformed / version-mismatched handoff contributes zero routing candidates while its blocking integrity gate still appears in the verdict (the bad-document boundary holds).
- **SC-006**: Every selecting-path entry produced from a declared governed path names a real path-map glob (no synthetic self-glob leaks into domain-gate selection), and the full selected-gate ordering is deterministic across repeated runs.

## Assumptions

- **Declared paths are treated exactly like changed paths for routing, findings, and cost.** A declared path inside the root that matches no domain produces an unknown-governed-path finding, and declared paths contribute to the cost roll-up. The alternative — using declared paths only for gate selection while suppressing their findings/cost — was considered and rejected as a special case that violates FR-002's "same machinery" intent. `/speckit-clarify` may revisit if the unknown-path finding proves too noisy for declared surfaces.
- **Provenance is the path value, not a new source field.** The existing selecting-path shape (`{ path, matchedGlob }`) is reused; no new "source = diff | declared" discriminator is added, because the declared path's real matched glob already makes the selection explainable. Adding a source field would be a wider surface change reserved for a follow-up if auditors require it.
- **This feature is consumer-side only.** The handoff contract (`governance-handoff@1`) shape is unchanged; no `contractVersion` or `schemaVersion` bump — only Governance's *consumption* posture changes (the same pattern ADR-0002 used for item #4 in F081). No SDD `ProjectReference` is added; the existing BCL-only, zero-new-package posture of `FS.GG.Governance.Adapters.SddHandoff` is preserved.
- **The three host MVU loops change only at the candidate-assembly seam.** Declared paths are merged into the candidate set fed to `Routing.route` before `Route.select`; the F081 post-select gate-union fold for the handoff's own gates is preserved. The no-handoff path remains an identity transform (byte-identical).
- **Tier 1 classification.** Exposing declared paths as candidates adds public surface to the consumer adapter (and possibly the host candidate-assembly), so this is a Tier 1 change requiring additive `.fsi` / baseline updates, per the constitution.
- **This is added to the Coordination board as a P3 Governance follow-up** (the board's Governance lane was otherwise Done); cross-repo coordination is not required since the contract is unchanged.

## Out of Scope

- Any change to the handoff document **shape**, contract version, or the SDD producer side.
- A new selecting-path provenance/source discriminator (deferred to a follow-up unless clarified otherwise).
- `governedReferences`-driven changes to the handoff's own evidence / readiness / integrity gate pre-selection (unchanged from F081).
- Re-opening item #4's gate-vs-merge-fence decision; readiness remains a first-class gate-registry entry.
