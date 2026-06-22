---
description: "Task list — Persist the evidence-reuse store to disk from the host commands"
---

# Tasks: Persist The Evidence-Reuse Store To Disk From The Host Commands

**Input**: Design documents in `/specs/048-evidence-reuse-store-persist/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tier**: Tier 1 (contracted change) — overall and for every phase; no per-task `[T1]` annotation needed.

**Tests**: Explicitly required by the spec (SC-008 mandates pure-transition + effects-boundary tests; the
plan names `PersistenceTransitionTests.fs` and `PersistenceEdgeTests.fs`). Test tasks are therefore included,
ordered before the implementation they cover (Principle I — Spec → FSI → Semantic Tests → Implementation).

**Organization**: Phases run in sequence; `[P]` tasks within a phase touch disjoint files and may run in
parallel. RouteCommand and ShipCommand are mirror edits — their `[P]` siblings touch different files.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]` / `[US2]` / `[US3]`; `[INF]` for shared infrastructure
- Each task names an exact file path.

## Elmish/MVU applicability (Principle IV — applies, satisfied)

This is I/O-bearing/stateful. The persistence **decision** (whether to write, what bytes, suppress-on-degrade,
non-fatal-on-error) is a pure `Loop.update` transition emitting a `PersistStore` effect (FR-010); only the byte
write runs at the `Interpreter` edge, reified back as the non-fatal `StorePersisted` msg. Tasks below cover the
`.fsi` contract, pure transition tests, emitted-effect assertions, and real-interpreter evidence.

---

## Phase 1: Setup — references + committed `.fsi` contracts (Principle I: contracts before `.fs`)

**Purpose**: Reference the F047 core and commit the public surface deltas before any `.fs` edit.

- [X] T001 [P] [INF] Add `<ProjectReference Include="..\FS.GG.Governance.EvidenceReuseStore\FS.GG.Governance.EvidenceReuseStore.fsproj" />` to `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj`.
- [X] T002 [P] [INF] Add the same `EvidenceReuseStore` `<ProjectReference>` to `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj`.
- [X] T003 [P] [INF] Add the three new public declarations to `src/FS.GG.Governance.RouteCommand/Loop.fsi` per `contracts/RouteCommand.Loop.fsi.delta`: `Effect.PersistStore of path: string * content: string`, `Msg.StorePersisted of Result<unit, string>`, and `RunRequest.PersistStore: bool`. Existing declarations unchanged (additive).
- [X] T004 [P] [INF] Add the mirror declarations to `src/FS.GG.Governance.ShipCommand/Loop.fsi` per `contracts/ShipCommand.Loop.fsi.delta`.

---

## Phase 2: Foundational — compiling MVU skeleton (BLOCKS all user stories)

**Purpose**: Make both commands compile against the new `.fsi`, wire the edge, parse the flag, bless the
surface. The persisted-document *content* and the *decisions* are added per-story (Phases 3–5); here the
plumbing exists end-to-end with a content placeholder that the stories fill in.

**⚠️ CRITICAL**: No user-story task may begin until this phase is complete.

- [X] T005 [INF] In `src/FS.GG.Governance.RouteCommand/Loop.fs`: add `PersistStore = false` to the `RunRequest` default/constructor; add the `--persist-store` boolean flag to the existing argv parser (absent ⇒ `false`); add the `Model` degrade/ack tracking (`StoreDegraded`, `PersistAcked` per data-model §3d); add a compiling `StorePersisted` `update` arm (sets `PersistAcked = true`, no `Exit` change). Module must compile against the new `.fsi` with `TreatWarningsAsErrors`.
- [X] T006 [P] [INF] Mirror T005 in `src/FS.GG.Governance.ShipCommand/Loop.fs` (different file from T005).
- [X] T007 [P] [INF] In `src/FS.GG.Governance.RouteCommand/Interpreter.fs`: add the edge arm `| Loop.PersistStore(path, content) -> Loop.StorePersisted(guard (fun () -> ports.Write path content))`, reusing the existing atomic `writeAtomic`/`ports.Write` (temp + rename). No new port.
- [X] T008 [P] [INF] Mirror T007 in `src/FS.GG.Governance.ShipCommand/Interpreter.fs`.
- [X] T009 [P] [INF] Update `surface/FS.GG.Governance.RouteCommand.surface.txt` additively to reflect exactly the three new public cases/field (drives the existing reflective surface-drift test green).
- [X] T010 [P] [INF] Update `surface/FS.GG.Governance.ShipCommand.surface.txt` additively.
- [X] T011 [INF] Build both projects: `dotnet build src/FS.GG.Governance.RouteCommand/...fsproj` and `...ShipCommand/...fsproj` succeed under `TreatWarningsAsErrors` (confirms the `.fsi` cases + EvidenceReuseStore reference compile). Depends on T001–T010.

