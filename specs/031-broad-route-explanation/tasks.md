---

description: "Task list for 031-broad-route-explanation implementation"
---

# Tasks: Broad-Route Cost Explanation Core

**Input**: Design documents from `/specs/031-broad-route-explanation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/route-explain-api.md,
contracts/explanation-semantics.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a high-cost-filter / alternative-rule / determinism / no-hide contract — the tests *are* the
deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no
new third-party dependency — plan Constitution Check). No per-task tier annotations needed — all tasks share
the feature tier.

**Elmish/MVU**: **Not applicable** — one pure, total function (`explain`) plus one exposed value
(`highCostThreshold`) over supplied values; no state, no I/O, no workflow (plan Constitution Check, Principle
IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The `RouteResult`/`GateRegistry` are values
handed in, not stateful stores.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.RouteExplain/FS.GG.Governance.RouteExplain.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.RouteExplain`, `Version` `0.1.0`, `IsPackable=true` (override
  the `Directory.Build.props` `IsPackable=false` default, like FreshnessKey/EvidenceReuse/Gates/Config).
  `<Compile>` order: `Model.fsi`, `Model.fs`, `RouteExplain.fsi`, `RouteExplain.fs`. **Two**
  `<ProjectReference>`s — `../FS.GG.Governance.Route/FS.GG.Governance.Route.fsproj` and
  `../FS.GG.Governance.Gates/FS.GG.Governance.Gates.fsproj` — and **no other src reference** (Config/Routing/
  Findings arrive transitively; never reference Snapshot/FreshnessKey/EvidenceReuse/Host/Cli — plan D1). **No
  third-party `PackageReference`** (FR-013, plan D1). Add a header comment mirroring the EvidenceReuse
  `.fsproj` (pure total broad-route cost-explanation core; Route+Gates graph; reuses F019
  `RouteResult`/`SelectedGate`/`SelectingPath` and F018 `GateRegistry`/`Gate` verbatim; high-cost threshold
  fixed at `High`; cheaper-local alternative = same-domain ∧ strictly cheaper ∧ local; no git/filesystem
  coupling — D1–D6).
- [X] T002 [P] Create `tests/FS.GG.Governance.RouteExplain.Tests/FS.GG.Governance.RouteExplain.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no
  version literals in the `.fsproj`); `<ProjectReference>`s to the new core **and** to
  `../../src/FS.GG.Governance.Route/...` and `../../src/FS.GG.Governance.Gates/...` (the test code constructs
  real `RouteResult`/`GateRegistry`). `<Compile>` order: `Support.fs`, `HighCostFindingTests.fs`,
  `AlternativeTests.fs`, `DeterminismTests.fs`, `EmptyRouteTests.fs`, `PurityTests.fs`, `SurfaceDriftTests.fs`,
  `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing F030 entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies
+ test scaffolding so the library and tests compile and tests can FAIL before implementation. **⚠️ No story
work begins until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.RouteExplain/Model.fsi` — the SOLE public surface for the types
  (contracts/route-explain-api.md, data-model.md): `open FS.GG.Governance.Config.Model`,
  `open FS.GG.Governance.Gates.Model`, `open FS.GG.Governance.Route.Model` (for `Gate`, `SelectedGate`,
  `Cost`, etc., reused verbatim — no redefinition, FR-009); the closed `AlternativeOutcome` DU
  (`CheaperLocalAlternative of Gate | NoCheaperLocalAlternative`); the `HighCostFinding` record
  (`{ Selected: SelectedGate; Alternative: AlternativeOutcome }`); the `RouteExplanation` record
  (`{ Findings: HighCostFinding list }`). Curated doc comments in the F019/F030 `.fsi` style:
  `AlternativeOutcome` is the no-hide result, always present, never `option`/null (FR-006); `HighCostFinding`
  embeds the F019 `SelectedGate` verbatim, re-deriving no identity/domain/cost/trace (D2); `Findings` is
  sorted by `Selected.Gate.Id` ordinal and `[]` is a valid success (D5, FR-011). No access modifiers will
  appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.RouteExplain/RouteExplain.fsi` — the SOLE public surface for the
  operations (contracts/route-explain-api.md): `val highCostThreshold: Cost`;
  `val explain: route: RouteResult -> registry: GateRegistry -> RouteExplanation`. Doc comments stating
  purity/totality and the laws (one finding per selected gate with `Cost >= highCostThreshold`, none below;
  `SelectedGate` carried verbatim; alternative = same-domain ∧ strictly cheaper ∧ local, cheapest-then-`GateId`,
  else explicit none; `Findings` ordered by `GateId`; empty route ⇒ empty explanation; reads no
  clock/filesystem/git/environment/network).
