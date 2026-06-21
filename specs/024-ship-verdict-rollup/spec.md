# Feature Specification: Ship Verdict Rollup (Pure Core)

**Feature Branch**: `024-ship-verdict-rollup`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the next
unstarted Phase-2 row in `docs/initial-implementation-plan.md:394`: "Add `fsgg ship --mode gate
--profile standard --json` (the ship/merge verdict host edge — `audit.json`, blockers, profile-adjusted
enforcement, exit-code basis)." Sliced — consistent with every prior Phase-2 row and with how the
`route` row split into a pure core (F019 selection) → a deterministic projection (F020 `route.json`) → a
host command (F022 `fsgg route`) — to the **pure ship-verdict rollup core** alone: the total,
deterministic function that takes an already-routed change plus a chosen run mode and profile and rolls
the per-finding **effective severity** (F023) up into one whole-change **ship decision** (verdict,
blockers, warnings, exit-code basis). It defers the `audit.json` projection (the next row, the
`route.json`/`gates.json` sibling) and the `fsgg ship` host command (the row after that, the `fsgg route`
sibling) — because each prior row landed the pure decision first and let the JSON projection and host
edge consume it unchanged.

## Overview

F023 landed the first Phase-5 pure core: `deriveEffectiveSeverity`, which decides **one** finding's
effective severity (and a reason) from its base severity, rule maturity, the run mode, and the profile.
But nothing yet applies that decision across a **whole change**. A routed change (F019 `RouteResult`)
carries the gates it selected — each an F018 `Gate` with a declared `Maturity` — and the F017
unknown-governed-path findings it surfaced, but no value yet says *whether this change may ship*: which
selected gates and findings actually block under the active mode and profile, which are merely warnings,
and what exit-code category the whole result implies.

This feature is that pure decision: the **ship verdict rollup**. Given a `RouteResult`, a run **mode**,
and a **profile**, it derives — for every selected gate and every finding — the F023 effective severity
and reason, then rolls those per-item decisions up into one closed **ship verdict** (`pass` when nothing
effective-blocks, `fail` when at least one item does), the deterministic **blockers** list (the
effective-blocking items), the **warnings** list (items that are base-blocking but relaxed to advisory by
the current mode/maturity/profile — i.e. would block at a stricter boundary), and a closed **exit-code
basis** (clean vs blocked) that a later host edge translates into a process exit code.

It is a **pure, total, side-effect-free value-to-value computation** — no I/O, no clock, no
`policy.yml` parsing, no JSON. It composes already-typed, already-tested values (F023 derivation, F019
route result, F017 findings, F014 facts) and **re-derives, re-sorts, and re-classifies nothing** those
cores already fixed. It honours the design's hard rule that a profile **must never hide the underlying
verdict** (`docs/initial-design.md:575`, `:806`): every rolled-up item carries its base severity,
effective severity, mode, profile, maturity, and reason, so a relaxed blocker is always visible as a
warning that explains itself, never silently dropped.

It computes **no** `audit.json` document (the next row's deterministic projection, the sibling of F020/
F021), runs **no** command and writes **no** file (the `fsgg ship` host edge, the sibling of F022),
evaluates **no** cache eligibility or freshness (Phase 11), and applies **no** project-authored
per-class `policy.yml` profile dials (the deferred Config layer F023 already held out of scope). The four
canonical profiles carry their strictness intrinsically, exactly as F023 established.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Roll a routed change up into a ship verdict (Priority: P1)

A caller (the future `fsgg ship` command, or a test) has a routed change — the selected gates, the
findings, the cost — and a chosen run mode and profile. They pass these to the rollup and get back a
single ship decision: a `pass`/`fail` verdict, the list of items that block, the list of items relaxed
to warnings, and the exit-code basis — with every item's base and effective severity, mode, profile,
maturity, and reason attached.

**Why this priority**: This is the MVP and the whole point of the row — the first time the Phase-5
per-finding derivation (F023) becomes a *whole-change* answer. Without it the route result is inert under
enforcement; with it the later `audit.json` and `fsgg ship` rows have exactly one pure decision to
project and exit on, re-deriving nothing.

**Independent Test**: Build a `RouteResult` with a mix of selected gates (varied maturities) and findings,
pick a mode and profile, run the rollup, and confirm the verdict is `fail` exactly when at least one item
derives an effective-blocking severity, the blockers list is exactly those items, the warnings list is
exactly the base-blocking-but-relaxed items, and every item carries its full lever set and reason.

**Acceptance Scenarios**:

1. **Given** a routed change whose selected gates and findings all derive effective `advisory` under the
   chosen mode and profile, **When** the rollup runs, **Then** the verdict is `pass`, the blockers list is
   empty, and the exit-code basis is the clean (zero) category.
2. **Given** a routed change with at least one selected gate or finding that derives effective `blocking`
   under the chosen mode and profile, **When** the rollup runs, **Then** the verdict is `fail`, the
   blockers list is exactly the effective-blocking items, and the exit-code basis is the blocked (non-zero)
   category.
3. **Given** a routed change with an item that is base-`blocking` but relaxed to effective `advisory` by
   the active mode/maturity/profile, **When** the rollup runs, **Then** that item appears in the warnings
   list (not the blockers list), carrying its base severity, effective severity, mode, profile, maturity,
   and the F023 reason explaining why it did not block.

---

### User Story 2 - Re-routing and worked example: the design's truth table holds at change scale (Priority: P1)

Whoever defines the project's profiles needs to trust that the whole-change verdict matches the design's
documented mode/profile/maturity semantics — the same worked example F023 reproduced for a single
finding must roll up correctly when that finding is one item among several in a change.

**Why this priority**: The design requires that "every combination that can alter enforcement must have a
golden fixture" (`docs/initial-design.md:578`) and that the rollup never hides a verdict. Pinning the
worked example at change scale is what makes the verdict trustworthy as the basis for a merge gate.

**Independent Test**: Construct the design's worked example (`docs/initial-design.md:516` — a
`block-on-ship` item, base `blocking`, run mode `inner`, profile `light` ⇒ effective `advisory`) as one
item in a multi-item change and confirm it lands in warnings with the documented reason while a sibling
`block-on-release` item under `--mode gate` lands in blockers, and the overall verdict reflects the
blocker.

**Acceptance Scenarios**:

1. **Given** the design's worked-example item embedded in a change, **When** the rollup runs at that
   item's mode/profile, **Then** the item is a warning with effective `advisory` and the documented
   reason, and contributes no blocker.
2. **Given** the same change re-evaluated at a stricter profile or a later run mode that moves that item
   above its maturity floor, **When** the rollup runs, **Then** the item moves into the blockers list and
   the verdict flips to `fail` — and nothing about the change's inputs (base severities, maturities,
   findings) was altered, only the levers.
3. **Given** any item, **When** the rollup runs, **Then** its base severity in the output equals its base
   severity in the input byte-for-byte — the profile changed effective enforcement only, never the
   underlying verdict basis.

---

### User Story 3 - Deterministic, total, never-throwing decision for downstream projection (Priority: P2)

The future `audit.json` projection and `fsgg ship` exit decision consume this value programmatically. For
identical inputs it must produce a byte-identical decision, be defined for every routed change (including
the empty one), and never throw — so the artifacts and exit codes built on it are themselves stable and
safe.

**Why this priority**: Determinism and totality are what let the verdict be golden-snapshotted and let the
host edge map it to a process exit without defensive guards. It is P2 because US1/US2 already deliver the
decision; this hardens it as a contract.

**Independent Test**: Run the rollup twice over the same `RouteResult`/mode/profile and confirm the
decisions are structurally identical; run it over an empty route result and over the full enumerated
cross-product of lever combinations and confirm it never throws and always yields a well-formed decision.

**Acceptance Scenarios**:

1. **Given** a fixed `RouteResult`, mode, and profile, **When** the rollup runs twice, **Then** the two
   ship decisions are identical (same verdict, same blockers, same warnings, same exit-code basis, same
   ordering).
2. **Given** an empty `RouteResult` (no selected gates, no findings), **When** the rollup runs at any mode
   and profile, **Then** it succeeds with verdict `pass`, empty blockers and warnings, and the clean
   exit-code basis — never an error and never a fabricated blocker.
3. **Given** any combination of selected-gate maturities, finding zones, mode, and profile, **When** the
   rollup runs, **Then** it returns a decision rather than throwing, and the blockers and warnings lists
   are deterministically ordered (a stable per-item order, not input-arrival order).

---

### Edge Cases

- **Empty change**: a `RouteResult` with no selected gates and no findings rolls up to `pass`, empty
  blockers/warnings, clean exit-code basis — the explicit "nothing to enforce" outcome, never an error.
- **All-advisory change**: every item derives effective `advisory` ⇒ `pass`; items that are base-blocking
  but relaxed still appear as warnings so the relaxation is visible, never hidden.
- **Mixed maturities sharing a gate id**: F019 already union-deduped selected gates by `GateId`; the
  rollup evaluates each distinct selected gate once and never double-counts a shared gate in blockers,
  warnings, or any rollup tally.
- **Finding with no associated gate**: an unknown-governed-path / protected-boundary finding is enforced
  on its own base severity and a documented maturity-equivalent, independent of any gate — a protected
  boundary's escalated finding can block even when the change selected no gate.
- **Profile relaxes a base-blocker below its floor**: the item is a warning carrying the F023 reason; the
  verdict does not fail on it — but its base `blocking` severity remains visible in the output (the design
  forbids hiding it).
- **Base-advisory item under the strictest profile**: stays advisory (this core never escalates a
  base-advisory item — escalation is the deferred per-class `policy.yml` dial layer, per F023's D4); it is
  neither a blocker nor a warning, just a passing item.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide one total function that takes a routed change (the F019
  `RouteResult`), a run **mode**, and a **profile**, and returns one **ship decision** value.
- **FR-002**: The decision MUST carry a closed **verdict** that is `fail` when at least one selected gate
  or finding derives an effective-`blocking` severity under the given mode and profile, and `pass`
  otherwise.
- **FR-003**: The system MUST derive each selected gate's and each finding's effective severity and reason
  by applying the existing F023 `deriveEffectiveSeverity` to that item's base severity, its maturity, the
  given mode, and the given profile — re-implementing none of that derivation.
- **FR-004**: The decision MUST expose a deterministic **blockers** list containing exactly the items whose
  effective severity is `blocking`, and a deterministic **warnings** list containing exactly the items that
  are base-`blocking` but relaxed to effective `advisory` by the active mode/maturity/profile.
- **FR-005**: Every item carried in the decision (blocker, warning, or passing) MUST carry its base
  severity, effective severity, run mode, profile, maturity, and the F023 reason — the design's rule that a
  profile MUST NOT hide the underlying verdict, alter inputs, or remove items.
- **FR-006**: The decision MUST echo each item's **base severity** unchanged from the input — the rollup
  alters effective enforcement only, never the underlying base-severity basis.
- **FR-007**: The decision MUST expose a closed **exit-code basis** category — a clean (passing) basis when
  the verdict is `pass` and a blocked basis when the verdict is `fail` — as a typed value a later host edge
  maps to a numeric process exit; this core MUST NOT itself set a process exit code or exit.
- **FR-008**: The function MUST be **total**: defined for every combination of selected-gate maturities,
  finding zones, run mode, and profile, including the empty `RouteResult`, and MUST NOT throw for any input.
- **FR-009**: The function MUST be **deterministic**: identical inputs yield a structurally identical
  decision, with no influence from wall-clock, environment, host path, or input-arrival ordering; the
  blockers and warnings lists MUST be ordered by a stable, documented per-item key.
- **FR-010**: The function MUST evaluate each distinct selected gate exactly once (honouring F019's
  union-dedup by `GateId`) and MUST NOT drop any selected gate or finding from consideration — every input
  item is accounted for as a blocker, a warning, or a passing item.
- **FR-011**: This core MUST NOT escalate a base-`advisory` item to `blocking` — a base-`advisory` item is
  always a passing item — consistent with F023's no-escalation rule; per-class escalation remains the
  deferred `policy.yml` dial layer.
- **FR-012**: This core MUST NOT render any `audit.json` (or other) document, perform any I/O, run any
  command, read or parse `policy.yml`, or evaluate cache eligibility / freshness — those are the later
  projection row, the `fsgg ship` host row, and Phase 11 respectively.
- **FR-013**: The mapping of each selected gate's and each finding's typed facts to an F023 enforcement
  input — specifically how a **base severity** and a **maturity** are obtained for a gate (which carries a
  declared `Maturity` but no explicit base severity) and for a finding (which carries a zone) — MUST be
  derived deterministically from the facts those cores already fixed, with no new fact source introduced.

### Key Entities *(include if data involved)*

- **Ship decision**: the whole-change outcome of one rollup — the closed verdict (`pass`/`fail`), the
  blockers list, the warnings list, the full per-item enforcement detail, and the exit-code basis.
- **Enforced item**: one selected gate or one finding after enforcement — its identity (gate id or finding
  id/path), its base severity, effective severity, run mode, profile, maturity, and F023 reason.
- **Run mode / profile**: the two F023 typed levers chosen for this rollup (the six-value run mode and
  four-value profile), reused verbatim — not redefined.
- **Exit-code basis**: a closed category (clean vs blocked) that a later host edge maps to a numeric
  process exit; deliberately a *basis*, not a number, in this pure core.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any routed change, the rollup's verdict is `fail` exactly when at least one selected gate
  or finding derives an effective-`blocking` severity under the given mode and profile, and `pass`
  otherwise — verified against the per-item F023 derivations of the same inputs.
- **SC-002**: The design's worked example (`docs/initial-design.md:516`), embedded as one item in a
  multi-item change, lands in the warnings list with effective `advisory` and the documented reason, while
  a sibling item above its maturity floor lands in blockers and drives a `fail` verdict.
- **SC-003**: Every item in the decision carries its base severity unchanged from input, plus its effective
  severity, mode, profile, maturity, and reason — no item is dropped and no underlying base severity is
  altered or hidden (auditable against the input route result and the design's no-hide rule).
- **SC-004**: Running the rollup twice over identical inputs produces a structurally identical decision
  (same verdict, blockers, warnings, exit-code basis, and ordering).
- **SC-005**: The rollup is total over the full enumerated cross-product of selected-gate maturities,
  finding zones, run modes, and profiles (and the empty route result) and never throws.
- **SC-006**: Mapping the rollup over the items of a change is 1:1 — N selected gates plus M findings yield
  exactly N+M accounted-for enforced items partitioned across blockers, warnings, and passing items, with
  each distinct selected gate evaluated once.
- **SC-007**: The decision contains none of: an `audit.json` (or other) serialized document, a process
  exit code (only a typed exit-code *basis*), a cache-eligibility verdict, a freshness evaluation, a
  `policy.yml`-derived per-class dial, a wall-clock value, a machine-absolute path, or any
  environment-derived value.

## Assumptions

- **Slice boundary**: This row delivers the **pure ship-verdict rollup core** only. The deterministic
  `audit.json` projection (the F020/F021 sibling) and the `fsgg ship --mode gate --profile standard
  --json` host command (the F022 sibling, which translates the exit-code basis into a process exit and
  persists the artifact) are explicitly **out of scope** and remain the next two Phase-2 rows — exactly as
  the `route` row split pure selection (F019) from `route.json` (F020) from `fsgg route` (F022).
- **Reuse, don't re-derive**: The core composes F023 `deriveEffectiveSeverity`, the F019 `RouteResult`
  (selected gates with their `Gate.Maturity`/`GateId`/`Cost`), the F017 findings, and the F014 typed facts
  verbatim. It adds the whole-change rollup, not new severity/derivation logic.
- **Base-severity / maturity source (plan-time reconciliation)**: A `Gate` carries a declared `Maturity`
  but no explicit base severity, and a finding carries a zone. The mapping that yields an F023 enforcement
  input for each (how a base severity and a maturity are obtained) is derived deterministically from those
  carried facts — e.g. a gate's `block-on-*` maturity implying a base-`blocking` item and `observe`/`warn`
  implying base-`advisory`, and a finding's zone (governed-root vs escalated protected-boundary) implying
  its base severity and maturity-equivalent. The exact mapping is a plan-time reconciliation, consistent
  with how F018 used an `Environment=Release` heuristic for `productCheck`; an independent per-rule base
  severity is the deferred `policy.yml` dial layer (F023 FR-015).
- **Profiles carry strictness intrinsically**: The four canonical profiles' strictness comes from F023's
  established maturity-floor + profile-tighten model; project-authored per-class `policy.yml` dials remain
  deferred (FR-012). Within this core `light` and `standard` may differ only in reason text, as F023
  disclosed.
- **Exit-code basis, not exit code**: This pure core yields a typed exit-code *basis* (clean vs blocked).
  Mapping it to an actual numeric process exit — and to the distinct usage/input-error categories F022
  already defined — is the `fsgg ship` host edge's job, not this core's.
- **Boundary discipline**: Because the feature is a pure, total, side-effect-free value-to-value
  computation with no multi-step state and no I/O, it is a **pure leaf** like F015/F017/F018/F019/F021/F023
  — not an Elmish/MVU edge. Constitution Principle IV's MVU obligation triggers only once behavior includes
  stateful workflow or I/O, which this row deliberately excludes.
- **No cache / freshness evaluation**: Each selected gate's freshness-key inputs are carried (inside the
  F018 `Gate`) but never evaluated here — that remains Phase 11.
- **Project home / surface**: Whether this lands as a new packable pure-leaf project (the F023 shape,
  likely referencing `Enforcement` + `Route` + `Findings` + `Config`) or extends an existing one, and the
  exact module/function spelling, are plan-time reconciliations — consistent with how prior rows deferred
  project home to plan.
