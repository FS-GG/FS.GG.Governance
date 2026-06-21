# Feature Specification: Broad-Route Cost Explanation Core

**Feature Branch**: `031-broad-route-explanation`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "next item in plan" — resolved against `docs/initial-implementation-plan.md`.
**Phase 11: Cost, Cache, and Provenance** has landed its first two rows as pure cores: F029
(`FS.GG.Governance.FreshnessKey`) — *"Define freshness keys over rule hash, artifact hash, command version,
generator version, base/head, environment class, and output digest"* — and F030
(`FS.GG.Governance.EvidenceReuse`) — *"Cache reusable evidence only when all freshness inputs match."* The
**next** unchecked Phase-11 line is *"Explain high-cost routes with matched rule, changed path, affected
capability, selected gate, cost, and cheaper local alternative"* (the design's *"Explain broad routes"* row).
Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F030 each landed a pure, total,
deterministic core before any host edge consumed it), this row is sliced to that single projection: the
typed **broad-route explanation vocabulary** and the total, deterministic function that, given an already-
computed route and the gate catalog, names every high-cost gate the route selected together with **why** it
was selected (matched rule, changed path, affected capability, selected gate, cost) and **the cheaper local
alternative** — if one exists. It performs **no persistence** (no filesystem/database read or write), reads
**no clock / filesystem / git / network**, runs **no gate**, computes **no ship verdict / severity /
enforcement / freshness verdict**, persists **no artifact**, and adds **no CLI**.

## Overview

Governance routes a change to the gates that govern it (F019 `Route.select`), rolling up the route's cost as
a multiset of the closed `Cost` tiers (`Cheap < Medium < High < Exhaustive`). When a change pulls in an
**expensive** gate — a CI- or release-boundary build, a full generated-product verify, a visual capture —
the developer deserves to know **why** that gate is on the route and **whether there is a cheaper way to get
local feedback first**, rather than silently paying the high cost or being told only "this route is
expensive." The design states this directly: *"Explain broad routes — include matched rule, changed path,
affected capability, selected gate, cost, and cheaper local alternative,"* and its exit criterion: *"Route
reports explain cost and cheaper local alternatives."*

This feature answers that question deterministically: *"For this route, which selected gates are high-cost,
why is each on the route, and is there a cheaper gate in the same capability that I could run locally first?"*

The explanation must be **deterministic, total, and auditable**: the same route against the same catalog
always yields the same explanation, every high-cost selected gate is accounted for, and the "cheaper local
alternative" is either a concrete named gate or an explicit "none" — never an unstated omission. The
explanation is **honest** in the design's sense (generated views must identify the source and what drove the
result): each high-cost finding carries the route trace that selected it, expressed verbatim in the F019
vocabulary, so "why is this expensive gate on my route?" is answerable without re-deriving anything outside
the core.

This row delivers that as a pure core that reuses F019 and F018 verbatim:

- **Model a broad-route explanation as a pure value** — a *high-cost route finding* names one selected gate
  whose cost is at or above the high-cost threshold, the route trace that selected it (the changed paths and
  matching globs — F019 `SelectingPath`), the affected capability domain, the gate's declared cost, and the
  **cheaper local alternative** (a concrete gate, or an explicit "none"). A *route explanation* is the closed,
  deterministically-ordered collection of such findings for a route.
- **Derive the explanation with a single total function** — given an F019 `RouteResult` and the F018
  `GateRegistry`, return the route explanation: one finding per selected gate at or above the high-cost
  threshold, each carrying its F019 route trace verbatim and its resolved cheaper-local-alternative.
- **Resolve the cheaper local alternative purely** — among the catalog's gates for the **same affected
  capability domain**, the cheaper local alternative is a gate whose declared cost is **strictly lower** and
  whose declared environment **permits local execution**; when several qualify it is chosen deterministically,
  and when none qualifies the finding states "no cheaper local alternative" explicitly.

The core is **pure over supplied data**, exactly like F019/F020/F029/F030: the route and the catalog are
values handed in; nothing is sensed, run, persisted, or measured. The **actual cost weights / numeric
budgets** (F019's `CostRollup` is deliberately a multiset, not a weighted scalar — no numeric weights are
declared anywhere yet), the **persistence** of the explanation (rendering it into route.json or any
artifact), command-run records, provenance/attestation, and any **CLI** are **later Phase-11 rows or a host
edge** and remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Explain every high-cost gate a route selected, with its route trace (Priority: P1)

