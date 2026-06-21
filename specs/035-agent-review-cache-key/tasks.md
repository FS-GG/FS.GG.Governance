---
description: "Task list for 035-agent-review-cache-key implementation"
---

# Tasks: Agent-Review Verdict Cache-Key Core

**Feature branch**: `035-agent-review-cache-key`
**Spec**: `specs/035-agent-review-cache-key/spec.md`
**Plan**: `specs/035-agent-review-cache-key/plan.md`

**Input**: Design documents from `/specs/035-agent-review-cache-key/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/agent-review-key-api.md,
contracts/agent-review-key-format.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the
spec is itself a determinism / injectivity / totality / set-semantics contract — the tests *are* the
deliverable's proof.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`)
for traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no
new third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — four pure, total functions over supplied tokens, no state, no I/O, no
workflow (plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks.
The actual review (sending the prompt to a model) and the digest production are a later host edge, out of
scope.

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

- [X] T001 Create `src/FS.GG.Governance.AgentReviewKey/FS.GG.Governance.AgentReviewKey.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.AgentReviewKey`, `Version` `0.1.0`, `IsPackable=true`
  (override `Directory.Build.props` like FreshnessKey/Gates/Config). `<Compile>` order: `Model.fsi`,
  `Model.fs`, `AgentReviewKey.fsi`, `AgentReviewKey.fs`. **Single** `<ProjectReference>` to
  `../FS.GG.Governance.FreshnessKey/FS.GG.Governance.FreshnessKey.fsproj` (owns `RuleHash` + `ArtifactHash`,
  reused verbatim — FR-008, plan D1). **No third-party `PackageReference`** (FR-011, plan D1). Add a header
  comment mirroring the FreshnessKey `.fsproj` (pure total core; FreshnessKey-only graph; reuses F029
  `RuleHash`/`ArtifactHash`; no Gates/Snapshot/host/CLI coupling).
- [X] T002 [P] Create `tests/FS.GG.Governance.AgentReviewKey.Tests/FS.GG.Governance.AgentReviewKey.Tests.fsproj`
  — `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`,
  `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`);
  `<ProjectReference>`s to the new core and to `FS.GG.Governance.FreshnessKey` (for real `RuleHash`/
  `ArtifactHash` literals). `<Compile>` order: `Support.fs`, `ComputeTests.fs`, `SetSemanticsTests.fs`,
  `DiffTests.fs`, `InjectivityTests.fs`, `DeterminismTests.fs`, `PurityTests.fs`, `SurfaceDriftTests.fs`,
  `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh
  GUIDs and the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies
so the library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins until
this phase is complete.**

- [X] T004 Write `src/FS.GG.Governance.AgentReviewKey/Model.fsi` — the SOLE public surface for the types
  (data-model.md): `open FS.GG.Governance.FreshnessKey.Model` (brings `RuleHash`/`ArtifactHash`); the new
  opaque newtypes `ModelId`, `ModelVersion`, `ReviewerPromptHash`, `ModelConfig`, `QuestionText` (each `of
  string`); the `AgentReviewInputs` record (seven fields + order per data-model.md, `ReviewedArtifacts:
  ArtifactHash list` compared as a set); the `CacheKey` newtype (`CacheKey of string` — **not** `Key`, to
  avoid collision with F029's `Key` brought in by the `open`, research D3); the closed seven-case `ReviewInput`
  DU; and `val inputToken: input: ReviewInput -> string`. Curated doc comments in the FreshnessKey `.fsi`
  style. The `CacheKey` doc comment MUST state it is the computed canonical fingerprint distinct from F029's
  `Key`. No access modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.AgentReviewKey/AgentReviewKey.fsi` — the SOLE public surface for the
  operations (contracts/agent-review-key-api.md): `val compute: inputs: AgentReviewInputs -> CacheKey`,
  `val matches: a: AgentReviewInputs -> b: AgentReviewInputs -> bool`, `val diff: a: AgentReviewInputs -> b:
  AgentReviewInputs -> ReviewInput list`, `val value: key: CacheKey -> string`, each with doc comments stating
  purity / totality / set-semantics and the laws (`matches a b = (compute a = compute b)`; `diff a b = [] ⇔
  matches a b`; injective across inputs).
