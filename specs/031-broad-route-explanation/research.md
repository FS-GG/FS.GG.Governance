# Phase 0 Research: Broad-Route Cost Explanation Core (F031)

This row resolves the spec's deferred home/representation choices and pins the explanation semantics. There
were **no open `NEEDS CLARIFICATION` markers** in the spec; the items below are the planning decisions the
spec flagged for `/speckit-plan` (module home, threshold fixed-vs-parameter, alternative tie-break, input
shape) plus the supporting facts that make the projection pure and total.

## Decisions

### D1 — New pure core `FS.GG.Governance.RouteExplain`, referencing Route + Gates

- **Decision**: Add a new packable library `src/FS.GG.Governance.RouteExplain` with a `Model` file and a
  same-named operations file, referencing **`FS.GG.Governance.Route`** and **`FS.GG.Governance.Gates`**. The
  F014 typed facts (`Cost`, `EnvironmentClass`, `DomainId`, `GateId`, `CheckId`) arrive **transitively
  through Route/Gates**. It references no Config project directly, no Snapshot/host/edge assembly, and
  **not** FreshnessKey/EvidenceReuse.
- **Rationale**: Mirrors the F015–F030 pure-core-first rhythm (a new minimal core per Phase row). Cost
  explanation joins the route (F019) and the catalog (F018); those are exactly the two cores referenced. The
  freshness/cache cores (F029/F030) are a *sibling* Phase-11 line, not a dependency of cost explanation — so
  they are deliberately not referenced, keeping the dependency graph one-way: `RouteExplain → {Route, Gates}
  → {Routing, Findings} → Config`.
- **Alternatives considered**: (a) *Extend F019 `Route`* — rejected: it would enlarge a merged core's surface
  and baseline (a Tier-1 edit to existing API), breaking the additive pattern and the "merged cores
  untouched" guarantee. (b) *Extend F020 `RouteJson`* — rejected: that core renders bytes; explanation is a
  typed value that should exist before any rendering row consumes it. (c) *Reference only Route* (rely on
  `SelectedGate.Gate` for catalog data) — rejected: a *cheaper local alternative* may be a gate the route did
  **not** select, so the full `GateRegistry` is a genuine input (D4).

### D2 — Reuse F019/F018 verbatim; the finding embeds `SelectedGate`

- **Decision**: `HighCostFinding = { Selected: SelectedGate; Alternative: AlternativeOutcome }` embeds the
  F019 `SelectedGate` (the `Gate` + its `SelectingPaths`) **whole**. The design's six fields are read off it:
  *selected gate* `Selected.Gate.Id`, *cost* `Selected.Gate.Cost`, *affected capability* `Selected.Gate.Domain`,
  and each *changed path* / *matched rule* pair `Selected.SelectingPaths.[i].Path` / `.MatchedGlob`. The only
  new datum is the resolved `Alternative`.
- **Rationale**: F019 already typed and deterministically ordered the route trace; re-projecting its parts
  would duplicate (and risk drifting from) that contract. Embedding `SelectedGate` makes "consume F019
  verbatim" literal (FR-005, FR-009) and keeps the new surface tiny.
- **Alternatives considered**: A flat record copying `GateId`/`DomainId`/`Cost`/paths out of `SelectedGate` —
  rejected as redundant re-derivation that invites drift and grows the surface for no behavioral gain.

### D3 — High-cost threshold fixed at `High`, via the closed `Cost` ordering

- **Decision**: `highCostThreshold : Cost = High`; a selected gate is high-cost iff `gate.Cost >= High`
  (i.e. `High` or `Exhaustive`). The comparison uses `Cost`'s **built-in structural ordering**.
- **Rationale**: F014 declares `type Cost = Cheap | Medium | High | Exhaustive`, a plain DU whose
  **declaration order is its F# structural `IComparable` order** (`Cheap < Medium < High < Exhaustive`, as the
  Config `.fsi` doc states). So `>= High` is total, needs no rank table, and matches the design's "high-cost"
  language exactly. Exposing `highCostThreshold` as a value documents the cutoff and lets tests assert it
  without hard-coding `High`.
- **Alternatives considered**: (a) *Threshold as an `explain` parameter* — deferred (Spec Assumptions): no
  budget/weight is declared anywhere yet, so a parameter would be untested ceremony; a later row MAY add it.
  (b) *A numeric weight per tier* — rejected: F019's `CostRollup` is deliberately a **multiset** because F014
  declares no numeric weights; inventing them here would fabricate magnitudes (FR-010). (c) *Threshold at
  `Exhaustive`* — rejected: `High` is itself a broad/expensive tier the design wants explained.

### D4 — Cheaper local alternative = same domain ∧ strictly cheaper ∧ locally runnable; tie-break cheapest then GateId

