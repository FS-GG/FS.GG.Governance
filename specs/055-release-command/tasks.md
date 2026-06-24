# Tasks: The `fsgg release` Host Command

**Input**: Design documents from `/specs/055-release-command/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/cli.md, contracts/release.schema.md, quickstart.md

**Tier**: Tier 1 (contracted change) — full chain owed: `.fsi`, surface baselines, test evidence, docs. Two new public projects (`FS.GG.Governance.ReleaseCommand`, `FS.GG.Governance.ReleaseJson`); F014/F053/F054 untouched. Tests are in scope (Constitution V; plan lists both `.Tests` projects).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`; foundational/setup/polish tasks carry no story tag
- Discipline (Constitution I/II): draft each public module's `.fsi` and exercise it in `scripts/prelude.fsx` **before** its `.fs` body; semantic tests call the loaded public surface (`Loop.parse`, `Interpreter.run`, `ReleaseJson.ofRelease`, `Declaration.parse`), never internals.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the two new `src` projects, their test projects, and wire the solution. Mirror the `ShipCommand`/`CacheEligibilityJson` entries exactly.

- [X] T001 [P] Create `src/FS.GG.Governance.ReleaseJson/FS.GG.Governance.ReleaseJson.fsproj` (net10.0, `GenerateDocumentationFile`; refs: `FS.GG.Governance.ReleaseRules`, `FS.GG.Governance.ReleaseFactsSensing`, `FS.GG.Governance.Config`) — mirror `CacheEligibilityJson.fsproj`.
- [X] T002 [P] Create `src/FS.GG.Governance.ReleaseCommand/FS.GG.Governance.ReleaseCommand.fsproj` (net10.0, `OutputType=Exe`; refs: `ReleaseRules`, `ReleaseFactsSensing`, `Config`, `ReleaseJson`, `YamlDotNet`) with compile order `Declaration.fs(i)` → `Loop.fs(i)` → `Interpreter.fs(i)` → `Program.fs` — mirror `ShipCommand.fsproj`.
- [X] T003 [P] Create `tests/FS.GG.Governance.ReleaseJson.Tests/FS.GG.Governance.ReleaseJson.Tests.fsproj` (Expecto + FsCheck; ref `ReleaseJson`, `ReleaseRules`, `ReleaseFactsSensing`) with `Main.fs` Expecto entry.
- [X] T004 [P] Create `tests/FS.GG.Governance.ReleaseCommand.Tests/FS.GG.Governance.ReleaseCommand.Tests.fsproj` (Expecto + FsCheck; ref `ReleaseCommand`, `ReleaseJson`, `ReleaseRules`, `ReleaseFactsSensing`, `Config`) with `Main.fs` Expecto entry.
- [X] T005 Add the four new projects to `FS.GG.Governance.sln` (mirror the `ShipCommand` + `CacheEligibilityJson` solution-folder entries); confirm `dotnet build FS.GG.Governance.sln` resolves the new graph.

**Checkpoint**: Solution restores and builds with empty/stub modules.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All public `.fsi` contracts, the shared test fixture, and the surface-drift harness. **No story body may begin until the contracts compile and the fixtures exist.**

**⚠️ CRITICAL**: Blocks US1/US2/US3.

