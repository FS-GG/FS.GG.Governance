# Tasks: Deferred tail of the 2026-07-02 code review

**Input**: Design documents from `/specs/111-review-deferred-tail/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/surface-deltas.md

**Tests**: INCLUDED — the constitution (Principle V) mandates real test evidence; the spec's FR-015
requires RED→GREEN (or compile-fail) for behaviour/type changes and byte-identical-output assertions
for the rest. Test tasks come first within each story.

**Organization**: One phase per user story = **one PR** (SC-005). Stories are mutually independent
(no shared blocking foundation) and land in P1→P3 order. `[P]` = no dependency on another incomplete
task in the same phase. `[T1]`/`[T2]` mark the tier where it differs per task; the surface-moving
tasks are `[T1]`.

## Format: `[ID] [P?] [Story] [Tier?] Description`

## Path Conventions

Single multi-project repo: `src/FS.GG.Governance.*/`, `tests/FS.GG.Governance.*.Tests/`. Surface
baselines are embedded in `tests/<Project>.Tests/SurfaceDriftTests.fs`.

---

## Phase 1: Setup (shared, tiny)

**Purpose**: Capture the current outputs each story must reproduce byte-for-byte.

- [ ] T001 [P] Confirm clean baseline: `dotnet build FS.GG.Governance.sln -c Release && dotnet test FS.GG.Governance.sln` green on `111-review-deferred-tail`.
- [ ] T002 [P] Snapshot current byte-identical references for the diff-based acceptance: capture existing JSON/human fixtures the projection suites already assert (GateRun, RouteJson, AuditJson, GatesJson, VerifyJson, ReleaseJson, Calibration, ValidationMatrix, Snapshot, Config) — these are the "before" the byte-identical tasks compare against; no new fixtures needed.

**Checkpoint**: Baseline green; stories may proceed independently.

---

## Phase 2: Foundational

**None.** These seven stories share no blocking prerequisite — each touches disjoint modules and
ships as its own PR. (Recorded explicitly so no false "foundation" phase is invented.)

---

## Phase 3: User Story 1 — Illegal gate-outcome states unrepresentable (B4) · P1 🎯 MVP · **Tier 1**

**Goal**: `GateResult` makes `Executed`-without-exit-code unrepresentable; `commandFor` returns a
typed `NoCommand` reason. Contract C1+C2.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~GateRun"` + the projection suites;
only `GateRun/Model` and `GateRun/Plan` baselines move.

### Tests for US1 (write first; must FAIL / not-compile before impl)

- [ ] T003 [US1] [T1] In `tests/FS.GG.Governance.GateRun.Tests/`, add positive tests asserting each `GateResult` case (`Executed(code,passed)`, `Reused(code,passed)`, `NotExecuted`) projects to byte-identical `disposition`/`exitCode`/`passed` JSON + human tokens vs the captured baseline; add a commented "does not compile" negative for `Executed` without an exit code (R5).
- [ ] T004 [P] [US1] [T1] In `tests/FS.GG.Governance.GateRun.Tests/`, add RED tests: `commandFor` returns `NoPrerequisite`, `UnresolvedCommand id`, `EmptyCommandLine` for the three no-command inputs (fails on `main` where all are `None`).

### Implementation for US1

