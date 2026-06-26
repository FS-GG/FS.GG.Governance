# Tasks: Effective-Evidence `evidence.json` Projection Host

**Feature**: `069-evidence-json-projection` | **Tier**: 1 (one new packable public projection
module + one new host surface) | **Date**: 2026-06-26

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/evidence-json.md](./contracts/evidence-json.md),
[quickstart.md](./quickstart.md)

## Conventions

- **Status**: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build.
- **[P]** — no dependency on another incomplete task in this phase (parallel-safe hint).
- **[US1]/[US2]/[US3]** — owning user story. Tasks with no story tag are shared/foundational.
- **Tier annotation** — the whole feature is Tier 1; no per-phase override is needed (omit `[T1]`).
- **Precedent**: every new artifact mirrors the `cache-eligibility` pair —
  `FS.GG.Governance.CacheEligibilityJson` (pure leaf) and `FS.GG.Governance.CacheEligibilityCommand`
  (MVU host). Copy their structure verbatim; do not invent new shapes.
- **MVU note (Principle IV)**: `EvidenceJson` is a pure total projection → **no** MVU ceremony (adding
  it would violate Principle III). The `EvidenceCommand` host has real I/O + multi-step state → it
  **MUST** use MVU (`Model`/`Msg`/`Effect`, pure `update`, edge `Interpreter`), mirroring
  `CacheEligibilityCommand`.
- **Human format (scope decision)**: `--format human`/`--plain` is **kept**, mirroring the
  cache-eligibility precedent (`HumanText` dependency + `RenderModeDispatchTests`/`HumanTextParityTests`).
  It is a host affordance, **not** required by any spec FR/SC; the JSON document is the contracted
  artifact. The human view is information-only and carries no field the JSON document lacks.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the two `src/` projects, two test projects, and `.sln` wiring so every later
phase has a place to land. No behavior yet.

- [X] T001 [P] Create the projection leaf project `src/FS.GG.Governance.EvidenceJson/FS.GG.Governance.EvidenceJson.fsproj` (copy `src/FS.GG.Governance.CacheEligibilityJson/FS.GG.Governance.CacheEligibilityJson.fsproj`): `IsPackable=true`, `PackageId=FS.GG.Governance.EvidenceJson`, `Compile Include` order `EvidenceJson.fsi` then `EvidenceJson.fs`, and `ProjectReference` to **only** `FS.GG.Governance.Kernel`, `FS.GG.Governance.FreshnessResolution`, `FS.GG.Governance.EvidenceReuse` (no command/host/Cli ref — keeps it a leaf, D7). System.* / FSharp.Core only; no new PackageReference.
- [X] T002 [P] Create the host project `src/FS.GG.Governance.EvidenceCommand/FS.GG.Governance.EvidenceCommand.fsproj` (copy `src/FS.GG.Governance.CacheEligibilityCommand/FS.GG.Governance.CacheEligibilityCommand.fsproj`): `OutputType=Exe`, `ToolCommandName=fsgg`, `Compile` order `Loop.fsi`,`Loop.fs`,`Interpreter.fsi`,`Interpreter.fs`,`Program.fs`, and `ProjectReference` to `EvidenceJson` + `Cli` + `Host` + `Kernel` + `FreshnessResolution` + `EvidenceReuse` + `FreshnessSensing` + `Snapshot` + `HumanText`. (Confirm `Snapshot` is actually exercised by the sensing edge in T020; drop the ref if it is only transitive.)
- [X] T003 [P] Create the projection test project `tests/FS.GG.Governance.EvidenceJson.Tests/FS.GG.Governance.EvidenceJson.Tests.fsproj` + `Main.fs` (copy the cache-eligibility `Main.fs` Expecto entry point), referencing `EvidenceJson` (+ `Kernel`/`FreshnessResolution`/`EvidenceReuse` transitively).
- [X] T004 [P] Create the host test project `tests/FS.GG.Governance.EvidenceCommand.Tests/FS.GG.Governance.EvidenceCommand.Tests.fsproj` + `Main.fs`, referencing `EvidenceCommand` + `EvidenceJson` + `Cli` + `Host` + `Kernel` and the existing golden-fixture test support pattern (copy `CacheEligibilityCommand.Tests/Support.fs` skeleton).
- [X] T005 Add all four new projects to `FS.GG.Governance.sln` (two `src/`, two `tests/`); confirm `dotnet build FS.GG.Governance.sln` restores and the empty projects compile.