**Checkpoint**: Both commands compile with the new surface; the edge writes; `--persist-store` parses; nothing
yet emits a `PersistStore` effect (content/decision added next).

---

## Phase 3: User Story 1 — Write the store back to disk (Priority: P1) 🎯 MVP

**Goal**: With `--persist-store`, the loaded store is serialised (F047 `serialise`) and written atomically to
the `--store` path, re-reading losslessly through the real reader; absent file ⇒ empty `v1` document; writes are
deterministic.

**Independent Test**: Point a command at a temp repo with a well-formed non-empty `v1` store, run with
`--persist-store`, assert the post-run file re-reads via `FreshnessSensing.realStoreReader` to the persisted
store; assert absent-file ⇒ empty `v1` (parent dirs created); assert two identical runs are byte-identical.

### Tests for User Story 1 (write FIRST, ensure they FAIL before implementation) ⚠️

- [X] T012 [P] [US1] New `tests/FS.GG.Governance.RouteCommand.Tests/PersistenceTransitionTests.fs` (pure, no filesystem): given `PersistStore = true` and a non-degraded `StoreLoaded(Ok store)`, `update` emits exactly one `PersistStore(StorePath, EvidenceReuseStore.serialise store)` effect; given `PersistStore = false`, NO `PersistStore` effect. **Fixture precondition (so this assertion survives the US2 derivation change in T022)**: the `store` fixture MUST be within `EvidenceReuseStore.defaultRetentionBound` and free of strictly-superseded entries, so that `retain defaultRetentionBound (prune store) = store` and `serialise store` stays the expected content after T022 swaps the derivation to the full `prune |> retain |> serialise` pipeline. Use disclosed synthetic evidence refs (`Synthetic` token, Principle V). Register the file in `FS.GG.Governance.RouteCommand.Tests.fsproj` compile order (before `Main.fs`).
- [X] T013 [P] [US1] New `tests/FS.GG.Governance.RouteCommand.Tests/PersistenceEdgeTests.fs` (real `Interpreter.run` + `realPorts` against a temp repo): (a) SC-001 lossless round-trip via `FreshnessSensing.realStoreReader`; (b) AC-2 absent store path ⇒ well-formed empty `v1` written, parent dir created, re-reads as `EvidenceReuse.empty`; (c) SC-002 two identical runs byte-identical; (d) FR-007 the file written is exactly the `--store`/`RunRequest.StorePath` target (a custom non-default path), confirming the write target is the existing store path and no default location is used. Register in the `.fsproj`.
- [X] T014 [P] [US1] Mirror T012 — `tests/FS.GG.Governance.ShipCommand.Tests/PersistenceTransitionTests.fs`, registered in the ship test `.fsproj`.
- [X] T015 [P] [US1] Mirror T013 — `tests/FS.GG.Governance.ShipCommand.Tests/PersistenceEdgeTests.fs`, registered in the ship test `.fsproj`.

### Implementation for User Story 1

- [X] T016 [US1] In `src/FS.GG.Governance.RouteCommand/Loop.fs`: in the `tryProject` transition, when `Request.PersistStore = true` and the load is non-degraded, append `PersistStore(Request.StorePath, EvidenceReuseStore.serialise (model.Store))` to the emitted effects (prune/retain added in US2); gate `EmitSummary` on `(not PersistStore) || PersistAcked` (data-model §4, D10); on `StorePersisted(Ok ())` set `PersistAcked = true`. Verdicts/`route.json` emission untouched. Depends on T012–T013 failing first.
- [X] T017 [US1] Mirror T016 in `src/FS.GG.Governance.ShipCommand/Loop.fs` (emits to `audit.json` path; ship verdict/partition untouched). Depends on T014–T015 failing first.

**Checkpoint**: `fsgg route --persist-store` and `fsgg ship --persist-store` durably write a lossless,
deterministic `v1` store; US1 tests pass.

---

## Phase 4: User Story 2 — Keep the persisted store bounded and pruned (Priority: P2)

**Goal**: Each enabled write applies F047 `prune` then `retain defaultRetentionBound` to the loaded store before
serialising — bounded, newest-first, no strictly-superseded entry, removal-of-whole-entries only.

