---
description: "Task list for F019 - 019-route-gate-selection: the pure route-resolution core — a single pure, total `Route.select : GateRegistry -> RouteReport -> FindingReport -> RouteResult` that joins F015 per-path routing to F018 gates by declared-id equality (`Gate.Domain` = routed `DomainId`), unions the selected gates deduplicated by `GateId`, annotates each with the selecting path(s) and the glob each won on, rolls up the distinct selected gates' declared costs as a per-tier multiset, and carries the F017 findings through unchanged — deterministically `GateId`-ordinal sorted and byte-identical for identical input, with NO selection-everything fallback and NO diagnostics/severity/enforcement/freshness/ship-verdict/JSON/CLI."
---

# Tasks: Route Gate Selection

**Feature branch**: `019-route-gate-selection` (active spec; git branch currently `main`)
**Spec**: [`specs/019-route-gate-selection/spec.md`](./spec.md)
**Plan**: [`specs/019-route-gate-selection/plan.md`](./plan.md)

**Input**: Design documents from `/specs/019-route-gate-selection/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Route.fsi](./contracts/Route.fsi), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`s; new surface baseline). Credible evidence is **public-surface** testing only: `Route.select` exercised over **real upstream-assembled inputs** — a `GateRegistry` from `Gates.buildRegistry`, a `RouteReport` from `Routing.route`, and a `FindingReport` from `Findings.findUnknownGovernedPaths`, all from real in-memory `TypedFacts` (the genuine values the later `fsgg route`/`fsgg ship`, route/audit JSON rows pass), never private helpers and never mocks (Principle V, research D8). Driving the real F015→F017→F018→F019 chain also transitively re-exercises the upstream rows, catching any join-time mismatch a mock would hide. No network, git, agent, clock, or filesystem is reachable, so **no synthetic evidence is anticipated** — every case is reachable from real upstream outputs. Any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — `FS.GG.Governance.Gates`, `FS.GG.Governance.Routing`, `FS.GG.Governance.Findings`, and `FS.GG.Governance.Config` are referenced as-is (their existing public types suffice); the only new baseline is `surface/FS.GG.Governance.Route.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total join of already-typed inputs (FR-008): no I/O, no git sensing, no clock, no multi-step state, no retries. It is exactly the "single rule evaluation / pure function" case Principle IV explicitly exempts from MVU ceremony (plan Constitution Check; the same call F015 `route`, F017 `findUnknownGovernedPaths`, and F018 `buildRegistry` made). The boundary is one pure function `select : GateRegistry -> RouteReport -> FindingReport -> RouteResult` — no `Model`/`Msg`/`Effect`/`update`/interpreter. The pure/edge separation the principle protects is satisfied trivially: everything is pure.

**Selection minimums (FR-002/FR-003/FR-010, SC-001)**: selection joins a gate's declared `Domain` to a path's `Routed` `DomainId` by **id equality** — via a `Map<DomainId, Gate list>` index over the registry; the `GateId` string is **never** re-parsed to recover a domain. Each `Routed (d, glob, _)` path selects **every** gate with `Gate.Domain = d`; the change's selected set is the **union** across `Routed` paths, **deduplicated by `GateId`**. `UnmatchedInRoot`/`OutOfScope` select **no** gate — there is **no** "select everything" fallback. A `Routed` path whose domain has no gate, an empty registry, and an empty routing report all yield a valid **empty** route, never an error.

**Trace minimums (FR-004, SC-002)**: every `SelectedGate` carries the F018 `Gate` verbatim (supplying `Id`/`Domain`/`Cost`/metadata) plus its `SelectingPaths` — each a `{Path; MatchedGlob}` where `MatchedGlob` is the F015 `Routed` winning glob (the "rule"). A gate reached by several paths appears **once** with all selecting paths recorded.

**Carry-through minimums (FR-005, SC-003)**: `RouteResult.Findings` is the F017 `FindingReport` placed on the result **unchanged** — no re-derive, re-sort, re-classify, or filter; an empty report stays an empty finding list (a success). A finding-bearing `UnmatchedInRoot` path selects no gate yet its finding is present — the two coexist.

**Cost minimums (FR-006, SC-004, research D5)**: the rollup is a **multiset** `CostRollup = {Cheap; Medium; High; Exhaustive}` counting the **distinct** selected gates per `Cost` tier (a shared gate counted once) — **not** a summed scalar (F014 declares no numeric tier weights). An empty selection yields the all-zero identity; the rollup is identical on re-run.

**Determinism minimums (FR-007/FR-012, SC-005/SC-006)**: `SelectedGates` is sorted by `String.CompareOrdinal (gateIdValue Id)`; each gate's `SelectingPaths` by normalized path ordinal; `Findings` are carried in their F017 order; `CostRollup` is order-free. Re-ordering the input candidate paths OR the registry's gate list leaves every output byte-identical. No wall-clock, environment, or host-path value enters the result.

**Scope-guard minimums (FR-011/FR-013, SC-007)**: no base/effective severity, no profile/mode/maturity enforcement, no evidence-freshness computation or cache-reuse decision (a gate's carried `FreshnessKey` is propagated inside `Gate`, never evaluated), no gate/check/command execution or ordering, no ship verdict/blockers/warnings/exit-code basis, no route/audit JSON or `.fsgg/gates.json`, no CLI command. The route carries only declared id newtypes (`GovernedPath`, `DomainId`, `GateId`) and the declared `Cost`. The library lives in the product-neutral Governance layer, requires no FS.GG package installed in any inspected repo, and the kernel never sees the route/gate-selection vocabulary.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US5]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional route-selection library `FS.GG.Governance.Route`, its test project, the public contracts (copied verbatim), the in-memory fixtures + upstream-assembly helpers, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references `FS.GG.Governance.Gates`, `FS.GG.Governance.Routing`, and `FS.GG.Governance.Findings` (with `FS.GG.Governance.Config` arriving transitively, research D1); its own code is BCL + FSharp.Core (the transitive YamlDotNet edge arrives via Config and is unused here).

- [X] T001 Create `src/FS.GG.Governance.Route/FS.GG.Governance.Route.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Route`, `RootNamespace=FS.GG.Governance.Route`, with exactly three `<ProjectReference>`s — `../FS.GG.Governance.Gates/FS.GG.Governance.Gates.fsproj`, `../FS.GG.Governance.Routing/FS.GG.Governance.Routing.fsproj`, `../FS.GG.Governance.Findings/FS.GG.Governance.Findings.fsproj` — and **no** `<PackageReference>` (Config arrives transitively, research D1). Compile order `Model.fs` → `Route.fs`. Add an fsproj header comment (mirroring the F017/F018 fsprojs) noting this is the route-*selection* core — distinct from `FS.GG.Governance.Routing` (domain-per-path): `Routing` answers "which domain owns each path"; `Route` is the resolved route trace ("which gates this change selects, and why") (research D1).
- [X] T002 Copy `specs/019-route-gate-selection/contracts/Model.fsi` → `src/FS.GG.Governance.Route/Model.fsi` and `contracts/Route.fsi` → `src/FS.GG.Governance.Route/Route.fsi` verbatim as the curated public surface (Principle II — these `.fsi`s are the SOLE public surface; the matching `.fs` files carry no top-level access modifiers).
- [X] T003 Add `failwith "F019"` stub bodies in `src/FS.GG.Governance.Route/Model.fs` and `src/FS.GG.Governance.Route/Route.fs` that satisfy the `.fsi` contracts, in the fsproj compile order `Model.fs` → `Route.fs`, so the library compiles against the contracts before any real logic lands (Principle I).
- [X] T004 Create `tests/FS.GG.Governance.Route.Tests/FS.GG.Governance.Route.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.Route`, `src/FS.GG.Governance.Gates`, `src/FS.GG.Governance.Routing`, `src/FS.GG.Governance.Findings`, and `src/FS.GG.Governance.Config` (the tests assemble real `TypedFacts` and call the real upstream functions to build the inputs).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.Route.Tests/`: `Support.fs`, `SelectionTests.fs`, `TraceTests.fs`, `FindingsCarryTests.fs`, `CostRollupTests.fs`, `DeterminismTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.Route` and `tests/FS.GG.Governance.Route.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the fixture + upstream-assembly helpers in `tests/FS.GG.Governance.Route.Tests/Support.fs` over **real** values (no mocks): (a) reuse/adapt the F017/F018 fixture style to build a real `Config.Model.TypedFacts` from declared domains, checks, a `PathMap` of `glob → domain`, surfaces, and (optionally) tooling commands — the genuine `Valid TypedFacts` a downstream caller holds; (b) a `registryOf : TypedFacts -> GateRegistry` wrapper that calls the real `Gates.buildRegistry`; (c) a `reportOf : TypedFacts -> GovernedPath list -> RouteReport` wrapper that calls the real `Routing.route`; (d) a `findingsOf : TypedFacts -> RouteReport -> FindingReport` wrapper that calls the real `Findings.findUnknownGovernedPaths`; (e) a convenience `selectOf : TypedFacts -> GovernedPath list -> RouteResult` chaining (b)+(c)+(d) into `Route.select`. These produce REAL downstream inputs, never fakes.
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F019 design sketch that `#r`s the built `FS.GG.Governance.Route` (+ `Gates`/`Routing`/`Findings`/`Config`) assemblies, opens the namespaces, builds a small in-memory `TypedFacts` declaring two domains (`build`, `docs`) with path-map globs and one `ProtectedSurface`, then for a change touching one `build` path, one `docs` path, and one unclassified in-root path calls `Gates.buildRegistry` → `Routing.route` → `Findings.findUnknownGovernedPaths` → `Route.select` and prints each selected gate's `gateIdValue`/domain/selecting paths/glob/cost, the carried findings, and the `CostRollup` — recording the intended chain flow before real bodies land (Principle I; mirrors [quickstart.md](./quickstart.md) §FSI smoke).
- [X] T009 [P] Create `specs/019-route-gate-selection/readiness/README.md` listing the required FSI transcripts (a two-domain change showing the union of selected gates in `GateId` order + each gate's selecting path/glob/cost; a multi-path-to-one-gate dedup showing the gate once with all selecting paths; a change with a finding-bearing `UnmatchedInRoot` path showing the finding carried while that path selects no gate; an empty/no-`Routed` change → empty route with all-zero `CostRollup`; a twice-identical + candidate-path/registry-gate reordered determinism run) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.Route` and `dotnet test tests/FS.GG.Governance.Route.Tests` compile against stubs; the solution lists the two new projects; the Gates/Routing/Findings references resolve (Config transitively); the Support helpers assemble real upstream inputs.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the route-domain model, the `Map<DomainId, Gate list>` registry index, the ordinal sort keys, and the `select` skeleton (per-path union + dedup accumulator) — everything the stories specialize. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `src/FS.GG.Governance.Route/Model.fs` exactly matching `Model.fsi`: the `SelectingPath` record (`{ Path: GovernedPath; MatchedGlob: GovernedPath }`), the `SelectedGate` record (`{ Gate: Gate; SelectingPaths: SelectingPath list }`), the `CostRollup` record (`{ Cheap; Medium; High; Exhaustive }` of `int`), and the `RouteResult` record (`{ SelectedGates: SelectedGate list; Findings: FindingReport; Cost: CostRollup }`). Reuses the upstream types (`Gates.Model.Gate`, `Findings.Model.FindingReport`, `Config.Model.GovernedPath`) — does NOT redefine them.
- [X] T011 Implement the selection primitives in `src/FS.GG.Governance.Route/Route.fs`: (a) a private domain→gates **index** `Map<DomainId, Gate list>` built once from `registry.Gates` grouped by `Gate.Domain`, each bucket in `GateId` ordinal order, so per-path gate lookup is a single O(1) map read keyed on the **declared** `DomainId` (FR-010 — the key IS `Gate.Domain`; the `GateId` string is never re-parsed for a domain); (b) the ordinal comparators `bySelectedGateId` (`String.CompareOrdinal (gateIdValue a.Gate.Id) (gateIdValue b.Gate.Id)`) and `bySelectingPath` (ordinal on the normalized `GovernedPath` string) used for the two output sorts (FR-007). Disclose any `mutable` accumulator at its use site (Principle III).
- [X] T012 Implement the `select` skeleton in `src/FS.GG.Governance.Route/Route.fs`: read only `report.Routings` (ignore `report.Diagnostics`, research D7); fold the routings, and for each `Routed (d, glob, _)` look up the T011 index bucket for `d` and accumulate, keyed by `GateId`, the gate plus a `{Path = routing.Path; MatchedGlob = glob}` selecting path (dedup by `GateId` — a gate reached again grows its selecting-path list); `UnmatchedInRoot`/`OutOfScope` contribute nothing (FR-003 — no fallback); build `SelectedGates` by sorting the accumulator's gates with `bySelectedGateId` and each gate's selecting paths with `bySelectingPath`; place `findings` on the result unchanged (FR-005, completed/asserted in US3); leave the `Cost` as the all-zero `CostRollup` placeholder (filled in US4). PURE and TOTAL — never throws, re-validates/re-routes/re-classifies nothing; empty `Routings` or empty `registry` yields `{ SelectedGates = []; Findings = findings; Cost = zero }`, a valid success (FR-008/FR-009).

**Checkpoint**: the library builds with the real Model + the domain→gates index + the sort comparators + the selection skeleton; `select` over an empty report/registry returns an empty-but-successful `RouteResult`; `select` over a `Routed` change returns the union of the reached domains' gates in `GateId` order with selecting paths recorded; the surface compiles against the `.fsi`s.

---

## Phase 3: User Story 1 - Select the gates a change must run (Priority: P1) 🎯 MVP

**Goal**: for each `Routed`-to-`d` path, select exactly the registry gates with `Gate.Domain = d`; the change's selected set is the union across `Routed` paths deduplicated by `GateId`; a gate in an unreached domain is absent; `UnmatchedInRoot`/`OutOfScope` select nothing; selection is on declared-id equality, never on a re-parsed `GateId` string.

**Independent Test**: fixture registry with gates in domains `build` and `docs`, and a routing report in which `src/Kernel/Core.fs` is `Routed` to `build` and a second path `Routed` to `docs`; select the route; assert the selected set is exactly the `build` gates ∪ the `docs` gates (by `GateId`), and a registry gate in an unreached domain `release` is **not** selected.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T013 [P] [US1] In `tests/FS.GG.Governance.Route.Tests/SelectionTests.fs`, add selection tests over real `selectOf`/`registryOf`+`reportOf` fixtures: (1) a path `Routed` to domain `d` selects **every** gate with `Gate.Domain = d`, each annotated with that path (US1 AS1, **SC-001**); (2) a registry gate whose domain is reached by no `Routed` path is **absent** from `SelectedGates` (US1 AS2); (3) two distinct `Routed` paths to different domains → the **union** of both domains' gates, none omitted, none duplicated (US1 AS3); (4) `UnmatchedInRoot` and `OutOfScope` paths select **no** gate, and there is no "select everything" fallback (FR-003); (5) selection matches `Gate.Domain` to the routed `DomainId` by **id equality** — a fixture whose `GateId` string would mis-parse to a different domain still selects on the declared `Gate.Domain` (FR-010, INV-9); (6) a `Routed` path whose `RouteReport` carries an `AmbiguousRoute` diagnostic for that path still selects its **resolved** domain's gates — `select` reads only `report.Routings` and acts on the resolved `Routed` outcome, never re-resolving ambiguity and never consuming `report.Diagnostics` (spec edge case "Routing diagnostics on the report", research D7).

### Implementation for User Story 1

- [X] T014 [US1] Confirm/complete the union + dedup selection in `select` (`src/FS.GG.Governance.Route/Route.fs`, building on T012): every `Routed` path contributes all gates of its domain's index bucket; the accumulator dedups by `GateId` so a gate reached by several paths/domains appears once; unreached-domain gates never enter the accumulator; `UnmatchedInRoot`/`OutOfScope` add nothing. Verify the join reads only the declared `DomainId` index key (FR-010) — **do not** re-parse `gateIdValue`. Note explicitly if no change was needed beyond Foundation. **Done — no change needed beyond Foundation:** the T012 skeleton already implements the union+dedup accumulator keyed on the declared `DomainId` index; verified the join never re-parses `gateIdValue`. (SelectionTests: 7 green.)

**Checkpoint**: the selected set is exactly the union of the reached domains' gates, deduped by `GateId`, with unreached-domain gates absent and unrouted paths selecting nothing — the MVP. US1 stands alone.

---

## Phase 4: User Story 2 - Explain every selected gate (route trace) (Priority: P1)

**Goal**: every selected gate carries a route trace naming the selecting path(s), the affected `Domain`, the matching glob each selecting path won on, and the gate's declared `Cost`; a gate reached by more than one path appears once with all selecting paths in a documented deterministic order; fields are declared ids only.

**Independent Test**: select a route in which `src/Api/Surface.fs` is `Routed` to domain `api` via glob `src/Api/**`; assert the selected `api` gate carries that path, the domain `api`, the matching glob `src/Api/**`, and the gate's declared cost — and that when two paths both reach `api` the selection records **both** selecting paths for the single shared gate.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T015 [P] [US2] In `tests/FS.GG.Governance.Route.Tests/TraceTests.fs`, add route-trace tests over real fixtures: (1) a gate selected because path `p` `Routed` to its domain via glob `g` — the `SelectedGate` carries `p` and `MatchedGlob = g` in `SelectingPaths`, the gate's `Domain`, and the declared `Cost` (via the embedded `Gate`) (US2 AS1, **SC-002**); (2) a single gate reached by more than one `Routed` path appears **once** with **all** selecting paths (and their globs) recorded, sorted by normalized path ordinal (US2 AS2, FR-007); (3) every selected gate carries only declared ids (`GateId`, `DomainId`, normalized globs/paths) and the declared cost — no raw YAML, host paths, severity, enforcement, freshness verdict, or product vocabulary (US2 AS3, **SC-007**).

### Implementation for User Story 2

- [X] T016 [US2] Complete the route-trace population in `select` (`src/FS.GG.Governance.Route/Route.fs`): ensure each `SelectingPath` carries the routing's `Path` and the F015 `Routed` winning `matchedGlob` (the "rule", read straight off `RoutingResult.Routed` — re-route nothing, FR-008); ensure the embedded `Gate` is carried verbatim (supplying `Id`/`Domain`/`Cost`/metadata without re-derivation, FR-004/FR-012); ensure each gate's `SelectingPaths` is sorted by `bySelectingPath` (T011). Note explicitly if no change was needed beyond Foundation/US1. **Done — no change needed beyond Foundation:** the skeleton already carries the routing `Path` + `Routed` winning glob into each `SelectingPath`, the embedded `Gate` verbatim, and sorts `SelectingPaths` by `bySelectingPath`. (TraceTests: 3 green.)

**Checkpoint**: every selected gate explains itself — selecting path(s) + glob(s) + domain + declared cost, deduped to one entry per gate with all selecting paths ordered. US1 + US2 together are the co-equal P1 MVP pairing.

---

## Phase 5: User Story 3 - Carry unknown-governed-path findings onto the route (Priority: P2)

**Goal**: the route result carries the F017 `FindingReport` unchanged alongside the selected gates, so one value explains both what runs and what is unclassified; an empty finding report yields an empty finding list (a success); a finding-bearing `UnmatchedInRoot` path selects no gate yet its finding is present.

**Independent Test**: select a route for a change whose paths include one `Routed` path (selecting a gate) and one `UnmatchedInRoot` path for which F017 produced an `UnknownGovernedPath` finding; assert the result contains both the selected gate and that finding, with the finding byte-identical to the F017 input.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T017 [P] [US3] In `tests/FS.GG.Governance.Route.Tests/FindingsCarryTests.fs`, add carry-through tests over real fixtures: (1) a route from a registry + report + **non-empty** F017 report carries exactly those findings, **unchanged** (byte-identical to the `findingsOf` input), alongside the selected gates (US3 AS1, **SC-003**); (2) an **empty** F017 report → `RouteResult.Findings` has an empty finding list and the route is a successful result, not an error or a fabricated finding (US3 AS2); (3) a change with a `Routed` path (selects a gate) **and** an `UnmatchedInRoot` path that produced a finding — the result holds both the selected gate and the finding, and the `UnmatchedInRoot` path selects **no** gate (the two facts coexist) (US3 AS3).

### Implementation for User Story 3

- [X] T018 [US3] Confirm `select` (`src/FS.GG.Governance.Route/Route.fs`) places the `findings` argument onto `RouteResult.Findings` **verbatim** — no re-sort, re-derive, re-classify, or filter (FR-005); the F017 report is already deterministically ordered, so carrying it preserves determinism. Fix only if the skeleton (T012) altered or reconstructed it; **do not** introduce any finding logic here. Note explicitly if no change was needed beyond Foundation. **Done — no change needed:** `select` places the `findings` argument on `RouteResult.Findings` verbatim; no finding logic introduced. (FindingsCarryTests: 3 green.)

**Checkpoint**: one route value explains both selected gates and unmatched governed paths; findings are byte-identical to F017; empty findings is a success; unrouted finding-bearing paths select nothing.

---

## Phase 6: User Story 4 - Roll up the route cost (Priority: P2)

**Goal**: the route carries a `CostRollup` — the per-tier count of the **distinct** selected gates' declared costs (each shared gate counted once); an empty selection yields the all-zero identity; the rollup is additive only and never changes which gates are selected; identical inputs yield an identical rollup.

**Independent Test**: select a route whose selected gates carry known declared costs; assert the route's `CostRollup` counts exactly the distinct selected gates per tier (each shared gate counted once), and re-running yields the identical rollup.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T019 [P] [US4] In `tests/FS.GG.Governance.Route.Tests/CostRollupTests.fs`, add cost-rollup tests over real fixtures: (1) a route selecting a set of distinct gates with declared costs → `CostRollup` is the per-tier count over exactly those distinct gates (a gate reached by several paths counted **once**) (US4 AS1, **SC-004**); (2) a route selecting **no** gates → the all-zero `CostRollup` (`{Cheap=0; Medium=0; High=0; Exhaustive=0}`) and a valid successful empty route (US4 AS2, FR-009); (3) identical inputs → identical `CostRollup` on re-run (US4 AS3); (4) the rollup does not change `SelectedGates` (additive only) — assert the same selection with/without reading the cost.

### Implementation for User Story 4

- [X] T020 [US4] Implement the cost rollup in `select` (`src/FS.GG.Governance.Route/Route.fs`): from the **distinct** `SelectedGates` (already deduped by `GateId`), tally each gate's `Gate.Cost` into the matching `CostRollup` field, producing `{Cheap; Medium; High; Exhaustive}` counts; the empty selection yields all-zero (the identity). A multiset of tiers — **no** summed scalar and **no** invented tier weights (research D5, FR-006). Deterministic (order-free counts). Place it on `RouteResult.Cost`, replacing the T012 placeholder.

**Checkpoint**: the route reports its total declared cost as a per-tier multiset over the distinct selected gates; empty selection → all-zero; stable on re-run; selection unaffected.

---

## Phase 7: User Story 5 - Deterministic, stable route trace (Priority: P2)

**Goal**: the same inputs always produce the same selected gates, selecting paths, findings, and cost in the same order; re-ordering the input candidate paths or the registry's gates never changes the result; `select` is total over any well-typed input.

**Independent Test**: compute a route twice over the same inputs, and once with the input candidate paths and the registry's gate list reordered; assert all three selected-gate lists (and their selecting-path sub-lists, findings, and cost) are byte-for-byte identical, including order.

### Tests for User Story 5 (write first; must FAIL before implementation)

- [X] T021 [P] [US5] In `tests/FS.GG.Governance.Route.Tests/DeterminismTests.fs`, add an FsCheck twice-identical property: `select` over the same registry/report/findings yields a byte-identical `RouteResult` — selected gates, selecting paths, findings, and cost, including order (US5 AS1, **SC-005**).
- [X] T022 [P] [US5] In the same file, add an FsCheck **permutation-invariance** property: presenting the input candidate paths (and/or the registry's gates) in a different order yields an unchanged `RouteResult` (ordering depends on documented sort keys — `GateId` ordinal for gates, normalized path for selecting paths — not on input order) (US5 AS2/AS3, **SC-005**). Permute the candidate paths by shuffling the `GovernedPath list` passed to `reportOf`. Because `Gates.buildRegistry` already returns `GateId`-sorted gates, the registry-order case CANNOT come from `registryOf`; instead construct a `GateRegistry { Gates = shuffled }` value directly from a real registry's gates (the registry is still a real value — only its list order is varied) and pass it to `Route.select`, asserting the result is unchanged.
- [X] T023 [P] [US5] In the same file, add an FsCheck **totality** property over generated valid inputs (including empty registry, empty routings, `Routed` paths whose domain has no gate): `select` **never throws** and **never yields a partial result**; an empty selection is a valid successful route with the all-zero `CostRollup` (**SC-006**, FR-008/FR-009).

### Implementation for User Story 5

- [X] T024 [US5] Confirm the deterministic ordering in `select` (`src/FS.GG.Governance.Route/Route.fs`): `SelectedGates` sorted by `bySelectedGateId`, each `SelectingPaths` by `bySelectingPath`, `Findings` carried in F017 order, `CostRollup` order-free (T011/T012/T016/T020). Fix any residual input-order leakage (e.g. an accumulator iterated in insertion order without a final sort). **Do not** introduce a clock, environment, or host-path value. Note explicitly if no change was needed beyond earlier phases. **Done — no change needed beyond earlier phases:** `SelectedGates` sorted by `bySelectedGateId`, `SelectingPaths` by `bySelectingPath`, findings in F017 order, `CostRollup` order-free; no clock/env/host value. (DeterminismTests: 5 green, FsCheck 200 cases each.)

**Checkpoint**: the route trace is byte-stable and permutation-invariant — usable as a CI contract and a golden route.json snapshot — and total over any well-typed input.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: lock the public surface, prove the dependency boundary, and finish the docs/evidence.

- [X] T025 [P] Generate `surface/FS.GG.Governance.Route.surface.txt` capturing exactly the public `Model` + `Route` modules (the `.fsi` surface — `SelectingPath`, `SelectedGate`, `CostRollup`, `RouteResult`, `select`), nothing private.
- [X] T026 In `tests/FS.GG.Governance.Route.Tests/SurfaceDriftTests.fs`, add the surface-drift test asserting the built public surface matches `surface/FS.GG.Governance.Route.surface.txt` (Principle II), and assert the `Route → {Gates, Routing, Findings} → Config` one-way dependency (no kernel/host/adapters/snapshot/CLI edge; no new third-party `PackageReference`) — mirroring the F017/F018 `SurfaceDriftTests` dependency assertion.
- [X] T027 [P] Verify [quickstart.md](./quickstart.md) end-to-end: run the documented `dotnet test` and the prelude FSI smoke (the F015→F017→F018→F019 chain), confirm the acceptance→evidence map holds, and fill `specs/019-route-gate-selection/readiness/README.md` with the real FSI transcripts (T009) and the SC-001…SC-007 traceability note.
- [X] T028 [P] Update [`specs/019-route-gate-selection/plan.md`](./plan.md) with an **Implementation Progress** header (status table + evidence summary, mirroring the F018 plan) once the suite is green, and confirm `CLAUDE.md`'s SPECKIT block points at this plan.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundation (Phase 2)** — depends on Setup; **BLOCKS all user stories** (the Model, the domain→gates index, the sort comparators, and the `select` skeleton everything specialises).
- **User Stories (Phases 3–7)** — all depend on Foundation. US1 (P1) is the MVP. US2 (P1) refines the trace US1 already records. US3/US4/US5 (P2) build on the selection: US3 asserts the carry-through the skeleton wires, US4 fills the cost placeholder, US5 proves ordering/totality across all of it.
- **Polish (Phase 8)** — depends on all desired user stories being complete.

### User-story dependencies

- **US1 (P1)** — after Foundation; no dependency on other stories (the core selection/union/dedup).
- **US2 (P1)** — after Foundation; reads the same selection US1 produces (the trace fields the skeleton already populates). Independently testable.
- **US3 (P2)** — after Foundation; the findings carry-through is independent of which gates are selected (asserts coexistence). Independently testable.
- **US4 (P2)** — after US1's selection exists (cost rolls up the *distinct selected* gates); additive — does not change selection.
- **US5 (P2)** — after the result is *correct* (US1–US4); proves its *ordering* and *totality* are stable.

### Within each user story

- Tests are written first and MUST FAIL before implementation (Principle I/V).
- Model + index + skeleton (Foundation) before any story.
- Each story is independently completable and testable; complete a story before moving to the next priority.

### Parallel opportunities

- **Setup**: T005, T007, T008, T009 are `[P]` (distinct files) once T001–T004 exist.
- **Tests across stories**: T013, T015, T017, T019, T021–T023 are `[P]` — distinct test files, no shared state.
- **Stories**: once Foundation is done, US1–US5 test-writing can proceed in parallel by different developers; the implementation tasks (T014, T016, T018, T020, T024) all touch `Route.fs`, so serialize those edits (or have one owner sweep them in phase order).
- **Polish**: T025, T027, T028 are `[P]`; T026 depends on T025.

---

## Parallel Example: cross-story test authoring

```bash
# After Foundation (Phase 2), launch the per-story test files together (distinct files):
Task: "SelectionTests.fs   — US1 selection/union/dedup/id-equality (T013)"
Task: "TraceTests.fs       — US2 route-trace fields + multi-path dedup (T015)"
Task: "FindingsCarryTests.fs — US3 findings carried unchanged (T017)"
Task: "CostRollupTests.fs  — US4 per-tier rollup over distinct gates (T019)"
Task: "DeterminismTests.fs — US5 twice-identical + permutation + totality (T021–T023)"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: a change's `Routed` paths select exactly the union of their domains' gates, deduped by `GateId`, unreached domains absent, unrouted paths selecting nothing.

### Incremental delivery

1. Setup + Foundation → foundation ready.
2. US1 → selection works → the MVP.
3. US2 → every selected gate explains itself (the co-equal P1 trace).
4. US3 → findings carried onto the route.
5. US4 → cost rolled up.
6. US5 → determinism + totality proven.
7. Polish → surface baseline + dependency assertion + readiness/quickstart.

---

## Notes

- `[P]` = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- The five `Route.fs` implementation tasks (T014, T016, T018, T020, T024) edit one file — serialize them in phase order; most are "confirm/complete", since the Foundation skeleton (T012) already wires the join.
- Verify tests fail before implementing; commit after each task or logical group.
- **No synthetic evidence is anticipated** (research D8) — every case is reachable from real upstream-assembled inputs. Any unavoidable literal carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- Scope guards (FR-011/FR-013): no severity, enforcement, freshness evaluation, execution, ship verdict, JSON, or CLI — the route trace stops at the typed `RouteResult`.