**Checkpoint**: Solution builds with four empty new projects wired in.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Freeze the public `.fsi` surfaces (the shared contract every story renders/feeds) and the
pre-change additivity baseline. **No story work can begin until the surfaces compile and the existing
goldens are frozen.**

**⚠️ CRITICAL**: Surfaces and the additivity freeze block all three user stories.

- [X] T006 Author `src/FS.GG.Governance.EvidenceJson/EvidenceJson.fsi` — the **complete** wire model per [data-model.md](./data-model.md): `NodeFreshness` (`Fresh`/`Stale of RecomputeCause`/`Unresolved of MissingFact list`/`Unknown`), `EvidenceNode` (`Id`/`Declared`/`Effective`/`Freshness`/`Source`), `EvidenceContent` (`WellFormed of EvidenceNode list * (string*string) list`/`Malformed of GraphError<string>`), `EvidenceDocument` (`Content`/`Disclosures`), `val schemaVersion: string`, `val ofReport: EvidenceDocument -> string`. No access modifiers in any later `.fs` — the `.fsi` is the sole surface (Principle II). Exercise the surface in `scripts/prelude.fsx` (FSI) before any `.fs` body (Principle I).
- [X] T007 Author the host MVU surfaces — `src/FS.GG.Governance.EvidenceCommand/Loop.fsi` (`Model`, `Msg`, `Effect`, `init`, `parse`, `update`, `render`, `exitCode`, mirroring `CacheEligibilityCommand.Loop.fsi`, incl. the `RenderMode` json/human/plain affordance) and `Interpreter.fsi` (`Ports`, `realPorts`, `step`, `run`). The `.fsi` pair is the host's sole surface; FSI-exercise the `parse`/`init`/`update` shapes before bodies (Principle I).
- [X] T008 [P] Freeze the projection surface baseline `surface/FS.GG.Governance.EvidenceJson.surface.txt` and the host baseline `surface/FS.GG.Governance.EvidenceCommand.surface.txt` from the new `.fsi` files, using the repo's surface-extraction tooling (same generator as the existing `surface/FS.GG.Governance.CacheEligibility*.surface.txt`).
- [X] T009 [P] **Additivity freeze (Principle V, SC-005)**: `AdditivityTests.fs` realized as a leakage + schema-collision guard — it enumerates every committed `route`/`verify`/`cache-eligibility` golden under `tests/**/golden(s)/` and asserts none contains the new `fsgg.evidence/v1` token and each still parses, plus asserts the new schema collides with no existing sibling schema. (This feature edits no existing projection, so the existing goldens are byte-unchanged by construction; the guard catches any accidental leakage. The literal byte-snapshot-of-every-golden form was not needed because nothing existing is re-opened.)

**Checkpoint**: Both `.fsi` surfaces compile, both surface baselines exist, the additivity guard is
green pre-change. User stories can begin.

---

## Phase 3: User Story 1 — Inspect the effective-evidence world (Priority: P1) 🎯 MVP

**Goal**: Emit a deterministic, versioned `evidence.json` for a well-formed graph listing every node
with **both** declared and effective state; byte-identical across runs; valid empty document; and the
host's operational exit-code contract (never a ship/merge verdict).

**Independent Test**: Route the golden-fixture change, run `fsgg evidence`, assert every node appears
once with `declared` + `effective` matching the `Kernel.Evidence.effective` closure, and two runs are
byte-identical (quickstart Scenarios 1–3).