- [X] T006 [P] Author `src/FS.GG.Governance.ReleaseJson/ReleaseJson.fsi` — `val schemaVersion: string` and `val ofRelease: ReleaseDecision -> SensedRelease -> string` (data-model §`ReleaseJson`).
- [X] T007 [P] Author `src/FS.GG.Governance.ReleaseCommand/Declaration.fsi` — `ReleaseDeclaration { Rules; Expectations; Layout }`, `DeclError { Reason }`, `val parse: lines:string list -> Result<ReleaseDeclaration, DeclError>` (pure over file contents; data-model §`ReleaseDeclaration`/`DeclError`, cli.md §declaration).
- [X] T008 [P] Author `src/FS.GG.Governance.ReleaseCommand/Loop.fsi` — `OutputFormat`, `RunRequest`, `UsageError`, `ExitDecision` (5 cases), `Model`/`Msg`/`Effect`, and `parse`/`init`/`update`/`render`/`exitCode` signatures (data-model §`RunRequest`/`ExitDecision`/MVU).
- [X] T009 Author `src/FS.GG.Governance.ReleaseCommand/Interpreter.fsi` — `ArtifactWriter`, `OutputSink`, `Ports { Files; Sense; Write; Out }`, `val realPorts: repo:string -> Ports`, `val step: Ports -> Loop.Effect -> Loop.Msg`, `val run: Ports -> Loop.RunRequest -> Loop.Model` (depends on T008; data-model §`Ports`).
- [X] T010 Exercise all four `.fsi` surfaces to prove they compile and compose before/with the `.fs` bodies (Constitution I). Depends on T006–T009. *Note: surface composition proven by `dotnet build` of both `src` projects (the `.fsi` are the sole declarations the `.fs` satisfy) and by the semantic suites loading the packed public surface (`Loop.parse`/`Interpreter.run`/`ReleaseJson.ofRelease`/`Declaration.parse`) — the FSI `prelude.fsx` load was not used this row.*
- [X] T011 [P] Add `tests/FS.GG.Governance.ReleaseCommand.Tests/Support.fs` — a `withTempRepo` helper that materializes a `.fsgg/release.yml` + the six governing source files in a temp dir and cleans up (the F016/F054/ShipCommand `withTempRepo` precedent); include compliant, un-bumped, missing-source, and **advisory-only-unmet** (a rule unmet but declared `advisory`/non-`block-on-release` under the active posture) fixture builders.
- [X] T012 [P] Add `tests/FS.GG.Governance.ReleaseCommand.Tests/SurfaceDriftTests.fs` and `tests/FS.GG.Governance.ReleaseJson.Tests/SurfaceDriftTests.fs` — load the public surface, compare to `surface/FS.GG.Governance.ReleaseCommand.surface.txt` / `…ReleaseJson.surface.txt`, honor `BLESS_SURFACE=1` (mirror the existing surface-drift test). Baselines committed in Phase 6 once `.fs` bodies stabilize.

**Checkpoint**: Contracts compile, FSI green, fixtures and surface harness in place — story work can begin.

---

## Phase 3: User Story 1 — Gate a release from a real repository in CI (Priority: P1) 🎯 MVP

**Goal**: One `fsgg release --repo <dir>` invocation that loads the declaration, senses six families (F054), evaluates them (F053 `evaluateRelease` verbatim), prints a human verdict, and exits with the right code among the five.

**Independent Test**: Compliant fixture ⇒ passing verdict, every rule satisfied, exit 0 (US1.1/SC-001). Un-bumped fixture ⇒ failing verdict naming version-bump as blocker, other five passing, exit 1 distinct from 2/3/4 (US1.2/SC-002). Advisory-only unmet ⇒ warnings + exit 0 (US1.4).

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T013 [P] [US1] `ParseTests.fs` — `Loop.parse`: valid argv → `Ok RunRequest` (defaults: `Format=Text`, `--out`=`<repo>/release.json`); unknown flag / missing `--repo` / malformed value → `Error UsageError`; **no I/O on rejection** (cli.md invocation table). Also assert the **flags-only** contract (cli.md §subcommand mapping): `parse` does not expect or strip a leading `release` token, so a leading bare `release` (or any unknown leading positional) → `Error UsageError` (exit 2), while the flags-only argv (`--repo …`) parses `Ok`.
- [X] T014 [P] [US1] `DeclarationTests.fs` — `Declaration.parse`: well-formed `release.yml` → `Ok` with six `ReleaseRule`s normalized to the F053 stable composite key, `ReleaseExpectations`, `SourceLayout`; tokens map to `releaseRuleKindToken` / F023-F014 severity+maturity vocabulary; product-neutral (no hardcoded ids).
- [X] T015 [P] [US1] `LoopTests.fs` — pure transitions: `init` emits `LoadDeclaration`; `DeclarationLoaded(Ok)` emits `SenseRelease(layout,expectations)`; `Sensed` computes `decision = Release.evaluateRelease rules facts` purely in `update`, sets `ExitDecision` from `ExitCodeBasis` (Clean→Success, Blocked→Blocked), and emits `EmitSummary`. Assert emitted-effect lists (Constitution IV).
- [X] T016 [P] [US1] `LoopTests.fs` — `exitCode`: Success→0, Blocked→1, UsageError'→2, InputUnavailable→3, ToolError→4 (cli.md exit-code table, SC-005).
- [X] T017 [P] [US1] `EndToEndTests.fs` — via `Interpreter.run` with `realPorts` over a `withTempRepo` compliant fixture: passing verdict, six satisfied rules, `ExitDecision=Success`; un-bumped fixture: `Blocked`, version-bump in blockers, five passing (SC-001/SC-002/SC-006). **Advisory-only-unmet fixture (US1.4 / spec edge "mixed blocking and advisory"):** an unmet rule whose effective severity is advisory surfaces in the warnings (not the blockers), the overall verdict stays passing, and `ExitDecision=Success` (exit 0).

