---

description: "Task list for 029-freshness-key-core implementation"
---

# Tasks: Freshness Key Computation Core

**Input**: Design documents from `/specs/029-freshness-key-core/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/freshness-key-api.md,
contracts/freshness-key-format.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a determinism/injectivity/totality contract — the tests *are* the deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline,
no new third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — three pure, total functions, no state, no I/O, no workflow
(plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.FreshnessKey/FS.GG.Governance.FreshnessKey.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.FreshnessKey`, `Version` `0.1.0`, `IsPackable=true`
  (override `Directory.Build.props` like Gates/Config). `<Compile>` order: `Model.fsi`, `Model.fs`,
  `FreshnessKey.fsi`, `FreshnessKey.fs`. Single `<ProjectReference>` to
  `../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj`. **No third-party `PackageReference`** (FR-013,
  plan D1). Add a header comment mirroring the Gates `.fsproj` (pure total core; Config-only graph; reuses
  F014 newtypes the gate identity is built from; no Gates/Snapshot/git coupling — D1/D3).
- [X] T002 [P] Create `tests/FS.GG.Governance.FreshnessKey.Tests/FS.GG.Governance.FreshnessKey.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`);
  `<ProjectReference>`s to the new core and to `FS.GG.Governance.Config`. `<Compile>` order: `Support.fs`,
  `DeterminismTests.fs`, `DistinctionTests.fs`, `InjectivityTests.fs`, `InspectionTests.fs`, `PurityTests.fs`,
  `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs`
bodies so the library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins
until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.FreshnessKey/Model.fsi` — the SOLE public surface for the types:
  `open FS.GG.Governance.Config.Model`; the new opaque newtypes `RuleHash`, `ArtifactHash`,
  `CommandVersion`, `GeneratorVersion`, `Revision` (each `of string`); the `FreshnessInputs` record (fields
  + order per data-model.md, Cost deliberately absent — D5); the `Key` newtype (`Key of string`); the closed
  `InputCategory` DU (10 cases per data-model.md); and `val categoryToken: InputCategory -> string`. Curated
  doc comments in the F018/F016 `.fsi` style. The `Key` doc comment MUST state it is the computed
  fingerprint — distinct from F018's carried `Gates.Model.FreshnessKey` identity and from this project's
  `FreshnessKey` operations module (naming note, data-model.md). No access modifiers will appear in the
  matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.FreshnessKey/FreshnessKey.fsi` — the SOLE public surface for the
  operations: `val compute: FreshnessInputs -> Key`, `val matches: FreshnessInputs -> FreshnessInputs ->
  bool`, `val diff: FreshnessInputs -> FreshnessInputs -> InputCategory list`, `val value: Key -> string`,
  each with doc comments stating purity/totality and the laws (contracts/freshness-key-api.md).