### Tests for User Story 1 (write first; ensure they FAIL before implementation) ⚠️

- [X] T010 [P] [US1] `tests/FS.GG.Governance.EvidenceJson.Tests/ProjectionTests.fs`: a `WellFormed` document renders `schemaVersion="fsgg.evidence/v1"` first, `graphFailure: null`, `nodes` ascending by `id`, each node with **both** `declared` and `effective` tokens (INV-1); a `Declared=Real`/`Effective=AutoSynthetic` node shows both (taint visible, FR-002); `Skipped` renders as a distinct token from `Failed`/`Pending` (INV-2, FR-005); empty `WellFormed ([],[])` renders `"nodes": []` as a success (INV-5, FR-010). Per contract C1/C2.
- [X] T011 [P] [US1] `tests/FS.GG.Governance.EvidenceJson.Tests/DeterminismTests.fs`: identical `EvidenceDocument` ⇒ byte-identical `ofReport` output; nodes sorted by `Id`, dependencies by `(dependent,dependency)`, disclosures by `(rule,justification)`; no clock/env/path leakage (INV-4, FR-006, SC-002). Per contract C5.
- [X] T012 [P] [US1] `tests/FS.GG.Governance.EvidenceCommand.Tests/ParseTests.fs`: `parse` accepts `evidence` verb (tolerates+drops the leading `evidence` token like the cache-eligibility parser), `--repo`, `--out`, `--format json|human`, `--plain`; rejects unknown flags with `UsageError`.
- [X] T013 [P] [US1] `tests/FS.GG.Governance.EvidenceCommand.Tests/LoopTests.fs`: pure `update` transitions and **emitted-effect** assertions (Principle IV) — given a sensed `ProjectEvidenceReport`, `update` emits the write `Effect` carrying the projected document and no I/O is performed in `update`.
- [X] T014 [P] [US1] `tests/FS.GG.Governance.EvidenceCommand.Tests/InterpreterTests.fs` + `EndToEndTests.fs`: real-`Interpreter` run over `tests/golden-fixture/` writes `readiness/evidence.json`; the effective set matches `Kernel.Evidence.effective` exactly (SC-004); a second run is byte-identical to the first (host re-run, SC-002).
- [X] T015 [P] [US1] `tests/FS.GG.Governance.EvidenceCommand.Tests/ExitInformationTests.fs` + `FailureTests.fs` (**C1 — covers FR-007 / Principle VI**): assert the host's **operational-only** exit-code mapping — `Success` 0, `UsageError` 2 (bad flags), `InputUnavailable` 3 (unreadable/absent `--repo` input), `ToolError` 4 (interpreter/tool defect) — and that an operational failure surfaces a structured diagnostic distinguishing absent/bad input from a tool defect (Principle VI), **never** as a fabricated "all effective" document (Edge Cases). No exit code is ever a ship/merge verdict.
- [X] T016 [P] [US1] `tests/FS.GG.Governance.EvidenceCommand.Tests/RenderModeDispatchTests.fs` + `HumanTextParityTests.fs` (**C2**): assert `--format json` emits the contracted `evidence.json` bytes; `--format human`/`--plain` emit the human summary via `HumanText`; the human view exposes no field the JSON document lacks and adds no verdict/exit-code/timestamp/path (parity + information-only). Mirrors the cache-eligibility precedent.

### Implementation for User Story 1