- [X] T006 Add stub `src/FS.GG.Governance.RouteExplain/Model.fs` and
  `src/FS.GG.Governance.RouteExplain/RouteExplain.fs` — real type definitions in `Model.fs` (records/DU are
  data, define them fully, with the three `open`s); `highCostThreshold` as `High` and `explain` as
  `failwith "not implemented"` in `RouteExplain.fs` so the assembly compiles. No `private`/`internal`/`public`
  modifiers (Principle II). Confirm `dotnet build src/FS.GG.Governance.RouteExplain/...` is clean under
  `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F031 design-first section to `scripts/prelude.fsx` after the F030 section — `#r` the
  new Debug DLL plus the Route and Gates DLLs; build a small worked example matching
  contracts/explanation-semantics.md (a route selecting an `Exhaustive` `Ci` gate `build:full` over a catalog
  also holding a `Cheap`/`Local` `build:unit` and a `Medium`/`LocalOrCi` `build:integration`); `printfn` the
  intended calls with expected results: `RouteExplain.highCostThreshold` ⇒ `High`; `explain route registry`
  has one finding for `build:full` carrying its verbatim `SelectingPaths`, and `Alternative =
  CheaperLocalAlternative` of `build:unit` (the cheapest local same-domain gate); removing the cheaper gates
  ⇒ `NoCheaperLocalAlternative`; a route of only `Cheap`/`Medium` gates ⇒ `{ Findings = [] }`. Render gate ids
  via `Gates.gateIdValue`. Expected outputs as inline comments. This is the Principle-I FSI proof; it documents
  the shape even while the body is stubbed.