### Implementation for User Story 1

- [X] T018 [US1] `Declaration.fs` — row-local YamlDotNet adapter parsing `.fsgg/release.yml` into `ReleaseDeclaration`; absent/malformed → `Error DeclError`; deterministic rule ordering; F014 schema untouched. Makes T014 pass.
- [X] T019 [US1] `Loop.fs` — `parse` (argv → `RunRequest`/`UsageError`), `init`, pure `update` (wires F053 `evaluateRelease`; resolves `ExitDecision`), `render` (text summary: verdict, blockers w/ reason, warnings, passing), `exitCode`. Makes T013/T015/T016 pass.
- [X] T020 [US1] `Interpreter.fs` — `Ports`, `realPorts repo` (binds `Loader.fileSystemReader` to `<repo>/.fsgg`; `Sense` = `senseRelease (realPort repo layout)`; `Out` = stdout), pure `step` dispatch over `Effect`, `run` loop folding `Msg`→`update` to a terminal `Model`. Makes T017 pass. Depends on T018, T019.
- [X] T021 [US1] `Program.fs` — thin `[<EntryPoint>]`: `Loop.parse argv` → on `Error` print usage to stderr + `exit 2`; on `Ok` build `realPorts` + `Interpreter.run` + emit + `exit (Loop.exitCode model.Exit)`. stderr diagnostics tagged `fsgg release [<category>]: <message>` (cli.md stderr).

**Checkpoint**: MVP — text gating works end to end against real fixtures; five exit codes wired; `release.json` not yet emitted.

---

## Phase 4: User Story 2 — Deterministic `release.json` audit artifact (Priority: P2)

**Goal**: `--format json|both` writes a byte-deterministic `release.json` projecting verdict + per-rule findings + per-family evidence; text and JSON never contradict.

**Independent Test**: Two runs over identical state ⇒ byte-identical `release.json` (SC-003); JSON contains verdict, every rule's base+effective severity / fact state / outcome / reason, and per-family evidence, validating against the committed golden baseline and matching the text verdict (SC-007, US2.1–2.4).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T022 [P] [US2] `tests/FS.GG.Governance.ReleaseJson.Tests/ReleaseJsonTests.fs` — `ofRelease` shape: fixed top-level field order (`schemaVersion`,`verdict`,`exitCodeBasis`,`rules`,`evidence`); `rules` exactly six in F053 composite order; each rule carries `kind/surface/factState/outcome/baseSeverity/effectiveSeverity/reason`; `unrecoverable` family ⇒ `null` evidence object (release.schema.md).
- [X] T023 [P] [US2] `tests/FS.GG.Governance.ReleaseJson.Tests/DeterminismTests.fs` — `ofRelease` called twice on the same inputs is byte-identical; FsCheck property over reordered inputs yields identical bytes (no clock/path/env content) (FR-008/SC-003).
- [X] T024 [P] [US2] `tests/FS.GG.Governance.ReleaseJson.Tests/GoldenTests.fs` — `ofRelease` over a fixed fixture equals a committed golden baseline file (SC-007).
- [X] T025 [P] [US2] `tests/FS.GG.Governance.ReleaseCommand.Tests/DeterminismTests.fs` — full `Interpreter.run --format both` over a fixture twice ⇒ byte-identical artifact (SC-003); and the text verdict/per-rule outcomes equal those in the JSON (FR-009/US2.4).
- [X] T026 [P] [US2] `tests/FS.GG.Governance.ReleaseCommand.Tests/PersistenceEdgeTests.fs` — atomic write: a failed/interrupted `Write` leaves no partial `release.json` (FR-012/US2.3) using a faked failing `ArtifactWriter`.