- [X] T006 Add stub `src/FS.GG.Governance.AgentReviewKey/Model.fs` and
  `src/FS.GG.Governance.AgentReviewKey/AgentReviewKey.fs` — real type definitions in `Model.fs` (newtypes /
  record / DU are data, define them fully); `inputToken` and the four operations as `failwith "not
  implemented"` stubs so the assembly compiles. No `private`/`internal`/`public` modifiers (Principle II).
  Confirm `dotnet build src/FS.GG.Governance.AgentReviewKey/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F035 design-first section to `scripts/prelude.fsx` — `#r` the new Debug DLL and the
  FreshnessKey DLL; construct the literal `AgentReviewInputs` from quickstart.md; `printfn` the intended
  `compute |> value` (the canonical worked-example key string), the order/dup-invariant match (reordered
  artifacts ⇒ `matches = true`, `diff = []`), a `ModelVersion`-flipped non-match naming `[ModelVersionInput]`
  / `["modelVersion"]`, and the empty-artifact-set key (`…\nart=0;\n…`). This is the Principle-I FSI proof; it
  documents the shape even while bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.AgentReviewKey.Tests/Support.fs` — real, literally-constructible
  builders (Principle V, no mocks): a `baseInputs` value built from real `ModelId`/`ModelVersion`/
  `ReviewerPromptHash`/`ModelConfig`/`QuestionText` and real F029 `RuleHash`/`ArtifactHash`s; `with`-style
  helpers to vary one input at a time; an `allInputs` list (the seven `ReviewInput` cases) for table-driven
  distinction tests; FsCheck generators for `AgentReviewInputs` (and for shuffled/duplicated artifact lists);
  and the `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` / `repoRoot` helper copied from the
  FreshnessKey/AuditJson `Support.fs` precedent. No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.AgentReviewKey.Tests/Main.fs` — the Expecto entry point
  (`[<EntryPoint>] runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now
FAILS only because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — Key a verdict by its full judge / prompt / check / artifact identity (Priority: P1) 🎯 MVP

**Goal**: `compute` (+ `value`) turns the seven supplied inputs into one byte-stable, injective `CacheKey`:
identical inputs ⇒ byte-identical key; any single differing input ⇒ a different key; no token can spoof a
field boundary.

**Independent Test**: Build two equal input sets ⇒ keys byte-equal; flip each of the seven inputs in turn ⇒
keys differ; compute the worked example ⇒ byte-exact match to the format contract.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../ComputeTests.fs` — (1) the key carries all seven inputs and changing exactly
  one input changes the key, table-driven over the seven inputs incl. one changed reviewed-artifact hash
  (SC-001, US1 #1–#3); (2) the **worked-example byte-pin**: `value (compute exampleInputs)` equals the exact
  multi-line string in contracts/agent-review-key-format.md (`mid=13:claude-opus-4\n…\nart=2;2:h1;2:h2\nq=13:
  explains API?`), and the empty-artifact-set variant renders `…\nart=0;\n…` (SC-002 anchor, Edge cases).
- [X] T011 [P] [US1] `tests/.../InjectivityTests.fs` — moving the same opaque string between two different
  inputs (e.g. model id text vs question text) yields different keys; tokens containing `:`/`=`/`;`/`\n`
  cannot spoof a field boundary or bleed across a segment (length-prefix guarantee); an empty token encodes to
  a distinct segment that never collides with absence or another field; FsCheck property: distinct input sets
  ⇒ distinct keys (SC-005, FR-003, Edge cases).

### Implementation for User Story 1

- [X] T012 [US1] Implement `compute` (and `value` as the `CacheKey` unwrap) in `AgentReviewKey.fs` per
  contracts/agent-review-key-format.md — the tagged, length-prefixed segment encoder
  `<tag>=<byteLen>:<value>` (UTF-8 **byte** length, **no** presence digit since all seven inputs are
  required), the fixed seven-field order joined by `\n` with no trailing newline, and the reviewed-artifact
  **set** rendering under the `art` tag: dedup → ordinal/culture-invariant sort → `art=<count>;<len>:<v>;…`
  (empty set ⇒ `art=0;`). BCL string building only (`System.Text.StringBuilder`); no hashing, no I/O. Run
  T010–T011: green.

**Checkpoint**: US1 is functional — the byte-stable, injective key over the full judge/prompt/check/artifact
identity. MVP reached.

---

## Phase 4: User Story 2 — Detect and explain judge / prompt drift so stale verdicts are not reused (Priority: P1)

**Goal**: `matches` decides a key hit; `diff` (+ `inputToken`) makes a cache miss explainable by naming
exactly the inputs that changed — the observable face of *"a judge or prompt change invalidates prior cached
verdicts."*

**Independent Test**: For two input sets, `matches` is true IFF all seven inputs are equal; for two differing
sets, `diff` returns exactly the changed inputs in fixed order (none hidden, no equal input reported), incl.
artifact-only and several-inputs-at-once cases; `matches a b ⇔ (diff a b = [])`.

### Tests for User Story 2 (write first; must FAIL against stubs)

- [X] T013 [P] [US2] `tests/.../DiffTests.fs` — reflexive: `matches x x = true` and `diff x x = []`; for each
  single-input variant from `allInputs`, `matches base variant = false` and `diff base variant = [thatInput]`
  (all seven, incl. a judge-identity change — model id/version/config — and a prompt/question change, US2
  #1–#3); a multi-input variant returns exactly the changed set in fixed encoding order; the artifact-only
  case names `ReviewedArtifactsInput`; FsCheck properties `matches a b = (compute a = compute b)` and
  `matches a b = (diff a b = [])` (predicate/key and diff/predicate agreement, SC-003, FR-004/FR-005). Also
  assert `inputToken` is total and injective over all seven `ReviewInput` cases, each equal to its value in
  the **`inputToken` table** in contracts/agent-review-key-api.md (`modelId`/`checkHash`/`reviewedArtifacts`/…
  — the human-readable vocabulary, intentionally **distinct** from the terse encoding tags `mid`/`chk`/`art`
  in agent-review-key-format.md).
- [X] T014 [US2] Implement `inputToken` (Model.fs) and `matches` + `diff` (AgentReviewKey.fs) — `matches a b`
  bound as `compute a = compute b` (predicate and key cannot disagree); `diff` compares the two inputs
  input-by-input in the fixed encoding order, comparing `ReviewedArtifacts` as a **set** (dedup+sort so
  reorder/dup is never reported), and returns the differing `ReviewInput` list. Run T013: green.

**Checkpoint**: US1 + US2 — the cache reuses a verdict exactly when every identity input agrees, and every
miss is explainable as a named judge / prompt / check / artifact change.

---

## Phase 5: User Story 3 — Deterministic, pure, set-correct over the reviewed-artifact hashes (Priority: P2)

**Goal**: Pin the trust guarantees of `compute`/`matches`/`diff`: byte-stable determinism, set semantics for
the reviewed artifacts, and purity (no clock/filesystem/git/environment/network read, no model invoked, no
bytes hashed).

**Independent Test**: Recompute a fixed key ⇒ byte-identical; reorder/duplicate the artifact set ⇒ same key,
match, and diff; recompute under a changed cwd / temp filesystem state ⇒ identical results.

### Tests for User Story 3 (write first; must FAIL against stubs)

- [X] T015 [P] [US3] `tests/.../SetSemanticsTests.fs` — reordering `ReviewedArtifacts` ⇒ identical key,
  `matches = true`, `diff = []`; a duplicated artifact hash ⇒ identical key to the deduped set; the empty
  artifact set keys to a distinct, unambiguous value (≠ any one-artifact set) and is never treated as
  "absent"; FsCheck over shuffled/duplicated artifact lists (SC-002, FR-006, Edge cases).
- [X] T016 [P] [US3] `tests/.../DeterminismTests.fs` — `value (compute x)` is byte-identical on repeat;
  `matches`/`diff` over the same inputs are identical on repeat; FsCheck property over generated
  `AgentReviewInputs` (SC-004).
- [X] T017 [P] [US3] `tests/.../PurityTests.fs` — the key for a fixed input is byte-identical when recomputed
  after changing `Environment.CurrentDirectory` and after creating/deleting an unrelated temp file (and across
  repeated calls at different times), demonstrating no clock/cwd/filesystem influence and no model invocation
  (SC-006, FR-007).

### Implementation for User Story 3

- [X] T018 [US3] Reconcile `compute`'s set/encoding logic (T012) against the format contract until T015–T017
  are green: confirm the length-prefix uses UTF-8 **byte** length, the set dedup+sort is ordinal/culture-
  invariant, and the key is independent of enumeration order, cwd, and time. Adjust the encoder (not the
  tests) if any byte differs; keep BCL-only. (No new public surface — refines the US1 body.)

**Checkpoint**: All three stories functional and independently testable — a trustworthy, deterministic,
set-correct, explainable cache key.

---

## Phase 6: Cross-cutting evidence & Tier-1 surface obligations

**Purpose**: The surface baseline + scope guard and the no-regression promise. (Purity/totality/determinism
properties land with US3; totality is also exercised throughout via empty/separator-bearing/equal-text
tokens.)

- [X] T019 `tests/.../SurfaceDriftTests.fs` — the reflective surface test (FreshnessKey/AuditJson precedent):
  render the assembly's public surface, compare to `surface/FS.GG.Governance.AgentReviewKey.surface.txt` with
  the `BLESS_SURFACE=1` re-bless path; assert exactly the two public modules (`Model`, `AgentReviewKey`)
  export and no token/encoder/buffer helpers leak; **scope-hygiene**: referenced assemblies are only
  `FSharp.Core`, `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config` (transitive), and the BCL —
  **NOT** `Gates`/`Snapshot`/`Route`/`Adapters.*`/`Host`/`Cli` (plan D1, contracts negative scope guard,
  SC-007).
- [X] T020 Generate the committed baseline `surface/FS.GG.Governance.AgentReviewKey.surface.txt` by running
  the suite once with `BLESS_SURFACE=1`, then review the file by eye against `Model.fsi`/`AgentReviewKey.fsi`
  to confirm it contains exactly the intended surface. Commit it. (After T019.)
- [-] T021 [P] SKIPPED unless the README's enumerated core list has been kept current through F034 — confirm
  whether F030–F034 extended it; if they did **not** (the F029 precedent), adding only F035 would be
  inconsistent, so make no README change and record that rationale here. If they did, append the F035 line to
  match. **Rationale (confirmed 2026-06-21):** README.md carries NO per-core enumeration — F030–F034
  (`EvidenceReuse`/`Provenance`/`SensedMetadata`/…) added no per-core README lines, and `FreshnessKey` appears
  only as a *carried-gate* concept, not as a listed core. Adding only an F035 line would be inconsistent with
  the F029–F034 precedent, so no README change was made.

**Checkpoint**: Tier-1 obligations met; the public surface is pinned and scope-clean.

---

## Phase 7: Validation & polish

- [X] T022 Run `dotnet test tests/FS.GG.Governance.AgentReviewKey.Tests/...` — all green; capture the run as
  evidence.
- [X] T023 Run `dotnet test FS.GG.Governance.sln` and confirm the no-regression promise (SC-007): existing
  projects' tests and every existing `surface/*.surface.txt` baseline are unchanged (F029 `RuleHash`/
  `ArtifactHash` consumed verbatim, not modified); only the new project's tests are added.
- [X] T024 [P] Run `dotnet fsi scripts/prelude.fsx` end-to-end and confirm the F035 section's printed results
  now match the real `compute`/`matches`/`diff`/`value` output, incl. the byte-exact worked-example key
  (Principle I evidence, closes T007).
- [X] T025 [P] Walk `quickstart.md` top-to-bottom (build → FSI → test → re-bless → no-regression) and fix any
  drift between the guide and reality.
- [X] T026 Update `CLAUDE.md` plan pointer if needed (already points at this plan) and confirm the spec's
  scope boundaries hold: no verdict carried/stored, no cache store/lookup/invalidation, no model invoked, no
  hash computed from bytes, no CLI added (FR-009).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Foundational)** → after Phase 1; **BLOCKS** all stories (the `.fsi` + stubs + Support must exist
  to compile any test).
- **Phase 3 (US1)** → after Phase 2. Delivers `compute` + `value` (the MVP).
- **Phase 4 (US2)** → after **US1** specifically (`matches`/`diff` are defined in terms of `compute`).
- **Phase 5 (US3)** → after Phase 2; the determinism/set/purity properties pin the `compute` built in
  T012/T014, so practically after US1–US2. Independently testable once the operations land.
- **Phase 6 (cross-cutting)** → after the operations exist (US1–US3); T020 after T019.
- **Phase 7 (validation)** → last.

### Within each story

- Tests are written first and must FAIL against the Phase-2 stubs before the implementation task greens them.
- `Model.fs` types / `inputToken` before the operation that uses them (T014).

### Parallel opportunities

- Phase 1: T002 ‖ (T001→T003).
- Phase 2: T007 ‖ T008 ‖ T009 (after T004–T006 land the `.fsi` + stubs).
- Within a story, the `[P]` test files are independent of each other; the implementation task follows them.
- Phase 5: T015 ‖ T016 ‖ T017 (then T018 reconciles).
- Phase 6: T019 → T020 sequential; T021 ‖.
- Phase 7: T024 ‖ T025 (after T022–T023).

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (`compute` + `value`) →
4. **STOP & VALIDATE**: byte-identical key for identical inputs, a different key for any single changed input,
   and the worked-example byte-pin holds.

### Incremental delivery

US1 (the byte-stable injective key = MVP) → US2 (explainable `matches`/`diff` drift detection) →
US3 (determinism / set-semantics / purity hardening) → cross-cutting surface + scope obligations →
full-suite no-regression validation. Each phase is independently testable and adds value without breaking the
previous.

---

## Notes

- Tier 1 throughout: any public-surface change requires re-blessing
  `surface/FS.GG.Governance.AgentReviewKey.surface.txt` (`BLESS_SURFACE=1`).
- `[P]` = different files, no in-phase dependency. `[USx]` maps a task to its spec user story.
- Principle IV (Elmish/MVU) is **N/A** — pure total functions, no state/I/O (recorded once here, not per
  task). The actual review and digest production are a later host edge, out of scope (FR-009).
- This core references **only** the sibling pure core `FreshnessKey` (for `RuleHash`/`ArtifactHash`, FR-008)
  and adds **no** new third-party dependency (FR-011).
- No mocks anywhere (Principle V); all inputs are real literal `AgentReviewInputs` incl. real F029
  `RuleHash`/`ArtifactHash`s. No `Synthetic` disclosure needed.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