- **Decision**: For a high-cost finding gate `h`, a **candidate** is a registry gate `g` such that
  `g.Domain = h.Domain` **and** `g.Cost < h.Cost` (strict, same closed ordering) **and** `g`'s declared
  environment **permits local execution** (D6). Among candidates, the chosen alternative is the one with the
  **lowest `Cost`**, breaking ties by **`GateId` ordinal** (ascending) — `CheaperLocalAlternative g` of that
  head. If there are no candidates, `NoCheaperLocalAlternative`.
- **Rationale**: Uses only already-declared facts (no new schema field): a cheaper *local* alternative is a
  same-capability gate that gives feedback before the expensive boundary gate. "Strictly cheaper" excludes
  the high-cost gate itself and equal-cost peers (an equal-cost gate is no saving). The cheapest-then-`GateId`
  tie-break offers the biggest saving first and is fully deterministic (FR-007). The gate's environment is
  read from `g.FreshnessKey.Environment` (D6).
- **Alternatives considered**: (a) *Restrict candidates to the route's selected gates* — rejected: the most
  useful cheaper alternative is often an unselected catalog gate, so the full registry is the candidate pool.
  (b) *Allow equal-cost alternatives* — rejected: not a cost saving (FR-006 "strictly lower"). (c) *Tie-break
  by `GateId` only* — rejected: offering the *cheapest* qualifying gate is the more useful default; `GateId`
  only breaks remaining ties.

### D5 — Two-argument `explain`; findings ordered by `GateId`

- **Decision**: `explain : RouteResult -> GateRegistry -> RouteExplanation`. It filters `route.SelectedGates`
  to those with `Cost >= High`, resolves each gate's `Alternative` against `registry.Gates`, and emits the
  resulting `HighCostFinding`s **sorted by `Selected.Gate.Id` ordinal**.
- **Rationale**: The route and the catalog are the two values being joined, so two arguments are the plainest
  signature (matching F019 `select`'s multi-input shape). Re-sorting by `GateId` ordinal makes the output
  **order-independent** of the input selected-gate order, the registry-gate order, and selecting-path order
  (FR-008, SC-005) — even for hand-built `RouteResult` values that are not already F019-sorted.
- **Alternatives considered**: A single combined input record — rejected as unnecessary wrapping; the two
  cores' values are already the natural arguments. Preserving F019's incoming order without re-sorting —
  rejected: a hand-built or future re-ordered route would then change the output, violating FR-008.

### D6 — "Local" is exactly `Local` / `LocalOrCi`

- **Decision**: A gate "permits local execution" iff its declared `EnvironmentClass` is `Local` or
  `LocalOrCi`. `Ci` and `Release` gates do **not** qualify as cheaper *local* alternatives. A gate's declared
  environment is `gate.FreshnessKey.Environment`.
- **Rationale**: F014's `EnvironmentClass = Local | Ci | LocalOrCi | Release` is a closed class; the two
  local-bearing cases are exactly `Local` and `LocalOrCi`. This is a classification over an existing closed
  class — no heuristic, no new field. F018's `Gate` carries the `EnvironmentClass` inside its `FreshnessKey`
  record (there is no separate top-level `Environment` field), so the source of truth is
  `gate.FreshnessKey.Environment`.
- **Alternatives considered**: Treating `Release` as "local because runnable anywhere" — rejected: a release
  gate is a boundary gate, the opposite of a cheap local pre-check.

## Supporting facts (purity & totality)

- **`Cost` and `EnvironmentClass` are closed, comparable DUs.** `Cost`'s structural order is its declaration
  order, so `>=`/`<` are total and need no helper. `EnvironmentClass` is matched exhaustively for the
  local-permission test. No partial function, no exception path.
- **F019 `SelectedGate` and F018 `Gate` are pure values.** `SelectedGate = { Gate; SelectingPaths }`; `Gate`
  carries `Id: GateId`, `Domain: DomainId`, `Cost: Cost`, and `FreshnessKey: { …; Environment; … }`. The
  explanation reads these fields only; it re-routes nothing, re-selects nothing, re-builds no registry.
- **Determinism is by construction.** `List.filter` + `List.sortBy` over `GateId`/`Cost` ordinals are pure
  and order-independent; identical inputs yield byte-identical findings. No clock, filesystem, git,
  environment, or network is read (SC-006).
- **Empty is a value, not an error.** An empty `RouteResult.SelectedGates`, a route with no high-cost gate,
  or a registry with no qualifying candidate all yield ordinary values (`{ Findings = [] }` or
  `NoCheaperLocalAlternative`), never an exception (FR-011, SC-004).
- **Real-evidence test path exists.** The F019/F020 `Support.fs` already builds real `RouteResult`/
  `GateRegistry` values from a real `TypedFacts` via `Gates.buildRegistry` + `Routing.route` +
  `Findings.findUnknownGovernedPaths` + `Route.select` (`facts`/`registryOf`/`resultOf`). F031's `Support.fs`
  reuses that shape, so every test input is a genuine typed value (Principle V); hand-built `RouteResult`/
  `GateRegistry` values cover the disordered/duplicate cases the chain would not naturally produce.
</content>