**Independent Test**: Seed a store exceeding `defaultRetentionBound` and containing a strictly-superseded entry;
run with `--persist-store`; assert the persisted file is within bound, newest-first, free of the superseded
entry, and every surviving entry is byte-for-byte one of the loaded entries.

### Tests for User Story 2 (FAIL first) ⚠️

- [X] T018 [P] [US2] Add bounded+pruned cases to `tests/FS.GG.Governance.RouteCommand.Tests/PersistenceEdgeTests.fs` (SC-003): over-bound store ⇒ persisted within `defaultRetentionBound`, newest-first; strictly-superseded entry absent; every persisted entry byte-for-byte a loaded entry; an already-bounded/unsuperseded store persists value-equal to the loaded store (AC-3, no spurious reorder/rewrite).
- [X] T019 [P] [US2] Add the equivalent assertion to `PersistenceTransitionTests.fs` (route): the emitted `PersistStore` `content` equals `EvidenceReuseStore.serialise (retain defaultRetentionBound (prune loaded))`.
- [X] T020 [P] [US2] Mirror T018 in `tests/FS.GG.Governance.ShipCommand.Tests/PersistenceEdgeTests.fs`.
- [X] T021 [P] [US2] Mirror T019 in the ship `PersistenceTransitionTests.fs`.

### Implementation for User Story 2

- [X] T022 [US2] In `src/FS.GG.Governance.RouteCommand/Loop.fs`, change the persisted-content derivation to the full F047 pipeline: `model.Store |> EvidenceReuseStore.prune |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound |> EvidenceReuseStore.serialise` (data-model §2). No new policy/bound of our own.
- [X] T023 [US2] Mirror T022 in `src/FS.GG.Governance.ShipCommand/Loop.fs`.

**Checkpoint**: Persisted stores stay bounded and pruned across runs; US1 + US2 tests pass.

---

## Phase 5: User Story 3 — Persistence never changes a verdict, never fails the command (Priority: P3)

**Goal**: Verdicts come from the **loaded** store (write decoupled); a store-write failure is non-fatal (exit
code, emitted `route.json`/`audit.json`, and ship verdict unchanged; non-fatal note); a malformed-on-load store
is not clobbered; persistence-off is byte-identical to the pre-row baseline.

**Independent Test**: Run each command with and without `--persist-store`; assert verdicts in
`route.json`/`audit.json` identical and ship verdict/partition/enforcement/exit byte-for-byte identical; induce
a write failure (unwritable target) and assert exit/artifacts/verdict unchanged, no partial/`.tmp-*` file, a
non-fatal note; write garbage to the store and assert it is left untouched with a note; run without the flag and
assert no store file written and every artifact + golden baseline byte-identical.

### Tests for User Story 3 (FAIL first) ⚠️

- [X] T024 [P] [US3] In route `PersistenceTransitionTests.fs`: `StorePersisted(Error _)` changes neither `Exit` nor the emitted `route.json`/`gates.json` effects (FR-006); with `PersistStore = true` and `StoreDegraded = true`, NO `PersistStore` effect is emitted and a non-fatal "store not persisted: failed to parse; left untouched" note is appended (D6).
- [X] T025 [P] [US3] In route `PersistenceEdgeTests.fs`: SC-004 verdict decoupling — run with and without `--persist-store`, assert `route.json` per-gate cache verdicts byte-identical; SC-005 induced write failure (read-only target) — exit `0`, `route.json`/`gates.json` unchanged, no partial/`.tmp-*` file, non-fatal note present; Scenario 7 — garbage store left untouched.
- [X] T026 [P] [US3] In ship `PersistenceTransitionTests.fs`: mirror T024, and additionally assert `StorePersisted(Error _)` never becomes `ToolError` — ship `Exit` stays governed solely by `ExitCodeBasis` (Clean→0 / Blocked→1).
- [X] T027 [P] [US3] In ship `PersistenceEdgeTests.fs`: mirror T025, plus SC-004 ship-specific — verdict, blockers/warnings/passing partition, every enforcement field, and `audit.json` verdict content byte-for-byte identical with vs without `--persist-store`; exit code unchanged on induced write failure.
- [X] T028 [P] [US3] Assert SC-006 persistence-off byte-identity: confirm the existing `DeterminismTests.fs` / `EndToEndTests.fs` (route and ship) still pass unchanged with `--persist-store` absent, and that no store file is written. Add an explicit "no store file written when flag absent" assertion to each command's existing end-to-end test if not already covered.

### Implementation for User Story 3