- [X] T008 Write `tests/FS.GG.Governance.RouteExplain.Tests/Support.fs` — real, literally-constructible
  builders (Principle V, no mocks). Reuse the F019/F020 `Support.fs` real-chain shape: copy `gp`, `check`,
  `command`, `surface`, `facts`, `registryOf`, `reportOf`, `findingsOf`, `resultOf` so tests build a real
  `TypedFacts` → real `GateRegistry` (via `Gates.buildRegistry`) → real `RouteResult` (via `Route.select`).
  Add: a convenience to assemble a catalog with gates spanning all `Cost` tiers and `EnvironmentClass`es in a
  domain (for the alternative tables); hand-built `RouteResult`/`GateRegistry` builders from literal
  `SelectedGate`/`Gate` lists (for disordered/duplicate inputs the chain won't produce); FsCheck generators
  for `RouteResult` and `GateRegistry` (varying gate `Cost`/`Domain`/`Environment`, selecting-path order, and
  list order); and the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from
  the F030 `Support.fs`. No I/O beyond repo-root resolution.
- [X] T009 [P] Write `tests/FS.GG.Governance.RouteExplain.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because `explain`'s body is a stub (not because of compile errors).

---

## Phase 3: User Story 1 — Explain every high-cost gate with its route trace (Priority: P1) 🎯 MVP

**Goal**: `explain` emits exactly one `HighCostFinding` per selected gate whose declared `Cost >= High`, none
below, each embedding the F019 `SelectedGate` verbatim (gate identity, affected capability domain, declared
cost, and the full changed-path/matched-rule trace), with `Findings` deterministically ordered by `GateId`.

**Independent Test**: A route selecting gates across all `Cost` tiers ⇒ a finding for each `High`/`Exhaustive`
gate and none for `Cheap`/`Medium`; each finding's `Selected` equals the F019 selected gate (every selecting
path carried); a route with no high-cost gate ⇒ empty `Findings`. (US1 acceptance #1–#4.)

### Tests for User Story 1 (write first; must FAIL against the stub)

- [X] T010 [P] [US1] `tests/.../HighCostFindingTests.fs` — (a) table-driven over the `Cost` class: a real
  route (built via `resultOf`) selecting a gate of each tier ⇒ `explain` yields a finding iff the tier is
  `High` or `Exhaustive` (SC-001, US1 #1–#3), and `RouteExplain.highCostThreshold = High`; (b) verbatim trace:
  for a high-cost gate reached by several changed paths, the finding's `Selected` equals the matching
  `route.SelectedGates` entry — same `Gate.Id`/`Gate.Domain`/`Gate.Cost` and the same `SelectingPaths`
  (path + matchedGlob) set, not a subset (SC-002, US1 #1/#4); (c) ordering: with several high-cost gates the
  `Findings` are sorted by `Selected.Gate.Id` ordinal (US1 #3, D5); (d) count sanity: for representative
  routes the finding count equals the number of selected gates with `Cost >= High` — no high-cost gate dropped,
  none duplicated. (The below-threshold ⇒ `{ Findings = [] }` case is owned by T015 EmptyRouteTests; reference
  it here rather than re-asserting, to avoid duplicate coverage.)

### Implementation for User Story 1

- [X] T011 [US1] Implement `highCostThreshold = High` and `explain`'s high-cost filter + finding construction
  + ordering per contracts/explanation-semantics.md §1/§3 in `RouteExplain.fs`: `route.SelectedGates |>
  List.filter (fun sg -> sg.Gate.Cost >= highCostThreshold) |> List.map (fun sg -> { Selected = sg;
  Alternative = NoCheaperLocalAlternative }) |> List.sortBy (fun f -> let (GateId s) = f.Selected.Gate.Id in s)
  |> fun fs -> { Findings = fs }`. (The `Alternative` is the placeholder "none" until US2.) **Assumption to
  confirm at first compile (analysis U1):** the high-cost filter and the strict-cheaper test of US2 rely on
  `Cost` having F# structural `IComparable` so `sg.Gate.Cost >= highCostThreshold` (and later `g.Cost < h.Cost`
  / `List.sortBy … g.Cost`) compile and order `Cheap < Medium < High < Exhaustive` — verified sound (Config
  `Cost` is a plain DU with no `[<NoComparison>]`/`[<CustomComparison>]`), but assert it builds before
  proceeding. Route+Gates only; no clock/filesystem/git (FR-003). Run T010: the finding set/ordering/trace/
  count assertions go green (alternative assertions remain for US2).

**Checkpoint**: US1 is functional — every high-cost selected gate is explained with its verbatim route trace,
in deterministic order; below-threshold gates produce nothing. MVP reached (minus the alternative).

---

## Phase 4: User Story 2 — Offer the cheaper local alternative, or state there is none (Priority: P1)

**Goal**: Each high-cost finding's `Alternative` is resolved against the catalog: a same-domain, strictly
cheaper, locally-runnable gate (cheapest, ties by `GateId`) as `CheaperLocalAlternative`, else the explicit
`NoCheaperLocalAlternative` — always present.

**Independent Test**: A catalog with a strictly-cheaper local same-domain gate ⇒ the finding names it; flip
any one condition (equal/higher cost, different domain, `Ci`/`Release` environment) ⇒ the finding states
"none"; several qualifying candidates ⇒ the cheapest (ties by `GateId`) is named, deterministically. (US2
acceptance #1–#4.)

### Tests for User Story 2 (write first)

- [X] T012 [P] [US2] `tests/.../AlternativeTests.fs` — using the §2 worked example
  (contracts/explanation-semantics.md): (a) named alternative: a high-cost gate with a same-domain
  strictly-cheaper `Local`/`LocalOrCi` catalog gate ⇒ `Alternative = CheaperLocalAlternative g` where `g` is
  that gate (SC-003/SC-004, US2 #1); (b) each failing condition ⇒ `NoCheaperLocalAlternative`, tested
  independently — equal-cost same-domain local; strictly-cheaper same-domain but `Ci` and `Release`;
  strictly-cheaper local but **different domain** (SC-004, US2 #2, Edge cases); (c) local truth table: assert
  `Local` and `LocalOrCi` qualify and `Ci`/`Release` do not, over `g.FreshnessKey.Environment` (D6); (d)
  deterministic tie-break: two qualifying candidates ⇒ the **cheapest** is named; two equally-cheapest ⇒ the
  least `GateId` ordinal is named; same inputs ⇒ same named alternative (US2 #3, FR-007); (e) presence: every
  finding (FsCheck routes/catalogs) carries a present `Alternative` — `CheaperLocalAlternative` or
  `NoCheaperLocalAlternative`, never an omitted/ambiguous value (US2 #4, SC-003).
- [X] T013 [US2] Implement `explain`'s per-finding alternative resolution per
  contracts/explanation-semantics.md §2 in `RouteExplain.fs`, replacing the T011 placeholder: for each
  high-cost finding gate `h`, `registry.Gates |> List.filter (fun g -> g.Domain = h.Domain && g.Cost < h.Cost
  && (match g.FreshnessKey.Environment with Local | LocalOrCi -> true | Ci | Release -> false)) |> List.sortBy
  (fun g -> g.Cost, (let (GateId s) = g.Id in s)) |> List.tryHead` ⇒ `Some g -> CheaperLocalAlternative g | None
  -> NoCheaperLocalAlternative`. (Strict `<` excludes the gate itself and equal-cost peers; the sort gives
  cheapest-then-`GateId`.) Route+Gates only; no clock/filesystem/git. Run T012: green. Re-run T010: still green.

**Checkpoint**: US1 + US2 — every high-cost gate is explained with its trace **and** a present cheaper-local
alternative (named or explicit none). The design's six fields + the alternative are all delivered.

---

## Phase 5: User Story 3 — The explanation is deterministic and pure over supplied data (Priority: P2)

**Goal**: `explain` is a pure, deterministic function of the supplied route + catalog: identical inputs ⇒
identical explanation, and reordering/duplicating selected gates, registry gates, or selecting paths never
changes it; no clock/filesystem/git/environment/network is read.

**Independent Test**: `explain` twice ⇒ structurally equal. Permute the selected-gate order, the registry-gate
order, and a gate's selecting-path order (and duplicate entries) ⇒ unchanged explanation. Recompute after
changing cwd / creating a temp file ⇒ unchanged. (US3 acceptance #1–#3.)

### Tests for User Story 3 (write first)

- [X] T014 [P] [US3] `tests/.../DeterminismTests.fs` — (a) `explain route registry` called twice yields
  structurally identical `RouteExplanation` for representative and FsCheck-generated `(route, registry)`
  (SC-005, US3 #1); (b) order/dup invariance: shuffle/duplicate `route.SelectedGates`, shuffle/duplicate
  `registry.Gates`, and shuffle a high-cost gate's `SelectingPaths` (via the hand-built builders) ⇒ the
  resulting `RouteExplanation` equals the unpermuted one — same findings, same order, same trace, same
  alternative (SC-005, US3 #2, Edge: order/dup). Build permutations with the Support FsCheck helpers.
- [X] T015 [P] [US3] `tests/.../EmptyRouteTests.fs` — the owner of the empty/degenerate-route coverage:
  `explain { SelectedGates = []; … } registry = { Findings = [] }`; a route whose selected gates are all
  `< High` ⇒ `{ Findings = [] }` (the below-threshold case T010 defers here); a high-cost gate with an
  **empty** registry ⇒ one finding with `Alternative = NoCheaperLocalAlternative`. Total on all degenerate
  inputs — no exception (FR-011, SC-001/SC-004).
- [X] T016 [P] [US3] `tests/.../PurityTests.fs` — a fixed `explain` result is identical when recomputed after
  changing `Environment.CurrentDirectory` and after creating/deleting an unrelated temp file (and across
  repeated calls), demonstrating no clock/cwd/filesystem influence (SC-006, US3 #3).

**Note**: US3 has no new implementation task — determinism/purity are properties of the `explain` built in
US1+US2 (the `List.filter`/`List.sortBy`/`List.tryHead` pipeline is pure and order-independent by
construction). If T014–T016 reveal a gap, fix `explain` (never weaken a test).

**Checkpoint**: All three stories functional and independently testable — the explanation is correct,
complete, and deterministic.

---

## Phase 6: Cross-cutting Tier-1 surface obligations

**Purpose**: The surface baseline + scope guard and the no-regression promise.

- [X] T017 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (F029/F030/AuditJson precedent):
  render the assembly's public surface, compare to `surface/FS.GG.Governance.RouteExplain.surface.txt` with
  the `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `RouteExplain`) export
  and no helper leaks; **scope-hygiene**: referenced assemblies are only `FSharp.Core`,
  `FS.GG.Governance.Route`, `FS.GG.Governance.Gates`, `FS.GG.Governance.Routing`, `FS.GG.Governance.Findings`,
  `FS.GG.Governance.Config` (all transitive via Route/Gates), and BCL (`System.*`/`netstandard`/`mscorlib`) —
  NOT `Snapshot`/`FreshnessKey`/`EvidenceReuse`/`RouteJson`/`GatesJson`/`Adapters.*`/`Host`/`Cli`/`Ship`/
  `Enforcement`/`AuditJson`/`RouteCommand`/`ShipCommand` (plan D1, contracts negative scope guard).
