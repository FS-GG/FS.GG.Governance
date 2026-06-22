---
description: "Task list for 036-agent-verdict-invalidation implementation"
---

# Tasks: Agent-Reviewed Verdict Store & Invalidation Decision Core

**Feature branch**: `036-agent-verdict-invalidation`
**Spec**: `specs/036-agent-verdict-invalidation/spec.md`
**Plan**: `specs/036-agent-verdict-invalidation/plan.md`

**Input**: Design documents from `/specs/036-agent-verdict-invalidation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/verdict-store-api.md,
contracts/lookup-decision-semantics.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a validity / no-hide-explanation / determinism / totality contract — the tests *are* the
deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no
new third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — pure, total functions over a supplied `VerdictStore` value, no state, no
I/O, no workflow (plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter
tasks. The actual review (sending the prompt to a model), the minting/dereferencing of a `VerdictRef`, and the
persistence of the store are a later host edge, out of scope.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No
semantics yet.

- [X] T001 Create `src/FS.GG.Governance.VerdictReuse/FS.GG.Governance.VerdictReuse.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.VerdictReuse`, `Version` `0.1.0`, `IsPackable=true`
  (override `Directory.Build.props` like AgentReviewKey/FreshnessKey/Config). `<Compile>` order: `Model.fsi`,
  `Model.fs`, `VerdictReuse.fsi`, `VerdictReuse.fs`. **Single** `<ProjectReference>` to
  `../FS.GG.Governance.AgentReviewKey/FS.GG.Governance.AgentReviewKey.fsproj` (owns `AgentReviewInputs`,
  `ReviewInput`, `matches`, `diff`, reused verbatim — FR-010, plan D1; F029 `RuleHash`/`ArtifactHash` and
  F014 facts arrive transitively through it). **No third-party `PackageReference`** (FR-014, plan D1). Add a
  header comment mirroring the AgentReviewKey `.fsproj` (pure total core; AgentReviewKey-only graph; reuses
  F035 `matches`/`diff` verbatim; no Gates/Snapshot/EvidenceReuse/host/CLI coupling).
- [X] T002 [P] Create `tests/FS.GG.Governance.VerdictReuse.Tests/FS.GG.Governance.VerdictReuse.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`,
  no new package); `<ProjectReference>`s to the new core and to `FS.GG.Governance.AgentReviewKey` (for real
  `AgentReviewInputs` over real F029 `RuleHash`/`ArtifactHash` literals). `<Compile>` order: `Support.fs`,
  `LookupDecisionTests.fs`, `ExplanationTests.fs`, `EmptyStoreTests.fs`, `RecordTests.fs`,
  `DeterminismTests.fs`, `PurityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies
so the library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins until
this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.VerdictReuse/Model.fsi` — the SOLE public surface for the types
  (data-model.md): `open FS.GG.Governance.AgentReviewKey.Model` (brings `AgentReviewInputs`/`ReviewInput`,
  reused verbatim); the new opaque newtype `VerdictRef` (`VerdictRef of string`); the `CachedVerdict` record
  (`{ Inputs: AgentReviewInputs; Verdict: VerdictRef }`); the `VerdictStore` single-case DU (`VerdictStore of
  CachedVerdict list`, newest-first by `record` convention); the `IdentityGroup` DU (`JudgeIdentity` /
  `PromptIdentity` / `CheckArtifactIdentity`); `val inputGroup: input: ReviewInput -> IdentityGroup` (total
  over all seven cases per the data-model table); the `InvalidationCause` DU (`NoCachedVerdict` /
  `InputsChanged of ReviewInput list`); and the `LookupDecision` DU (`Valid of VerdictRef` / `Invalidated of
  InvalidationCause`). Curated doc comments in the AgentReviewKey `.fsi` style: `VerdictRef` is opaque/never
  dereferenced (FR-001); `InputsChanged` carries a non-empty list never containing `CheckHashInput`
  (data-model). No access modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.VerdictReuse/VerdictReuse.fsi` — the SOLE public surface for the
  operations (contracts/verdict-store-api.md): `val empty: VerdictStore`; `val record: inputs:
  AgentReviewInputs -> verdict: VerdictRef -> store: VerdictStore -> VerdictStore`; `val lookup: request:
  AgentReviewInputs -> store: VerdictStore -> LookupDecision`; `val entries: store: VerdictStore ->
  CachedVerdict list`; `val referenceValue: verdict: VerdictRef -> string`. Doc comments state
  purity/totality and the laws (`lookup` = `Valid` iff some entry `AgentReviewKey.matches` the request, else
  `Invalidated` with a located cause; `record` is non-mutating, de-duplicating, most-recent-wins; reads no
  clock/filesystem/git/environment/network).
