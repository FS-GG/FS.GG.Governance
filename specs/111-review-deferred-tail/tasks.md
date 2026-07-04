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

**Goal**: the exit/pass payload folded into `GateDisposition.Executed`/`.Reused` makes
`Executed`-without-exit-code unrepresentable; `commandFor` returns a typed `NoCommand` reason.
Contract C1+C2.

> **Design refinement (T005):** payload folded into the existing `GateDisposition` cases rather than
> a separate `GateResult` DU — same B4 outcome, and `JsonTokens.dispositionToken`'s signature is
> untouched (no third surface move). See data-model E1 note.
> **Correction (T007):** `RefreshCommand/Interpreter.fs:102` was mis-listed — it reads a
> `CommandOutcome` (`.Stderr`), not a `GateOutcome`; left untouched. Real consumer edits below.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~GateRun"` + the projection suites;
only `GateRun/Model` and `GateRun/Plan` baselines move.

### Tests for US1

- [X] T003 [US1] [T1] `PlanTests`/projection suites assert each disposition (`Executed(code,passed)`, `Reused(code,passed)`, `NotExecuted`) projects to byte-identical `disposition`/`exitCode`/`passed` JSON + human tokens (JsonWriters/VerifyJson/HumanText tests carry literal expected strings). Compile-safety of "no exit-less Executed" is the type itself (R5).
- [X] T004 [US1] [T1] `GateRun.Tests/PlanTests.fs` `commandForTests` rewritten to assert `Ok`, `Error Plan.NoPrerequisite`, `Error (Plan.UnresolvedCommand id)`, `Error Plan.EmptyCommandLine` for the four inputs (RED on `main`, green now). 19/19 GateRun tests pass.

### Implementation for US1

- [X] T005 [US1] [T1] Reshaped `Model.fsi` then `Model.fs`: `GateDisposition.Executed`/`.Reused` carry `(exitCode: ExitCode, passed: bool)`; `GateOutcome = { GateId; Disposition }`; added `isPassing`. `.fsi` first.
- [X] T006 [US1] [T1] Added `NoCommand` DU (`Plan.fsi` + `Plan.fs`), `commandFor` now `Result<GateCommand, NoCommand>` (`NoPrerequisite`/`UnresolvedCommand id`/`EmptyCommandLine` — one per former `None`).
- [X] T007 [US1] Updated consumers: construction sites `VerifyCommand/Loop.fs`, `RoutePipeline/Loop.fs`, `ShipCommand/Loop.fs` (+ their `isPassing` passing-sets); the `commandFor` caller `CommandHost.classify` (Option.map → `Ok`/`Error _`); test helper `Tests.Common` + `RouteCommand`/`ShipCommand` Support folds.
- [X] T008 [US1] Updated the three field-readers to match on `Disposition`, exact tokens preserved: `JsonWriters/JsonWriters.fs`, `VerifyJson/Core.fs`, `HumanText/ReportView.fs`; the three `dispositionToken` bodies (`JsonTokens`, VerifyJson-local, HumanText-local) `Executed`→`Executed _`. Camelcase-vs-hyphen divergence preserved. (AuditJson/RouteJson pass `GateOutcome` through shared writers — no field reads to change.)
- [X] T009 [US1] [T1] Blessed the `GateRun` surface baseline (`BLESS_SURFACE=1`); diff confined to `GateRun/Model` (+isPassing, disposition payload, GateOutcome ctor) and `GateRun/Plan` (`commandFor`→Result, +NoCommand) — matches C1/C2, no other module moved.
- [X] T010 [US1] Verify: T003/T004 green; **full suite green** (85 projects + Cli.Tests 72/72 — the lone full-run red was an orphaned-MSBuild-node OOM, passes clean in isolation); surface-drift shows only C1/C2 ✓. **Vertical slice**: the `Packaging` Cli test packs `fsgg`, installs it from a local feed, and runs `fsgg route` end-to-end (exit 0) — the reshaped `GateOutcome`/`commandFor` exercised through the real packed CLI producing `route.json`. PR US1 pending push (awaiting go-ahead).

