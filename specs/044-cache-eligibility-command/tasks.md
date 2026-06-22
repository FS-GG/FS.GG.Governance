---
description: "Task list for Cache-Eligibility Host Command (F044)"
---

# Tasks: Cache-Eligibility Host Command (Sense → Resolve → Evaluate → Emit)

**Input**: Design documents from `/specs/044-cache-eligibility-command/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/cache-eligibility-command-cli.md ✅, contracts/cache-eligibility-artifacts.md ✅

**Tier**: Tier 1 (contracted change — new public CLI surface + assembly). Tests are **mandatory**
(Principle V): the spec requests the three-tier `RouteCommand` shape (pure `Loop`, faked-port
`Interpreter`, one real-`git` end-to-end). Tasks omitting `[T1]`/`[T2]` inherit the feature tier (T1).

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.
Stories map to spec user stories (US1 P1 / US2 P2 / US3 P3). MVP = Phase 1 → 2 → 3 (US1).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability; unlabeled = shared infrastructure
- Exact repo-root-relative file paths in every description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new host project and test project skeletons and wire them into the build.
Nothing existing is edited beyond the solution file and `CLAUDE.md` (SC-007).

- [X] T001 Create `src/FS.GG.Governance.CacheEligibilityCommand/FS.GG.Governance.CacheEligibilityCommand.fsproj` —
  `Exe`, `net10.0`, `PackAsTool=true`, `ToolCommandName=fsgg`; `ProjectReference`s to the F022 selection cores
  (`Config`, `Snapshot`, `Routing`, `Findings`, `Gates`, `Route`) and the cache cores (`FreshnessResolution`,
  `CacheEligibility`, `CacheEligibilityJson`, `EvidenceReuse`); compile order `Loop.fsi`→`Loop.fs`→
  `Interpreter.fsi`→`Interpreter.fs`→`Program.fs`. No new third-party `PackageReference` (C6). Mirror
  `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj`.
- [X] T002 [P] Create `tests/FS.GG.Governance.CacheEligibilityCommand.Tests/FS.GG.Governance.CacheEligibilityCommand.Tests.fsproj` —
  references the command project **and** the cores (for genuine expected-report computation, no core mocks) plus
  the central test packages (Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).
  Mirror `tests/FS.GG.Governance.RouteCommand.Tests/...Tests.fsproj`; declare the source-file `<Compile>` order
  (`Support.fs` first, `Main.fs` last).
- [X] T003 [P] Add both new projects to `FS.GG.Governance.sln`.
- [X] T004 [P] Point the SPECKIT plan reference in `CLAUDE.md` at
  `specs/044-cache-eligibility-command/plan.md`.

**Checkpoint**: `dotnet sln list` shows the two projects; solution restores (bodies may be stubs).

---

## Phase 2: Foundational (Blocking — the contracts + design proof)

**Purpose**: Author the public `.fsi` surface (Principle I/II), prove it in FSI before any `.fs` body
(Principle I), and build the test fixtures every story depends on. **Blocks all user stories.**

**⚠️ CRITICAL**: the `.fsi` files are the contract; no story test can compile until they exist.

- [X] T005 Author `src/FS.GG.Governance.CacheEligibilityCommand/Loop.fsi` — the pure surface from
  contract C4 / data-model.md: `ScopeSelector`, `OutputFormat`, `RunRequest`, the NEW local `UsageError`
  (`UnknownFlag`/`MissingValue`/`PathsAndSinceTogether`/`EmptyPaths`/`BadFormat`, mirroring
  `RouteCommand.Loop.UsageError`), `Phase`, `ExitDecision`, `ArtifactKind`, `Effect`
  (`SenseScope`/`LoadCatalog`/`SenseFreshness`/`LoadStore`/`WriteArtifact`/`EmitSummary`), `Msg`
  (`Begin`/`Sensed`/`Loaded`/`FreshnessSensed`/`StoreLoaded`/`Wrote`/`Emitted`), `Model`, and the vals
  `parse`/`init`/`update`/`render`/`exitCode`. Reuses merged core types verbatim (FR-012) — redefines none.
- [X] T006 Author `src/FS.GG.Governance.CacheEligibilityCommand/Interpreter.fsi` — contract C5: the NEW
  `FreshnessSensor` record (`SenseRuleHash`/`SenseGeneratorVersion`/`SenseCoveredArtifacts`/`SenseCommandVersion`,
  each `… option`, with the `Some [] = sensed-empty` / `None = unsensed` semantics in a doc-comment), the
  `StoreReader` alias (`Ok None = absent ⇒ empty`), the `Ports` record, and `val realPorts`/`val run`.
- [X] T007 Append an F044 section to `scripts/prelude.fsx` — design-first FSI proof (Principle I): load the
  cores + the new `.fsi`, construct a `RunRequest`, fold a fixed `Msg` sequence through `update`, and show the
  computed `CacheDoc`/`UnresolvedDoc` strings — **before** any `Loop.fs`/`Interpreter.fs` body exists.
- [X] T008 [P] Create `tests/FS.GG.Governance.CacheEligibilityCommand.Tests/Main.fs` — Expecto entry point
  (mirror `tests/FS.GG.Governance.RouteCommand.Tests/Main.fs`).
- [X] T009 Create `tests/FS.GG.Governance.CacheEligibilityCommand.Tests/Support.fs` — shared fixtures (no core
  mocks, Principle V): real F018 `Gate`s with five-field `FreshnessKey`s, a minimal valid `.fsgg` catalog text,
  a fake `FreshnessSensor` returning fixed facts (with a knob to make a fact `None`), a canned `Snapshot.Ports`
  with a fixed `RepoSnapshot.Range`, an in-memory `StoreReader` (and an absent-store case), a **real on-disk
  `fsgg.evidence-reuse-store/v1` fixture** (artifacts §A5) whose newest matching entry makes a chosen gate
  reusable (built via the public F029/F030 constructors — for T012/T028/T030), a capturing `Write`/`Out`, and an
  **expected-report computer** that calls the genuine `FreshnessResolution.resolve` / `CacheEligibility.evaluate`
  / `CacheEligibilityJson.ofReport` so assertions compare against real core output. (Depends on T005/T006.)

**Checkpoint**: `.fsi` files compile against the cores; `prelude.fsx` runs; fixtures compile.

---

## Phase 3: User Story 1 — Emit the cache-eligibility verdict (Priority: P1) 🎯 MVP

**Goal**: Pointed at a repo, sense the selected gates, resolve, evaluate against the loaded store, and write a
schema-valid `cache-eligibility.json` (resolved gates in `GateId` order) + a deterministic summary; exit 0.

**Independent Test**: temp git repo + minimal catalog + a small change + a store making one selected gate
reusable ⇒ written `cache-eligibility.json` validates against `fsgg.cache-eligibility/v1`, lists exactly the
selected gates in `GateId` order, marks the prepared gate `reusable` and the rest `mustRecompute`.

### Tests for User Story 1 (write first — must FAIL before implementation) ⚠️

- [X] T010 [P] [US1] `tests/.../ParseTests.fs` — `parse`: verb stripped by `Program`; `--repo`/`--paths`/
  `--since`/`--store`/`--out`/`--format` parse to the right `RunRequest` (incl. `UnresolvedOut` derived from
  `CacheOut`'s `…unresolved.json` stem, C1); each `UsageError` case is a value, never a throw — `UnknownFlag`,
  `MissingValue`, `PathsAndSinceTogether` (`--paths` + `--since`), `EmptyPaths`, `BadFormat` (`--format` ≠
  human|json). Assert the **defaults** (U2/D8): `Repo = "."`, `CacheOut = <repo>/readiness/cache-eligibility.json`,
  `StorePath = <repo>/readiness/evidence-reuse.json`, scope = `DefaultRange`, format = `Human`; and the
  `--repo .` clean-relative form.
- [X] T011 [P] [US1] `tests/.../LoopTests.fs` — pure `init`/`update` over fixed selected gates + fixed
  `SensedFacts` + fixed `ReuseStore`: assert the **emitted effects** at each step (`init`→`SenseScope`/
  `LoadCatalog`; `Loaded(Valid)`→`SenseFreshness`+`LoadStore`; both-present→`WriteArtifact CacheArtifact` then
  `WriteArtifact UnresolvedArtifact` then `EmitSummary`) and that `CacheDoc` equals a genuine
  `CacheEligibilityJson.ofReport` of the expected report (L6), with entries in `GateId` order (SC-001). Assert the
  reusable gate carries its `EvidenceRef` and others carry the correct `RecomputeCause` (SC-002). Cover the
  empty-selection case → valid empty-entry doc, exit 0 (US1 scenario 3).
- [X] T012 [P] [US1] `tests/.../InterpreterTests.fs` (US1 slice) — drive `Interpreter.run` over faked ports
  (in-memory `Files`, canned `Git`, fake `Freshness`, in-memory `Store`, capturing `Write`/`Out`): assert the
  captured `CacheArtifact` content equals a genuine `ofReport` of the expected report, and that an **absent**
  store (`StoreReader` ⇒ `Ok None`) yields `empty` ⇒ every resolvable gate `MustRecompute NoPriorEvidence`
  (L7, US1 scenario 2), exit 0.

### Implementation for User Story 1

- [X] T013 [US1] Implement `src/FS.GG.Governance.CacheEligibilityCommand/Loop.fs` — `parse`, `init`, the
  `update` pipeline (selection via the verbatim F022 call-sequence `Routing.route`→`Gates.buildRegistry`→
  `Findings.findUnknownGovernedPaths`→`Route.select`, set `SelectedGates = SelectedGates |> List.map (.Gate)`;
  then `FreshnessResolution.resolve`→`List.choose candidate`→`CacheEligibility.evaluate store`→
  `CacheEligibilityJson.ofReport`), `render` (human/JSON summary), `exitCode`. Both artifact strings computed in
  `update` **before** any `WriteArtifact` is emitted (C4 law). No access modifiers (Principle II). Makes T010/
  T011 pass.
- [X] T014 [US1] Implement the resolved/evaluate/project tail wiring in `Loop.fs` so the `Interpreter`-driven
  US1 path (T012) passes: `Wrote(_,Ok)` acks advance `Projected`→`Persisted`→`EmitSummary`; absent-store path
  reaches exit 0 with `NoPriorEvidence`. (Same file as T013 — sequential after it.)
- [X] T015 [US1] Implement `src/FS.GG.Governance.CacheEligibilityCommand/Interpreter.fs` (US1 slice) — `run`
  driving `init`→`update*` to `Done`, assembling `SensedFacts` from `RepoSnapshot.Range` (base/head, D4) + the
  `FreshnessSensor`; a real `realPorts` wiring `Config`/`Snapshot`/a BCL-crypto `FreshnessSensor`/the read-only
  `StoreReader`/atomic `Write`/`Console.Out`. The real `StoreReader` deserializes `fsgg.evidence-reuse-store/v1`
  (artifacts §A5) into F030 `ReuseStore` via the public F029/F030 constructors only — no hash/key computed
  (FR-013, L12); absent file ⇒ `Ok None` ⇒ `empty`; malformed present ⇒ `Error` ⇒ `ToolError`. Every `step`
  guarded (exception ⇒ failure `Msg`); `run` never throws (C5 law). Makes T012 pass.
- [X] T016 [US1] Implement `src/FS.GG.Governance.CacheEligibilityCommand/Program.fs` — thin entry: strip the
  `cache-eligibility` verb, `parse`, build `realPorts repo`, `run`, print, `Environment.Exit (exitCode …)`.

**Checkpoint**: US1 fully testable — a real change emits a schema-valid `cache-eligibility.json` + summary, exit 0.

---

## Phase 4: User Story 2 — Honest unresolved attribution, recompute by default (Priority: P2)

**Goal**: A gate whose required fact cannot be sensed is surfaced recompute-by-default in the
`cache-eligibility.unresolved.json` sidecar, naming **exactly and only** the missing facts; never reusable,
never dropped. Sensed-empty is distinguished from unsensed; absent command never causes unresolved.

**Independent Test**: temp repo where one selected gate's covered artifact is absent ⇒ that gate appears in the
sidecar with the missing fact named, is absent from the reusable set, others unaffected.

### Tests for User Story 2 (write first — must FAIL) ⚠️

- [X] T017 [P] [US2] `tests/.../UnresolvedTests.fs` — a gate missing a sensed fact (`None`) ⇒ `update` produces
  a `UnresolvedDoc` whose `unresolved` array names exactly and only the missing facts via `missingFactToken`, in
  `MissingFact` enum order, gate keyed by `gateIdValue`, entries in `GateId` order; the gate is **absent** from
  `CacheDoc` (L3/L6, SC-003, A4 honesty). Assert the sidecar is **always** written, empty as `"unresolved": []`
  when all resolve (A2).
- [X] T018 [P] [US2] `tests/.../SensedEmptyTests.fs` — `SenseCoveredArtifacts g = Some []` resolves and is
  evaluated (sensed-empty ≠ unsensed, L4, SC-005); a gate with `FreshnessKey.Command = None` resolves with
  absent command + absent command version, evaluated normally, **never** unresolved on that basis (L5, US2
  scenario 2). Contrast with `= None` ⇒ unresolved on covered artifacts.
- [X] T019 [P] [US2] Extend `tests/.../InterpreterTests.fs` — over faked ports, the written
  `cache-eligibility.unresolved.json` names exactly the missing facts and the resolved/unresolved partition of
  selected gates is exhaustive and disjoint (A4: every gate in exactly one document, duplicates preserved).

### Implementation for User Story 2

- [X] T020 [US2] Implement the unresolved-sidecar renderer in `src/.../Loop.fs` — deterministic render over
  `entries report |> List.filter (outcome = Unresolved)` using the public F043 `gateIdValue` + `missingFactToken`
  accessors to the `fsgg.cache-eligibility.unresolved/v1` shape (A2 fixed field order, `GateId` order); computed
  before either write; closed-DU `match` over `ResolutionOutcome`/`MissingFact`, wildcard-free. Makes T017
  pass.
- [X] T021 [US2] Confirm the `update` partition in `src/.../Loop.fs` routes every selected gate to exactly one
  of `CacheDoc` (resolved) / sidecar (unresolved) — `candidate` chooses resolved only (FR-005); sensed-empty and
  absent-command paths flow through `resolve` unchanged. Makes T018/T019 pass. (Same file as T020 — sequential.)

**Checkpoint**: US1 **and** US2 hold — no gate hidden, no fabricated reusable verdict.

---

## Phase 5: User Story 3 — Deterministic, reproducible artifact (Priority: P3)

**Goal**: Identical repo state + store ⇒ byte-identical `cache-eligibility.json`, `cache-eligibility.unresolved.json`,
and summary, regardless of cwd/process/clock/input order; per-gate entries in `GateId` order.

**Independent Test**: run twice over fixed repo state from two different working directories ⇒ byte-identical files.

### Tests for User Story 3 (write first — must FAIL) ⚠️

- [X] T022 [P] [US3] `tests/.../DeterminismTests.fs` — via faked ports: identical inputs ⇒ byte-identical
  `CacheDoc`, `UnresolvedDoc`, and `render` output; selected gates supplied in a **different discovery order**
  produce identical `GateId`-ordered entries (L9, SC-004); no clock/cwd/absolute-path content in any output (C3).
  Use FsCheck to permute input order.

### Implementation for User Story 3

- [X] T023 [US3] Audit/lock determinism in `src/.../Loop.fs` `render` and the sidecar renderer — sort/emit by
  `GateId`, surface no wall-clock (no F034 reference, D9), no absolute paths or cwd-dependent text. Makes T022
  pass. (Determinism of `cache-eligibility.json` itself is inherited from F042 `ofReport`, L6.)

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Cross-Cutting — Failure, Exit-as-Information, Surface, End-to-End, Docs

**Purpose**: The safe-failure / exit-code laws, the additive-surface guard, the one real-`git` proof, and the
quickstart validation. Spans all stories; do last.

- [X] T024 [P] `tests/.../FailureTests.fs` — Edge cases via faked failing ports: not-a-git-repo / unsensable
  scope ⇒ `InputUnavailable` (exit 3); declared catalog absent ⇒ exit 3; invalid catalog / **malformed present**
  store (`fsgg.evidence-reuse-store/v1` bad JSON / unknown schema id) / unwritable output / guarded sensing
  exception ⇒ `ToolError` (exit 4); on every non-zero exit **no partial artifact** is written (atomic
  temp+rename, FR-010/C2). Assert the failure emits a **structured stderr diagnostic** naming the
  missing/malformed input distinctly from a tool defect (Constitution VI, C3) — not just the exit code. Assert
  the **success overwrite** path: a pre-existing `cache-eligibility.json`/`.unresolved.json` is atomically
  replaced with the fresh document, no merge with stale content (Edge). `run` never throws (L11).
- [X] T025 [P] `tests/.../ExitInformationTests.fs` — exit **0** when every gate `MustRecompute` and/or some gates
  unresolved (FR-009, SC-006, L8); the command assigns no severity/profile/mode/enforcement/ship/provenance
  (L10) — assert only the cache documents are produced and nothing is written toward `route.json`/`audit.json`.
- [X] T026 Generate `surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt` via `BLESS_SURFACE=1`
  (Principle II) — the Tier-1 baseline for the `Loop` + `Interpreter` public surface.
- [X] T027 `tests/.../SurfaceDriftTests.fs` — reflective surface baseline assertion against T026's file with the
  `BLESS_SURFACE=1` re-bless path, **plus** the C6 reference-scope guard: the assembly references only the F022
  selection cores + the cache cores (+ transitive), and **no** `RouteJson`/`GatesJson`/`AuditJson`/`RouteCommand`,
  no new third-party package (SC-007/SC-008). Mirror `tests/FS.GG.Governance.RouteCommand.Tests/SurfaceDriftTests.fs`.
- [X] T028 `tests/.../EndToEndTests.fs` — the one real proof (Principle V): a real temp git repo + real `.fsgg`
  catalog + a **real on-disk `fsgg.evidence-reuse-store/v1`** (artifacts §A5) whose newest matching entry makes
  ≥1 selected gate `reusable` + `Interpreter.realPorts`; assert both artifacts validate against their schemas
  (`fsgg.cache-eligibility/v1`, `fsgg.cache-eligibility.unresolved/v1`), the reusable gate is marked `reusable`
  with its `EvidenceRef` over the **real** `StoreReader`+`evaluate` path (SC-002/L13 — not only faked ports), and
  the artifacts are **byte-identical** when re-run from a different working directory (SC-004). The
  `FreshnessSensor`/`git` are real here (faked in T011/T012/etc., disclosed `Synthetic` per Principle V); this
  tier proves the real path once.
- [X] T029 [P] `tests/.../StoreFormatTests.fs` — the read-only `StoreReader` over `fsgg.evidence-reuse-store/v1`
  (artifacts §A5): a well-formed document deserializes into the expected F030 `ReuseStore` (built independently
  via the public F029/F030 constructors — round-trip equality), entry order preserved; an **absent** path ⇒
  `Ok None`; a **malformed/unknown-schema** document ⇒ `Error` (FR-006/FR-013, L12). No hash/key computed by the
  reader.
- [X] T030 Run `quickstart.md` end-to-end (build, FSI-exercise, `dotnet test`, re-bless surface, pack + run the
  `fsgg cache-eligibility` verb) and confirm each step; fix any drift. Confirm SC-007 (`git status` shows no
  modified existing `src/`/`surface/`/merged-test file beyond the sln/CLAUDE.md/prelude additions).

**Checkpoint**: full solution builds clean, all prior tests green (SC-007), all SC-001…SC-008 covered.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1 — **blocks all stories** (the `.fsi` contracts + fixtures).
- **US1 (Phase 3)**: depends on Phase 2. MVP.
- **US2 (Phase 4)**: depends on Phase 2; builds on US1's `Loop.fs`/`Interpreter.fs` (sidecar renderer added to
  the same files) — sequence after US1 in practice (shared file), though conceptually independent.
- **US3 (Phase 5)**: depends on Phase 2; verifies determinism of the US1+US2 outputs — sequence after US2.
- **Cross-cutting (Phase 6)**: depends on the stories it asserts (failure/exit/surface/e2e) — do last.

### Within Each User Story

- Tests (T010–T012, T017–T019, T022) are written FIRST and must FAIL before the implementation tasks.
- `Loop.fsi`/`Interpreter.fsi` (Phase 2) before any `.fs` body (Principle I/II).
- `Loop.fs` before `Interpreter.fs` before `Program.fs` (compile order, Principle IV boundary).

### Parallel Opportunities

- **Phase 1**: T002/T003/T004 are `[P]` (T001 first — the fsproj the others reference).
- **Phase 2**: T008 is `[P]`; T005/T006 are the contract (sequential-ish, same surface), T007/T009 follow.
- **US1 tests** T010/T011/T012 are `[P]` (different files); implementation T013→T014→T015→T016 is sequential
  (T013/T014/T015 chain through `Loop.fs`/`Interpreter.fs`).
- **US2 tests** T017/T018/T019 are `[P]`; **Phase 6** T024/T025/T029 are `[P]` (T026→T027 sequential; T028 then
  T030 last).

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 (Setup) → 2. Phase 2 (Foundational — `.fsi` + fixtures + FSI proof) → 3. Phase 3 (US1) →
**STOP & VALIDATE**: a real change emits a schema-valid `cache-eligibility.json` + summary, exit 0.

### Incremental Delivery

US1 (emit + reusable/must-recompute) → US2 (no-hide unresolved sidecar) → US3 (determinism) →
Phase 6 (failure/exit/surface/end-to-end). Each adds value without breaking the prior.

---

## Task Count Summary

| Group | Tasks | Count |
|---|---|---|
| Phase 1 — Setup | T001–T004 | 4 |
| Phase 2 — Foundational | T005–T009 | 5 |
| Phase 3 — US1 (MVP) | T010–T016 | 7 (3 test, 4 impl) |
| Phase 4 — US2 | T017–T021 | 5 (3 test, 2 impl) |
| Phase 5 — US3 | T022–T023 | 2 (1 test, 1 impl) |
| Phase 6 — Cross-cutting | T024–T030 | 7 |
| **Total** | | **30** |

**Suggested MVP scope**: Phase 1 + Phase 2 + Phase 3 (US1) — T001–T016.

## Notes

- `[P]` = different file, no incomplete-dependency in the phase.
- Never mark a failing task `[X]`; faked `FreshnessSensor`/`git` evidence in unit/interpreter tiers is disclosed
  `Synthetic` (Principle V), with the real path proven once in T028 (end-to-end).
- All merged `src/`/`surface/`/merged-test projects stay untouched (SC-007/SC-008) — verified in T027/T029.
</content>
</invoke>