- [ ] T005 [US1] [T1] Reshape `src/FS.GG.Governance.GateRun/Model.fsi` then `Model.fs`: replace `GateDisposition` DU + flat `GateOutcome` with `GateResult = Executed of ExitCode*bool | Reused of ExitCode*bool | NotExecuted` and `GateOutcome { GateId; Result }` (data-model E1). `.fsi` first per Principle I.
- [ ] T006 [US1] [T1] Add `NoCommand` DU and change `commandFor` to `Result<GateCommand, NoCommand>` in `src/FS.GG.Governance.GateRun/Plan.fsi` then `Plan.fs:95-121` (one case per current `None` site) (E2).
- [ ] T007 [US1] Update GateRun consumers to the new shapes: `src/FS.GG.Governance.VerifyCommand/Loop.fs:591-607` (construct `Executed(code,passed)`/`NotExecuted`), `RefreshCommand/Interpreter.fs:102`, and the `commandFor` callers `CommandHost/CommandHost.fs:200`, `RoutePipeline/Loop.fs`, `ShipCommand/Loop.fs` (match `Ok`; surface `NoCommand` in the diagnostic path per Principle VI).
- [ ] T008 [US1] Update the four projections to match on `GateResult`, preserving exact tokens: `JsonWriters/JsonWriters.fs:55-61`, `VerifyJson/Core.fs:108-114` (+45-47), `HumanText/ReportView.fs:74-130`. Keep the intentional camelCase-vs-hyphen token divergence (out of scope).
- [ ] T009 [US1] [T1] Regenerate the `GateRun/Model` + `GateRun/Plan` surface baselines in `tests/FS.GG.Governance.GateRun.Tests/SurfaceDriftTests.fs`; diff MUST match contract C1/C2 and nothing else.
- [ ] T010 [US1] Verify: T003/T004 GREEN; full suite green; surface-drift shows only C1/C2. Open **PR US1**.

**Checkpoint**: illegal gate-outcome state unrepresentable; projections byte-identical.

---

## Phase 4: User Story 2 — Signatures state only what they use (B6/B7/B9) · P2 · **Tier 1**

**Goal**: drop dead `decideMatrix.boundary` (B7), drop unread `ComparisonSample.Agreement` (B6),
route Snapshot `GitUnavailable` through `assemble` via `RepoState` (B9). Contracts C3+C4+C5.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~Snapshot|Calibration|ValidationMatrix"`; only those three baselines move.

### Tests for US2

- [ ] T011 [P] [US2] [T1] `tests/FS.GG.Governance.Snapshot.Tests/`: assert a `GitUnavailable` snapshot built via `assemble { raw with RepoState = GitUnavailable }` equals the current hand-rolled record field-for-field (diagnostic id/op/message, `Range=None`, empty sets, digest order).
- [ ] T012 [P] [US2] [T1] `tests/FS.GG.Governance.Calibration.Tests/`: assert `decide` output is byte-identical across every fixture after `Agreement` is dropped.
- [ ] T013 [P] [US2] [T1] `tests/FS.GG.Governance.ValidationMatrix.Tests/`: assert `decideMatrix` `MatrixPlan` output is identical without the `boundary` argument.

### Implementation for US2

- [ ] T014 [US2] [T1] B9: replace `RawSensing.RepoOk: bool` with `RepoState = Ok | NotARepository | GitUnavailable` in `src/FS.GG.Governance.Snapshot/Snapshot.fsi` then `Snapshot.fs:70-80`; branch `assemble` (`Snapshot.fs:212`) on `RepoState`, emitting the matching `DiagnosticId` (E3).
- [ ] T015 [US2] B9: delete the hand-rolled record in `src/FS.GG.Governance.Snapshot/Interpreter.fs:186-198`; call `Snapshot.assemble { raw with RepoState = GitUnavailable }` (mirror the sibling not-a-work-tree path `:206-210`). Update the two other `RawSensing` construction sites (`:211`, `:245`) to `RepoState`.
- [ ] T016 [P] [US2] [T1] B6: remove `Agreement` from `ComparisonSample` in `src/FS.GG.Governance.Calibration/Model.fsi:44-47` then `Model.fs:24-27`; fix any sample constructors in tests. `AgreementClassification` stays.
- [ ] T017 [P] [US2] [T1] B7: drop the `boundary` parameter from `src/FS.GG.Governance.ValidationMatrix/Matrix.fsi:20-24` then `Matrix.fs:14-27` (remove `ignore boundary`); update all callers.
- [ ] T018 [US2] [T1] Regenerate the `Snapshot/Snapshot`, `Calibration/Model`, `ValidationMatrix/Matrix` baselines; diff MUST match C3/C4/C5 only.
- [ ] T019 [US2] Verify: T011–T013 GREEN; full suite green; surface-drift shows only C3/C4/C5. Open **PR US2**.

