# Feature Specification: Cost-Cache Host Wiring — `fsgg verify` / `fsgg ship` Budget Filtering, Kinded-Run Recording, and the Two Provenance Sidecars

**Feature Branch**: `064-cost-cache-host-wiring`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "work on the next backlog item." — Backlog resolution: F27 human-projection host
wiring (`063-human-projection-host-wiring`) is complete and committed, closing one of the three deferred
host-wiring passes the roadmap (`docs/initial-implementation-plan.md`) carries. Two deferred passes remain — F25
cost-cache and F26 release — each with its pure cores landed and only host-edge wiring left. The user selected the
**F25 cost-cache host wiring** as the next item: this feature is exactly Phase 8 of
`specs/060-cost-cache-command-provenance/tasks.md`, deferred there as a bounded follow-up.

## Overview

F25 (`060-cost-cache-command-provenance`) landed four pure cores — `CostBudget` (the ordered-`Cost`-ceiling budget,
the per-gate `CacheDecision` that folds the existing cache-eligibility verdict into reuse/recompute/over-budget, and
the advisory cost/cache `Findings`), `CommandKind` (the seven-kind command taxonomy and the provenance
`auditSnapshot` roll-up), and their two deterministic sidecar projections `CostBudgetJson` (`fsgg.cost-budget/v1`)
and `ProvenanceJson` (`fsgg.provenance/v1`) — fully built, packed, and exercised through their public surfaces (85
semantic/property tests; four blessed surface baselines). They are wired into **no command host**: today `fsgg
verify` and `fsgg ship` resolve their gates and execute them without consulting any budget, record no kinded command
runs, and write neither sidecar.

This feature wires those cores into the existing `fsgg verify` and `fsgg ship` hosts **additively**. The budget
consults the per-gate cache decision to filter which must-recompute gates the host actually executes; each executed
run is tagged with its command kind; the budgeted decision plus the cost/cache findings project to
`cost-budget.json`, and the kinded runs plus provenance inputs project to `provenance.json`. The cost/cache findings
are folded into the existing advisory rollup through the existing enforcement machinery — **no** new verdict, no new
exit-code scheme, no enforcement-truth-table change, and **every** existing `route.json` / `audit.json` /
`verify.json` / ship golden stays byte-identical (the two sidecars are brand-new artifacts beside them).

This is a host-edge integration row only: it adds **no** new pure core, **no** new report object, and **no** new
dependency. It consumes the four already-built F25 surfaces at the MVU interpreter edge of two mature host commands,
reusing each host's existing gate-execution, evidence-store, and artifact-write ports.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Expensive recompute is bounded by the (profile, mode) budget at verify and ship (Priority: P1) 🎯 MVP

A maintainer runs `fsgg verify` (and, at the merge boundary, `fsgg ship`) on a change. Some routed gates are cheap
and some are expensive (High/Exhaustive). The run consults the cost budget for the active profile and run mode:
expensive must-recompute gates that exceed the remaining budget are **deferred or skipped with a named reason**
rather than silently run, in-budget must-recompute gates run, and gates whose cached evidence is still fresh are
reused and charge nothing against the budget. A deferred or skipped gate is **never** reported as passed.

**Why this priority**: This is the feature's core value — bounding the most expensive work per run while keeping the
verdict honest. Without it the cores are inert. It is the smallest slice that delivers a behavioral change a
maintainer can observe (some expensive gate no longer runs in a tight budget, and is reported deferred).

**Independent Test**: Run `fsgg verify` standalone over a tree with one in-budget cheap must-recompute gate, one
over-budget expensive must-recompute gate, and one reusable gate; assert the over-budget gate is absent from the
executed runs and recorded as deferred, the in-budget gate runs, the reusable gate reuses, and the deferred gate is
not reported as passed.

**Acceptance Scenarios**:

1. **Given** a routed expensive (High/Exhaustive) must-recompute gate whose cost exceeds the remaining budget for
   the active (profile, run mode), **When** `fsgg verify` runs, **Then** the gate is not executed, is recorded as
   deferred (inner-loop modes) or skipped with a named reason, and is never reported as passed.
2. **Given** a routed must-recompute gate whose cost fits the remaining budget, **When** `fsgg verify` runs, **Then**
   the gate is executed and its result is recorded.
3. **Given** a routed gate whose freshness key matches recorded evidence, **When** `fsgg verify` runs, **Then** the
   gate's evidence is reused and charges nothing against the budget.
