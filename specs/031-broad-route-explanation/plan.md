# Implementation Plan: Broad-Route Cost Explanation Core

**Branch**: `031-broad-route-explanation` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/031-broad-route-explanation/spec.md`

## Summary

Land **Phase 11 (Cost, Cache, and Provenance)** row 3 — *"Explain high-cost routes with matched rule, changed
path, affected capability, selected gate, cost, and cheaper local alternative"* (the design's *"Explain broad
routes"* row, exit criterion *"Route reports explain cost and cheaper local alternatives"*). Continuing this
repo's maintainer-confirmed **pure-core-first** rhythm (F015–F030 each landed a pure, total, deterministic
core before any host edge consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.RouteExplain`**, that answers one operational question deterministically: *"For this
route, which selected gates are high-cost, why is each on the route, and is there a cheaper gate in the same
capability I could run locally first?"*

The core provides:

- **`AlternativeOutcome`** = `CheaperLocalAlternative of Gate | NoCheaperLocalAlternative` — the no-hide
  result (FR-006): either a concrete cheaper-local catalog gate or an explicit "none," always present.
- **`HighCostFinding`** — a closed record `{ Selected: SelectedGate; Alternative: AlternativeOutcome }`
  embedding F019's `SelectedGate` (the high-cost `Gate` + its `SelectingPaths` route trace) **verbatim** so
  identity/domain/cost/trace are carried, never re-derived.
- **`RouteExplanation`** = `{ Findings: HighCostFinding list }` — the deterministically-ordered explanation
  of a route's high-cost gates; an empty `Findings` is a valid, successful "nothing broad to explain."
- **`RouteExplain.highCostThreshold : Cost`** — the fixed MVP threshold (`High`); a gate is high-cost iff its
  declared `Cost` is at or above it (`High` or `Exhaustive`).
- **`RouteExplain.explain : RouteResult -> GateRegistry -> RouteExplanation`** — the pure, total projection:
  one finding per selected gate at/above the threshold, each carrying its F019 trace verbatim and its resolved
  cheaper-local alternative; findings sorted by `GateId` ordinal (order-independent of inputs).

**Plan-time reconciliations (maintainer to confirm):**

- **D1 — New pure core, Route+Gates dependency (Tier 1).** A new packable library
  `src/FS.GG.Governance.RouteExplain`, referencing **`FS.GG.Governance.Route` and `FS.GG.Governance.Gates`**.
  It consumes F019's `RouteResult`/`SelectedGate`/`SelectingPath` and F018's `GateRegistry`/`Gate` verbatim,
  and the F014 `Cost`/`EnvironmentClass`/`DomainId`/`GateId` arrive **transitively through Route/Gates** (it
  names no Config project directly, references no Snapshot/host/edge assembly, and does **not** reference
  FreshnessKey/EvidenceReuse — the freshness/cache cores are a sibling Phase-11 line, not a dependency of cost
  explanation). New `.fsi` + new `surface/*.surface.txt` baseline ⇒ **Tier 1**, but **no new third-party
  `PackageReference`** (Constitution Engineering Constraint: the helper core stays minimal). The dependency
  direction stays one-way: `RouteExplain → {Route, Gates} → … → Config`.
- **D2 — Reuse F019/F018 verbatim; the finding embeds `SelectedGate`.** The design's six fields map directly to
  F019's already-typed `SelectedGate`: *selected gate* = `Selected.Gate.Id`; *cost* = `Selected.Gate.Cost`;
  *affected capability* = `Selected.Gate.Domain`; *changed path* + *matched rule* = each
  `Selected.SelectingPaths.[i].Path` / `.MatchedGlob`. So `HighCostFinding` embeds the F019 `SelectedGate`
  whole rather than re-projecting its parts (FR-005, FR-009) — this core re-routes nothing, re-selects
  nothing, re-derives no cost. The only new datum per finding is the resolved `Alternative` (FR-001).