A Governance consumer routed a change and the route pulled in one or more expensive gates. Before paying that
cost, they want a per-gate explanation: which selected gates are high-cost, and for each, the changed path(s)
and matching rule(s) that put it on the route, the affected capability, and the declared cost — so the broad
route is explained, not just totalled.

**Why this priority**: This is the core of the design's *"Explain broad routes"* row and its exit criterion
(*"Route reports explain cost"*). A route that reports only an aggregate cost cannot tell a developer *why* a
specific expensive gate fired or *what change* dragged it in. Naming each high-cost gate with its route trace
is the load-bearing guarantee; it is independently demonstrable and delivers the core value alone.

**Independent Test**: Build a route (F019 `RouteResult`) selecting a mix of gates across cost tiers, with a
gate registry. Ask for the explanation and assert there is exactly one finding per selected gate at or above
the high-cost threshold, that each finding carries that gate's identity, affected capability domain, declared
cost, and the F019 selecting-path trace verbatim, and that no gate **below** the threshold produces a
finding. No host, no I/O, no other feature required.

**Acceptance Scenarios**:

1. **Given** a route that selected a high-cost gate reached by one or more changed paths, **When** the
   explanation is asked, **Then** it contains exactly one finding for that gate, carrying its gate identity,
   its affected capability domain, its declared cost, and the route trace (each changed path and the rule/glob
   it matched on) that selected it — taken verbatim from the F019 route.
2. **Given** a route that selected gates below the high-cost threshold only, **When** the explanation is asked,
   **Then** it contains no findings (an empty explanation is a valid, successful outcome — not an error).
3. **Given** a route that selected several gates spanning low and high cost tiers, **When** the explanation is
   asked, **Then** it contains a finding for each high-cost gate and none for any below-threshold gate, in a
   deterministic order.
4. **Given** a high-cost gate reached by several changed paths, **When** the explanation is asked, **Then** its
   finding carries every selecting path/rule that reached it (the full route trace), not just one.

---

### User Story 2 - Offer the cheaper local alternative, or state there is none (Priority: P1)

For each high-cost gate, the developer wants to know whether a cheaper gate in the same capability could be
run locally first to catch most issues before paying for the expensive boundary gate. The explanation must
either name that cheaper local alternative or state explicitly that none exists.

**Why this priority**: This is the other half of the design's row (*"...and cheaper local alternative"*) and
its exit criterion (*"Route reports explain cost and cheaper local alternatives"*). The whole point of
explaining a broad route is to offer a cheaper path; an explanation that omits the alternative — or silently
leaves it out when none exists — fails the honesty boundary. It is co-P1 with Story 1.

**Independent Test**: Build a catalog with a high-cost gate and a strictly-cheaper, locally-runnable gate in
the **same** capability domain; route to the high-cost gate; assert its finding names that cheaper gate as the
alternative. Then remove the cheaper gate (or make it non-local, or put it in a different domain) and assert
the finding states "no cheaper local alternative" explicitly rather than omitting the field.

**Acceptance Scenarios**:

1. **Given** a high-cost selected gate and a catalog containing a gate in the same affected capability domain
   whose declared cost is strictly lower and whose declared environment permits local execution, **When** the
   explanation is asked, **Then** the high-cost gate's finding names that cheaper gate as its cheaper local
   alternative.
2. **Given** a high-cost selected gate and a catalog containing no same-domain gate that is both strictly
   cheaper and locally runnable, **When** the explanation is asked, **Then** the finding states explicitly that
   there is no cheaper local alternative (a distinct, locatable "none" — never an omitted or ambiguous field).
3. **Given** a high-cost selected gate and several qualifying cheaper-local gates in its domain, **When** the
   explanation is asked, **Then** the finding's cheaper local alternative is chosen deterministically (same
   route + same catalog ⇒ same named alternative).
4. **Given** any high-cost finding, **When** it is inspected, **Then** the cheaper-local-alternative outcome is
   always present (either a named gate or an explicit "none") — no high-cost finding lacks it.

---