4. **Given** the same change at the merge boundary, **When** `fsgg ship` runs in gate mode, **Then** the (profile,
   Gate) budget filters which must-recompute gates execute under the same rules.
5. **Given** an agent-reviewed gate, **When** any budget or cache decision is made, **Then** the agent-reviewed
   check stays advisory and is never promoted to a blocker under any profile or mode.

---

### User Story 2 - Two deterministic provenance sidecars are written without disturbing any existing contract (Priority: P1)

After a verify or ship run, the host writes two new sidecar artifacts beside the existing outputs: `cost-budget.json`
(the budgeted decision plus the cost/cache findings) and `provenance.json` (the kinded command runs plus the
provenance audit snapshot). Both are deterministic — byte-identical on a re-run with unchanged inputs — and every
existing artifact the host already wrote (`route.json`, `audit.json`, `verify.json`, and the ship goldens) is left
**byte-identical** to before.

**Why this priority**: The sidecars are the durable, automatable record of what the budget decided and what actually
ran — the second half of the feature's value. Byte-identity of existing goldens is the non-negotiable safety anchor
that proves the wiring is purely additive. Both must land together with US1 for the row to be coherent.

**Independent Test**: Run `fsgg verify` twice over an unchanged tree; assert `cost-budget.json` (`fsgg.cost-budget/v1`)
and `provenance.json` (`fsgg.provenance/v1`) are written and byte-identical across the two runs, and the existing
`route.json` / `audit.json` / `verify.json` goldens are byte-identical to a pre-wiring baseline.

**Acceptance Scenarios**:

1. **Given** a completed `fsgg verify` run, **When** the host persists its outputs, **Then** `cost-budget.json` and
   `provenance.json` are written with their declared schema versions beside the existing artifacts.
2. **Given** two `fsgg verify` runs over the same inputs, **When** the sidecars are compared, **Then** they are
   byte-identical (stable ordering, normalized paths, no wall-clock / username / environment dependence).
3. **Given** the wiring is active, **When** the existing `route.json` / `audit.json` / `verify.json` / ship goldens
   are compared to their pre-wiring baselines, **Then** they are byte-identical (the sidecars are new artifacts, not
   edits to existing ones).
4. **Given** an empty input set (no expensive gates, no recorded runs), **When** the host persists, **Then** both
   sidecars are still well-formed (empty arrays), and existing goldens stay untouched.
5. **Given** the cost/cache findings (stale / synthetic-taint / cache-invalidated) for the run, **When** the host
   rolls up the verdict, **Then** the findings are folded in through the existing enforcement machinery as advisory
   only — no enforcement-truth-table change and no new exit-code.

---

### User Story 3 - A standalone generated product is cost-budgeted without monorepo access (Priority: P2)

A generated product checked out on its own (no monorepo) runs `fsgg verify`. The budget, cache decision, and
provenance snapshot use only the product's own recorded evidence, command runs, and provenance — no monorepo path is
required. A missing or unreadable evidence store surfaces a clear input diagnostic naming the offending source, with
no fabricated reuse and no fabricated pass.

**Why this priority**: The standalone-governance guarantee (F23/F24) must extend to the cost-budgeted path, or the
feature would silently regress products that run governance on their own. It depends on US1/US2 wiring being in place,
so it follows them.

**Independent Test**: Check out a generated product standalone with its own recorded evidence; run `fsgg verify` and
assert the decisions use only product-local sources; then remove/corrupt the evidence store and assert a clear input
diagnostic (a `NoPriorEvidence`-style cause surfaced through the existing store reader) rather than a crash or a
fabricated reuse.

**Acceptance Scenarios**:

1. **Given** a generated product checked out standalone, **When** `fsgg verify` runs, **Then** the budget/cache
   decision and the provenance snapshot draw only on the product's own evidence, runs, and provenance — no
   monorepo-only path.
2. **Given** a missing or unreadable evidence store, **When** `fsgg verify` runs, **Then** a clear input diagnostic
   names the offending source, distinct from a tool defect, with no swallowed error and no fabricated reuse or pass.

---

### Edge Cases

- **Over-budget at the boundary vs. inner loop**: an over-budget must-recompute gate is recorded **deferred** in
  inner-loop modes (verify) and handled per the boundary's budget at ship — in both cases never silently recomputed,
  never silently reused, never reported as passed.
- **Reordered candidate gates**: reordering the routed gates does not change any budget decision, cache decision,
  finding, or sidecar byte output (order-independent determinism).