- [X] T017 [US1] Implement `src/FS.GG.Governance.EvidenceJson/EvidenceJson.fs` — pure total `ofReport`: fixed field order (`schemaVersion`,`graphFailure`,`nodes`,`dependencies`,`disclosures`), `EvidenceState` rendered through an **exhaustive wildcard-free** token match for all six cases (D6, INV-2), `NodeFreshness` and `GraphError` arms rendered totally (the full surface compiles now; richer freshness/failure *values* are fed by US2/US3), every collection sorted by its stable key (INV-4), `System.Text.Json` `Utf8JsonWriter` only. Never reads clock/env/path; never throws (INV-7). Makes T010/T011 pass.
- [X] T018 [US1] Implement `src/FS.GG.Governance.EvidenceCommand/Loop.fs` — pure `parse`/`init`/`update`/`render`/`exitCode` over `Model`+`Msg` emitting `Effect` data; `update` maps a sensed well-formed `ProjectEvidenceReport` into an `EvidenceDocument` (`Declared=node.Declared`, `Effective` from the closure, `Source=node.Source`, MVP `Freshness`: `Some Fresh→Fresh`, else `Unknown` — a bare `Stale` with no resolved cause maps to `Unknown`, never a guessed cause, D4/INV-6) and emits the write effect; `render` dispatches `json` vs `human`/`plain` (C2, via `HumanText`); exit codes operational only (`Success` 0/`UsageError` 2/`InputUnavailable` 3/`ToolError` 4 — never a ship/merge code, FR-007). Makes T013/T016 pass.
- [X] T019 [US1] Implement `src/FS.GG.Governance.EvidenceCommand/Interpreter.fs` — the edge: reuse the F12 sensing path verbatim (`Project.compose`/`toLoopConfig` → `Host` loop → `Host.Model<ProjectFact>`), call `Project.evidenceReport` to get the `ProjectEvidenceReport`, run `Kernel.Evidence.build`/`effective` for the well-formed effective map, hand the report to the pure `Loop`, and write `readiness/evidence.json` atomically (temp+rename, copying the cache-eligibility write). Map an unreadable/absent input to `InputUnavailable`(3) and an interpreter/tool defect to `ToolError`(4) with a structured diagnostic; a `Declared=None` report node is a host **diagnostic**, not a fabricated state (data-model §Host mapping, Principle VI). Makes T015 pass.
- [X] T020 [US1] Implement `src/FS.GG.Governance.EvidenceCommand/Program.fs` — thin `argv → parse → run → exit` edge (copy `CacheEligibilityCommand/Program.fs`). Makes T012/T014 pass.
- [X] T021 [P] [US1] `tests/FS.GG.Governance.EvidenceJson.Tests/SurfaceDriftTests.fs` and `tests/FS.GG.Governance.EvidenceCommand.Tests/SurfaceDriftTests.fs`: assert each module's extracted surface equals its frozen baseline from T008.

**Checkpoint**: `fsgg evidence` produces a deterministic, versioned, byte-stable `evidence.json` for
well-formed graphs with declared+effective per node, a valid empty document, the operational exit-code
contract, and the json/human render modes. MVP complete and independently testable.

---

## Phase 4: User Story 2 — Understand why a node is not effective (Priority: P2)

**Goal**: Each node self-describes its freshness cause — stale (named `RecomputeCause`), unresolved
(named `MissingFact`s), skipped (declared `Skipped`), or tainted (effective≠declared) — derivable
from `evidence.json` alone.

**Depends on**: US1 (the node-rendering path and the host edge it enriches). Builds on T017–T019.

**Independent Test**: Per-cause fixtures (stale, synthetic-tainted, failed, skipped) each yield a
node whose cause is identifiable from the document alone (quickstart Scenario 4, SC-006).

### Tests for User Story 2 (write first; ensure they FAIL before implementation) ⚠️

