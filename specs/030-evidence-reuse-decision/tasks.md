---

description: "Task list for 030-evidence-reuse-decision implementation"
---

# Tasks: Evidence-Reuse Decision Core

**Input**: Design documents from `/specs/030-evidence-reuse-decision/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/evidence-reuse-api.md,
contracts/reuse-decision-semantics.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a reuse-iff-match / determinism / totality / no-hide contract — the tests *are* the
deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline,
no new third-party dependency — plan Constitution Check). No per-task tier annotations needed — all tasks
share the feature tier.

**Elmish/MVU**: **Not applicable** — two pure, total functions (`decide`, `record`) plus three total
accessors (`empty`, `entries`, `referenceValue`) over supplied values; no state, no I/O, no workflow (plan
Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The
`ReuseStore` is a value transformed by `record`, not a stateful store.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.EvidenceReuse/FS.GG.Governance.EvidenceReuse.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.EvidenceReuse`, `Version` `0.1.0`, `IsPackable=true`
  (override the `Directory.Build.props` `IsPackable=false` default, like FreshnessKey/Gates/Config).
  `<Compile>` order: `Model.fsi`, `Model.fs`, `EvidenceReuse.fsi`, `EvidenceReuse.fs`. A **single**
  `<ProjectReference>` to `../FS.GG.Governance.FreshnessKey/FS.GG.Governance.FreshnessKey.fsproj` — and **no
  other src reference** (Config arrives transitively; never reference Gates/Snapshot/Route/Host/Cli — plan
  D1). **No third-party `PackageReference`** (FR-014, plan D1). Add a header comment mirroring the
  FreshnessKey `.fsproj` (pure total reuse-decision core; FreshnessKey-only graph; reuses F029
  `FreshnessInputs`/`matches`/`diff`/`InputCategory` verbatim; `EvidenceRef` is an opaque edge-supplied token;
  no git/filesystem coupling — D1/D2/D3).
- [X] T002 [P] Create `tests/FS.GG.Governance.EvidenceReuse.Tests/FS.GG.Governance.EvidenceReuse.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`,
  no version literals in the `.fsproj`); `<ProjectReference>`s to the new core **and** to
  `../../src/FS.GG.Governance.FreshnessKey/FS.GG.Governance.FreshnessKey.fsproj` (the test code constructs
  real `FreshnessInputs`). `<Compile>` order: `Support.fs`, `ReuseDecisionTests.fs`, `EmptyStoreTests.fs`,
  `ExplanationTests.fs`, `RecordTests.fs`, `DeterminismTests.fs`, `PurityTests.fs`, `SurfaceDriftTests.fs`,
  `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing F029 entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs`
bodies + test scaffolding so the library and tests compile and tests can FAIL before implementation.
**⚠️ No story work begins until this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.EvidenceReuse/Model.fsi` — the SOLE public surface for the types
  (contracts/evidence-reuse-api.md, data-model.md): `open FS.GG.Governance.FreshnessKey.Model` (for
  `FreshnessInputs` and `InputCategory`, reused verbatim — no redefinition, FR-010); the new opaque newtype
  `EvidenceRef` (`EvidenceRef of string`); the `RecordedEvidence` record (`{ Inputs: FreshnessInputs;
  Evidence: EvidenceRef }`); the `ReuseStore` single-case DU (`ReuseStore of RecordedEvidence list`); the
  closed `RecomputeCause` DU (`NoPriorEvidence | InputsChanged of InputCategory list`); and the
  `ReuseDecision` DU (`Reuse of EvidenceRef | Recompute of RecomputeCause`). Curated doc comments in the
  F029/F018 `.fsi` style: `EvidenceRef` is opaque (never parsed/produced/dereferenced, empty string is a
  literal — FR-001/FR-012); `InputsChanged` carries a NON-EMPTY list that never includes
  `CheckIdentity`/`DomainIdentity` (FR-006). No access modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.EvidenceReuse/EvidenceReuse.fsi` — the SOLE public surface for the
  operations (contracts/evidence-reuse-api.md): `val empty: ReuseStore`;
  `val record: inputs: FreshnessInputs -> evidence: EvidenceRef -> store: ReuseStore -> ReuseStore`;
  `val decide: candidate: FreshnessInputs -> store: ReuseStore -> ReuseDecision`;
  `val entries: store: ReuseStore -> RecordedEvidence list`;
  `val referenceValue: reference: EvidenceRef -> string`. Each with doc comments stating purity/totality and
  the laws (reuse iff `FreshnessKey.matches`; most-recent-wins on duplicate; located cause via gate-identity
  split + `FreshnessKey.diff`; de-dup/no-mutation record).
