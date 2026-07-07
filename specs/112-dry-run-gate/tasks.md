---

description: "Task list for 112-dry-run-gate"
---

# Tasks: Dry-run / simulated governance gate

**Input**: Design documents from `/specs/112-dry-run-gate/`
(`spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/cli-dry-run.md`, `quickstart.md`)

**Feature tier**: **Tier 1** — one additive `.fsi` field (`RunRequest.DryRun`) + two new pure
modules with curated `.fsi` and surface baselines. Real `audit.json` (`fsgg.audit/v2`) stays
byte-identical.

**Tests**: REQUIRED (Constitution Principle V). Suites are **Expecto** (`testList`/`test`), not
xUnit. Write RED tests before implementation for parse + sufficiency; byte-identical / no-writes
assertions for the invariants.

## Status legend

`[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line). Never mark a
failing task `[X]`. Never weaken an assertion to green a build.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file).
- **[Story]**: `US1`/`US2`/`US3`. Tasks without a story are shared.

---

## Phase 1: Setup & baseline capture

**Purpose**: Establish a green pre-feature baseline so every later invariant is measured against
truth.

- [X] T001 Build the solution clean: `dotnet build FS.GG.Governance.sln -c Release`. Record it green.
- [X] T002 [P] Run `dotnet test tests/FS.GG.Governance.ShipCommand.Tests` and
  `tests/FS.GG.Governance.ReferenceGateSet.Tests` green — this is the pre-feature baseline the
  "real `audit.json` byte-identical" invariant (G5) is checked against.
- [X] T003 [P] Read `src/FS.GG.Governance.ShipCommand/Loop.fs` end-to-end and note the exact
  `update` sites that emit `ExecuteGates`, `WriteArtifact`, `PersistStore`, and `EmitSummary`
  effects — the dry-run branch withholds/redirects these. Note the `emptyAcc`/final-`Ok` sites in
  `parse`. (No code change; ground-truth for Phase 3.)

**Checkpoint**: baseline green; the exact effect-emission sites are known.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: The `.fsi`-first surface the stories build on. Per Principle I, contracts before code.

- [X] T004 Add `DryRun: bool` to `RunRequest` in
  `src/FS.GG.Governance.ShipCommand/Loop.fsi` (contract first) and to the record + hidden
  `ParseAcc`/`emptyAcc` (default `false`) and the final `Ok { … }` block in
  `src/FS.GG.Governance.ShipCommand/Loop.fs`. Build must still compile (field unused yet).
- [X] T005 Create the pure module contract `src/FS.GG.Governance.ShipCommand/Simulate.fsi` with the
  types from `data-model.md` — `SignalClass`, `GateSufficiency`, `Sufficiency`, `SimulatedResult`
  — and `val assemble: RouteResult -> ConsumeResult -> RunMode -> Profile -> SimulatedResult`.
- [X] T006 Create the projection contract
  `src/FS.GG.Governance.ShipCommand/SimulateProjection.fsi` — `val schemaVersion: string`,
  `val toJson: SimulatedResult -> string`, `val toText: SimulatedResult -> string`.
- [X] T007 Wire both new files into `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj`
  in dependency order (`Simulate` before `SimulateProjection`, both before `Interpreter`/`Program`).

**Checkpoint**: surface compiles with stub bodies; stories can proceed.

---

## Phase 3: User Story 1 — Simulated verdict, no execution, no writes (P1) 🎯 MVP

**Goal**: `fsgg ship --dry-run` prints a simulated verdict, runs no gate command, writes nothing.

**Independent Test**: parse accepts `--dry-run`; a faked run makes zero writes and never invokes
the `ExecutionPort`; output shows a simulated verdict; re-run is byte-identical.

### Tests (write first, expect RED)

- [X] T008 [P] [US1] Parse tests in `tests/FS.GG.Governance.ShipCommand.Tests/ParseTests.fs`:
  `--dry-run` ⇒ `Ok` with `DryRun = true`; composes with `--since`/`--paths`/`--mode`/`--profile`/
  `--json`; `--dry-runn` ⇒ `Error (UnknownFlag …)`.
- [X] T009 [P] [US1] No-writes / no-execution test (new file
  `tests/FS.GG.Governance.ShipCommand.Tests/DryRunTests.fs`): using `Support.fs` faked ports with a
  **spy** `ExecutionPort`, run a repo that would normally fail a gate with `DryRun = true`; assert
  `cap.Writes = []`, the spy count is `0`, and `model` carries a simulated decision. Add a
  determinism assertion (two runs ⇒ identical stdout).

### Implementation

- [X] T010 [US1] In `Loop.fs` `update`, add the dry-run branch: when `req.DryRun`, do **not** emit
  `ExecuteGates` (assign every selected gate `GateRun.Model.GateDisposition.NotExecuted`), and
  **withhold** `WriteArtifact` + `PersistStore`. Route to an `EmitSummary` carrying the simulated
  projection instead of the real audit. (Uses the sites found in T003.)
- [X] T011 [US1] Implement `Simulate.assemble` (real body) enough for US1: reuse
  `Ship.rollup route mode profile` for `Decision`; `Sufficiency`/diagnostics may be minimal here
  and are completed in US2 (leave a typed placeholder, not a lie — `RequiredAbsentCount = 0`,
  `AllNotEvaluated` computed from empty-required).
- [X] T012 [US1] Implement `SimulateProjection.toText` (real body): a `SIMULATED (dry-run) — not a
  real gate result` banner + reused `HumanText`/`ReportView` verdict view. Wire the `--dry-run`
  default/`--plain` path to print it via the existing `Out`/`RenderReport` seam.
- [X] T013 [US1] Confirm the exit-status contract: dry-run maps to exit 0 (preview) regardless of
  simulated verdict; adjust the verdict→`ExitDecision` mapping only under the dry-run branch.

**Checkpoint**: MVP — `fsgg ship --dry-run` previews a verdict with zero side effects. STOP & VALIDATE.

---

## Phase 4: User Story 2 — Handoff sufficiency breakdown (P2)

**Goal**: name the required-but-absent signals (the would-be-`notEvaluated` gaps); make the
all-absent state visible instead of a bare Pass.

**Independent Test**: a handoff missing a policy-required signal classifies that gate
`RequiredAbsent` with `RequiredAbsentCount > 0`; a stricter profile surfaces more requirements.

### Tests (write first, expect RED)

- [X] T014 [P] [US2] Sufficiency tests in
  `tests/FS.GG.Governance.ReferenceGateSet.Tests` (drive `Loader.loadAndValidate
  "samples/sdd-reference-gate-set"` → route → `Simulate.assemble`): required-absent, satisfied,
  not-required, all-absent (`AllNotEvaluated = true`), and stricter-profile-surfaces-more (SC-006).
- [X] T015 [P] [US2] FR-011 test: an all-`notEvaluated` evaluation is **not** rendered as a clean
  Pass with empty blockers — the absence is present in both `toText` and `toJson`.

### Implementation

- [X] T016 [US2] Complete `Simulate.assemble` sufficiency logic: consume the handoff via
  `SddHandoff.Consumer.consume`, classify each selected gate into
  `RequiredSatisfied`/`RequiredAbsent`/`NotRequired`, compute `RequiredAbsentCount` and
  `AllNotEvaluated`, and carry `ConsumeResult.Diagnostics` into `HandoffDiagnostics`.
- [X] T017 [US2] Extend `SimulateProjection.toText` with a **Sufficiency** section (required-absent
  first, then satisfied, then not-required) and an explicit all-absent statement.
- [X] T018 [US2] Safe-failure (FR-008): malformed / version-mismatch handoff diagnostics are
  surfaced and suppress a bare Pass; no-handoff ⇒ explicit nothing-to-evaluate signal.

**Checkpoint**: US1 + US2 — verdict *and* sufficiency; absence is named.

---

## Phase 5: User Story 3 — Machine-readable simulated document (P3)

**Goal**: `--dry-run --json` emits a `fsgg.audit.dryrun/v1` document, `simulated: true`, recognizable
yet unmistakable; real `audit.json` unchanged.

**Independent Test**: JSON has the distinct schema id + `simulated: true` + `sufficiency`; byte-
identical on re-run; the string `fsgg.audit/v2` never appears; real-audit tests still pass.

### Tests (write first, expect RED)

- [X] T019 [P] [US3] JSON contract tests in `DryRunTests.fs`: `schemaVersion =
  "fsgg.audit.dryrun/v1"` (G1), `simulated: true` (G2), `sufficiency` block present, byte-identical
  re-run (G3), `fsgg.audit/v2` absent.
- [X] T020 [P] [US3] Regression assertion: the existing ShipCommand byte-identical audit tests
  (`EndToEndTests.fs`) still pass unchanged — real `AuditJson.ofShipDecision` output is intact (G5).

### Implementation

- [X] T021 [US3] Implement `SimulateProjection.toJson` (real body): fixed key order per
  `contracts/cli-dry-run.md`, `schemaVersion = "fsgg.audit.dryrun/v1"`, `simulated: true`,
  reused verdict/blockers/warnings/passing shape, `sufficiency` + `handoffDiagnostics` blocks.
- [X] T022 [US3] Wire the `--dry-run --json` path to emit `toJson` via `Out` (never the
  `WriteArtifact` file path).

**Checkpoint**: all three stories functional and independently testable.

---

## Phase 6: Surface baselines, polish, validation

- [X] T023 Update the `.fsi` surface baselines: `RunRequest.DryRun` and the two new modules
  (`Simulate`, `SimulateProjection`) in `tests/FS.GG.Governance.ShipCommand.Tests/SurfaceDriftTests.fs`
  (and any repo-level surface-area baseline the api-compat gate reads). These are the ONLY intended
  surface deltas — any other drift is a defect.
- [X] T024 [P] Run `quickstart.md` end-to-end: build, both test suites, and a manual
  `dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --repo . --dry-run` on this repo;
  confirm zero writes (`git status` clean) and a simulated banner.
- [X] T025 [P] Confirm no unintended surface move: api-compat / surface-drift gate green with only
  the T023 baseline deltas.
- [X] T026 Update `specs/112-dry-run-gate/checklists/requirements.md` notes if any assumption
  changed during implementation; ensure the spec still matches what shipped.

---

## Dependencies & Execution Order

- **Phase 1 → 2 → 3** are sequential. Phase 3 (US1) is the MVP.
- **Phase 4 (US2)** and **Phase 5 (US3)** both depend on Phase 2 + the US1 `update` branch (T010)
  and `Simulate`/`SimulateProjection` skeletons; they are otherwise independent of each other and
  may proceed in parallel (US2 touches `Simulate.assemble` sufficiency + `toText`; US3 touches
  `toJson`).
- **Phase 6** depends on all desired stories.

### Within a story
Tests (RED) before implementation. `Simulate` (model/logic) before `SimulateProjection`
(projection). Real interpreter/byte-identical evidence before marking a task `[X]`.

### Elmish/MVU applicability
This is an I/O-bearing command: the "no execution / no writes" behaviour is a **pure `update`
decision** (withhold `ExecuteGates`/`WriteArtifact`/`PersistStore` effects) — T009/T010 give the
emitted-effect assertions and real-interpreter evidence; the interpreter/ports are unchanged, so no
new fake-port surface. `Simulate`/`SimulateProjection` are pure (no MVU needed).

---

## Parallel Opportunities

- T002 ∥ T003 (Phase 1).
- T008 ∥ T009 (US1 tests, different files).
- Phase 4 ∥ Phase 5 after T010–T012 land (different functions: `assemble`/`toText` vs `toJson`).
- T024 ∥ T025 (Phase 6).

## Suggested MVP scope

**User Story 1 (Phase 1 → 2 → 3)** — `fsgg ship --dry-run` previews a simulated verdict with zero
side effects. Delivers the core of issue #101 (a previewable, no-runtime gate) standalone; US2
(sufficiency) and US3 (JSON marker) layer on without breaking it.

## Task counts

- Shared/Setup+Foundational: 7 (T001–T007)
- US1: 6 (T008–T013) · US2: 5 (T014–T018) · US3: 4 (T019–T022)
- Polish/validation: 4 (T023–T026)
- **Total: 26**

## Implementation notes (as-built)

All 26 tasks complete and green. Real-evidence deltas from the task text, recorded for honesty:

- **T008 / T014 test locations.** The parse tests (T008) and the sufficiency tests (T014) landed in
  the new `tests/FS.GG.Governance.ShipCommand.Tests/DryRunTests.fs` rather than `ParseTests.fs` /
  `ReferenceGateSet.Tests`. Rationale: the ShipCommand suite already drives the **real**
  `Config → Routing → Route → Simulate` pipeline over the `validCatalog` fixture through the public
  `Loop.parse` / `Interpreter.run`, so sufficiency is exercised end-to-end there (11 DryRun tests:
  parse, no-writes/no-exec + real-execution contrast, exit-0-preview, determinism, `classify` unit
  table, required-absent naming, all-not-evaluated, malformed-handoff safe-failure, and the JSON
  schema/marker contract). `classify` is additionally unit-tested over every `DeclaredState`.
- **Vertical slice (US1–US3).** Exercised through the real CLI binary, not just unit tests:
  `dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --repo <tmp> --since HEAD~1 --dry-run`
  on a temp git repo carrying the bundled reference `.fsgg`. Evidence saved under
  `readiness/112-dry-run-gate/` (`112-dry-run-smoke.txt`, `112-dry-run-smoke.json`): the
  `SIMULATED (dry-run)` banner + sufficiency breakdown, exit 0 despite a `fail` verdict,
  `fsgg.audit.dryrun/v1` + `simulated: true` in JSON with `fsgg.audit/v2` absent, and
  **no `readiness/` written** (temp working tree unchanged). The subdirectory is matched by the
  `readiness/*/` gitignore rule, so the evidence is not committed.
- **Full ShipCommand suite: 115/115 green** — including the existing byte-identical real-`audit.json`
  tests (invariant G5: `AuditJson.ofShipDecision` output unchanged; dry-run added a *separate*
  projection). Surface baseline re-blessed (`BLESS_SURFACE=1`); the diff is exactly
  `RunRequest.DryRun` + the `Simulate`/`SimulateProjection` modules (T023/T025).
- **Pre-existing, unrelated:** `ReferenceGateSetPackage.T005` (3 tests) fail in this environment
  because they shell out to `pack-reference-gate-set.fsx`, which runs `dotnet test … -c Debug
  --no-build` and no Debug build / pack environment is present here. These are packaging tests for the
  reference-set NuGet artifact; they do not touch ShipCommand and are outside feature 112's scope.
  The 12 content/guard tests (G1–G7) pass.
- **Scope (research R5).** MVP is repo-scoped: dry-run runs in the current repo with execution
  suppressed (the "no runtime" value). Detached "evaluate an arbitrary handoff file against a bundled
  policy with no repo" is a separable follow-up (no bundled-policy loader exists yet).
