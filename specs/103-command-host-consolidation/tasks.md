# Tasks: Command-host second extraction pass (#49)

**Input**: Design documents in `/specs/103-command-host-consolidation/` (spec.md, plan.md, research.md, data-model.md, contracts/shared-leaves.md, quickstart.md)

**Tests**: Included and REQUIRED — the plan mandates RED→GREEN evidence for every behavior fix (Principle V). Write the failing test first.

**Organization**: Execution follows the plan's risk-isolated phases **A → B → C** (Tier-2 zero-surface first, then the two Tier-1 surface changes). Each task is tagged with the user story it serves (`[US1]`/`[US2]`/`[US3]`) and, where it differs from the spec's mixed tier, with `[T1]`/`[T2]`. `[P]` = parallel-safe within its phase (different files, no incomplete-task dependency).

## Legend

`[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line). Never mark a failing task `[X]`; never weaken an assertion to green a build.

---

## Phase 1: Setup & baseline

**Purpose**: Establish the green baseline and capture the four behavior defects as failing tests before any code moves.

- [ ] T001 Confirm clean baseline: `dotnet restore FS.GG.Governance.sln --locked-mode` then `dotnet fsi build.fsx test --no-restore` is fully green on branch `103-command-host-consolidation`. Record wall-time.
- [ ] T002 [P] [US1] Write a RED parser test for **M-CLI-3** covering every host: for each of Route/Ship/Verify/Release/Refresh/Evidence/CacheEligibility, assert that parsing `["--repo"; "--json"]` yields the host's missing-value error (not `Repo="--json"`), and that `["--repo"; "acme/x"; "--json"]` still parses to repo=`acme/x` + JSON mode. Place in the relevant host `*.Tests` `Loop`/parse test file (one test per host, or a shared theory in `Tests.Common`).
- [ ] T003 [P] [US1] Write a RED test for **F13** (Evidence Done-inertness) in `tests/FS.GG.Governance.EvidenceCommand.Tests/InterpreterTests.fs`: set `Model.Phase = Done`, feed `Wrote(Ok())` (and one of `Reported`/`Emitted`), assert `update` returns `(model, [])` with no phase mutation and no effects.
- [ ] T004 [P] [US1] Write a RED test for **F15** (`Wrote(Ok)` model) in `tests/FS.GG.Governance.ShipCommand.Tests/InterpreterTests.fs`: drive Ship's `Wrote(Ok())` transition and assert the emitted summary reflects `Phase = Persisted` (matching Verify's behavior).
- [ ] T005 [P] [US1] Write a test for **M-CLI-7** in `tests/FS.GG.Governance.EvidenceCommand.Tests/`: assert `--format json --plain` still emits the JSON contract string unchanged, and pin the intended semantics (Evidence `--plain` is an inert, documented no-op — the assertion target is finalized with T021's decision).

**Checkpoint**: T002–T005 are RED (or, where they already pass because behavior is coincidentally right, they lock the contract).

---

## Phase 2: Foundational — shared CommandHost leaves + argv guard

**⚠️ Blocks Phase 3.** These add the shared surface every host will call. Tier-1 only for the *new* `CommandHost.fsi` vals (baseline updated here); everything downstream is Tier-2.

- [X] T006 [US2] Added the shared impure leaves to `CommandHost.fs` + `val`s in `.fsi`: `writeAtomic`, `realHandoffs` (spelled-out `String.CompareOrdinal` sort), `senseEnvironmentReal` (fully-qualified `Config.Model` cases per D1 clash-avoidance), `senseBuilderReal`. Added a `CommandHost → Adapters.SddHandoff` ProjectReference (acyclic; SddHandoff refs only Kernel/Config/Gates/Route). CommandHost builds clean.
- [X] T007 [US2] Added the snapshot/catalog step-arm realizations as `senseSnapshotResult` (git+options → `Result<RepoSnapshot,string>`) and `loadCatalogValidation` (FileReader → `Validation`). Each host wraps the result in its own `Sensed`/`Loaded` msg, so no host `step` signature changed (SurfaceDrift host baselines unchanged — verified).
- [-] T008 [US1] Already merged in Phase A PR #69 (argv value-guard). Not re-done this pass.
- [X] T009 [US2] Blessed the `CommandHost` surface baseline (`BLESS_SURFACE=1`): diff is +6 `val`s only (`writeAtomic`/`realHandoffs`/`senseEnvironmentReal`/`senseBuilderReal`/`senseSnapshotResult`/`loadCatalogValidation`). No other baseline changed; the scope-guard test (no Host/Cli/*Command ref) stays green.

**Checkpoint**: `CommandHost` builds; new shared surface is baselined; nothing downstream rewired yet.

---

## Phase 3: Phase A — rewire hosts + zero-surface bug fixes (Tier-2) 🎯 core value

**Goal**: Delete per-host leaf copies, route parsers through the guard, land F13/F15/M-CLI-7. Surface baselines (except the CommandHost additions from Phase 2) MUST stay unchanged.

- [X] T010 [P] [US2] RouteCommand: deleted local `writeAtomic`/`realHandoffs`, call `CommandHost.*`; both step arms now call `CommandHost.senseSnapshotResult`/`loadCatalogValidation`. Built in isolation (green) as the template.
- [X] T011 [P] [US2] ShipCommand: deleted local `writeAtomic`/`realHandoffs`/`senseEnvironmentReal`/`senseBuilderReal`, call `CommandHost.*`; step arms rewired.
- [X] T012 [P] [US2] VerifyCommand: same as Ship (sense*/writeAtomic/realHandoffs deleted; step arms rewired).
- [X] T013 [P] [US2] ReleaseCommand: deleted local `writeAtomic`/`senseEnvironmentReal`/`senseBuilderReal`, call `CommandHost.*`. `CommandHost.senseEnvironmentReal` returns the same `Config.Model.EnvironmentClass` Release opens — types line up.
- [X] T014 [P] [US2] RefreshCommand: deleted local `writeAtomic`; both the `realPorts` field and the direct `writeProv` call site now use `CommandHost.writeAtomic`.
- [X] T015 [P] [US2] CacheEligibilityCommand: deleted local `writeAtomic`; step arms rewired to `CommandHost.*`.
- [X] T016 [P] [US2] EvidenceCommand: deleted local `writeAtomic` (the `with ex` outlier), added `CommandHost` ProjectReference + `open`, call `CommandHost.writeAtomic`.
- [-] T017 [US1] No-op: `Cli/ArtifactReading.locateHandoffs` already uses the spelled-out `String.CompareOrdinal` sort (the D3 alignment landed in an earlier PR). Not routed through `CommandHost.realHandoffs` — Cli sits below CommandHost in layering and adding the ref would invert it; output is already identical (the "or its `String.CompareOrdinal` sort" branch of the task).
- [ ] T018 [US1] Apply the shared value-guard on every single-value option arm across all seven `Loop.fs` parsers (`RouteCommand`/`ShipCommand`/`VerifyCommand`/`ReleaseCommand`/`RefreshCommand`/`EvidenceCommand`/`CacheEligibilityCommand`) — makes T002 GREEN. Each host keeps its own missing-value error DU via the guard's `onMissing` param.
- [ ] T019 [US1] Apply the same guard to `Cli.requireValue` in `src/FS.GG.Governance.Cli/Cli.fs:194`.
- [ ] T020 [US1] F15: change ShipCommand's `Wrote(Ok())` arm to bind the post-update model before `emitEffect` (match Verify) — makes T004 GREEN. `src/FS.GG.Governance.ShipCommand/Loop.fs` (~739).
- [ ] T021 [US1] M-CLI-7: make EvidenceCommand's `--plain` a documented inert no-op — drop the unused `ExplicitPlain` field plumbing if it drives nothing, and document the no-op in usage/help; do NOT change the (already-correct) JSON precedence. Makes T005 GREEN. `src/FS.GG.Governance.EvidenceCommand/Loop.fs`.
- [ ] T022 [US1] F13: prepend `if model.Phase = Done then model, []` to EvidenceCommand's `update` — makes T003 GREEN. `src/FS.GG.Governance.EvidenceCommand/Loop.fs:188`.
- [X] T023 [US2] Full `dotnet fsi build.fsx test` green (exit 0; 83 test DLLs pass, 0 failures). Single-definition greps confirm exactly 1 def each in `CommandHost` for all six leaves (`writeAtomic`/`realHandoffs`/`senseEnvironmentReal`/`senseBuilderReal`/`senseSnapshotResult`/`loadCatalogValidation`). Net src change: −246 lines (374 del / 128 ins across 8 `.fs`). Note: T018–T022 (M-CLI-3/F13/F15/M-CLI-7 behavior fixes) already merged in PR #69; this pass is the leaf-consolidation remainder of Phase A.

**Checkpoint (MVP+):** M-CLI-3/F13/F15/M-CLI-7 fixed, leaves consolidated, zero unintended surface delta. Real value is banked; a time-box may stop here.

---

## Phase 4: Phase B — EvidenceCommand ArtifactReading dedup (Tier-1)

**Goal**: Delete the ~325-line copy; single-source artifact reading. `[T1]` — deliberate `Cli/ArtifactReading.fsi` widening + baseline.

- [ ] T024 [T1] [US2] Determine the minimal `ArtifactReading.fsi` widening from EvidenceCommand's actual call sites (prefer having Evidence build a `RunRequest` and call the existing three `val`s; add adapters only if unavoidable). Record the chosen surface in contracts §C.
- [ ] T025 [T1] [US2] Add the chosen `val`s to `src/FS.GG.Governance.Cli/ArtifactReading.fsi` and their bodies in `ArtifactReading.fs`.
- [ ] T026 [US2] Rewire `src/FS.GG.Governance.EvidenceCommand/Interpreter.fs` to call `Cli.ArtifactReading` and delete lines ~33–357 (the copy). Confirm the dead `"present"`-check divergence is resolved by using Cli's implementation (D6 watch-out — check no Evidence test depended on the copy's behavior).
- [ ] T027 [T1] [US2] Update the `Cli`/`ArtifactReading` surface-drift baseline for the added `val`s.
- [ ] T028 [US2] Run full suite; verify `wc -l src/FS.GG.Governance.EvidenceCommand/Interpreter.fs` dropped ~325 lines (quickstart Story 2) and all Evidence tests green. Commit Phase B.

**Checkpoint**: biggest LOC win banked; artifact reading has one source.

---

## Phase 5: Phase C — ExitDecision consolidation + F9 vocabulary (Tier-1 + docs)

**Goal**: Resolve the dead-duplicate `ExitDecision` and the four format-flag vocabularies. Most `.fsi`/baseline churn — done last.

- [ ] T029 [T1] [US3] Adopt canonical `CommandHost.ExitDecision`/`exitCode` in each host + `Cli.fs`: delete the per-host `type ExitDecision`/`let exitCode` from every `Loop.fs` and remove them from each `Loop.fsi` (Route/Ship/Verify/Release/Evidence/CacheEligibility + Cli). Fallback per D5: if adoption is disproportionately noisy, instead delete the dead canonical from `CommandHost.fsi` and leave hosts — record which path was taken.
- [ ] T030 [T1] [US3] Update every affected surface-drift baseline for the `ExitDecision` removal/relocation, in the same commit as T029.
- [ ] T031 [US3] F9: decide converge-vs-document for the four format-flag vocabularies (bare `--json`; `--format text|json|both`; `--text/--json/--text-and-json`; `--format human|json`) — see research D2/F9 table. Either converge to one grammar, or add an in-repo note at each divergent parse arm explaining why it must differ. Apply across the host `Loop.fs` files.
- [ ] T032 [US3] Run full suite; confirm `grep "type ExitDecision"` shows the intended single (or zero-dead) state and F9 is converged/documented. Commit Phase C.

**Checkpoint**: all three stories complete.

---

## Phase 6: Polish, evidence & PR

- [ ] T033 Run the full quickstart.md validation end-to-end (all three stories + net-LOC check `git diff --stat main...HEAD -- 'src/**/*.fs'` on the order of −600…−800).
- [ ] T034 Confirm on the PR: SurfaceDrift green; deterministic gate green; API-compat gate reports ONLY the intended Phase B/C surface deltas (annotate them in the PR description as intended).
- [ ] T035 Open the PR against `main` titled `refactor(command hosts): second extraction pass (F2/M-CLI-3/M-CLI-7/F9/F13/F15, #49)`; link Epic #44; list the Tier-1 surface deltas and the four behavior fixes; move Coordination board #49 → In review.

---

## Dependencies & execution order

- **Phase 1** → no deps; do first (captures RED evidence).
- **Phase 2** blocks **Phase 3** (hosts call the new shared vals).
- **Phase 3** (Phase A) is independently shippable value; T010–T016 are `[P]` (distinct files); T017–T022 depend on Phase 2; T018 makes T002 green, T020→T004, T021→T005, T022→T003.
- **Phase 4** (Phase B) depends on Phase 3's Evidence `writeAtomic` rewire (T016) but is otherwise isolated; Tier-1.
- **Phase 5** (Phase C) is the highest-churn Tier-1 work; last so A/B value is safe if time-boxed.
- **Phase 6** after all desired phases.

## Parallel opportunities

- Phase 1: T002–T005 all `[P]`.
- Phase 3: the per-host rewires T010–T016 are `[P]` (different files). T018's parser edits touch seven files but are one logical change — do together, not blocking T010–T016.

## Task counts

- US1 (P1, behavior fixes): T002–T005, T008, T017–T022 — 11 tasks.
- US2 (P2, one-impl-per-leaf): T006–T007, T009–T016, T023–T028 — 16 tasks.
- US3 (P3, conventions): T029–T032 — 4 tasks.
- Setup/polish: T001, T033–T035 — 4 tasks.

## Suggested MVP scope

**Phases 1–3 (through T023)** — every behavior fix (M-CLI-3, M-CLI-7, F13, F15) plus the zero-surface leaf consolidation, with no unintended `.fsi`/baseline delta. This is a complete, shippable increment; Phases B and C add the two Tier-1 surface consolidations on top.