- [X] T006 Add stub `src/FS.GG.Governance.EvidenceReuse/Model.fs` and
  `src/FS.GG.Governance.EvidenceReuse/EvidenceReuse.fs` — real type definitions in `Model.fs` (records/DUs/
  newtypes are data, define them fully, `open FS.GG.Governance.FreshnessKey.Model`); the five operations as
  `failwith "not implemented"` stubs in `EvidenceReuse.fs` so the assembly compiles. No
  `private`/`internal`/`public` modifiers (Principle II). Confirm `dotnet build
  src/FS.GG.Governance.EvidenceReuse/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F030 design-first section to `scripts/prelude.fsx` after the F029 section — `#r` the
  new Debug DLL and the FreshnessKey DLL; reuse F029's worked `FreshnessInputs` (`f29Inputs`); `record` it
  under an `EvidenceRef`; `printfn` the intended calls with expected results: a matching candidate ⇒ `Reuse`;
  a one-field-changed candidate ⇒ `Recompute (InputsChanged [...])` (printing categories via
  `Model.categoryToken`); a different-gate candidate ⇒ `Recompute NoPriorEvidence`; and re-recording the same
  inputs refreshes with no duplicate (`entries` length unchanged). Expected outputs as inline comments. This
  is the Principle-I FSI proof; it documents the shape even while bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.EvidenceReuse.Tests/Support.fs` — real, literally-constructible
  builders (Principle V, no mocks): reuse the F029 `baseInputs` value + `allCategories` table shape (copy the
  `FreshnessInputs` builder and one-field-`with` varters and the 8 reuse-relevant `InputCategory` cases from
  the F029 `Support.fs` precedent); `EvidenceRef` builders (`E1`/`E2`/… and an empty-string ref); helpers to
  build a `ReuseStore` directly from a literal entry list (the DU constructor is public — stores need not go
  through `record`); FsCheck generators for `FreshnessInputs`, `EvidenceRef`, and `ReuseStore` (incl.
  shuffled/duplicated `CoveredArtifacts`); and the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` /
  `repoRoot` helper copied from the F029 `Support.fs`. No I/O beyond repo-root resolution.