### Implementation for User Story 2

- [X] T027 [US2] `src/FS.GG.Governance.ReleaseJson/ReleaseJson.fs` — hand-driven `Utf8JsonWriter` walk (AuditJson/RouteJson precedent): `schemaVersion` literal `"fsgg.release/v1"`, exhaustive token helpers for every enum (no wildcard), `rules` in F053 order, `evidence` per-family with `null` on unrecoverable, ordinal-sorted `diagnostics`. Makes T022–T024 pass.
- [X] T028 [US2] `Loop.fs` — extend `update`: when `Format` requests JSON, after `Sensed` emit `WriteArtifact(out, ReleaseJson.ofRelease decision sensed)`; handle `Wrote(Ok)`→Done and `Wrote(Error)`→`ToolError`; `render` honors `Text`/`Json`/`TextAndJson`. Depends on T027.
- [X] T029 [US2] `Interpreter.fs` — `Write` = atomic temp-then-rename `ArtifactWriter` returning `Result<unit,string>` (no partial file on failure). Makes T025/T026 pass. Depends on T028.

**Checkpoint**: US1 + US2 both work; deterministic auditable artifact emitted on request.

---

## Phase 5: User Story 3 — Fail safe on missing/malformed/unreadable repository (Priority: P3)

**Goal**: Distinguish bad input from tool defect in diagnostics **and** exit code; never fabricate a pass; always return a complete six-family verdict.

**Independent Test**: No/invalid `release.yml` ⇒ actionable diagnostic + exit 3 (not 1, not 4) (US3.1). Missing governing source ⇒ that family `Unrecoverable`/unmet with a named diagnostic, run still completes six families (US3.2/SC-004/SC-006). Bad argv ⇒ usage + exit 2 (US3.3). Unwritable `--out` ⇒ exit 4, no partial artifact.

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T030 [P] [US3] `FailureTests.fs` — `DeclarationLoaded(Error)` ⇒ `InputUnavailable` (exit 3), actionable diagnostic, **no** `SenseRelease`/`WriteArtifact` emitted; absent `release.yml` and malformed `release.yml` both reach exit 3 via `Interpreter.run` over fixtures (US3.1/SC-005).
- [X] T031 [P] [US3] `DegradeTests.fs` — `withTempRepo` missing-source fixture: the affected family is `Unrecoverable` ⇒ its rule unmet (never satisfied), verdict still covers all six families, run completes; assert 0 fabricated passes (US3.2/SC-004/SC-006).
- [X] T032 [P] [US3] `FailureTests.fs` — unwritable `--out` (faked `Write` error) ⇒ `ToolError` (exit 4) and no partial artifact, distinct from InputUnavailable; bad argv ⇒ `UsageError'` (exit 2) (US3.3/SC-005).

### Implementation for User Story 3

- [X] T033 [US3] `Loop.fs` / `Program.fs` — harden the `update`/entry diagnostics so absent/invalid declaration → `InputUnavailable` (exit 3) with a tagged input diagnostic, IO/write defect → `ToolError` (exit 4) with a tool-defect diagnostic, distinct categories on stderr (FR-010/FR-011, Constitution VI). Makes T030/T032 pass.
- [X] T034 [US3] Confirm via `Interpreter.run` that the all-`Unrecoverable` / missing-source paths complete with a six-family verdict and no crash (no impl change expected if F053/F054 reused verbatim; add the assertions and a guard if a gap surfaces). Makes T031 pass.

