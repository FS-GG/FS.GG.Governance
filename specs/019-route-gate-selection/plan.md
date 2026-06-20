# Implementation Plan: Route Gate Selection

**Branch**: `019-route-gate-selection` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/019-route-gate-selection/spec.md`

## Implementation Progress

**Status: ✅ COMPLETE** — all 28 tasks done; suite green (25 Route tests; full solution 12/12 test projects pass). No synthetic evidence used.

| Phase | Tasks | Status | Evidence |
|---|---|---|---|
| 1 · Setup | T001–T009 | 🟢 Done | New `FS.GG.Governance.Route` lib + test project in `.sln`; contracts copied verbatim; real upstream-assembly `Support.fs`; prelude F019 sketch; readiness README. |
| 2 · Foundation | T010–T012 | 🟢 Done | `Model.fs` matches `Model.fsi`; `Map<DomainId, Gate list>` index keyed on declared `Gate.Domain`; ordinal sort keys; pure/total `select` skeleton (union+dedup accumulator). |
| 3 · US1 Select gates (P1, MVP) | T013–T014 | 🟢 Done | `SelectionTests.fs` — 7 green: per-domain selection, unreached-domain absence, union/dedup, no-fallback, id-equality join (colon-in-domain), ambiguous-route resolved, empty registry (SC-001). |
| 4 · US2 Route trace (P1) | T015–T016 | 🟢 Done | `TraceTests.fs` — 3 green: selecting path + winning glob + domain + declared cost; multi-path→one gate dedup, path-ordered; declared-ids-only (SC-002/SC-007). |
| 5 · US3 Carry findings (P2) | T017–T018 | 🟢 Done | `FindingsCarryTests.fs` — 3 green: F017 report carried unchanged; empty stays empty; finding-bearing unmatched path coexists with selection (SC-003). |
| 6 · US4 Cost rollup (P2) | T019–T020 | 🟢 Done | `CostRollupTests.fs` — 4 green: per-tier multiset over distinct gates; empty → all-zero; stable; additive-only (SC-004, research D5). |
| 7 · US5 Determinism (P2) | T021–T024 | 🟢 Done | `DeterminismTests.fs` — 5 green: FsCheck twice-identical, permutation-invariance (paths + registry order), totality (SC-005/SC-006). |
| 8 · Polish | T025–T028 | 🟢 Done | `surface/FS.GG.Governance.Route.surface.txt` baseline + drift test; `Route → {Gates, Routing, Findings} → Config` one-way dependency assertion; quickstart FSI smoke; this progress header. |

**Decisions held:** per-tier `CostRollup` multiset (no summed scalar / invented weights, D5); selection by declared-id equality via the domain index (`GateId` never re-parsed, FR-010); findings carried verbatim (FR-005); single pure total `select` — no MVU (D2). The four `Route.fs` "confirm" tasks (T014/T016/T018/T024) needed no change beyond the Foundation skeleton, which implemented the full join.

## Summary

Define the Phase-2 **route resolution core**: the deterministic join that turns *which domain each
changed path belongs to* (F015 `RouteReport`) plus *which gates exist per domain* (F018
`GateRegistry`) into **the gates a change selects, with a route trace explaining each selection**, and
carries the F017 unknown-governed-path findings through onto one value. A single **pure, total**
function `Route.select : GateRegistry -> RouteReport -> FindingReport -> RouteResult` selects, for
each `Routed` path, every registry gate whose declared `Domain` equals the path's routed `DomainId`
(by id equality), unions them deduplicated by `GateId`, annotates each with the selecting path(s) and
the glob each won on, rolls up the distinct selected gates' declared costs as a per-tier multiset, and
places the F017 findings on the result unchanged. It is the source of route.json's *selected gates*,
*matched rules*, *unmatched governed paths*, and *cost* fields that the later `fsgg route` / `fsgg
ship` and route/audit JSON rows consume.

Because the inputs are already-typed, already-validated F015/F017/F018 outputs, the join is **total
and emits no diagnostics**: it has no failure mode of its own (FR-008/FR-009). The work lands as a new
optional, packable library **`FS.GG.Governance.Route`** plus its test project — the same shape as
Config, Routing, Snapshot, Findings, and Gates — referencing **Gates + Routing + Findings** (Config
transitive) and adding **no new third-party dependency**. This is the first row whose job is to *join*
three prior rows, so it is the first library to reference all three together — exactly the consumer
F018's research D1 anticipated. The boundary is a plain pure function — no MVU, no ports — because the
feature performs no I/O, senses no git, and holds no state (FR-008): it only joins already-typed
inputs, exactly as F015 `route`, F017 `findUnknownGovernedPaths`, and F018 `buildRegistry` do
(research D2).

The feature stops at the typed `RouteResult`. Held firm by FR-011, it does **not** assign base/
effective severity or profile/mode/maturity enforcement; compute evidence freshness or cache reuse (a
gate's carried `FreshnessKey` is propagated, never evaluated); run, execute, or order any gate/check/
command; decide a ship verdict, blockers, warnings, or exit-code basis; or emit route/audit JSON,
`.fsgg/gates.json`, or any CLI command. Those are later Phase-2 / Phase-5 / Phase-11 rows that consume
this route trace.

**Confirmed during planning (the two scope reconciliations the spec deferred to plan time — research
D1/D5):**

- **Project home**: a new sibling library `FS.GG.Governance.Route` → Gates + Routing + Findings
  (Config transitive); no new package, no kernel/host edge (research D1). The name is the spec's
  named candidate; its proximity to `Routing` is accepted because the two are distinct nouns
  (Routing = domain-per-path; Route = the resolved route trace).
- **Cost-rollup shape**: a **multiset of `Cost` tiers** (`{Cheap; Medium; High; Exhaustive}` counts
  over the distinct selected gates), **not** a summed scalar — F014's `Cost` declares an order but no
  numeric weights, so summing would invent magnitudes the schema never states. This is the
  F018-consistent "no invented semantics" choice (research D5); a weighted total is deferred to
  Phase 11, which would *declare* weights first.
- **Boundary shape**: a single pure total `Route.select`; no `Model`/`Msg`/`Effect`/`update`
  (research D2).
- **Selection joins on declared id equality** between `Gate.Domain` and the routed `DomainId` via a
  `Map<DomainId, Gate list>` index; the `GateId` string is never re-parsed to recover a domain
  (research D3, FR-010).
- **Findings carried through unchanged** from F017 — no re-derive, re-sort, or re-classify
  (research D6, FR-005).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** Three new `ProjectReference`s —
`FS.GG.Governance.Gates` (`GateRegistry`/`Gate`/`GateId`), `FS.GG.Governance.Routing` (`RouteReport`/
`PathRouting`/`RoutingResult`), and `FS.GG.Governance.Findings` (`FindingReport`). `FS.GG.Governance.
Config` (the `GovernedPath`/`Cost`/`DomainId` newtypes) arrives transitively via all three. Its own
code is BCL + FSharp.Core only; the transitive YamlDotNet edge arrives via Config and is unused here.
Test-only packages remain the centrally pinned Expecto/FsCheck/VSTest set in
`Directory.Packages.props`.

**Storage**: None. Pure in-memory values; no file, process, clock, or network access of any kind.

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). The pure join is exercised through its
public surface over **real upstream-assembled inputs** — a real `GateRegistry` from
`Gates.buildRegistry`, a real `RouteReport` from `Routing.route`, a real `FindingReport` from
`Findings.findUnknownGovernedPaths`, all from real `TypedFacts` (research D8): per-domain selection +
unreached-domain absence, the route-trace fields (path/domain/glob/cost) with multi-path dedup,
findings carry-through (non-empty + empty), the per-tier cost rollup, and FsCheck determinism
(twice-identical + candidate-path/registry-gate permutation + totality). A surface-drift test guards
`surface/FS.GG.Governance.Route.surface.txt`; an FSI/prelude transcript runs the whole
F015→F017→F018→F019 chain over a fixture.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host. No platform
capability is touched (no git executable, no filesystem) — like F017/F018, this row reaches nothing.

**Project Type**: Optional packable F# class library plus one test project — the same shape as
Config, Routing, Snapshot, Findings, and Gates.

**Performance Goals**: Deterministic join, not throughput. Build a `Map<DomainId, Gate list>` index
over the registry once (O(gates)), then for each `Routed` path look up its domain's gates
(O(routedPaths × gatesPerDomain)), accumulate selecting paths deduped by `GateId`, then one ordinal
sort of selected gates and of each gate's selecting paths. Byte-for-byte stable output for identical
inputs (SC-005). No wall-clock, environment, or host-path value enters the result.

**Constraints**: Pure and total (FR-008/FR-009) — no I/O, git, or clock; never throws; an empty route
(no `Routed` path, empty registry, or no `Routed` path reaching a gate's domain) is a valid success
with the all-zero cost identity, never an error and never a "select everything" fallback (FR-003).
Selection is by declared id equality on `Gate.Domain` = routed `DomainId`; the `GateId` string is
never re-parsed (FR-010). Findings are carried unchanged from F017 (FR-005). The route carries only
declared id newtypes (`GovernedPath`, `DomainId`, `GateId`) and the declared `Cost` — no raw YAML,
host paths, timestamps, severity, enforcement, freshness verdict, or ship verdict (FR-011/FR-012,
SC-007). Requires no installed FS.GG package in any inspected repo (FR-013). Out of scope held firm by
FR-011.

**Scale/Scope**: One new production project (`src/FS.GG.Governance.Route`) and one test project
(`tests/FS.GG.Governance.Route.Tests`). Public modules are `Model` and `Route`, each with a curated
`.fsi` and a single combined surface baseline. **No** change to any existing project's public surface
— Gates/Routing/Findings/Config are referenced as-is (their existing public types suffice).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design —
still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/Model.fsi`](./contracts/Model.fsi) and [`contracts/Route.fsi`](./contracts/Route.fsi) fix the public surface before any `.fs` exists. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Model.fsi` and `Route.fsi` are the sole public surface; `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Route.surface.txt` + a surface-drift test. No existing baseline changes (no cross-feature surface touch). |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a `Map<DomainId, Gate list>` index, an accumulator fold over the routed paths, list map/sort. A single pure function is the *simplest* boundary for a pure join (vs MVU ceremony), justified in research D2. **Refusing a summed-scalar cost (D5)** is itself the simplicity-via-honesty choice — no invented tier weights. Any `mutable` accumulator is disclosed at the use site. No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. |
| IV. Elmish/MVU boundary | **PASS** | Principle IV mandates the MVU boundary only for **stateful or I/O** features. This feature performs no I/O, senses no git, holds no multi-step state (FR-008) — it is a pure total join of already-typed inputs, the "single rule evaluation / pure function" case the principle explicitly exempts. The same call F015 `route`, F017 `findUnknownGovernedPaths`, and F018 `buildRegistry` made and the constitution blesses. |
| V. Test evidence mandatory | **PASS** | Tests run through the public surface over **real upstream-assembled inputs** — the genuine F015/F017/F018 outputs, not fakes (research D8), which also transitively re-exercises the upstream chain. No network/git/agent is reachable. **No synthetic evidence is anticipated** — every case is reachable from real upstream outputs. Any literal standing in for an un-derivable case would carry `Synthetic` in the test name + a use-site disclosure and be listed in the PR. |
| VI. Observability & safe failure | **PASS** | Each selected gate is a stable-id, located (domain), explained (selecting path + glob + cost) record — the route trace this feature *produces* for later route/audit. An empty route is a distinct successful outcome, never an error (FR-009). The function is total — no swallowed exception, because there is no operation that can throw and the inputs are already validated. A tool defect is a test failure, never a malformed route. |
| Change Classification | **Tier 1** | New public, packable surface (a route library), new public `.fsi`s, new surface baseline. Adds a new *project* but **no new third-party dependency** and **no change to any existing project's public surface**. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency direction (`Route → {Gates, Routing, Findings} → Config`; Kernel/Host/adapters/Snapshot/CLI unaffected and do not reference Route in this feature). No new third-party `PackageReference`; the kernel stays BCL-only and never sees the route/gate-selection vocabulary (FR-013). This is a *layered* capability in a separate project — exactly the constitution's prescription. |

**Constitution alignment on the boundary (Principle IV).** Principle IV requires the
Model/Msg/Effect/update boundary for features "with multi-step state, external I/O, retries, user
interaction, background work, or operational recovery," and explicitly exempts "simple pure functions
— a fact store, a single rule evaluation, an explanation formatter." F019 is squarely the exempt
case: a deterministic join from three typed inputs to a typed route trace, with no state and no
effect. F015/F017/F018 took the same path for the same reason; this row follows.

**Constitution alignment on simplicity (Principle III / D5).** The consequential value-shape decision
— a per-tier cost *multiset* rather than a summed scalar — is an application of Principle III, not a
gap: summing closed `Cost` tiers requires numeric weights F014 never declares, and inventing them
would be "complex features … without matching justification." The multiset preserves the declared
vocabulary and counts each distinct gate once; a weighted total is deferred to Phase 11, which would
declare weights first. The spec's cost-shape assumption was settled to the honest, F018-consistent
form.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/019-route-gate-selection/
├── plan.md              # This file
├── research.md          # Phase 0 output (D1–D10 + resolved Technical Context)
├── data-model.md        # Phase 1 output (consumed + produced types, invariants, determinism)
├── quickstart.md        # Phase 1 output (validation guide + acceptance→evidence map)
├── contracts/
│   ├── Model.fsi        # route-domain types: SelectingPath, SelectedGate, CostRollup, RouteResult
│   └── Route.fsi        # the pure entry point: select
├── checklists/
│   └── requirements.md  # spec quality checklist (created by /speckit-specify)
├── readiness/           # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md             # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Route/                         # NEW optional route-selection library
├── FS.GG.Governance.Route.fsproj                   # references Gates + Routing + Findings; no new package
├── Model.fsi                                        # = contracts/Model.fsi
├── Model.fs                                         # SelectingPath/SelectedGate/CostRollup/RouteResult
├── Route.fsi                                        # = contracts/Route.fsi
└── Route.fs                                         # select: domain→gates index, per-path union+dedup, cost rollup (PURE)

tests/FS.GG.Governance.Route.Tests/                 # NEW semantic tests
├── FS.GG.Governance.Route.Tests.fsproj             # references Route (+ Gates/Routing/Findings/Config)
├── Support.fs                                        # in-memory TypedFacts fixtures + upstream assembly helpers
├── SelectionTests.fs                                # US1: per-domain selection + unreached absence + id-equality join (SC-001)
├── TraceTests.fs                                    # US2: route-trace fields + multi-path dedup + id-only fields (SC-002/SC-007)
├── FindingsCarryTests.fs                            # US3: findings carried unchanged; empty stays empty; unrouted selects nothing (SC-003)
├── CostRollupTests.fs                               # US4: per-tier rollup over distinct gates; empty ⇒ zero; stable (SC-004)
├── DeterminismTests.fs                              # US5: FsCheck twice-identical + permutation + totality (SC-005/SC-006)
├── SurfaceDriftTests.fs                             # baseline drift check
└── Main.fs

surface/FS.GG.Governance.Route.surface.txt          # NEW public surface baseline
scripts/prelude.fsx                                 # extend with an F019 chain (route → findings → registry → select) sketch
FS.GG.Governance.sln                                # add Route project and Route test project
CLAUDE.md                                            # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.Route` class library, sibling to
Kernel/Host/adapters/Config/Routing/Snapshot/Findings/Gates, is the home for route gate selection. It
references **Gates + Routing + Findings** (Config transitive) and adds no third-party dependency,
keeping the dependency direction one-way (`Route → {Gates, Routing, Findings} → Config`) and the
kernel/host untouched. It is the first row to reference all three upstream libraries together because
it is the first whose job is to *join* them — exactly the consumer F018's research D1 anticipated when
it kept the registry Routing-free. Splitting `Model` (the route types) from `Route` (the selector)
mirrors the F014/F015/F016/F017/F018 pure-core layout and lets the surface baseline and the selection
logic be reviewed independently. The library lives in the product-neutral Governance layer, never the
kernel, because the route/gate-selection vocabulary must not reach the kernel (FR-013).

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