**Checkpoint**: three surfaces state only what they use; outputs byte-identical.

---

## Phase 5: User Story 3 — Config loader threads values (B5) · P2 · Tier 2

**Goal**: remove `.Value`/`Option.get` from the `Schema.fs` `finish` build thunks. No surface move.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~Config"` byte-identical; grep clean.

- [ ] T020 [US3] Confirm the existing `FS.GG.Governance.Config.Tests` suite covers every `finish` build path (valid + each diagnostic); add a fixture only if a thunk field is uncovered.
- [ ] T021 [US3] Refactor `src/FS.GG.Governance.Config/Schema.fs` `finish` thunks to thread the parsed `Some` payloads instead of force-unwrapping: sites `:540-547`, `:570-574`, `:588-590`, `:622-623`, and the `List.map Option.get` at `:372`. Behaviour unchanged; `finish` stays `private` (no `.fsi`).
- [ ] T022 [US3] Verify: Config suite green + byte-identical records/diagnostics for every fixture; `grep -n "\.Value\|Option.get" src/FS.GG.Governance.Config/Schema.fs` shows none in the `finish` thunks. Open **PR US3**.

---

## Phase 6: User Story 4 — Dedup into shared homes (A1/A4/A6) · P2 · Tier 2 (+ C6 export)

**Goal**: one definition per duplicated helper, each fence-validated. Contract C6 for the one
intentional export.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~DependencyFences"` green + the
projection/Kernel/Checks/SddHandoff suites; grep shows one definition per helper.

### A1 — CommandHost.guard/drive (commit 1)

- [ ] T023 [P] [US4] Replace EvidenceCommand's local `guard`/`drive` (`src/FS.GG.Governance.EvidenceCommand/Interpreter.fs:119,143`) with `CommandHost.guard`/`CommandHost.drive` (ref already present at `.fsproj:51`). Keep the separate `runHost` loop (`:80`) unless it maps cleanly.
- [ ] T024 [US4] Replace Scaffold's local `guard`/`drive` (`src/FS.GG.Governance.Scaffold/Interpreter.fs:26,164`) with the shared ones; **add** a `CommandHost` `<ProjectReference>` to `FS.GG.Governance.Scaffold.fsproj` (new fence edge).
- [ ] T025 [US4] Run `tests/FS.GG.Governance.DependencyFences.Tests` — the Scaffold→CommandHost edge MUST keep it green; EvidenceCommand/Scaffold behaviour tests green.

### A4 — four JSON writer pairs → JsonWriters (commit 2)

- [ ] T026 [P] [US4] Move `writeFreshnessKey`/`writePrerequisite`, `writeCacheEligibility`, `writeGeneratedView(s)`, and an attestation-ref writer taking `AttestationSummary option` into `src/FS.GG.Governance.JsonWriters/JsonWriters.fs` (+ `.fsi` if exported); delete the copies at GatesJson `:42,58`, RouteJson `:61,77,120`, AuditJson `:99,194,216`, VerifyJson/GeneratedViews `:23,45`, VerifyJson/ReleaseReadiness `:133`, ReleaseJson `:280`.
- [ ] T027 [US4] Add `JsonWriters` `<ProjectReference>` to GatesJson and ReleaseJson `.fsproj` (RouteJson/AuditJson/VerifyJson already reference it).
- [ ] T028 [US4] Verify every projection emits byte-identical JSON (RouteJson/AuditJson/GatesJson/ReleaseJson/VerifyJson suites green); fence suite green.

### A6 — cross-project dedup (commit 3; C6 export)