**Checkpoint**: All five outcome classes distinguishable; no fabricated pass; no crash on degraded repos.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Network-free guarantee, surface baselines, docs, and the quickstart validation pass.

- [X] T035 [P] `tests/FS.GG.Governance.ReleaseCommand.Tests/ScopeGuardTests.fs` — assert no network dependency in the command's reachable assembly surface (the F054 scope-guard precedent); reads are `System.IO`-only (SC-008).
- [X] T036 Bless and commit `surface/FS.GG.Governance.ReleaseCommand.surface.txt` and `surface/FS.GG.Governance.ReleaseJson.surface.txt` (`BLESS_SURFACE=1 dotnet test …`), then re-run drift tests green (T012).
- [X] T037 [P] Commit the `release.json` golden baseline file referenced by T024 under `tests/FS.GG.Governance.ReleaseJson.Tests/` (or `specs/055-release-command/contracts/`), generated from the stable `ofRelease`.
- [X] T038 [P] Update `CLAUDE.md` and the Phase 13 roadmap row: release-rules row 🟡→complete (F055 lands `fsgg release` host + `release.json`); note F014/F053/F054 untouched and the row-local `.fsgg/release.yml` adapter.
- [X] T039 Run the `quickstart.md` validation checklist (SC-001…SC-008) against built fixtures; record evidence (exit codes, `cmp` byte-identical, surface/golden green) and tick the checklist boxes.
- [X] T040 [P] `tests/FS.GG.Governance.ReleaseCommand.Tests/NoMutationTests.fs` — snapshot the `withTempRepo` tree (relative paths + content hashes) before `Interpreter.run`, run with `--format both` writing `--out` **outside** the repo working tree, and assert the repo tree is byte-for-byte unchanged afterward; repeat with `--out` *inside* the repo and assert the only added/changed path is the requested `release.json` (FR-016 — no repository mutation other than the explicitly requested artifact).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001–T004 parallel, T005 after them.
- **Foundational (Phase 2)** → after Setup. T006–T009 parallel (T009 after T008); T010 after T006–T009; T011–T012 parallel. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP.
- **US2 (Phase 4)** → after Foundational; integrates with US1's `Loop`/`Interpreter` but the `ReleaseJson` library (T027) is independently testable.
- **US3 (Phase 5)** → after Foundational; hardens US1/US2 paths, independently testable via fixtures.
- **Polish (Phase 6)** → after the desired stories; T036/T037/T039 need the stories' `.fs` bodies stable.

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- `.fsi` (Phase 2) before `.fs`; `Declaration.fs`/`Loop.fs` before `Interpreter.fs` before `Program.fs`.

### Parallel opportunities

- Phase 1: T001–T004 together.
- Phase 2: the four `.fsi` (T006–T008 + T009-after-T008), and T011/T012, in parallel.
- Each story's `[P]` test tasks run together; once Foundational lands, US1/US2/US3 can be staffed in parallel (US2/US3 stub against US1's contracts).

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL) → 3. Phase 3 US1 → **STOP & VALIDATE** (SC-001/SC-002/SC-005/SC-006 with text output) → demo the gate.

### Incremental delivery

Setup + Foundational → US1 (MVP gate) → US2 (`release.json`) → US3 (fail-safe) → Polish. Each story adds value without breaking the prior.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Reuse, don't reinvent: F053 `evaluateRelease` and F054 `senseRelease`/`realPort` are called verbatim — never mocked in end-to-end tests (Constitution V); only `Files`/`Write`/`Out` ports are faked for unit coverage.
- Elmish/MVU applies (stateful, I/O-bearing): `.fsi` contract, pure transition tests, emitted-effect assertions (T015), and real-interpreter evidence (T017) are all explicit tasks. `ReleaseJson` and `Declaration` are pure leaves — no MVU ceremony.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the task line.