- [X] T009 [P] Write `tests/FS.GG.Governance.EvidenceReuse.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Reuse iff all freshness inputs match (Priority: P1) 🎯 MVP

**Goal**: `decide` delivers the deterministic "some recorded entry matches on every category ⇒ Reuse that
entry's evidence; otherwise Recompute" guarantee — the single rule the whole cost/cache phase turns on.

**Independent Test**: Build a store with one entry; a candidate identical in every category ⇒ `Reuse` its
ref; a candidate differing in exactly one category (each in turn) ⇒ `Recompute`. A store with several entries
where exactly one matches ⇒ `Reuse` that one's ref. An empty store ⇒ `Recompute`. (US1 acceptance #1–#4.)

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../ReuseDecisionTests.fs` — (a) full match: `decide c (ReuseStore [{Inputs=c;
  Evidence=E1}]) = Reuse E1`; (b) table-driven over `allCategories`: from `baseInputs` produce a candidate
  differing in exactly that one category and assert `decide` is `Recompute _` (no reuse) — covering rule
  hash, covered-artifact set, command version present↔absent, generator version, base revision, head
  revision, environment class, and gate identity (Check/Domain) (SC-001, US1 #1–#2); (c) several entries of
  which exactly one fully matches ⇒ `Reuse` that one's ref regardless of order (US1 #3); (d) most-recent-wins:
  a hand-built store with two full-match entries ⇒ `Reuse` the head (newest) entry's ref deterministically
  (Edge: multiple matches, FR-005). Use `referenceValue` to compare carried refs.
- [X] T011 [P] [US1] `tests/.../EmptyStoreTests.fs` — `decide c empty = Recompute NoPriorEvidence` for
  representative and FsCheck-generated candidates (SC-004, US1 #4); plus totality on degenerate inputs: an
  `EvidenceRef ""` is carried verbatim on reuse and never rejected, and `decide`/`entries`/`referenceValue`
  throw no exception on empty store / empty ref (FR-012).

### Implementation for User Story 1

- [X] T012 [US1] Implement `empty`, `entries`, `referenceValue` (trivial unwraps) and `decide` per
  contracts/reuse-decision-semantics.md in `EvidenceReuse.fs`: step 1 — `entries store |> List.tryFind (fun e
  -> FreshnessKey.matches candidate e.Inputs)` ⇒ `Some e ⇒ Reuse e.Evidence`; step 2 (no full match) ⇒ a
  located `Recompute` cause (the gate-identity split is hardened in US2; for now return the cause shape the
  semantics contract specifies). `List.tryFind` is head-first over the newest-first store ⇒ most-recent-wins.
  FreshnessKey-only; no clock/filesystem/git (FR-009). Run T010–T011: green.

**Checkpoint**: US1 is functional — evidence is reused exactly when some recorded entry matches on every
freshness input, else recompute. MVP reached.

---

## Phase 4: User Story 2 — A recompute decision is always explained, never opaque (Priority: P1)

**Goal**: Pin the no-hide cause built into `decide`: every `Recompute` carries a located cause —
`NoPriorEvidence` when no entry shares the candidate's gate identity, or `InputsChanged (diff …)` naming
exactly the differing non-identity categories of the most-recent same-gate entry.

**Independent Test**: A store with a same-gate entry differing only in head revision ⇒ `Recompute
(InputsChanged [HeadRevisionCat])`. A store with no entry for the candidate's gate (or empty) ⇒ `Recompute
NoPriorEvidence`. Every `Recompute` has a present, non-ambiguous cause. (US2 acceptance #1–#3.)

### Tests for User Story 2 (write first)

- [X] T013 [P] [US2] `tests/.../ExplanationTests.fs` — (a) same-gate single-field change: for each
  non-identity category, a store entry differing from the candidate in only that category ⇒ `Recompute
  (InputsChanged [thatCategory])` (SC-003, US2 #1); (b) multi-field same-gate change ⇒ `InputsChanged` lists
  exactly the changed categories in F029's fixed `diff` order (e.g. `[RuleHashCat; HeadRevisionCat]`, never
  reversed) — assert against the worked decision table in contracts/reuse-decision-semantics.md; (c) no
  same-gate entry (Domain or Check differs, and the empty store) ⇒ `Recompute NoPriorEvidence`, distinct from
  `InputsChanged` (US2 #2, Edge: no entry shares gate); (d) property over FsCheck inputs: every `Recompute`
  carries `NoPriorEvidence` or a **non-empty** `InputsChanged`, and `InputsChanged` **never** contains
  `CheckIdentity`/`DomainIdentity` (SC-003, US2 #3, FR-006).
- [X] T014 [US2] Reconcile `decide`'s step-2 cause branch (T012) against contracts/reuse-decision-semantics.md
  until T013 is green: when no entry fully matches, find the most-recent entry whose **Check AND Domain** both
  equal the candidate's ⇒ `Recompute (InputsChanged (FreshnessKey.diff candidate e.Inputs))`; if none shares
  the gate ⇒ `Recompute NoPriorEvidence`. The `diff` is non-empty by construction (step 2 is reached only on
  non-match) and excludes the identity categories (the chosen entry agrees on Check/Domain). Adjust `decide`,
  never the tests; keep FreshnessKey-only.

**Checkpoint**: US1 + US2 — reuse fires exactly on a full match, and every recompute names why (no prior
evidence vs the specific changed categories).

---

## Phase 5: User Story 3 — Recording is pure, deterministic, and de-duplicating (Priority: P2)

**Goal**: `record` makes a just-recorded entry immediately reusable, refreshes (never duplicates) on
matching inputs, leaves non-matching entries independently reusable, does not mutate its input store, and
replays deterministically.

**Independent Test**: `record` into `empty` ⇒ a matching candidate decides `Reuse` that ref. `record` again
under matching inputs with a new ref ⇒ matching candidate decides `Reuse` the **new** ref with no duplicate
entry. `record` under non-matching inputs ⇒ both entries independently reusable. Same start store + same
recording sequence ⇒ identical decisions for all candidates. (US3 acceptance #1–#4.)

### Tests for User Story 3 (write first)

- [X] T015 [P] [US3] `tests/.../RecordTests.fs` — (a) reflexive reuse: `decide i (record i E1 empty) = Reuse
  E1` (US3 #1, SC-005); (b) refresh/de-dup most-recent-wins: a matching candidate against `record i E2 (record
  i E1 s)` decides `Reuse E2`, and `entries` holds no duplicate for `i` (count unchanged vs single record)
  (US3 #2, FR-008); (c) independence: `record` under inputs matching no existing entry leaves every prior
  entry reusable by its own matching candidate, and the new one reusable too (US3 #3, FR-008); (d) no
  mutation: the input `ReuseStore` value is unchanged after `record` (compare `entries` of the original)
  (FR-007); (e) replay determinism: the same start store + same record sequence yields a store giving
  identical `decide` results for representative and FsCheck candidates (US3 #4, SC-005).

### Implementation for User Story 3

- [X] T016 [US3] Implement `record` per contracts/reuse-decision-semantics.md in `EvidenceReuse.fs`: `let
  (ReuseStore es) = store in ReuseStore ({ Inputs = inputs; Evidence = evidence } :: (es |> List.filter (fun e
  -> not (FreshnessKey.matches inputs e.Inputs))))` — drop any prior full-match, cons the new entry at the
  head (newest-first), return a new value (no mutation). FreshnessKey-only; no clock/filesystem/git. Run
  T015: green.

**Checkpoint**: All three stories functional and independently testable — decide, explain, and record compose.

---

## Phase 6: Cross-cutting evidence & Tier-1 surface obligations

**Purpose**: Determinism, purity, the surface baseline + scope guard, and the no-regression promise.

- [X] T017 [P] `tests/.../DeterminismTests.fs` — `decide c s` called twice yields structurally identical
  results for representative and FsCheck-generated `(c, s)` (SC-002); reordering or duplicating the
  `CoveredArtifacts` in the candidate **or** in a stored entry never changes the decision (set semantics
  inherited from F029 `matches`/`diff` — SC-002, Edge: covered-artifact order/dup).
- [X] T018 [P] `tests/.../PurityTests.fs` — a fixed `decide` result and a fixed `record` result (compared via
  `entries`/`referenceValue` and resulting decisions) are identical when recomputed after changing
  `Environment.CurrentDirectory` and after creating/deleting an unrelated temp file (and across repeated
  calls), demonstrating no clock/cwd/filesystem influence (SC-006).
- [X] T019 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (F029/AuditJson precedent): render
  the assembly's public surface, compare to `surface/FS.GG.Governance.EvidenceReuse.surface.txt` with the
  `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `EvidenceReuse`) export and
  no helper leaks; **scope-hygiene**: referenced assemblies are only `FSharp.Core`,
  `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config` (transitive), and BCL (`System.*`/`netstandard`/
  `mscorlib`) — NOT `Gates`/`Snapshot`/`Route`/`Routing`/`Findings`/`Adapters.*`/`Host`/`Cli`/`Ship`/
  `Enforcement`/`AuditJson` (plan D1, contracts negative scope guard).
- [X] T020 Generate the committed baseline `surface/FS.GG.Governance.EvidenceReuse.surface.txt` by running the
  suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`EvidenceReuse.fsi` to
  confirm it contains exactly the intended surface (the two modules, five vals, five types). Commit it.
  (After T019.)
- [-] T021 [P] README cores pointer — **SKIPPED**: verified the README's enumerated core list still stops at
  F018 (Gates, README.md:47) and F019–F029 deliberately did not extend it (the F029 T024 SKIP precedent).
  Adding only F030 would be inconsistent with the eleven intervening unlisted cores; SKIP per the task's
  stated conditional rather than partially extend the list.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned.

---

## Phase 7: Validation & polish

- [X] T022 Run `dotnet test tests/FS.GG.Governance.EvidenceReuse.Tests/...` — all suites green; capture the
  run as evidence (Principle V).
- [X] T023 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-007): existing
  projects' tests and **every** existing `surface/*.surface.txt` baseline (including F029) are unchanged; only
  the new project's tests and the new surface baseline are added. Confirm `src/**` and existing `surface/**`
  show no diff.