### User Story 3 - The explanation is deterministic and pure over supplied data (Priority: P2)

The explanation is consumed by later rows (route.json rendering, CLI) and by auditors, so it must be a pure,
deterministic function of the supplied route and catalog values: identical inputs always yield an identical
explanation, and reordering the route's selected gates, the catalog's gates, or a gate's selecting paths never
changes the explanation.

**Why this priority**: Determinism and order-independence are what let the explanation feed byte-stable
artifacts and reproducible audits (the same guarantee F019/F020/F029/F030 hold). It is essential but builds on
the explanation contract of Stories 1–2, so it is P2.

**Independent Test**: Compute the explanation twice for the same route and catalog and assert equality. Then
permute the order of the route's selected gates, the catalog's gates, and a gate's selecting paths, recompute,
and assert the explanation is unchanged (findings in the same deterministic order, each carrying the same
trace and alternative).

**Acceptance Scenarios**:

1. **Given** any route and catalog, **When** the explanation is computed twice, **Then** the two results are
   identical (determinism).
2. **Given** a route and catalog, **When** the selected gates, the catalog gates, or a high-cost gate's
   selecting paths are reordered or duplicated, **When** the explanation is recomputed, **Then** it is
   unchanged — same findings, same order, same trace, same alternative.
3. **Given** the explanation is computed in different working directories, at different times, and with
   unrelated repository/filesystem state changed between computations, **Then** the result is identical
   (purity — no clock, filesystem, git, environment, or network read).

---

### Edge Cases

- **Empty route (no selected gates).** A valid input; the explanation is empty (no high-cost findings), never
  an error.
- **No high-cost gates selected.** A route of only cheap/medium gates yields an empty explanation — a
  successful "nothing to explain," never a "select everything" fallback.
- **High-cost gate with no cheaper-local alternative.** The finding is still produced, with an explicit "no
  cheaper local alternative" — the high-cost gate is never dropped just because no alternative exists.
- **Cheaper gate exists but is not locally runnable.** A strictly-cheaper same-domain gate whose declared
  environment does **not** permit local execution does **not** qualify as a cheaper *local* alternative; if it
  is the only cheaper candidate, the finding states "none."
- **Cheaper gate exists but in a different capability domain.** Does not qualify (the alternative must cover
  the same affected capability); if it is the only cheaper candidate, the finding states "none."
- **Equal-cost same-domain local gate.** Does **not** qualify — the alternative must be **strictly** cheaper
  (an equal-cost gate is not a cost saving).
- **The high-cost gate is itself locally runnable.** It is still explained as a high-cost finding (the
  threshold is about cost, not environment); whether a cheaper local alternative exists is resolved
  independently.
- **A selected gate is absent from the catalog / catalog has gates not selected.** The explanation is over the
  route's selected high-cost gates; unselected catalog gates are only candidate alternatives. A selected gate
  carries its own F019-embedded cost and identity, so the explanation never depends on re-finding it in the
  catalog (inherited from F019, which embeds the gate verbatim).