**Checkpoint**: illegal gate-outcome state unrepresentable; projections byte-identical; packed CLI runs route end-to-end. ✅ **US1 complete.**

---

## Phase 4: User Story 2 — Signatures state only what they use (B6/B7/B9) · P2 · **Tier 1**

**Goal**: drop dead `decideMatrix.boundary` (B7), drop unread `ComparisonSample.Agreement` (B6),
route Snapshot `GitUnavailable` through `assemble` via `RepoState` (B9). Contracts C3+C4+C5.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~Snapshot|Calibration|ValidationMatrix"`; only those three baselines move.

> **Corrections during US2:** (a) `RepoState` cases named `WorkTree | NotAWorkTree | GitAbsent` — `Ok` collides with `Result.Ok` and `NotARepository`/`GitUnavailable` collide with the `DiagnosticId` cases. (b) B6: `ObservedAgreement` is `AgreementLevel`, not `AgreementClassification`, so dropping the field leaves `AgreementClassification` dead — removed too (not "stays"). (c) removed the interpreter's now-dead local `sortDigests`.

### Tests for US2

- [X] T011 [US2] [T1] `AssembleTests.fs`: added `only GitUnavailable (assemble { baseRaw with RepoState = Snapshot.GitAbsent })` alongside the existing `NotARepository` case; the interpreter's git-unavailable path is exercised by the existing SensingTests (all Snapshot tests byte-identical, 38/38).
- [X] T012 [US2] [T1] `Calibration.Tests`: `decide` output byte-identical across all fixtures (27/27); sample builders/generators updated to the 2-field `ComparisonSample`.
- [X] T013 [US2] [T1] `ValidationMatrix.Tests`: `decideMatrix` `MatrixPlan` output identical without `boundary` (7/7); all test callers dropped the arg.

### Implementation for US2

- [X] T014 [US2] [T1] B9: replaced `RawSensing.RepoOk: bool` with `RepoState = WorkTree | NotAWorkTree | GitAbsent` (`Snapshot.fsi` then `Snapshot.fs`); `assemble` branches on `RepoState` via a shared `repoCheckFailure id message` helper emitting the matching `DiagnosticId`.
- [X] T015 [US2] B9: deleted the interpreter's hand-rolled `GitUnavailable` record + dead local `sortDigests`; routes through `Snapshot.assemble { … RepoState = Snapshot.GitAbsent }`. All three `RawSensing` sites use `RepoState`.
- [X] T016 [US2] [T1] B6: removed `Agreement` from `ComparisonSample` and the now-dead `AgreementClassification` DU (`Model.fsi`/`Model.fs`); updated the test sample builders/generators.
- [X] T017 [US2] [T1] B7: dropped the `boundary` parameter from `decideMatrix` (`Matrix.fsi` then `Matrix.fs`, removed `ignore boundary`); updated both src callers (VerifyCommand, ReleaseCommand) and all ValidationMatrix test callers. `MatrixBoundary` type itself retained.
- [X] T018 [US2] [T1] Blessed the three baselines; diff confined to `Snapshot` (RepoOk→RepoState + RepoState DU), `Calibration/Model` (−Agreement, −AgreementClassification), `ValidationMatrix/Matrix` (−boundary) — matches C3/C4/C5, no other module moved.
- [ ] T019 [US2] Verify: T011–T013 green; **full suite green (running)**; surface-drift shows only C3/C4/C5 ✓. Then open **PR US2**.

**Checkpoint**: three surfaces state only what they use; outputs byte-identical (affected suites green). ⏳ full-suite confirmation pending.

---

## Phase 5: User Story 3 — Config loader threads values (B5) · P2 · Tier 2