- [ ] T029 [P] [US4] Hoist `mkFinding` (×4: Design/Docs/Package/Skill `*.fs:19-25`), `safe` (the four `*Checks` `Interpreter.fs` copies), and `valuesFor` (DesignChecks `:91`, SkillChecks `:78`) into `src/FS.GG.Governance.SurfaceChecks/` with `domain` + `maturity` parameters; delete the leaf copies. **Leave `ReleaseFactsSensing/Interpreter.fs:102` `safe` local** (no SurfaceChecks ref) and note it on #83.
- [ ] T030 [US4] [T1] C6: export `combineReasons` in `src/FS.GG.Governance.Kernel/Verdict.fsi`; replace the inlined split/distinct/sort/concat in `Route.stakesOf` (`Kernel/Route.fs:48-54`) with `Verdict.combineReasons (tripped |> List.map (fun f -> f.Name))`; regenerate the `Kernel/Verdict` baseline (contract C6 — the only widen).
- [ ] T031 [P] [US4] Collapse `SddHandoff.buildGate` to one private definition in `src/FS.GG.Governance.Adapters.SddHandoff/` (keep Readiness `:35`, delete the mirror at Consumer `:41`, or vice-versa; thread `domain`).
- [ ] T032 [US4] **Conditional** `sha256Hex`: attempt a single Kernel (or lowest-common) home for the four copies (CacheEligibilityCommand `:54`, CurrencySensing `:102`, FreshnessSensing `:38`, RefreshCommand `:48`) **and** their adjacent `digestPath` twins. Run the fence suite: **if green**, land it; **if it introduces a fence violation, revert and keep `sha256Hex` duplicated**, annotating #83 with the re-deferral rationale (FR-009).
- [ ] T033 [US4] Verify: fence suite green; SurfaceChecks/Kernel/SddHandoff + affected Command/Sensing suites green; byte-identical outputs; grep shows one definition per landed helper. Open **PR US4** (or split commits into 3 PRs if review prefers).

---

## Phase 7: User Story 5 — Dead code removed (C1a/C1b) · P3 · Tier 2

**Goal**: remove the unreachable DocsChecks example path and the write-only `SurfacesPending`.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~DocsChecks|VerifyCommand"`; grep clean.

- [ ] T034 [P] [US5] C1a: remove `exampleFindings` (`src/FS.GG.Governance.DocsChecks/DocsChecks.fs:61-70`) and its call in `evaluate` (`:78-81`); remove the now-dead `ExampleOutcome`/`ExampleFact`/`Examples` vocabulary from `DocsChecks/Model.fs` + `Model.fsi` and the `Examples = []` producers (`Interpreter.fs:110,139`) + the `.fsi:28` note. DocsChecks output byte-identical.
- [ ] T035 [P] [US5] C1b: remove `SurfacesPending` from `src/FS.GG.Governance.VerifyCommand/Loop.fs:179` + `Loop.fsi:258` and its four writes (`:337,776,784,898`); confirm the readiness gate no longer references it (it never read it). `verify.json` unchanged.
- [ ] T036 [US5] Verify: DocsChecks + VerifyCommand suites green byte-identical; `grep -rn "ExampleFact\|exampleFindings\|SurfacesPending" src` → none. Open **PR US5**.

---

## Phase 8: User Story 6 — Cosmetic hygiene (C1g) · P3 · Tier 2

**Goal**: headers describe actual access modifiers; dead opens gone.

**Independent Test**: `dotnet build FS.GG.Governance.sln -c Release` green (removed opens were unused).