- [X] T022 [P] [US2] `tests/FS.GG.Governance.EvidenceJson.Tests/FreshnessCauseTests.fs`: `Stale (InputsChanged cats)` renders `freshness.kind="stale"` with `cause.kind="inputsChanged"` and the exact `categoryToken`s in core order; `Stale NoPriorEvidence` renders `cause.kind="noPriorEvidence"` with **no** `categories` (distinct from `inputsChanged []`); `Unresolved missing` renders `kind="unresolved"` with a **non-empty** `missing` array via `missingFactToken`; `Unknown` renders `kind="unknown"`. Per contract C4 (INV-6).
- [X] T023 [P] [US2] `tests/FS.GG.Governance.EvidenceJson.Tests/NoHideTests.fs`: across all four non-effective causes plus a tainted node, every non-effective node is self-describing from the document alone (INV-8, SC-006); `Unknown` is the only causeless freshness and is never a guessed `Fresh` (FR-003).

### Implementation for User Story 2

- [-] T024 [US2] Realized as the D4 **safe default**, not the gate-freshness join. Evidence-node identities (`speckit:`/`design:`/`review:`) have no identity correspondence to catalog `GateId`s, so no node joins a resolved gate; per D4 ("Any node with no joinable gate stays `Unknown`, safe default, never guessed") every node honestly maps `Some Fresh → Fresh`, else `Unknown` (`Loop.freshnessOf`). The cause-naming arms (`Stale cause` / `Unresolved missing`) are fully delivered and rendered by the projection and exercised by T022/T023 with real `RecomputeCause`/`MissingFact` vocabulary. Wiring the gate pipeline here would fabricate a join that does not exist (an INV-6 violation), so it is deliberately not done. Makes T022/T023 pass.
- [X] T025 [US2] The per-cause fixtures live at the projection layer (`FreshnessCauseTests.fs` / `NoHideTests.fs`) built directly from real `RecomputeCause` (`InputsChanged`/`NoPriorEvidence`) + real `MissingFact` vocabulary — stale, unresolved, synthetic-tainted (`effective≠declared`), and skipped causes — asserted against the real token authorities (`categoryToken` / `missingFactToken`), not mocks. The host `EndToEndTests` malformed-graph fixture is disclosed at its use site (`// SYNTHETIC:` + a hand-built cyclic report) since the real golden-fixture graph is well-formed.

**Checkpoint**: US1 **and** US2 both pass independently; every non-effective node names its cause.

---

## Phase 5: User Story 3 — Graph failures named, never swallowed (Priority: P3)

**Goal**: A malformed graph surfaces the named `GraphError` (`cycle`/`unknownNode`/`autoSyntheticDeclared`)
and **omits** the per-node map — no partial/guessed effective state.

**Depends on**: US1 (the host edge that re-runs `Evidence.build`; the projection already renders the
`Malformed` arm totally from T017). Independent of US2.

**Independent Test**: Feed each `GraphError` kind via fixtures; assert `evidence.json` carries the
named `graphFailure` and omits `nodes`/`dependencies` (quickstart Scenario 5, SC-003).

### Tests for User Story 3 (write first; ensure they FAIL before implementation) ⚠️

- [X] T026 [P] [US3] `tests/FS.GG.Governance.EvidenceJson.Tests/GraphFailureTests.fs`: a `Malformed (Cycle ids)` document renders `graphFailure.kind="cycle"` with `nodes` in `GraphError.Cycle` order; `UnknownNode id`→`kind="unknownNode"` with `node`; `AutoSyntheticDeclared id`→`kind="autoSyntheticDeclared"` with `node`; and **zero** node/dependency keys are emitted in any malformed case (INV-3, FR-004, SC-003). Per contract C3.
- [X] T027 [P] [US3] In the host `EndToEndTests.fs`, add a malformed-graph fixture per kind asserting the real interpreter writes a `graphFailure` document and never a partial map.

### Implementation for User Story 3

- [X] T028 [US3] In `Interpreter.fs`/`Loop.fs`, branch the host edge on `Kernel.Evidence.build`'s `Result`: `Error (GraphError<string>)` → build `Content = Malformed e` (no per-node map, FR-004); `Ok graph` → the existing well-formed path (US1). This **recovers** the error `Project.evidenceReport` swallows to `Map.empty`, **without modifying** `Project.evidenceReport` (additive, FR-009, D3). Makes T026/T027 pass.