- **D3 — High-cost threshold is fixed at `High`, via the closed `Cost` ordering.** F014's `Cost` is a plain
  closed DU `Cheap | Medium | High | Exhaustive` whose **declaration order is its F# structural ordering**
  (`Cheap < Medium < High < Exhaustive`), so "at or above `High`" is the total expression `cost >= High`
  (`High` and `Exhaustive`). `highCostThreshold : Cost = High` is exposed for inspection/tests. No numeric
  weight is invented (F019's `CostRollup` deliberately models cost as a multiset for this reason, FR-010); a
  later row MAY parameterize the threshold once budgets are declared (Spec Assumptions).
- **D4 — "Cheaper local alternative" = same domain, strictly cheaper, locally runnable; tie-break cheapest
  then `GateId`.** For a high-cost finding gate `h`, a candidate is a registry gate `g` with
  `g.Domain = h.Domain` **and** `g.Cost < h.Cost` (strict, by the same closed ordering) **and** `g`'s declared
  environment **permits local execution**. A gate's declared environment is `g.FreshnessKey.Environment`
  (F018 carries the `EnvironmentClass` inside the gate's `FreshnessKey`; there is no separate top-level field);
  *permits local* = `Local` or `LocalOrCi` (FR-006). When several candidates qualify, the chosen one is the
  **cheapest** (lowest `Cost`), breaking ties by **`GateId` ordinal** — `CheaperLocalAlternative g` of that
  head; when none qualifies, `NoCheaperLocalAlternative` (FR-007, the explicit "none"). Equal-cost,
  cross-domain, and non-local candidates never qualify (Edge cases).
- **D5 — Two-argument `explain`; findings ordered by `GateId`.** `explain : RouteResult -> GateRegistry ->
  RouteExplanation` takes the route and catalog as the two values it joins. It filters `route.SelectedGates`
  to the high-cost ones, resolves each gate's alternative against `registry.Gates`, and emits the findings
  **sorted by `GateId` ordinal** so re-ordering the input selected gates, the registry gates, or a gate's
  selecting paths never changes the result (FR-008). An empty route, or a route with no high-cost gate, yields
  empty `Findings` — a valid success, never an error or a "select everything" fallback (FR-011).
- **D6 — "Local" is exactly `Local`/`LocalOrCi`.** F014's `EnvironmentClass` is `Local | Ci | LocalOrCi |
  Release`; a gate "permits local execution" iff its environment is `Local` or `LocalOrCi`. `Ci` and `Release`
  gates are not local alternatives even when strictly cheaper and same-domain (Edge cases, SC-004). This is a
  classification over an existing closed class — no new field, no heuristic beyond the two local-bearing cases.

This row **renders no JSON / artifact** (no route.json field, no persistence), computes **no numeric cost
weight or budget**, performs **no severity / enforcement / freshness verdict / ship verdict**, runs **no
gate**, reads **no clock / filesystem / git / environment / network**, and adds **no CLI**. The merged cores
and their `surface/*.txt` baselines are **untouched**; `dotnet build` / `dotnet test` over the existing
projects stays unchanged, and the new project + its test project are purely additive.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`,
`TreatWarningsAsErrors=true` inherited from `Directory.Build.props`). One new `src/` library with a curated
`.fsi`, plus one new test project.

**Primary Dependencies**: **`FS.GG.Governance.Route`** (for `RouteResult`/`SelectedGate`/`SelectingPath`/
`CostRollup`) and **`FS.GG.Governance.Gates`** (for `GateRegistry`/`Gate`/`FreshnessKey`), both reused
verbatim (FR-009). The F014 typed facts (`Cost`/`EnvironmentClass`/`DomainId`/`GateId`/`CheckId`) arrive
**transitively through Route/Gates** and are only ever read as fields via structural equality/ordering — this
core names no Config type's project directly and references no Snapshot/host/edge assembly. **No new
third-party `PackageReference`** (FR-013): the projection is plain `List` filtering/sorting + `FSharp.Core`
only. Test frameworks already on the central feed (`Directory.Packages.props`): **Expecto**,
**Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the route and registry are in-value inputs.
The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established
pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`RouteExplain.explain` /
`highCostThreshold`) over real, literally-constructible `RouteResult` and `GateRegistry` values built through
the genuine F014→F015→F017→F018→F019 chain (the F019/F020 `Support.fs` real-chain precedent — `facts` /
`registryOf` / `resultOf`), plus hand-built `RouteResult`/`GateRegistry` values for disordered/duplicate
inputs (Principle V — no mocks, no private helpers). Concerns: (1) **one finding per high-cost gate, none
below threshold**, across the full `Cost` class (SC-001); (2) **every finding carries its F019 trace +
domain + cost verbatim** (SC-002); (3) **every finding carries a present alternative outcome** — named or
explicit "none" (SC-003); (4) **the named alternative is same-domain ∧ strictly cheaper ∧ locally-runnable**,
and each failing condition forces "none" (SC-004); (5) **determinism + order/dup invariance** of the
explanation (SC-005); (6) **purity** under changed cwd/time/filesystem (SC-006); (7) **surface drift + scope
hygiene** (Principle II, SC-007). Threshold coverage, the alternative rule, determinism, and totality are
FsCheck properties; the rest are example tests.

**Target Platform**: Developer/CI .NET SDK running `dotnet test`. No host, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a route selects a
modest number of gates and a catalog holds a modest number of gates.

**Constraints**: Pure / total / deterministic (FR-003/FR-008): reads no clock, filesystem, git, environment,
or network; identical route + identical registry always yields the identical explanation; reordering or
duplicating the selected gates, the registry gates, or a gate's selecting paths never changes the result. A
finding is produced for exactly the selected gates whose `Cost >= High` (FR-004); the named alternative is
exactly same-domain ∧ strictly-cheaper ∧ locally-runnable (FR-006); every finding carries a present
alternative outcome (FR-006, no-hide). The merged cores and baselines are not modified (FR-009/SC-007).

**Scale/Scope**: One new `src/` library (`RouteExplain` — `Model.fsi/fs` + `RouteExplain.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.RouteExplain.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); a `README.md` cores
pointer; the `CLAUDE.md` plan pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `RouteExplain.fsi` and exercised in `scripts/prelude.fsx` (a new F031 section) before any `.fs` body exists; semantic tests call the packed public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.RouteExplain.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029/F030 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain records, one small closed DU (`AlternativeOutcome`), and `List.filter`/`List.sortBy`/`List.tryHead`. Cost comparison uses the DU's built-in **structural** ordering (`Cost` is declared `Cheap < Medium < High < Exhaustive`) — no rank table, no SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — one pure total function over two supplied values. Like F019 `Route`, F020 `RouteJson`, F029 `FreshnessKey`, F030 `EvidenceReuse`, this is a pure projection that needs no MVU ceremony. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value — `RouteResult`/`GateRegistry` driven through the genuine F014→F019 chain (the F019/F020 `Support.fs` real-chain precedent), plus hand-built values for disorder/dup cases. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | `AlternativeOutcome` is the observability surface: a high-cost finding never silently omits its alternative — it is either `CheaperLocalAlternative g` or an explicit `NoCheaperLocalAlternative` (the no-hide requirement, FR-006). The function is total: no exception, no swallowed failure, no silent truncation (an empty route / empty explanation is an ordinary value, FR-011). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (Route/Gates/Config consumed verbatim). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-013); references only `Route` + `Gates` (Config transitive), honoring "the helper core stays minimal — MUST NOT depend on git/filesystem scanning" (no Snapshot/git reference; no host/CLI). No rendering package IDs/paths/templates assumed — inputs are the product-neutral route/catalog values supplied by the caller. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only
N/A (no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass.

## Project Structure

### Documentation (this feature)

```text
specs/031-broad-route-explanation/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the explain/alternative semantics facts
├── data-model.md        # Phase 1 — AlternativeOutcome, HighCostFinding, RouteExplanation
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── route-explain-api.md          # the public function signatures + their laws
│   └── explanation-semantics.md      # the high-cost filter + alternative-selection decision tables
├── checklists/
│   └── requirements.md  # spec quality checklist (already present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.RouteExplain/                  # NEW — the pure broad-route cost-explanation core
├── Model.fsi                                        # NEW — AlternativeOutcome, HighCostFinding, RouteExplanation (sole public surface)
├── Model.fs                                         # NEW — the type bodies (no access modifiers)
├── RouteExplain.fsi                                 # NEW — highCostThreshold / explain signatures + laws
├── RouteExplain.fs                                  # NEW — pure high-cost filter + alternative resolution
└── FS.GG.Governance.RouteExplain.fsproj             # NEW — references ../FS.GG.Governance.Route + ../FS.GG.Governance.Gates; no new package

tests/FS.GG.Governance.RouteExplain.Tests/           # NEW — the semantic tests
├── Support.fs                                        # NEW — real RouteResult/GateRegistry builders (reusing the F019/F020 facts/registryOf/resultOf shape) + hand-built disordered values + FsCheck generators + repoRoot
├── HighCostFindingTests.fs                           # NEW — one finding per high-cost gate, none below threshold, every Cost tier; trace+domain+cost carried verbatim (SC-001/SC-002)
├── AlternativeTests.fs                               # NEW — named alt is same-domain ∧ strictly cheaper ∧ local; each failing condition ⇒ none; deterministic tie-break (SC-003/SC-004)
├── DeterminismTests.fs                               # NEW — explain-twice equality + selected-gate/registry/selecting-path order&dup invariance (SC-005)
├── EmptyRouteTests.fs                                # NEW — empty route / no high-cost gate ⇒ empty explanation (FR-011)
├── PurityTests.fs                                    # NEW — explanations identical across cwd/time/fs changes (SC-006)
├── SurfaceDriftTests.fs                              # NEW — baseline equality + scope-hygiene (Route/Gates/Routing/Findings/Config/BCL/FSharp.Core only) (SC-007)
├── Main.fs                                           # NEW — Expecto entry point
└── FS.GG.Governance.RouteExplain.Tests.fsproj         # NEW — references the core + Route + Gates + test frameworks

surface/FS.GG.Governance.RouteExplain.surface.txt     # NEW — committed public-surface baseline (Principle II)
FS.GG.Governance.sln                                 # CHANGED — add the new library + test project
scripts/prelude.fsx                                  # CHANGED — add the F031 design-first FSI section
README.md                                            # CHANGED — short pointer to the new core in the cores list
CLAUDE.md                                            # CHANGED — SPECKIT plan pointer → this plan

# Deliberately UNCHANGED:
src/** (existing), surface/** (existing)             # no merged core/.fsi/surface-baseline changes (FR-009)
tests/** (existing projects)                         # untouched; the new project is purely additive
```

**Structure Decision**: A **new pure-core library** mirroring F019 `Route` / F020 `RouteJson` (a `Model` file
of types + a same-named operations file), the established Phase-pattern for a deterministic projection. It
references **`Route` + `Gates`** (D1) — the two cores whose values it joins — and stays free of the
git-sensing/host assemblies, reusing F019/F018 vocabulary verbatim. Tier 1 (new public surface + baseline),
no new third-party dependency.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
</content>