- [X] T024 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F030 section's printed results
  now match the real `decide`/`record`/`entries` output (Principle I evidence, closes T007).
- [X] T025 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality. Confirm the `CLAUDE.md` plan pointer already targets
  `specs/030-evidence-reuse-decision/plan.md` (it does — no change needed).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist
  to compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `decide`'s reuse-iff-match (the MVP) + the accessors.
- **Phase 4 (US2)** → after **US1** specifically (it pins/refines the cause branch of the `decide` built in
  T012). Independently testable once the cause branch lands (T014).
- **Phase 5 (US3)** → after Phase 2; `record` shares F029 `matches` with `decide`, so practically after US1
  (T012) so reflexive-reuse tests can call `decide`. Independently testable once `record` lands.
- **Phase 6 (cross-cutting)** → after the operations exist (US1–US3); T020 after T019.
- **Phase 7 (validation)** → last.

### Within each story

- Tests are written first and must FAIL against the Phase-2 stubs before the implementation task greens them.
- `Model.fs` types before the operations that use them (already in place from Phase 2).

### Parallel opportunities

- Phase 1: T002 ‖ (T001→T003).
- Phase 2: T007 ‖ T009 (after T004–T006 land the `.fsi`+stubs; T008 Support precedes the story tests).
- Within a story, the `[P]` test files are independent of each other; the implementation task follows them.
- Phase 6: T017 ‖ T018 ‖ T021 (T019→T020 sequential).
- Phase 7: T024 ‖ T025 (after T022–T023).

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`decide` reuse-iff-match + accessors) →
4. **STOP & VALIDATE**: full match ⇒ Reuse the ref; any single-category change ⇒ Recompute; empty store ⇒
Recompute — for every freshness-input category.

### Incremental delivery

US1 (reuse decision = MVP) → US2 (no-hide located cause hardening) → US3 (pure de-duplicating `record`) →
cross-cutting evidence (determinism/purity/surface) → full-suite validation. Each phase is independently
testable and adds value without breaking the previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.EvidenceReuse.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — pure total functions, no state/I/O (recorded once here, not per
  task). `ReuseStore` is a value transformed by `record`, not a stateful store.
- No mocks anywhere (Principle V); all inputs are real literal `FreshnessInputs`/`EvidenceRef`/`ReuseStore`.
  No `Synthetic` disclosure needed.
- F029 (`FreshnessKey`) is consumed verbatim — `FreshnessInputs`, `matches`, `diff`, `InputCategory`,
  `categoryToken`. Nothing in F029/Config is modified (FR-010, SC-007).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