- [X] T018 Generate the committed baseline `surface/FS.GG.Governance.RouteExplain.surface.txt` by running the
  suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`RouteExplain.fsi` to
  confirm it contains exactly the intended surface (the two modules, one val + one value, three types). Commit
  it. (After T017.)
- [-] T019 [P] README cores pointer — **SKIPPED.** Verified the README's enumerated core list still stops
  at F018 (its highest `F0xx` token is F015; F019–F030 cores, including FreshnessKey/EvidenceReuse, are not
  enumerated). Per the F029/F030 SKIP precedent, not partially extending it for F031 alone — rationale
  recorded here rather than a one-off pointer.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned.

---

## Phase 7: Validation & polish

- [X] T020 Run `dotnet test tests/FS.GG.Governance.RouteExplain.Tests/...` — all suites green; capture the run
  as evidence (Principle V).
- [X] T021 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-007): existing
  projects' tests and **every** existing `surface/*.surface.txt` baseline (including F019/F018/F030) are
  unchanged; only the new project's tests and the new surface baseline are added. Confirm `src/**` and existing
  `surface/**` show no diff.
- [X] T022 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F031 section's printed results
  now match the real `explain`/`highCostThreshold` output (Principle I evidence, closes T007).
- [X] T023 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality. Confirm the `CLAUDE.md` plan pointer already targets
  `specs/031-broad-route-explanation/plan.md` (it does — no change needed).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist
  to compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `explain`'s high-cost filter + verbatim finding + ordering (the
  MVP) and `highCostThreshold`.