- [X] T006 Add stub `src/FS.GG.Governance.FreshnessKey/Model.fs` and
  `src/FS.GG.Governance.FreshnessKey/FreshnessKey.fs` — real type definitions in `Model.fs` (records/DUs/
  newtypes are data, define them fully); `categoryToken` and the three operations as `failwith "not
  implemented"` stubs so the assembly compiles. No `private`/`internal`/`public` modifiers (Principle II).
  Confirm `dotnet build src/FS.GG.Governance.FreshnessKey/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F029 design-first section to `scripts/prelude.fsx` — `#r` the new Debug DLL; construct
  a literal `FreshnessInputs`; `printfn` the intended `compute`/`matches`/`diff` calls with expected results
  (canonical key shape, order/dup invariance, a flipped-field non-match naming `ruleHash`, a command-less
  match). This is the Principle-I FSI proof; it documents the shape even while bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.FreshnessKey.Tests/Support.fs` — real, literally-constructible
  builders (Principle V, no mocks): a `baseInputs` value and `with`-style helpers to vary one field at a
  time; an `allCategories` list (the 10 `InputCategory` cases) for table-driven distinction tests; FsCheck
  generators for `FreshnessInputs` (and for shuffled/duplicated artifact lists); and the
  `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the AuditJson
  `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.FreshnessKey.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Identical inputs reuse; any changed input forbids reuse (Priority: P1) 🎯 MVP

**Goal**: `compute` + `matches` deliver the deterministic "same inputs ⇒ reuse; any single changed input ⇒
no reuse" guarantee for every input category.

**Independent Test**: Build two equal input sets ⇒ keys equal & `matches` true; flip each category in turn
⇒ keys differ & `matches` false.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../DistinctionTests.fs` — table-driven over `allCategories`: from `baseInputs`,
  produce a variant differing in exactly that one category, assert `compute base <> compute variant` and
  `matches base variant = false`. Cover all 10 categories incl. `Command`/`CommandVersion` present↔absent and
  environment-class change (SC-003, acceptance scenarios US1 #2–#4).
- [X] T011 [P] [US1] In `tests/.../DeterminismTests.fs` add the reflexive-match cases: `matches x x = true`
  and `compute x = compute x` for representative and FsCheck-generated `x` (US1 #1; also seeds SC-001/SC-002).

### Implementation for User Story 1

- [X] T012 [US1] Implement `compute` in `FreshnessKey.fs` per contracts/freshness-key-format.md — the tagged,
  length-prefixed segment encoder (`tag '=' presence payload`), the fixed 10-field order joined by `\n` with
  no trailing newline, the `environmentToken` for `EnvironmentClass`, option presence (`0`/`1`), and the
  covered-artifact **set** rendering (dedup + ordinal sort + count prefix). BCL string building only
  (`System.Text.StringBuilder`); no hashing, no I/O. Returns `Key`.
- [X] T013 [US1] Implement `matches` as `compute a = compute b` and `value` as the `Key` unwrap, in
  `FreshnessKey.fs` (binds predicate to key so they cannot disagree — contracts law "predicate/key
  agreement"). Run T010–T011: green.

**Checkpoint**: US1 is functional — reuse is permitted exactly when all inputs agree. MVP reached.

---

## Phase 4: User Story 2 — The key is byte-stable and order-independent (Priority: P1)

**Goal**: Pin the determinism/injectivity guarantees of the `compute` built in US1: byte-stability,
covered-artifact set semantics, and cross-category injectivity (no value masquerading).

**Independent Test**: Recompute a fixed key ⇒ byte-identical; reorder/duplicate covered artifacts ⇒ same
key; move a string between categories ⇒ different key; representative inputs match committed golden strings.

### Tests for User Story 2 (write first)

- [X] T014 [P] [US2] Complete `tests/.../DeterminismTests.fs` — compute-twice byte-equality (string compare
  of `value (compute x)`); order-invariance (shuffled `CoveredArtifacts` ⇒ identical key); duplication-
  invariance (a repeated artifact hash ⇒ identical key); plus a small **golden table** of fixed inputs →
  expected canonical strings taken verbatim from contracts/freshness-key-format.md (incl. the worked
  example) (SC-001, SC-002).
- [X] T015 [P] [US2] `tests/.../InjectivityTests.fs` — for representative category pairs, place the same
  opaque string in category A vs category B and assert `compute` differs; assert a value containing the
  delimiter characters (`:`, `=`, `\n`, `;`) cannot collide with a neighbouring field (length-prefix
  guarantee) (SC-004, FR-006).

### Implementation for User Story 2

- [X] T016 [US2] Reconcile `compute` (T012) against the format contract until T014–T015 are green: confirm
  the length-prefix uses UTF-8 **byte** length, the set dedup is ordinal/culture-invariant, and the golden
  strings match exactly. Adjust the encoder (not the tests) if any byte differs; keep BCL-only.

**Checkpoint**: US1 + US2 — the key is a deterministic, order-independent, injective fingerprint.

---

## Phase 5: User Story 3 — Every freshness input is named, never hidden (Priority: P2)

**Goal**: `diff` (+ `categoryToken`) make a non-reuse decision explainable: it names exactly the differing
input categories, and is consistent with `matches`.

**Independent Test**: For two non-matching inputs, `diff` returns exactly the changed categories; for equal
inputs `diff = []`; `matches a b ⇔ (diff a b = [])`.

### Tests for User Story 3 (write first)

- [X] T017 [P] [US3] `tests/.../InspectionTests.fs` — `diff x x = []`; for each single-field variant from
  `allCategories`, `diff base variant = [thatCategory]`; a multi-field variant returns exactly the changed
  set in the fixed order; property `matches a b = (diff a b = [])` over FsCheck inputs; covered-artifacts
  compared as a set (reordered/duplicated artifacts ⇒ not reported by `diff`) (SC-005, FR-007, US3 #1–#2).
- [X] T018 [P] [US3] In `InspectionTests.fs`, assert `categoryToken` is total and injective over all 10
  `InputCategory` cases and that each token equals its value in the **`categoryToken` table** in
  contracts/freshness-key-api.md (the human-readable vocabulary — `ruleHash`/`coveredArtifacts`/… —
  which is intentionally **distinct** from the terse key-encoding tags in freshness-key-format.md).

### Implementation for User Story 3

- [X] T019 [US3] Implement `categoryToken` (Model.fs) and `diff` (FreshnessKey.fs) — `diff` compares the two
  inputs field-by-field in the fixed category order, comparing `CoveredArtifacts` as a set (dedup+sort), and
  returns the differing `InputCategory` list. Run T017–T018: green.

**Checkpoint**: All three stories functional and independently testable.

---

## Phase 6: Cross-cutting evidence & Tier-1 surface obligations

**Purpose**: Purity, totality, the surface baseline + scope guard, and the no-regression promise.

- [X] T020 [P] `tests/.../TotalityTests.fs` — every degenerate input is a total, ordinary outcome (FR-011):
  empty `CoveredArtifacts`; `Command = None`/`CommandVersion = None` (and `None`/`None` matches while
  `None`/`Some` does not); `Base = Head`; empty-string hash/version values (two empties match, empty ≠
  non-empty). No exception thrown by `compute`/`matches`/`diff`.
- [X] T021 [P] `tests/.../PurityTests.fs` — the key for a fixed input is byte-identical when recomputed after
  changing `Environment.CurrentDirectory` and after creating/deleting an unrelated temp file (and across
  repeated calls), demonstrating no clock/cwd/filesystem influence (SC-006).
- [X] T022 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (AuditJson/Gates precedent): render
  the assembly's public surface, compare to `surface/FS.GG.Governance.FreshnessKey.surface.txt` with the
  `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `FreshnessKey`) export and
  no token/encoder helpers leak; **scope-hygiene**: referenced assemblies are only `FSharp.Core`,
  `FS.GG.Governance.Config`, and BCL — NOT `Gates`/`Snapshot`/`Route`/`Adapters.*`/`Host`/`Cli` (plan D1,
  contracts negative scope guard).
- [X] T023 Generate the committed baseline `surface/FS.GG.Governance.FreshnessKey.surface.txt` by running the
  suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`FreshnessKey.fsi` to
  confirm it contains exactly the intended surface. Commit it. (After T022.)
- [-] T024 [P] SKIPPED — README's enumerated core list (and per-feature prose) stops at F18 (Gates);
  F19–F28 deliberately did not extend it, so adding only F029 would be inconsistent. No README change.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned.

---

## Phase 7: Validation & polish

- [X] T025 Run `dotnet test FS.GG.Governance.FreshnessKey.Tests` — all green; capture the run as evidence.
- [X] T026 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-007): existing
  projects' tests and every existing `surface/*.surface.txt` baseline are unchanged; only the new project's
  tests are added.
- [X] T027 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F029 section's printed results
  now match the real `compute`/`matches`/`diff` output (Principle I evidence, closes T007).
- [X] T028 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist
  to compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `compute`+`matches` (the MVP).
- **Phase 4 (US2)** → after **US1** specifically (it pins properties of the `compute` built in T012/T016).
- **Phase 5 (US3)** → after Phase 2; `diff` shares `compute`'s set/ordering logic, so practically after US1
  (T012). Independently testable once `diff` lands.
- **Phase 6 (cross-cutting)** → after the operations exist (US1–US3); T023 after T022.
- **Phase 7 (validation)** → last.

### Within each story

- Tests are written first and must FAIL against the Phase-2 stubs before the implementation task greens them.
- `Model.fs` types/`categoryToken` before the operation that uses them.

### Parallel opportunities

- Phase 1: T002 ‖ (T001→T003).
- Phase 2: T007 ‖ T008 ‖ T009 (after T004–T006 land the `.fsi`+stubs).
- Within a story, the `[P]` test files are independent of each other; the implementation task follows them.
- Phase 6: T020 ‖ T021 ‖ T024 (T022→T023 sequential).
- Phase 7: T027 ‖ T028 (after T025–T026).

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`compute`+`matches`) →
4. **STOP & VALIDATE**: distinct keys for distinct inputs, equal keys for equal inputs, for every category.

### Incremental delivery

US1 (reuse decision = MVP) → US2 (byte-stability/injectivity hardening) → US3 (explainable `diff`) →
cross-cutting evidence (purity/totality/surface) → full-suite validation. Each phase is independently
testable and adds value without breaking the previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.FreshnessKey.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — pure total functions, no state/I/O (recorded once here, not per
  task).
- No mocks anywhere (Principle V); all inputs are real literal `FreshnessInputs`. No `Synthetic` disclosure
  needed.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