- **Empty inputs**: no routed expensive gates and no recorded runs still yields two well-formed (empty-array)
  sidecars and untouched existing goldens.
- **Agent-reviewed gate with a stale cache identity**: re-reviewed on a changed judge/prompt/check-artifact
  identity, but never promoted to a blocking verdict.
- **Duration-only difference between two runs**: two command runs differing only in wall-clock duration share a
  reproducible identity and do not change the provenance snapshot bytes.
- **Mixed cheap/expensive under a tight budget**: every cheap gate that fits runs while expensive gates that exceed
  the budget defer — the budget filters by cost tier, not by gate order.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg verify` MUST consult the cost budget for the active (profile, run mode) and execute **only** the
  must-recompute gates that fit the remaining budget; an over-budget must-recompute gate MUST be deferred or skipped
  with a named reason and MUST NOT be silently recomputed, silently reused, or reported as passed.
- **FR-002**: `fsgg ship` MUST apply the same budget-filtered gate selection at its merge-boundary run mode, using
  the (profile, Gate) budget and the same reuse/recompute/over-budget rules.
- **FR-003**: A gate whose freshness key matches recorded evidence MUST be reused and MUST charge nothing against the
  budget; a gate that must recompute MUST be charged when it runs and named when it cannot.
- **FR-004**: Each executed gate run MUST be recorded with its **command kind** (build / test / pack / template
  instantiation / git diff / package inspection / visual capture), with reproducible identity and sensed-only
  duration (two runs differing only in duration share an identity).
- **FR-005**: `fsgg verify` and `fsgg ship` MUST write two new sidecar artifacts — `cost-budget.json`
  (`fsgg.cost-budget/v1`: the budgeted decision plus cost/cache findings) and `provenance.json`
  (`fsgg.provenance/v1`: the kinded runs plus the provenance audit snapshot) — beside their existing outputs.
- **FR-006**: Both sidecars MUST be deterministic — stable ordering, normalized paths, no wall-clock / username /
  environment dependence — so identical inputs yield byte-identical output, and reordering the candidate gates
  changes nothing.
- **FR-007**: Every existing artifact the hosts already write (`route.json`, `audit.json`, `verify.json`, and the
  ship goldens) MUST stay **byte-identical** for identical repository state — the sidecars are additive, never edits
  to an existing contract.
- **FR-008**: The cost/cache findings (stale / synthetic-taint / cache-invalidated) MUST be folded into the existing
  verdict rollup through the existing enforcement machinery as **advisory** only; this feature MUST NOT re-open the
  enforcement truth table, add a new verdict, or add a new exit-code scheme.
- **FR-009**: Agent-reviewed checks MUST remain advisory under every profile and mode regardless of their cache
  decision, and agent-reviewed evidence MUST reuse only on matching judge/prompt/check-artifact identity; this
  feature MUST NOT promote any agent-reviewed check to a blocker.
- **FR-010**: The budget and cache decision MUST be computed by the existing pure cores over already-sensed inputs;
  the only new I/O — recording command runs, reading the evidence/provenance sources, and writing the two sidecars —
  MUST live at the host interpreter edge through the **existing** gate-execution, store-reader, and artifact-writer
  ports, with no filesystem / process / registry dependency added to any pure core.
- **FR-011**: A generated product MUST be able to run the cost-budgeted path **standalone** (no monorepo access)
  using only its own recorded evidence, command runs, and provenance; no budget or cache decision may require a
  monorepo-only path.
- **FR-012**: A missing / malformed / unreadable input (no recorded evidence, an unreadable store, an absent
  provenance input) MUST be distinguished from a tool defect, naming the offending source, with no swallowed error
  and no fabricated reuse or fabricated pass.

### Key Entities *(reused from F25 — no new entity introduced)*

- **Cost budget** (F25 `CostBudget`): the maximum expensive recompute a run may perform, scoped to a (profile, run
  mode) pair over the existing `Cost` tiers — the bound the run/skip/defer decision is made against.
- **Cache decision** (F25 `CostBudget`): the per-gate decision folding the existing cache-eligibility verdict with
  the budget into reuse (free) / recompute (charged) / over-budget (skipped or deferred), each carrying its cause.
- **Cost/cache finding** (F25 `Findings`): the advisory stale / synthetic-taint / cache-invalidated finding naming
  the gate and cause, enforced through the existing severity machinery, never blocking.
- **Kinded command run** (F25 `CommandKind` over F032 `CommandRecord`): a recorded expensive command of a named kind
  with reproducible, duration-invariant identity.
- **Provenance audit snapshot** (F25 `CommandKind` over F033 `Provenance`): the deterministic roll-up of provenance
  inputs and kinded runs that proves what ran against what inputs in what environment.
- **`cost-budget.json` / `provenance.json` sidecars** (F25 `CostBudgetJson` / `ProvenanceJson`): the two
  deterministic, byte-identical, order-independent additive projections written beside the existing artifacts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Under a tight (profile, mode) budget, every expensive must-recompute gate that exceeds the budget is
  deferred or skipped with a named reason while every cheap in-budget gate runs; under a high (Release) budget every
  gate runs — verified by a real-filesystem budget-integration fixture at both `fsgg verify` and `fsgg ship`.
- **SC-002**: A deferred or skipped gate is **never** reported as passed and is never silently recomputed or silently
  reused — verified by the budget-integration fixtures.
- **SC-003**: `cost-budget.json` and `provenance.json` are written with their declared schema versions and are
  byte-identical on a re-run with unchanged inputs — verified by a re-run determinism fixture.
- **SC-004**: Every existing `route.json` / `audit.json` / `verify.json` / ship golden is byte-identical to its
  pre-wiring baseline after the wiring lands — verified by the existing goldens compared against frozen baselines.
- **SC-005**: A generated product checked out standalone produces a budget/cache decision and provenance snapshot
  from product-local sources only, and a missing/unreadable evidence store yields a clear input diagnostic rather
  than a crash or fabricated reuse — verified by a standalone fixture and a missing-store fixture.
- **SC-006**: No agent-reviewed check changes a blocking verdict under any profile or mode regardless of its cache
  decision — verified across the enforcement path.
- **SC-007**: The full-solution build + test sweep is green with all existing goldens byte-identical and the two new
  sidecars deterministic — verified by the full-suite gate.

## Assumptions

- **Backlog resolution**: per the roadmap currency check in `063`'s spec and `docs/initial-implementation-plan.md`,
  the remaining work after F27 (063) is two deferred host-wiring passes (F25 cost-cache, F26 release); the user
  selected F25 cost-cache as the next item. The spec directory is `064-cost-cache-host-wiring` (sequential).
- **F25 cores are reused verbatim**: `CostBudget`, `CommandKind`, `CostBudgetJson` (`fsgg.cost-budget/v1`), and
  `ProvenanceJson` (`fsgg.provenance/v1`) are built, packed, green, and surface-baselined. This row adds **no** new
  library, report object, verdict, exit-code, JSON schema, or dependency — it only consumes their public surfaces at
  the host edges.
- **Scope is exactly F25 Phase 8**: this feature is the host-edge slice deferred in
  `specs/060-cost-cache-command-provenance/tasks.md` Phase 8 (T040–T045). The pure-core stories (US1–US5 of that
  feature) are already complete; nothing in the four cores changes here.
- **Two hosts, additive only**: only `fsgg verify` (`FS.GG.Governance.VerifyCommand`) and `fsgg ship`
  (`FS.GG.Governance.ShipCommand`) are wired. The budget filter, kinded-run recording, and sidecar writes are layered
  onto each host's existing gate-execution, evidence-store, and artifact-write ports; no new port is introduced.
- **JSON is the only contract; sidecars are additive contracts**: existing persisted `*.json` outputs stay the
  deterministic, byte-identical contract; the two sidecars are new deterministic contracts written beside them, never
  edits to an existing one (FR-007, SC-004).
- **MVU discipline preserved**: the pure budget/cache/audit decisions live in each host's `update`; recording command
  runs, reading sources, and writing sidecars are effects at the interpreter edge through the existing ports — no I/O
  enters any pure core (FR-010), consistent with Constitution IV and the F029/F041/F051 leaf-plus-sensor precedent.
- **Safe failure preserved**: a missing/unreadable evidence store or absent provenance input surfaces a clear input
  signal distinct from a tool defect, with no crash and no fabricated reuse or pass (FR-012), consistent with
  F14–F063.
- **Tier**: **Tier 1 (contracted change).** It adds two new deterministic JSON contracts (`cost-budget.json`,
  `provenance.json`) and changes the two wired hosts' public effect/model surface (re-blessing their surface
  baselines), while leaving every existing JSON golden byte-identical and introducing no new dependency. The full
  chain applies: spec, plan, host `.fsi` updates, re-blessed surface baselines, test evidence, and docs (including
  flipping F25 Phase 8 to complete and updating the plan's "Remaining" note).