- **Order/duplication of selecting paths or catalog gates.** Never changes the explanation (deterministic,
  order-independent — inherited from F019's ordinal-sorted outputs).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a typed **high-cost route finding** that, for one selected gate at or
  above the high-cost threshold, carries: the gate's identity, its affected capability domain, its declared
  cost, the route trace (the changed path(s) and the matching rule/glob each won on) that selected it, and the
  resolved cheaper-local-alternative outcome.
- **FR-002**: The system MUST define a **route explanation** as an immutable value holding the
  deterministically-ordered collection of high-cost route findings for a route. An empty explanation MUST be a
  valid, successful outcome (no high-cost gates), never an error.
- **FR-003**: The system MUST provide a single, pure, total **explain** function that, given an F019
  `RouteResult` and the F018 `GateRegistry`, returns the route explanation. It MUST be defined for every
  well-typed input (no value causes failure or exception).
- **FR-004**: The explanation MUST contain **exactly one finding per selected gate whose declared cost is at or
  above the high-cost threshold**, and **no** finding for any selected gate below the threshold. The high-cost
  threshold MUST be defined over F014's closed, ordered `Cost` class (`Cheap < Medium < High < Exhaustive`);
  the default threshold is `High` (i.e. `High` and `Exhaustive` are high-cost — see Assumptions).
- **FR-005**: Each finding's route trace MUST be taken **verbatim** from the F019 route (the gate's
  `SelectingPath` set — every changed path that reached the gate's domain and the matching glob it won on).
  This feature MUST re-route no globs, re-select no gates, and re-derive no cost — it consumes the already-
  computed F019 route.
- **FR-006**: For each high-cost finding, the system MUST resolve a **cheaper local alternative**: among the
  catalog's gates for the **same affected capability domain**, a gate whose declared cost is **strictly lower**
  than the high-cost gate's and whose declared environment **permits local execution**. The outcome MUST be a
  total value — either a single named alternative gate or an explicit "none" — present on **every** high-cost
  finding (the no-hide rule).
- **FR-007**: When more than one catalog gate qualifies as a cheaper local alternative, the chosen alternative
  MUST be selected **deterministically** (same route + same catalog ⇒ same named alternative).
- **FR-008**: The core MUST be **deterministic and pure over supplied data**: it MUST read no clock, no
  filesystem, no git, no environment, and no network. Identical route + identical catalog always yields the
  identical explanation. Reordering or duplicating the route's selected gates, the catalog's gates, or a
  gate's selecting paths MUST never change the explanation (findings emitted in a deterministic order).
- **FR-009**: The core MUST **consume F019 and F018 verbatim** — `RouteResult`/`SelectedGate`/`SelectingPath`/
  `CostRollup` and `GateRegistry`/`Gate`/`Cost`/`EnvironmentClass`/`DomainId`/`GateId` — without modifying
  F019, F018, or any other merged core. It MUST NOT redefine the route, gate, cost, or environment
  vocabulary. This feature is additive.
- **FR-010**: The core MUST compute **no persistence, no rendering into route.json or any artifact, no numeric
  cost weight or budget, no severity, no enforcement, no freshness verdict, and no ship verdict**; it MUST run
  no gate, sense nothing, persist nothing, and add no CLI surface. Its sole output is the route-explanation
  value.
- **FR-011**: The core MUST handle the degenerate cases as ordinary, total outcomes (not errors): an empty
  route, a route with no high-cost gates, a high-cost gate with no cheaper-local alternative, cheaper-but-
  non-local or cheaper-but-cross-domain candidates, equal-cost candidates, and reordered/duplicated inputs
  (each as described in Edge Cases).
- **FR-012**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1**
  change (see Assumptions). [The concrete module home and name are a planning decision deferred to
  `/speckit-plan`.]
- **FR-013**: The core MUST NOT add a new third-party package dependency; the projection MUST use only
  facilities already available to the merged cores (the shared framework / BCL) plus F019 and F018.

### Key Entities *(include if feature involves data)*

- **High-cost route finding**: For one selected high-cost gate — its identity, affected capability domain,
  declared cost, the F019 route trace (changed paths + matching rules) that selected it, and the resolved
  cheaper-local-alternative outcome.
- **Route explanation**: The immutable, deterministically-ordered collection of high-cost route findings for a
  route — the design's *"explain broad routes"* projection. An empty explanation is a valid, successful "no
  broad route to explain."
- **Cheaper local alternative outcome** (the no-hide result): For a high-cost finding, either a named catalog
  gate (same affected capability, strictly cheaper, locally runnable) or an explicit "none."
- **High-cost threshold**: The point on F014's closed, ordered `Cost` class at or above which a selected gate
  is "high-cost" (default `High`).
- **Route result / selected gate / selecting path / cost rollup** (reused from F019): The already-computed
  route, each selected gate with its route trace, and the rolled-up cost — consumed verbatim, not redefined.
- **Gate registry / gate / cost / environment class / domain id** (reused from F018/F014): The gate catalog
  and a gate's declared cost, environment, and domain — the source of candidate cheaper-local alternatives;
  consumed verbatim, not redefined.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a route selecting gates spanning all cost tiers, the explanation contains a finding for every
  selected gate at or above the high-cost threshold and none below it, in 100% of cases — verified across the
  full closed `Cost` class (`Cheap`, `Medium`, `High`, `Exhaustive`).
- **SC-002**: Every high-cost finding carries its F019 route trace verbatim (every selecting path/rule that
  reached the gate) and its affected capability domain and declared cost, in 100% of cases — no high-cost gate
  is reported without its trace.
- **SC-003**: Every high-cost finding carries a present cheaper-local-alternative outcome in 100% of cases —
  either a named gate or an explicit "none" — and never an omitted or ambiguous field.
- **SC-004**: A named cheaper local alternative is, in 100% of cases, a catalog gate in the **same** affected
  capability domain whose declared cost is **strictly lower** and whose declared environment **permits local
  execution**; a candidate failing any of those three conditions is never named, and when none qualifies the
  outcome is the explicit "none."
- **SC-005**: For any route and catalog, computing the explanation twice yields identical results in 100% of
  cases (determinism); reordering or duplicating the selected gates, the catalog gates, or a gate's selecting
  paths never changes the explanation.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network — demonstrable by explanations
  being identical when computed in different working directories, at different times, and with unrelated
  repository/filesystem state changed between computations.
- **SC-007**: The merged cores (including F019 and F018) and their `surface/*.surface.txt` baselines, and
  `dotnet build` / `dotnet test` over the existing projects, are **unchanged** by this feature except for the
  additive new surface (if any) — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the broad-route explanation projection, over pure route + catalog values.** The persistence /
  rendering of the explanation (into route.json or any artifact), numeric cost weights or budgets, command-run
  records, provenance/attestation, and any CLI are **later Phase-11 rows or a host edge** and are out of scope
  here. This row produces only the deterministic route-explanation value.
- **High-cost threshold defaults to `High`.** F014's `Cost` is a closed, ordered class
  (`Cheap < Medium < High < Exhaustive`) with **no declared numeric weights** (F019 deliberately models cost as
  a multiset, not a weighted scalar, for exactly this reason). The reasonable default for "high-cost" is the
  upper part of that order — `High` and `Exhaustive`. Whether the threshold is fixed or a supplied parameter is
  a small planning detail deferred to `/speckit-plan`; the contract here is that it is defined over the closed
  `Cost` ordering, defaulting to `High`.
- **"Cheaper local alternative" = same domain, strictly cheaper, locally runnable.** Using only already-
  declared facts: a candidate alternative is a catalog gate in the **same affected capability domain**
  (`DomainId`) whose declared `Cost` is **strictly lower** and whose declared `EnvironmentClass` **permits
  local execution** (`Local` or `LocalOrCi`). No new "alternative" field is invented in the schema — the
  alternative is *derived* from the existing catalog facts. The precise tie-break when several qualify (e.g.
  cheapest first, then `GateId` ordinal) is a planning detail deferred to `/speckit-plan`; the contract is only
  that the choice is deterministic and that "none" is explicit.
- **Inputs are the F019 `RouteResult` and the F018 `GateRegistry`.** The route supplies the selected gates,
  their verbatim route traces, costs, and domains; the catalog supplies the candidate cheaper-local
  alternatives (gates the route did not select can still be cheaper local alternatives). Both are values handed
  in; nothing is sensed, re-routed, or re-built. Whether the function takes both as separate arguments or a
  combined input is a planning detail deferred to `/speckit-plan`.
- **The explanation reuses F019/F018 vocabulary verbatim.** Route traces are F019 `SelectingPath` values; gate
  identity/cost/domain/environment are F018/F014 values. This core redefines none of them and modifies no
  merged core.
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. It consumes F019 and F018 (and transitively the F014 typed facts) verbatim and modifies none of
  them. Whether it lands as a new pure-core module or extends an existing one (e.g. `Route`) is the only home
  decision left to `/speckit-plan`; the established rhythm suggests a new minimal core depending on F019/F018.
- **Determinism is the contract, not performance.** A route selects a modest number of gates and the catalog
  holds a modest number of gates; there is no latency or throughput target. Byte-stability of the explanation
  and totality are the guarantees.
- **The explanation and finding representations are planning decisions.** Whether the cheaper-local-alternative
  outcome is modeled as an option, a small closed union, or a record, and whether the explanation is a list or
  a richer structure, is deferred to `/speckit-plan`; the spec constrains only observable behavior (which gates
  are explained, what each finding carries, the alternative rule, determinism), not representation.
</content>
</invoke>