- [ ] T037 [P] [US6] Correct the six stale "no access modifiers" headers: `ReleaseReport/Report.fs:14`, `Gates/Gates.fs:3`, `HumanRender/Capability.fs:2`, `CostBudget/Findings.fs:15`, `Findings/Findings.fs:3`, `AttestationJson/AttestationJson.fs:22`.
- [ ] T038 [P] [US6] Remove the three command-host dead `open System.IO`: `VerifyCommand/Interpreter.fs:14`, `ShipCommand/Interpreter.fs:13`, `EvidenceCommand/Interpreter.fs:14`. Optionally sweep the wider 073 dead-`open` set (spec Edge Case — keep the PR from ballooning; minimum = these 3 + the 6 headers).
- [ ] T039 [US6] Verify: build green; each edited header matches its file's real modifiers. Open **PR US6**.

---

## Phase 9: User Story 7 — Docs (C2f/C2g) · P3 · Tier 2

**Goal**: document the VerifyCommand reference convention; index the local decision records.

**Independent Test**: `ls docs/decisions/README.md`; a reader can find both rationales in-repo.

- [ ] T040 [P] [US7] C2f: add a `<!-- -->` comment to `src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj` explaining the full-surface-host convention (43 declared / ~32 transitively reachable; a top-of-tree host wires its surface explicitly). Prune nothing.
- [ ] T041 [P] [US7] C2g: create `docs/decisions/README.md` indexing 0001–0008 and cross-linking `docs/adr/README.md`; resolve the "0012/0013 stubs" ask by pointing at the org ADR-0012/0013 rather than minting local numbers.
- [ ] T042 [US7] Verify: index lists every local decision; VerifyCommand still builds. Open **PR US7**.

---

## Phase 10: Close-out

- [ ] T043 Confirm surface-drift moved for **only** C1–C6 across all merged PRs (SC-001); no other `SurfaceDriftTests` touched.
- [ ] T044 Tick every landed item on issue #83; annotate any fence-blocked helper (e.g. `sha256Hex`) as re-deferred with rationale (SC-004).
- [ ] T045 Flip board items #83 → Done and confirm epic #44 can close; update the Coordination board.

---

## Dependencies & parallel opportunities

- **Cross-phase**: Phases 3–9 are mutually independent (disjoint modules) → the seven PRs can be
  authored in parallel; P1→P3 is the suggested *merge* order, not a hard dependency.
- **Within US1**: T003/T004 before T005–T008; T009 after impl; T007/T008 depend on T005/T006.
- **Within US2**: B6 (T016), B7 (T017), B9 (T014/T015) are independent `[P]` triplets; T018 after all.
- **Within US4**: A1 (T023–T025), A4 (T026–T028), A6 (T029–T033) are independent commits; T032 is the
  only conditional (fence-gated) task.
- **Close-out** (T043–T045) is last.

## Task count per story

| Story | Tasks | Tier | PR |
|---|---|---|---|
| Setup | T001–T002 | — | (shared) |
| US1 (B4) | T003–T010 (8) | **T1** | 🎯 MVP |
| US2 (B6/B7/B9) | T011–T019 (9) | **T1** | 2 |
| US3 (B5) | T020–T022 (3) | T2 | 3 |
| US4 (A1/A4/A6) | T023–T033 (11) | T2 + C6 | 4 (or 3) |
| US5 (C1a/C1b) | T034–T036 (3) | T2 | 5 |
| US6 (C1g) | T037–T039 (3) | T2 | 6 |
| US7 (C2f/C2g) | T040–T042 (3) | T2 | 7 |
| Close-out | T043–T045 (3) | — | (final) |

**Suggested MVP**: **US1 (B4)** — the single item that closes a type-safety hole (illegal gate
outcome unrepresentable). It is self-contained, delivers the most value, and is independently
testable.

**Elmish/MVU note**: Principle IV applies to US4-A1 (adopting the shared `CommandHost` MVU
`guard`/`drive` edge) and US2-B9 (interpreter routes through the pure `assemble`); both *tighten* the
existing pure/edge split rather than adding new state — no new `Model`/`Msg`/`Effect` surface is
introduced, so no additional MVU contract tasks are needed beyond the consumer updates above.
