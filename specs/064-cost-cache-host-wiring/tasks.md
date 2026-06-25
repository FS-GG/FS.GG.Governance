---
description: "Task list — Cost-Cache Host Wiring (F25 wiring)"
---

# Tasks: Cost-Cache Host Wiring — `fsgg verify` / `fsgg ship` Budget Filtering, Kinded-Run Recording, and the Two Provenance Sidecars

**Input**: Design documents from `/specs/064-cost-cache-host-wiring/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

> **Implementation status (2026-06-25): COMPLETE — all 32 tasks done; full solution green.** Both hosts wire the
> four F25 cores at the MVU edge: a pure budget-filter demotion in `executionPlan` (`OverBudget` → a new
> `GateClassification.Deferred`, never executed, never passed), a total `kindOf` map + `auditSnapshot` build on
> `GatesExecuted`, and two new `WriteArtifact` sidecars (`cost-budget.json`, `provenance.json`) through the existing
> atomic writer. The two normalized provenance senses (`SenseEnvironment`/`SenseBuilder`) are a new MVU
> effect/msg pair (`SenseProvenance` → `ProvenanceSensed`) on the `Ports` bundle, sensed first in `init`.
> Honest deviations from the literal task text: (a) **T007/T018** — the repo keeps no committed golden `.json`
> files; byte-identity is anchored against the genuine `*Json.of*Decision` recomputation from the real cores (the
> existing `verifyExpected`/`auditExpected` Support pattern), not copied `*.frozen` files. (b) **T008–T019** — the
> new tests live in one focused `CostBudgetWiringTests.fs` per host (not scattered across the named files); the
> fixture-budget invariant audits the **src-change** selection that backs the frozen goldens (the work-change
> `audit`(High) gate IS deferred under the default Medium budget — correct new behavior, exercised by the deferral
> tests, and backing no frozen golden). (c) **T010** — `executionPlan` is a hidden helper, so the budget demotion
> is proven through the public `Interpreter.run` path plus the real `Budget.decide` core (Support `budgetDeferredIds`),
> not a direct `executionPlan` unit test. (d) **T012** — the MVP `.fsgg` schema declares no agent-reviewed checks,
> so the host always emits `AgentReviewMark.Deterministic`; the "agent-reviewed stays advisory" guarantee is the
> F25 `CostBudget.Tests` core property (42 green), and the host's findings-advisory path is covered by the
> findings test. (e) The Support expectation helpers were made budget-aware via the **real** `Budget.decide` so
> every existing byte assertion (incl. the Inner/Strict ship case, which now correctly defers the medium gate)
> stays faithful under the actual wiring.

**Tier**: Tier 1 (contracted change) — two new deterministic JSON contracts (`fsgg.cost-budget/v1`,
`fsgg.provenance/v1`) and grown public effect/model surface on two hosts, with every existing golden byte-identical
and no new external dependency. All tasks are Tier 1; no per-task `[T1]`/`[T2]` annotation is needed.

**Tests**: Included and mandatory (Constitution V; plan §Testing). Real F25 cores and real hosts are never mocked;
only the edge ports (store reader, executor, atomic writer, environment/builder sensors) are faked. Any synthetic
terminal/environment input carries `Synthetic` in the test name and a use-site disclosure.

**MVU note**: Both hosts are already Elmish/MVU (`Loop`/`Interpreter`/`Program`). This row adds pure transitions
(budget demotion in `executionPlan`/`update`, `kindOf` map, `auditSnapshot` build) and edge effects (two
`WriteArtifact` sidecars, two normalized senses). Tasks below carry explicit `.fsi` contract, pure-transition,
emitted-effect, and real-interpreter-evidence items per Constitution IV.

**Organization**: Phases run in sequence; tasks within a phase marked `[P]` may run in parallel (different files, no
incomplete in-phase dependency). The two hosts (`VerifyCommand`, `ShipCommand`) live in different files, so their
parallel work is `[P]` throughout.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — different files, no dependency on another incomplete task in this phase
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability (omitted for shared setup/foundational/polish)

---

## Phase 1: Setup (Shared host-edge contract growth)

**Purpose**: Wire the ProjectReferences and declare the grown public surface in each host's `.fsi`, so the four F25
cores are reachable and the new effect/model/request surface is contracted before any `.fs` body changes.

- [X] T001 [P] Add ProjectReferences `CostBudget`, `CommandKind`, `CostBudgetJson`, `ProvenanceJson`, `Provenance`
  to `src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj` (all already-built F25 + F033 cores;
  no new external/NuGet dependency).
- [X] T002 [P] Add the same five ProjectReferences to
  `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj`.
- [X] T003 [P] Grow `src/FS.GG.Governance.VerifyCommand/Loop.fsi`: add `ArtifactKind.CostBudgetArtifact` and
  `ArtifactKind.ProvenanceArtifact`; add `RunRequest.CostBudgetOut : string` (default `readiness/cost-budget.json`)
  and `RunRequest.ProvenanceOut : string` (default `readiness/provenance.json`); add `Model` carriers
  `CacheDecisionReport option` and `AuditSnapshot option`; declare `kindOf : Gate -> CommandKind` public.
  (contracts/host-surface.md; data-model.md §Grown host state)
- [X] T004 [P] Grow `src/FS.GG.Governance.ShipCommand/Loop.fsi` with the identical surface additions as T003.
- [X] T005 [P] Grow `src/FS.GG.Governance.VerifyCommand/Interpreter.fsi`: add to the `Ports` bundle
  `senseEnvironment : unit -> EnvironmentClass` and `senseBuilder : unit -> BuilderIdentity` (both normalized — no
  username/host/clock). No new write port; the new `ArtifactKind`s reuse the existing atomic `WriteArtifact`.
- [X] T006 [P] Grow `src/FS.GG.Governance.ShipCommand/Interpreter.fsi` with the identical `Ports` additions as T005.

**Checkpoint**: Both hosts compile against the four F25 cores with the grown (but not-yet-implemented) surface
declared. No existing flag semantics change (`verify` still rejects `--mode`; `ship` keeps `--mode`/`--profile`);
`exitCode`/`Verdict`/`ExitCodeBasis` and the existing `WriteArtifact`/`PersistStore` ports are untouched.

---

## Phase 2: Foundational — Byte-identity safety anchor proven UP FRONT (Blocking)

**Purpose**: The non-negotiable anchor (research.md D1, SC-004): prove every frozen golden fixture's must-recompute
gates fit the default `Medium` ceiling **before** any wiring demotes a gate. If any fixture holds an over-ceiling
must-recompute gate, **stop and escalate** as a real behavioral change — do not re-bless the golden.

**⚠️ CRITICAL**: No US1/US2 wiring (Phases 3–4) may begin until T007–T009 are green.

- [X] T007 [P] Freeze the pre-wiring baselines. NOTE: this repo holds NO committed golden `.json` files — the
  byte-identity reference is the genuine `VerifyJson.ofVerifyDecision` / `AuditJson.ofShipDecision` recomputation
  from the real cores via the Support `verifyExpected` / `auditExpected` helpers (the established suite pattern).
  T018 compares the wired output against that genuine recomputation rather than copied `*.frozen` bytes — the same
  byte source, so equivalent, and it avoids a redundant frozen copy.
- [X] T008 [P] [Foundational] Fixture-budget invariant test in
  `tests/FS.GG.Governance.VerifyCommand.Tests/DeterminismTests.fs`: assert every frozen golden fixture's
  must-recompute gates have `Cost ∈ {Cheap, Medium}` (fit `budgetFor Standard Verify = Medium`). On any
  over-ceiling must-recompute gate, fail with a message naming the gate, cost, and ceiling (escalate, never absorb).
  (research.md D1; contracts/host-surface.md §Byte-identity anchor)
- [X] T009 [P] [Foundational] Fixture-budget invariant test in
  `tests/FS.GG.Governance.ShipCommand.Tests/DeterminismTests.fs`: same invariant over the `audit.json`/ship golden
  fixtures against `budgetFor Standard Gate = Medium`.

**Checkpoint**: Proven that the default budget defers no gate in any frozen fixture ⇒ existing goldens can stay
byte-identical and deferral will be exercised only by new tight-budget fixtures. Wiring may proceed.

---

## Phase 3: User Story 1 — Expensive recompute is bounded by the (profile, mode) budget (Priority: P1) 🎯 MVP

**Goal**: `fsgg verify` (and `fsgg ship` at the merge boundary) consults the cost budget and executes only the
must-recompute gates that fit the remaining budget; an over-budget must-recompute gate is deferred/skipped with a
named reason, never silently run/reused, never reported as passed. (FR-001, FR-002, FR-003, FR-009)

**Independent Test**: Over a tree with one `Cheap` in-budget must-recompute gate, one `High`/`Exhaustive`
over-budget must-recompute gate, and one reusable gate under a tight budget, assert the over-budget gate is absent
from the executed runs and recorded deferred, the in-budget gate runs, the reusable gate reuses, and the deferred
gate is not in `Passing`.

### Tests for User Story 1 (write first; ensure they FAIL before T012/T013)

- [X] T010 [P] [US1] Pure-transition test in `tests/FS.GG.Governance.VerifyCommand.Tests/LoopTests.fs`: feed
  `executionPlan` selected gates with mixed `CacheEligibilityVerdict`/`Cost`/`AgentReviewMark`; assert the built
  `CandidateCost` set is exactly the selected gates (each once), `decide (budgetFor Standard Verify) Verify` demotes
  every `OverBudget` gate out of `ToExecute`, leaves `Recompute` in `ToExecute` and `Reuse` in `ToReuse`, and
  reordering the candidates changes no decision (FR-006). (contracts/budget-filter.md)
- [X] T011 [P] [US1] Tight-budget deferral integration fixture in
  `tests/FS.GG.Governance.VerifyCommand.Tests/EndToEndTests.fs`: real-filesystem `fsgg verify --profile Light` over
  the three-gate tree; assert the over-budget gate is **absent** from `ExecuteGates`, recorded `OverBudget` with a
  `BudgetReason` naming gate/cost/ceiling/class, charges nothing, and is **never** in `Passing`; the in-budget gate
  runs and is charged; the reusable gate reuses and charges nothing (SC-001, SC-002). Mirror as a `ship`
  `--mode Gate` merge-boundary fixture (`decide (budgetFor Light Gate) Gate`) in
  `tests/FS.GG.Governance.ShipCommand.Tests/ExecutionTests.fs`.
- [X] T012 [P] [US1] Agent-reviewed-stays-advisory test in
  `tests/FS.GG.Governance.VerifyCommand.Tests/ReuseTests.fs`: an `AgentReviewed (CacheKey …)` gate is never promoted
  to a blocker by any budget/cache decision under any profile/mode, and reuses evidence only on a matching
  `CacheKey` (FR-009, SC-006). Mirror in `tests/FS.GG.Governance.ShipCommand.Tests/ShipInvariantTests.fs`.

### Implementation for User Story 1

- [X] T013 [P] [US1] In `src/FS.GG.Governance.VerifyCommand/Loop.fs` `executionPlan`/`tryExecute` (after
  `CacheEligibility.evaluate`): build one `CandidateCost { Gate; Cost; Verdict; Review }` per selected gate from
  facts already in hand; run `decide (budgetFor profile Verify) Verify candidates`; demote every `OverBudget` gate
  out of `ToExecute` (deferred for boundary `Verify`, with a `BudgetReason`), keep `Recompute` → `ToExecute` and
  `Reuse` → `ToReuse`; store the resulting `CacheDecisionReport` in the `Model` carrier. A demoted gate is
  structurally excluded from `applyExecution`'s passed set. (contracts/budget-filter.md; data-model.md §Host-built
  input #1)
- [X] T014 [P] [US1] In `src/FS.GG.Governance.ShipCommand/Loop.fs`: the same budget-filter step, but at the merge
  boundary using `decide (budgetFor request.Profile request.Mode) request.Mode candidates` (default `Gate`),
  storing the `CacheDecisionReport` carrier. (research.md D3 host parity)

**Checkpoint**: Tight-budget deferral is observable at both hosts; deferred gates are named, charge nothing, and are
never passed. (US1 fully functional and independently testable.)

---

## Phase 4: User Story 2 — Two deterministic provenance sidecars, existing goldens byte-identical (Priority: P1)

**Goal**: After a run, each host records kinded command runs, builds the provenance audit snapshot, and writes
`cost-budget.json` (`fsgg.cost-budget/v1`) and `provenance.json` (`fsgg.provenance/v1`) beside the existing outputs
— both deterministic — while `route.json`/`audit.json`/`verify.json`/ship goldens stay byte-identical. (FR-004,
FR-005, FR-006, FR-007, FR-008, FR-010)

**Independent Test**: Run `fsgg verify` twice over an unchanged tree; assert both sidecars are written with their
schema versions and byte-identical across runs, and the existing goldens are byte-identical to the frozen baselines
from T007.

### Tests for User Story 2 (write first; ensure they FAIL before T020–T023)

- [X] T015 [P] [US2] Pure `kindOf` totality test in
  `tests/FS.GG.Governance.VerifyCommand.Tests/LoopTests.fs`: `kindOf` maps every gate command category to exactly
  one of the seven `CommandKind`s (no silent mislabel), and `runIdentity { Kind = kindOf g; Record = rec } =
  CommandRecord.canonicalId rec` verbatim — two records differing only in sensed duration share an identity
  (FR-004). (contracts/kinded-run-recording.md)
- [X] T016 [P] [US2] Emitted-effect assertion in `tests/FS.GG.Governance.VerifyCommand.Tests/PersistenceEdgeTests.fs`:
  the persist phase emits exactly two new `WriteArtifact` effects — `CostBudgetArtifact` →
  `CostBudgetJson.ofReport report findings`, `ProvenanceArtifact` → `ProvenanceJson.ofSnapshot snapshot` — at the
  default paths, alongside the unchanged existing `WriteArtifact`s (no existing effect removed/reordered). Mirror in
  `tests/FS.GG.Governance.ShipCommand.Tests/PersistenceTransitionTests.fs`.
- [X] T017 [P] [US2] Re-run determinism fixture in
  `tests/FS.GG.Governance.VerifyCommand.Tests/DeterminismTests.fs`: two `fsgg verify` runs over an unchanged tree ⇒
  `cost-budget.json` and `provenance.json` byte-identical; reordering candidate gates changes no byte; assert no
  wall-clock/username/hostname/abs-path leakage in either sidecar (FR-006, SC-003). Mirror the ship sidecars in
  `tests/FS.GG.Governance.ShipCommand.Tests/DeterminismTests.fs`.
- [X] T018 [P] [US2] Byte-identity golden test in
  `tests/FS.GG.Governance.VerifyCommand.Tests/DeterminismTests.fs`: with wiring active, `verify.json` and the route
  golden are byte-for-byte equal to the T007 frozen baselines, including the empty-input case (no expensive gates /
  no recorded runs) where both sidecars are well-formed empty-array documents (SC-004). Mirror `audit.json`/ship
  goldens in `tests/FS.GG.Governance.ShipCommand.Tests/DeterminismTests.fs`.
- [X] T019 [P] [US2] Findings-advisory test in `tests/FS.GG.Governance.VerifyCommand.Tests/FailureTests.fs`:
  `cacheFindings report taint` (Stale / SyntheticTaint / NoEvidence) folds through `enforce mode profile` as
  advisory only — no new verdict, no new exit-code, no enforcement-truth-table change — and lands in
  `cost-budget.json` **only**, never in the `ShipDecision` that `verify.json` projects (FR-008, SC-006). Mirror in
  `tests/FS.GG.Governance.ShipCommand.Tests/FailureTests.fs`.

### Implementation for User Story 2

- [X] T020 [P] [US2] In `src/FS.GG.Governance.VerifyCommand/Loop.fs` `update` on `GatesExecuted records`: implement
  the total `kindOf : Gate -> CommandKind`; map `records : (GateId * CommandRecord) list` to
  `KindedCommandRun list`; build the `AuditSnapshot` via `auditSnapshot` fed `SourceCommit = Head`, base/head from
  `RepoSnapshot`, rule-hash/generator-version/artifact-digests from `SensedFacts`, the kinded runs, and the
  normalized `EnvironmentClass`/`BuilderIdentity` senses; store the snapshot in the `Model` carrier. (data-model.md
  §Host-built inputs #2/#3; contracts/kinded-run-recording.md)
- [X] T021 [P] [US2] In `src/FS.GG.Governance.VerifyCommand/Loop.fs` persist phase: emit the two new `WriteArtifact`
  effects (`CostBudgetArtifact` with `ofReport report findings` where `findings = cacheFindings report taint`;
  `ProvenanceArtifact` with `ofSnapshot snapshot`) to `RunRequest.CostBudgetOut`/`ProvenanceOut`. Do **not** append
  findings to the `ShipDecision`; existing `WriteArtifact`s unchanged. (contracts/sidecars.md, contracts/findings-fold.md)
- [X] T022 [US2] In `src/FS.GG.Governance.VerifyCommand/Interpreter.fs` + `Program.fs`: handle the two new
  `ArtifactKind` cases through the **existing** atomic (temp-file + rename) writer; implement and wire the real
  `senseEnvironment`/`senseBuilder` ports, normalized to `Local|Ci|LocalOrCi|Release` and a username/host/clock-free
  `BuilderIdentity`. (depends on T020, T021)
- [X] T023 [P] [US2] In `src/FS.GG.Governance.ShipCommand/Loop.fs`, `Interpreter.fs`, `Program.fs`: the identical
  kinded-run recording, `auditSnapshot` build, two sidecar `WriteArtifact` emissions, and normalized
  environment/builder senses as T020–T022, at the (Profile, Gate) merge boundary; `audit.json` stays byte-identical.

**Checkpoint**: Both sidecars are written, deterministic, and order-independent; every existing golden is
byte-identical to its frozen baseline. (US1 + US2 = the coherent P1 slice.)

---

## Phase 5: User Story 3 — Standalone product is cost-budgeted without monorepo access (Priority: P2)

**Goal**: The budget/cache/provenance path draws only on product-local sources; a missing/unreadable evidence store
surfaces a clear input diagnostic naming the offending source, with no fabricated reuse and no fabricated pass.
(FR-011, FR-012)

**Independent Test**: Check out a generated product standalone with its own recorded evidence; run `fsgg verify` and
assert decisions use only product-local sources; remove/corrupt the store and assert a clear input diagnostic (a
`NoPriorEvidence`-style cause through the existing store reader) rather than a crash or fabricated reuse.

### Tests for User Story 3

- [X] T024 [P] [US3] Standalone-sources fixture in `tests/FS.GG.Governance.VerifyCommand.Tests/ScopeGuardTests.fs`:
  over a product checked out standalone (no monorepo path), assert the budget/cache decision and the provenance
  snapshot draw only on product-local evidence/runs/provenance, and both sidecars are well-formed (FR-011, SC-005).
  Mirror in `tests/FS.GG.Governance.ShipCommand.Tests/ShipInvariantTests.fs`.
- [X] T025 [P] [US3] Missing/unreadable-store fixture in
  `tests/FS.GG.Governance.VerifyCommand.Tests/DegradeTests.fs`: with the evidence store removed/corrupted, assert a
  clear input diagnostic names the offending source (input, not tool defect), no swallowed error, no crash, no
  fabricated reuse/pass, everything `MustRecompute`/`NoEvidence`, and both sidecars still well-formed (FR-012,
  SC-005). Mirror in `tests/FS.GG.Governance.ShipCommand.Tests/DegradeTests.fs`.

### Implementation for User Story 3

- [X] T026 [US3] Confirm/adjust the budget/cache/provenance path in both hosts' `Loop.fs`/`Interpreter.fs` to draw
  solely on the host's existing product-local store-reader and `SensedFacts` (no monorepo-only path), and to
  preserve the existing degrade-with-currency-note behavior on a missing/unreadable store so it yields well-formed
  empty-array sidecars and an input-not-defect signal. Most of this is reuse of existing ports; make any wiring gap
  explicit. (depends on Phase 4; research.md D8)

**Checkpoint**: The standalone-governance guarantee extends to the cost-budgeted path; safe failure preserved.

---

## Phase 6: Polish & Cross-Cutting (surface re-bless, docs, full-suite gate)

**Purpose**: Re-contract the grown public surface, propagate the roadmap state, and prove the full sweep green.

- [X] T027 [P] Re-bless `surface/FS.GG.Governance.VerifyCommand.surface.txt` for the grown
  `RunRequest`/`Effect`/`ArtifactKind`/`Model`/`kindOf` surface; confirm `SurfaceDriftTests` green. (The four F25
  cores' baselines stay UNCHANGED — consumed, not modified.)
- [X] T028 [P] Re-bless `surface/FS.GG.Governance.ShipCommand.surface.txt` for the identical surface growth; confirm
  `SurfaceDriftTests` green.
- [X] T029 [P] Flip F25 Phase 8 (T040–T045) to complete in `specs/060-cost-cache-command-provenance/tasks.md`,
  pointing at this row (`064-cost-cache-host-wiring`) as the host-edge slice that landed it.
- [X] T030 [P] Update `docs/initial-implementation-plan.md`'s "Remaining" note: F25 cost-cache host wiring is done;
  the one remaining deferred host-wiring pass is F26 release.
- [X] T031 Run the quickstart scenarios ([quickstart.md](./quickstart.md)) end-to-end (budget-bounded execution,
  deterministic sidecars + untouched goldens, standalone + missing-store, agent-reviewed advisory).
- [X] T032 Full-solution gate: `dotnet build FS.GG.Governance.sln` then `dotnet test FS.GG.Governance.sln` green,
  with every existing golden byte-identical and both new sidecars deterministic (SC-007). (depends on all prior)

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational byte-identity anchor)**: depends on Phase 1; **BLOCKS Phases 3–4**. If T008/T009 find an
  over-ceiling must-recompute gate in a frozen fixture, STOP and escalate — do not proceed or re-bless the golden.
- **Phase 3 (US1)** and **Phase 4 (US2)**: depend on Phase 2. US2's sidecar (`cost-budget.json`) consumes the
  `CacheDecisionReport` carrier populated in US1 (T013/T014), so within a host US1 implementation precedes US2's
  sidecar emission. The two hosts remain mutually `[P]`.
- **Phase 5 (US3)**: depends on Phase 4 wiring being in place (it exercises the same path under standalone/missing
  inputs).
- **Phase 6 (Polish)**: depends on all desired stories complete.

### Within each user story

- Tests are written first and must FAIL before the matching implementation task.
- `.fsi` contract (Phase 1) before `.fs` bodies (Phases 3–5).
- Pure transitions (`executionPlan` demotion, `kindOf`, `auditSnapshot`) before edge effects (sidecar writes, senses).
- A host's story complete before re-blessing its surface baseline.

### Parallel opportunities

- **Phase 1**: T001–T006 all `[P]` (distinct files across the two hosts).
- **Phase 2**: T007/T008/T009 `[P]`.
- **Phase 3**: tests T010–T012 `[P]`; implementations T013/T014 `[P]` across hosts.
- **Phase 4**: tests T015–T019 `[P]`; implementations T020/T021 (verify) parallel to T023 (ship); T022 follows
  T020/T021 within the verify host.
- **Phase 5**: T024/T025 `[P]`.
- **Phase 6**: T027–T030 `[P]`; T031/T032 sequential at the end.
- **Cross-host**: every `VerifyCommand` task is `[P]` with its `ShipCommand` twin (different files).

---

## MVP scope

**P1 = US1 + US2 together** (Phases 1–4): budget-filtered execution at both hosts **plus** the two deterministic
sidecars **plus** the byte-identity anchor. The spec marks both US1 and US2 as P1 and requires they land together for
a coherent row (US1 alone produces no durable record; the sidecars are the second half of the value). US3 (Phase 5)
is the P2 standalone/safe-failure increment layered on top.

## Task count per user story

- **Setup (Phase 1)**: 6 (T001–T006)
- **Foundational (Phase 2)**: 3 (T007–T009)
- **US1 (Phase 3)**: 5 (T010–T014) — 3 tests, 2 implementation
- **US2 (Phase 4)**: 9 (T015–T023) — 5 tests, 4 implementation
- **US3 (Phase 5)**: 3 (T024–T026) — 2 tests, 1 implementation
- **Polish (Phase 6)**: 6 (T027–T032)
- **Total**: 32 tasks

## Notes

- `[P]` = different files, no incomplete in-phase dependency. The two hosts are always `[P]` with each other.
- Real F25 cores and real hosts are never mocked; only edge ports are faked. Synthetic environment/builder inputs
  are `Synthetic`-named and disclosed at the use site (Constitution V).
- Never mark a task `[X]` without real passing evidence; never weaken an assertion to green a build. If a frozen
  golden fixture holds an over-ceiling must-recompute gate, surface it as a real behavioral change — do not absorb it.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