- **Phase 4 (US2)** → after **US1** specifically (it replaces the placeholder `Alternative` of the `explain`
  built in T011). Independently testable once the alternative resolution lands (T013).
- **Phase 5 (US3)** → after US1+US2 (determinism/purity are properties of the completed `explain`). No new
  implementation; tests only.
- **Phase 6 (cross-cutting)** → after `explain` exists; T018 after T017.
- **Phase 7 (validation)** → last.

### Within each story

- Tests are written first and must FAIL against the Phase-2 stub before the implementation task greens them.
- `Model.fs` types before the operations that use them (already in place from Phase 2).

### Parallel opportunities

- Phase 1: T002 ‖ (T001→T003).
- Phase 2: T007 ‖ T009 (after T004–T006 land the `.fsi`+stubs; T008 Support precedes the story tests).
- Within a story, the `[P]` test files are independent of each other; the implementation task follows them.
- Phase 5: T014 ‖ T015 ‖ T016 (all tests, no new impl).
- Phase 6: T019 ‖ (T017→T018).
- Phase 7: T022 ‖ T023 (after T020–T021).

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`explain` high-cost filter + verbatim finding +
ordering, `highCostThreshold`) → 4. **STOP & VALIDATE**: every `High`/`Exhaustive` selected gate is explained
with its route trace, none below threshold, deterministically ordered.

### Incremental delivery

US1 (high-cost findings = MVP) → US2 (cheaper-local alternative resolution) → US3 (determinism/purity proof) →
cross-cutting surface obligations → full-suite validation. Each phase is independently testable and adds value
without breaking the previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.RouteExplain.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — one pure total function, no state/I/O (recorded once here, not per
  task). `RouteResult`/`GateRegistry` are values handed in, not stateful stores.
- No mocks anywhere (Principle V); all inputs are real `RouteResult`/`GateRegistry` values built through the
  genuine F014→F019 chain, plus hand-built literal values for the disorder/dup cases. No `Synthetic`
  disclosure needed.
- F019 (`Route`) and F018 (`Gates`) are consumed verbatim — `RouteResult`/`SelectedGate`/`SelectingPath`,
  `GateRegistry`/`Gate`/`Cost`/`EnvironmentClass`/`DomainId`/`GateId`. Nothing in Route/Gates/Config is
  modified (FR-009, SC-007).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
</content>