- [X] T006 Add stub `src/FS.GG.Governance.VerdictReuse/Model.fs` and
  `src/FS.GG.Governance.VerdictReuse/VerdictReuse.fs` — real type definitions in `Model.fs` (newtype / record
  / DUs are data, define them fully); `inputGroup` and the operations (`empty`, `record`, `lookup`, `entries`,
  `referenceValue`) as `failwith "not implemented"` stubs (give `empty` a stub body too, e.g. via a value
  binding that `failwith`s, so the assembly compiles). No `private`/`internal`/`public` modifiers
  (Principle II). Confirm `dotnet build src/FS.GG.Governance.VerdictReuse/...` is clean under
  `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F036 design-first section to `scripts/prelude.fsx` — `#r` the new Debug DLL and the
  AgentReviewKey DLL; construct the literal `AgentReviewInputs` from quickstart.md (reusing F035's worked
  example), `record` it under a `VerdictRef`, and `printfn` the intended decisions: a matching request ⇒
  `Valid v`; a model-version bump ⇒ `Invalidated (InputsChanged [ModelVersionInput])` with `inputGroup` ⇒
  `JudgeIdentity`; a prompt-hash / question change ⇒ `PromptIdentity`; a different-check request ⇒
  `Invalidated NoCachedVerdict`; and re-recording the same inputs refreshes (no duplicate). This is the
  Principle-I FSI proof; it documents the shape even while bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.VerdictReuse.Tests/Support.fs` — real, literally-constructible
  builders (Principle V, no mocks): a `baseInputs` `AgentReviewInputs` value built from real F035
  `ModelId`/`ModelVersion`/`ReviewerPromptHash`/`ModelConfig`/`QuestionText` and real F029 `RuleHash`/
  `ArtifactHash`s; `with`-style helpers to vary one input at a time (one per F035 input); literal `VerdictRef`
  builders (incl. an empty-string `VerdictRef`); a `storeOf` helper — fold the `record`
  operation over `(AgentReviewInputs * VerdictRef)` pairs with the store as the accumulator, i.e.
  `List.fold (fun s (i, v) -> record i v s) empty` (NOT `List.fold record empty` — `record`'s store
  parameter is last, so it cannot be the fold accumulator directly) — or direct `VerdictStore`
  construction for hand-built stores; an `allChangedInputs` list mapping each varied input to
  its expected `ReviewInput` (the six non-check inputs, for table-driven tests); FsCheck generators for
  `AgentReviewInputs`, `VerdictRef`, and `VerdictStore` (and for shuffled/duplicated artifact lists); and the
  `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the
  AgentReviewKey/FreshnessKey `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.VerdictReuse.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Reuse a cached verdict only when the full judge / prompt / check / artifact identity is unchanged (Priority: P1) 🎯 MVP

**Goal**: `lookup` is `Valid` (carrying the matching entry's `VerdictRef`) **iff** some cached entry
`AgentReviewKey.matches` the request on every one of the seven inputs; a change in any single input ⇒
`Invalidated`. The dual of *"invalidate when judge or prompt identity changes."*

**Independent Test**: Build a store holding one cached entry; a request equal in all seven inputs ⇒ `Valid`
with that entry's reference; for each of the seven inputs in turn, a request differing in only that input ⇒
`Invalidated`. With several entries of which exactly one fully matches ⇒ `Valid` with that one's reference.
No host, no I/O, no model invoked.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../LookupDecisionTests.fs` — (1) **Valid iff all match**: a store with one
  entry for `baseInputs` + a request equal on all seven ⇒ `Valid` carrying exactly that entry's `VerdictRef`
  (SC-001, US1 #1); (2) **single-field change ⇒ Invalidated**, table-driven over **every** F035 input (model
  id, model version, reviewer prompt hash, model config, check hash, reviewed-artifact set, question text) —
  each differing-in-one-input request ⇒ `Invalidated` (SC-001, US1 #2); (3) **selection among several
  entries**: a store with multiple entries where exactly one fully matches ⇒ `Valid` with that one's
  reference, regardless of the others (US1 #3); (4) FsCheck property: `lookup r s` is `Valid ref` ⇔ some entry
  in `s` `AgentReviewKey.matches` `r` (and `ref` is that entry's verdict) (the "Valid iff all match" law).

### Implementation for User Story 1

- [X] T011 [US1] Implement `lookup` step 1 (the validity decision) in `VerdictReuse.fs` per
  contracts/lookup-decision-semantics.md — destructure `VerdictStore es`, `es |> List.tryFind (fun e ->
  AgentReviewKey.matches request e.Inputs)`: `Some e ⇒ Valid e.Verdict` (head-first over newest-first ⇒
  most-recent matching entry, deterministic, FR-005), `None ⇒` (defer to the `Invalidated` branch built in
  Phase 4 — temporarily `Invalidated NoCachedVerdict` is acceptable to make T010 pass without the located
  cause). Also implement `entries` (unwrap `VerdictStore`) and `referenceValue` (unwrap `VerdictRef`). BCL
  list/option handling only; no I/O. Run T010: green.

**Checkpoint**: US1 is functional — `lookup` reuses a cached verdict exactly when every identity input agrees,
and any single judge/prompt/check/artifact change is `Invalidated`. MVP reached.

---

## Phase 4: User Story 2 — A judge or prompt change visibly invalidates the prior verdict, and the cause is always explained (Priority: P1)

**Goal**: Every `Invalidated` carries a **located, non-hidden** cause — `NoCachedVerdict` when no entry shares
the request's check hash, else `InputsChanged (diff request priorEntry.Inputs)` — and `inputGroup` attributes
each changed input to `JudgeIdentity` / `PromptIdentity` / `CheckArtifactIdentity`, so a judge change and a
prompt change are each visible as such. The observable face of *"a judge or prompt change invalidates prior
cached verdicts."*

**Independent Test**: Against a store whose only entry shares the request's check but differs in one input,
`lookup` ⇒ `Invalidated (InputsChanged [thatInput])`, naming exactly the differing input and attributing it
(via `inputGroup`) to the right group — `JudgeIdentity` for a model id/version/config change, `PromptIdentity`
for a prompt hash/question change. Against a store with no entry for the request's work ⇒ `Invalidated
NoCachedVerdict`, never a spurious input diff.

### Tests for User Story 2 (write first; must FAIL against stubs)

- [X] T012 [P] [US2] `tests/.../ExplanationTests.fs` — (1) **located cause / no-hide**: for a same-check entry
  differing in exactly one non-check input, `Invalidated (InputsChanged [thatInput])` names exactly that input
  and no equal input, table-driven over the six non-check inputs (SC-003, US2 #1); a multi-input variant ⇒
  `InputsChanged` carries exactly the changed set in F035's fixed `diff` order (e.g. `[ModelVersionInput;
  ModelConfigInput]`), never containing `CheckHashInput` (data-model, decision-semantics worked table); (2)
  **judge/prompt attribution**: a judge-only change (model id/version/config) ⇒ every changed input's
  `inputGroup` is `JudgeIdentity`; a prompt-only change (prompt hash/question text) ⇒ `PromptIdentity`; an
  artifact change ⇒ `CheckArtifactIdentity` (SC-002, US2 #2); (3) **NoCachedVerdict vs InputsChanged**: a
  store with no entry sharing the request's check ⇒ `Invalidated NoCachedVerdict`; a same-check non-matching
  entry ⇒ `InputsChanged`, crisply distinct (SC-003, US2 #3); a **question-only** change ⇒ `InputsChanged
  [QuestionTextInput]`, **not** `NoCachedVerdict` (Edge case, research D5); (4) FsCheck: every `Invalidated`
  carries either `NoCachedVerdict` or a **non-empty** `InputsChanged` that never contains `CheckHashInput`
  (the "cause located" law, FR-006, SC-003); `inputGroup` is total over all seven `ReviewInput` cases, equal
  to the data-model table.

### Implementation for User Story 2

- [X] T013 [US2] Implement `inputGroup` (Model.fs) per the data-model table (model id/version/config ⇒
  `JudgeIdentity`; prompt hash/question text ⇒ `PromptIdentity`; check hash/reviewed artifacts ⇒
  `CheckArtifactIdentity`; total over all seven cases) and `lookup` step 2 (the located cause) in
  `VerdictReuse.fs` per contracts/lookup-decision-semantics.md — when step 1 finds no full match, `es |>
  List.tryFind (fun e -> e.Inputs.Check = request.Check)`: `Some e ⇒ Invalidated (InputsChanged
  (AgentReviewKey.diff request e.Inputs))` (head-first ⇒ most-recent same-work entry; the `diff` is non-empty
  and never contains `CheckHashInput` by construction), `None ⇒ Invalidated NoCachedVerdict`. BCL
  list/option handling only; no I/O. Run T012: green.

**Checkpoint**: US1 + US2 — `lookup` reuses iff every identity input agrees, and every miss is explainable as
a located `NoCachedVerdict` or a named, group-attributable judge / prompt / check-artifact change.

---

## Phase 5: User Story 3 — Recording a cached verdict is pure, deterministic, and de-duplicating (Priority: P2)

**Goal**: `record` returns a **new** store (no mutation) in which a just-recorded verdict is immediately
reusable by a matching request, refreshes a matching entry most-recent-wins rather than accumulating
duplicates, and leaves non-matching prior entries independently reusable. `empty` is the starting store.

**Independent Test**: `record` an entry into `empty` ⇒ a matching request decides `Valid` with that reference.
`record` again under matching inputs with a new reference ⇒ a matching request resolves `Valid` to the
refreshed reference and the store holds no duplicate for those inputs. `record` under non-matching inputs ⇒
both entries remain independently reusable. The input store value is unchanged.

### Tests for User Story 3 (write first; must FAIL against stubs)

- [X] T014 [P] [US3] `tests/.../RecordTests.fs` — (1) **reflexive validity**: `lookup i (record i v empty) =
  Valid v` (SC-005, US3 #1); (2) **refresh / de-dup most-recent-wins**: `record i v2 (record i v1 s)` ⇒ a
  matching request decides `Valid v2` and `entries` holds no duplicate entry for `i` (SC-005, US3 #2); (3)
  **independence**: recording under inputs matching nothing leaves every prior entry reusable by its matching
  request (SC-005, US3 #3); (4) **no mutation**: capture a `VerdictStore` value, `record` into it, assert the
  original value is structurally unchanged (FR-007); (5) **replay determinism**: the same start store + the
  same recording sequence yields a store giving identical lookup decisions for every request (SC-005, US3 #4);
  (6) FsCheck properties for refresh/de-dup and independence over generated inputs/refs/stores.

### Implementation for User Story 3

- [X] T015 [US3] Implement `empty` (`VerdictStore []`) and `record` in `VerdictReuse.fs` per
  contracts/lookup-decision-semantics.md — `let (VerdictStore es) = store in let kept = es |> List.filter
  (fun e -> not (AgentReviewKey.matches inputs e.Inputs)) in VerdictStore ({ Inputs = inputs; Verdict =
  verdict } :: kept)` — drop any superseded full-match entry, cons the new entry at the head (newest-first),
  no mutation. BCL list handling only; no I/O. Run T014: green.

**Checkpoint**: US1 + US2 + US3 — record→lookup composes cleanly; recording is pure, deterministic, and holds
at most one entry per matching-input class.

---

## Phase 6: Cross-cutting guarantees — determinism, purity, totality, edges (covers all stories)

**Purpose**: Pin the trust guarantees of `lookup`/`record`: byte-stable determinism, set semantics for the
reviewed-artifact hashes (inherited from F035), purity (no clock/filesystem/git/environment/network read, no
model invoked), and totality over degenerate inputs.

### Tests (write/extend; must pass once implementation is complete)

- [X] T016 [P] `tests/.../EmptyStoreTests.fs` — `lookup r empty = Invalidated NoCachedVerdict` for every `r`
  (example + FsCheck); an empty / unusual `VerdictRef` string recorded then looked up ⇒ `Valid` carrying it
  verbatim (`referenceValue` round-trips it), never parsed or rejected; totality: no `AgentReviewInputs` /
  `VerdictRef` / `VerdictStore` value throws (SC-001, SC-003, FR-012).
- [X] T017 [P] `tests/.../DeterminismTests.fs` — `lookup r s` asked twice yields identical results
  (determinism, SC-004); reordering or duplicating the `ReviewedArtifacts` in the request **or** in a stored
  entry never changes the decision (set semantics inherited from F035 `matches`/`diff`, SC-004); a hand-built
  store with multiple full-match entries resolves to the head-most deterministically (Edge case); an
  **empty-artifact-set transition** — a same-check entry with `ReviewedArtifacts = []` vs a request with
  `ReviewedArtifacts = [ArtifactHash ...]` (and the reverse) ⇒ `Invalidated (InputsChanged
  [ReviewedArtifactsInput])`, confirming the empty set is an ordinary value and a to/from-empty change is a
  real diff (lookup-decision-semantics.md edge table); FsCheck over shuffled/duplicated artifact lists.
- [X] T018 [P] `tests/.../PurityTests.fs` — decisions and records are identical when performed in different
  working directories, at different times, and with unrelated repository / filesystem state changed between
  operations; no model invoked, no clock/filesystem/git/environment/network read (SC-006, FR-009). Mirror the
  AgentReviewKey/FreshnessKey purity-test precedent (change cwd / touch a temp file between two computations,
  assert byte-equal results).

**Checkpoint**: The determinism, set-semantics, purity, and totality contracts hold across all stories.

---

## Phase 7: Surface governance & polish (Tier-1 baseline, scope hygiene)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal
(SC-007). Bless the baseline only after the surface is final.

- [X] T019 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F029/F030/F035 precedent):
  enumerate the public surface of `FS.GG.Governance.VerdictReuse` and compare byte-for-byte to
  `surface/FS.GG.Governance.VerdictReuse.surface.txt`, with the `BLESS_SURFACE=1` re-bless path; plus a
  **scope-hygiene** assertion that the assembly references **only** `FSharp.Core`,
  `FS.GG.Governance.AgentReviewKey`, `FS.GG.Governance.FreshnessKey` (transitive), `FS.GG.Governance.Config`
  (transitive), and the BCL — and **not** `Gates`, `Snapshot`, `Route`/`Routing`, `Findings`,
  `EvidenceReuse`, any `Adapters.*`, `Host`, `Cli`, `Ship`, `Enforcement`, or `AuditJson`
  (contracts/verdict-store-api.md scope guard, SC-007). **Note**: FR-011's *behavioral* negatives (no
  advisory→blocking promotion, no review-request / response-digest recording, no artifact/instruction
  separation, no CLI, no persistence/eviction) are satisfied **by construction** — the surface contains only
  `lookup`/`record`/`empty`/`entries`/`referenceValue`/`inputGroup` (no such operation exists to call) — and
  are guarded by this reference-graph + surface-drift check, **not** by a positive behavioral test (absence
  of a feature is not directly assertable).
- [X] T020 Generate and commit `surface/FS.GG.Governance.VerdictReuse.surface.txt` via `BLESS_SURFACE=1 dotnet
  test tests/FS.GG.Governance.VerdictReuse.Tests/...`; review the diff (exactly the two public modules — the
  `Model` types + `inputGroup`, and `empty`/`record`/`lookup`/`entries`/`referenceValue`; no helper leak) and
  commit it as part of the Tier-1 change. After this, T019 runs green without `BLESS_SURFACE`.
- [X] T021 [P] Update `CLAUDE.md` — point the SPECKIT plan reference at
  `specs/036-agent-verdict-invalidation/plan.md` (already the active plan; confirm it is the committed
  pointer). No other doc changes.
- [X] T022 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F036 section prints the expected decisions), and `dotnet test
  tests/FS.GG.Governance.VerdictReuse.Tests/...` — all green under `TreatWarningsAsErrors`. Confirm `dotnet
  test FS.GG.Governance.sln` over the existing projects is unchanged (no existing baseline rewritten, no
  existing test changes outcome — SC-007).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal; the full
solution builds and tests green; existing cores untouched.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface, FSI proof, and
  compiling stubs must exist before any story test can be written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP.
- **Phase 4 (US2)**: depends on Phase 2; builds on the `lookup` skeleton from Phase 3 (T013 completes the
  `Invalidated` branch T011 stubbed). Sequenced after US1 for a clean `lookup` body.
- **Phase 5 (US3)**: depends on Phase 2; `record` is independent of US1/US2 bodies but its tests (T014) read
  back through `lookup`, so land after US1/US2 for end-to-end assertions.
- **Phase 6 (cross-cutting)**: depends on US1–US3 implementations being complete (asserts behavior of finished
  `lookup`/`record`).
- **Phase 7 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi`
  unchanged through implementation).

### Within each story

- Tests are written FIRST and must FAIL against the Phase-2 stubs, then pass after the implementation task.
- `Model` type/`inputGroup` definitions precede the `VerdictReuse` operation bodies that use them.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T007 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is
  fixed by T001. T004/T005 (the two `.fsi` files) can be drafted together; T006 needs both; T008/T009 need the
  compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T010, T012, T014, T016, T017,
  T018 touch different test files. They share `Support.fs` (T008) as a prerequisite.
- **Phase 7**: T021 `[P]` (CLAUDE.md) is independent of the surface test; T019→T020→T022 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 6 tasks (T004–T009).
- **US1 (Phase 3)**: 2 tasks (T010 test, T011 impl) 🎯 MVP.
- **US2 (Phase 4)**: 2 tasks (T012 test, T013 impl).
- **US3 (Phase 5)**: 2 tasks (T014 test, T015 impl).
- **Cross-cutting (Phase 6)**: 3 tasks (T016–T018).
- **Surface & polish (Phase 7)**: 4 tasks (T019–T022).
- **Total**: 22 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — the project skeleton, the `.fsi` surface + FSI proof, and the
`Valid`-iff-all-seven-match `lookup` decision over a hand-built store. This alone delivers the feature's
load-bearing guarantee (the dual of *"invalidate when judge or prompt identity changes"*) and is independently
testable with no host, no I/O, and no model. US2 (located/attributed cause) and US3 (pure `record`) layer on
auditability and the record→lookup round-trip; Phases 6–7 pin determinism/purity and the Tier-1 surface.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify each story's tests FAIL against the Phase-2 stubs before implementing, then pass after.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects
  untouched (SC-007).