- [X] T029 [US3] In `src/FS.GG.Governance.RouteCommand/Loop.fs`: make `StorePersisted(Error r)` append a non-fatal cache note ("store not persisted (…); run unaffected") and set `PersistAcked = true` with NO `Exit` change; in `tryProject`, when `Request.PersistStore && StoreDegraded` emit NO `PersistStore` effect and append the don't-clobber note (data-model §4). Confirm `CacheEligibility.evaluate` still reads the loaded store only (no change). Depends on T024–T025 failing first.
- [X] T030 [US3] Mirror T029 in `src/FS.GG.Governance.ShipCommand/Loop.fs`, ensuring no path maps `StorePersisted(Error _)` to `ToolError`/non-zero exit and the verdict basis is untouched. Depends on T026–T027 failing first.

**Checkpoint**: All three stories pass; persistence is decoupled, non-fatal, non-clobbering, and off-by-default
is byte-identical to baseline.

---

## Phase 6: Polish & Cross-Cutting

- [X] T031 [P] Add a persistence walkthrough section to `scripts/prelude.fsx` (FSI honest-audience exercise per quickstart "Exercise in FSI": build a real loaded store, run the `prune |> retain |> serialise` pipeline, round-trip through `FreshnessSensing.realStoreReader`).
- [X] T032 [P] Update the `CLAUDE.md` plan pointer if the active-plan reference needs to advance past this row.
- [X] T033 Run the full suite: `dotnet test tests/FS.GG.Governance.RouteCommand.Tests/...` and `...ShipCommand.Tests/...`, then `dotnet test` (whole solution) to confirm the reflective surface-drift tests pass with the two updated baselines and that NO F029–F047 baseline and NO route/audit golden changed (SC-007). Depends on all prior phases.
- [X] T034 Walk `quickstart.md` Scenarios 1–8 against the built commands to confirm acceptance ↔ success-criteria mapping end-to-end.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1 — **BLOCKS** all user stories (compiling skeleton + edge + flag + surface).
- **Phase 3 (US1, P1)**: depends on Phase 2. The MVP — stop and validate here.
- **Phase 4 (US2, P2)**: depends on Phase 2; in practice extends the US1 derivation (T022/T023 edit the same `Loop.fs` lines US1 introduced), so sequence US1 → US2 within each command.
- **Phase 5 (US3, P3)**: depends on Phase 2; edits the same `Loop.fs` transition, so sequence after US1/US2 per command.
- **Phase 6 (Polish)**: depends on all desired stories.

### Within each user story

- Tests are written and observed FAILING before the implementation task that satisfies them.
- The two commands are independent files and may proceed in parallel; within one command, `Loop.fs` is edited
  by US1 → US2 → US3 in sequence (same file).

### Parallel opportunities

- **Phase 1**: T001–T004 all `[P]` (four distinct files).
- **Phase 2**: T005/T006 (`[P]` route vs ship `Loop.fs`), T007/T008 (`[P]` interpreters), T009/T010 (`[P]` surfaces) run in parallel across the two commands; T011 is the join.
- **Phase 3**: T012–T015 are four distinct new test files, all `[P]`; T016 and T017 touch different commands' `Loop.fs` and may run `[P]` once their tests fail.
- **Phases 4–5**: route vs ship test additions are `[P]`; the `Loop.fs` impl tasks are `[P]` across commands but sequential within a command.

---

## Implementation Strategy

### MVP first (User Story 1)

1. Phase 1 (Setup) → Phase 2 (Foundational, CRITICAL) → Phase 3 (US1).
2. **STOP and VALIDATE**: durable lossless deterministic store write through the real reader.
3. The store is now writable on disk — the structural prerequisite the row exists to deliver.

### Incremental delivery

1. Setup + Foundational → skeleton compiles, edge writes, flag parses.
2. US1 → writable store (MVP) → validate Scenarios 1–3.
3. US2 → bounded + pruned → validate Scenario 4.
4. US3 → decoupled, non-fatal, non-clobbering, off-by-default identity → validate Scenarios 5–8.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Synthetic evidence references in fixtures are disclosed (`Synthetic` token, Principle V) — real evidence needs
  gate execution, which is **Out of Scope** for this row.
- Additive guarantee (FR-009/SC-007): zero edits to F029–F047 cores or their golden baselines; no schema bump;
  the read-only reader is unchanged and now consumes this row's persisted output. Never re-bless a route/audit
  golden to green a build — persistence-off must already be byte-identical.
- Never mark a task `[X]` while its assertions fail; narrow scope and document rather than weaken an assertion.