**Goal**: remove `.Value`/`Option.get` from the `Schema.fs` `finish` build thunks. No surface move.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~Config"` byte-identical; grep clean.

- [X] T020 [US3] The existing `Config.Tests` (65 tests) covers valid + each diagnostic path for all four parsers; no new fixture needed (a value present alongside a diagnostic, e.g. duplicate domains, is already exercised).
- [X] T021 [US3] Refactored all four `finish` thunks to bind the parsed required values via a `match … with Some… -> finish … | _ -> Error(List.ofSeq diags)` (parseProject/Policy/Capabilities/Tooling), and the `reqStringList` `List.map Option.get` → `List.choose id`. `finish` stays `private` (no `.fsi`). The only remaining `.Value` are YamlDotNet AST node accessors (`YamlScalarNode.Value`), not option unwraps.
- [X] T022 [US3] Config suite green (65/65) — byte-identical records + diagnostics; no surface baseline moved (Tier 2). PR US3 pending push.

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

**Goal**: remove the write-only `SurfacesPending`. **C1a re-deferred** (see below).

**Independent Test**: `dotnet test --filter "FullyQualifiedName~VerifyCommand"`; grep clean.

- [-] T034 [US5] C1a — **RE-DEFERRED on #83 with rationale.** The `exampleFindings` path is *production-unreachable* (`senseDocs` hardcodes `Examples = []`) but it is NOT dead code: `Interpreter.fsi:28` documents it as a **reviewer-supplied `ExampleFact` extension point** ("automated example-freshness judgement is out of scope … supplied by a reviewer"), and `AdvisoryBoundaryTests` (feature T050) intentionally exercises it, asserting the `docs.example-freshness` Advisory-vs-Blocking severity boundary. Removing the vocabulary would delete a designed extension and real Advisory-boundary coverage — disproportionate and unsound for a "dead-code" finding. Left in place; annotate #83.
- [X] T035 [US5] C1b: removed `SurfacesPending` from `VerifyCommand/Loop.fsi` + `Loop.fs` and its four writes (init + 2× `true` + 1× `false`). Confirmed **zero reads** repo-wide (`grep .SurfacesPending` → none) — the readiness gate never consulted it. Shrinks the `VerifyCommand/Loop` surface (dead-field removal).
- [X] T036 [US5] VerifyCommand suite: 84/84 behavioral pass; the sole drift failure was the intended surface shrink — blessed, diff confined to the `Loop` `.ctor` (−1 bool) + `SurfacesPending` property/getter. `grep SurfacesPending src` → none. Full-suite confirmation running.

---

## Phase 8: User Story 6 — Cosmetic hygiene (C1g) · P3 · Tier 2

**Goal**: headers describe actual access modifiers; dead opens gone.

**Independent Test**: `dotnet build FS.GG.Governance.sln -c Release` green (removed opens were unused).

> **Better fix than planned:** the headers claimed "no access modifiers" while the files carried `let private` — a **Principle II violation** (visibility must live in the `.fsi`). Rather than edit the headers to admit the violation, I removed the redundant `let private` (each symbol is already hidden by absence from its `.fsi`), which makes every header *true* without editing it and restores Principle II compliance. Zero surface change (verified: no baseline moved).

- [X] T037 [US6] Removed the redundant `let private` from all six files (Report 3, Gates 5, Capability 3, CostBudget/Findings 2, Findings 12, AttestationJson 11 = 36 sites) → their "no access modifiers" headers are now accurate. Surface-drift green for every affected project; **no baseline moved** (the `.fsi` already governed visibility).
- [X] T038 [US6] Removed the three dead `open System.IO` (VerifyCommand/ShipCommand/EvidenceCommand interpreters — verified zero `System.IO` usage). Left the wider 073 sweep out to keep the PR bounded (spec Edge Case).
- [X] T039 [US6] Solution build green; each header now matches its file (no modifiers). Full-suite confirmation running.

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