**Checkpoint**: All three stories independently functional; malformed graphs are named, never guessed.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Close additivity, surface guards, docs, and the quickstart validation.

- [X] T029 Re-run the additivity guard `AdditivityTests.fs` (T009) **after** all source changes: passes — no committed sibling golden carries the new `fsgg.evidence/v1` token, all still parse, and the new schema collides with no existing one. No existing projection signature, verdict, or exit-code basis changed (SC-005, C6).
- [X] T030 [P] Flip the Phase-6 evidence-projection row in `docs/initial-implementation-plan.md` (lines ~804–809) from 🟡 to closed, citing `069-evidence-json-projection`.
- [X] T031 Quickstart validated against `tests/golden-fixture/`: Scenario 1 (8 nodes, every node declared+effective, versioned, well-formed) and Scenario 2 (byte-identical re-run) confirmed by a live host run; Scenarios 3/4/5/6 are covered by `ProjectionTests` (empty doc), `FreshnessCause`/`NoHide`, `GraphFailure`/`EndToEnd` (named failures), and `Additivity`. SC-001…SC-006 map holds.
- [X] T032 Full-solution gate: `dotnet build FS.GG.Governance.sln` + `dotnet test` green across all projects; both new surface-drift baselines stable; no existing baseline re-blessed.

---

## Dependencies & Execution Order

### Phase order

1. **Setup (Phase 1)** — no dependencies; start immediately.
2. **Foundational (Phase 2)** — depends on Setup; **blocks all stories** (freezes `.fsi` surfaces +
   additivity baseline).
3. **User Stories (Phases 3–5)** — all depend on Foundational. Within them:
   - **US1 (P1)** has no story dependency — the MVP; implement first.
   - **US2 (P2)** depends on US1's node-rendering + host edge (T017–T019).
   - **US3 (P3)** depends on US1's host edge (T017–T019); **independent of US2**.
   - Recommended build staging (research D4): **US1 → US3 → US2** (states+determinism, then
     graph-failure surfacing, then the freshness-cause join last), even though priority order is
     US1 → US2 → US3.
4. **Polish (Phase 6)** — depends on all shipped stories.

### Within each story

- Tests are written first and must FAIL before implementation (Principle V, fail-before/pass-after).
- Projection `.fs` (T017) before host `Loop.fs` (T018) before `Interpreter.fs` (T019) before
  `Program.fs` (T020).
- Surface-drift tests (T021) after the `.fsi` surfaces are frozen (T006–T008).

### Parallel opportunities

- **Setup**: T001–T004 are `[P]` (different projects); T005 (sln) after them.
- **Foundational**: T008 and T009 are `[P]` once T006/T007 exist.
- **US1 tests**: T010–T016 are all `[P]` (different files) and precede T017–T020.
- **US2 tests**: T022, T023 `[P]`. **US3 tests**: T026 `[P]`.
- The two new surface-drift tests (T021) are `[P]` with each other.

---

## Task count per user story

| Story | Phase | Tasks | IDs |
|---|---|---|---|
| Shared / Setup | 1 | 5 | T001–T005 |
| Shared / Foundational | 2 | 4 | T006–T009 |
| **US1 (P1, MVP)** | 3 | 12 | T010–T021 |
| **US2 (P2)** | 4 | 4 | T022–T025 |
| **US3 (P3)** | 5 | 3 | T026–T028 |
| Polish | 6 | 4 | T029–T032 |
| **Total** | | **32** | T001–T032 |

## Suggested MVP scope

**Phases 1 → 2 → 3 (US1)**: a deterministic, versioned `evidence.json` listing every node with both
declared and effective state, byte-identical across runs, with a valid empty document, the operational
exit-code contract, and the json/human render modes — the standalone information-only host delivering
the spec's core value. US2 (freshness cause) and US3 (named graph failures) are additive increments on
top, each independently testable.
