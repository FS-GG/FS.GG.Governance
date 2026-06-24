# Tasks: The `fsgg verify` Host Command

**Input**: Design documents from `/specs/056-verify-command/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/cli.md, contracts/verify.schema.md, quickstart.md

**Tier**: Tier 1 (contracted change) — full chain owed: `.fsi`, surface baselines, test evidence, docs. Two new public projects (`FS.GG.Governance.VerifyCommand`, `FS.GG.Governance.VerifyJson`); the F014–F052 cores are reused **verbatim**, not edited. Tests are in scope (Constitution V; plan lists both `.Tests` projects).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`; foundational/setup/polish tasks carry no story tag
- Discipline (Constitution I/II): draft each public module's `.fsi` and exercise it in `scripts/prelude.fsx` **before** its `.fs` body; semantic tests call the loaded public surface (`Loop.parse`, `Interpreter.run`, `VerifyJson.ofVerifyDecision`), never internals.
- Verify is the **closest sibling of `ShipCommand`**: same pipeline, same edge `Ports` bundle, same reused reference set (F014/F016/F015/F017/F018/F019/F023/F024/F043/F041/F046/F047/F048/F049/F051/F052). It differs only in (a) the fixed `RunMode.Verify` (**no `--mode` flag**), (b) the first-class **currency-findings** projection, (c) the `verify.json` schema id, and (d) pre-PR framing + the "nothing to verify" empty-selection report. **There is no `Declaration` adapter and no new sensing** (the F055 declaration surface was release-specific).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the two new `src` projects, their test projects, and wire the solution. Mirror the `ShipCommand` + `CacheEligibilityJson` entries exactly.

- [X] T001 [P] Create `src/FS.GG.Governance.VerifyJson/FS.GG.Governance.VerifyJson.fsproj` (net10.0, `GenerateDocumentationFile`; refs: `FS.GG.Governance.Ship`, `FS.GG.Governance.Enforcement`, `FS.GG.Governance.CacheEligibility`, `FS.GG.Governance.GateRun`, `FS.GG.Governance.EvidenceReuse`) — mirror `CacheEligibilityJson.fsproj`. **No** YamlDotNet, **no** AuditJson ref.
- [X] T002 [P] Create `src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj` (net10.0, `OutputType=Exe`; refs: the **exact ShipCommand reference set** — `Config`, `Snapshot`, `Routing`, `Findings`, `Gates`, `Route`, `Enforcement`, `Ship`, `FreshnessResolution`, `CacheEligibility`, `FreshnessSensing`, `EvidenceReuse`, `EvidenceReuseStore`, `EvidenceCapture`, `GateExecution`, `GateRun` — **plus** `VerifyJson`) with compile order `Loop.fs(i)` → `Interpreter.fs(i)` → `Program.fs` — mirror `ShipCommand.fsproj`. **No** `Declaration.fs(i)`, no YamlDotNet.
- [X] T003 [P] Create `tests/FS.GG.Governance.VerifyJson.Tests/FS.GG.Governance.VerifyJson.Tests.fsproj` (Expecto + FsCheck; ref `VerifyJson`, `Ship`, `Enforcement`, `CacheEligibility`, `GateRun`, `EvidenceReuse`) with `Main.fs` Expecto entry.
- [X] T004 [P] Create `tests/FS.GG.Governance.VerifyCommand.Tests/FS.GG.Governance.VerifyCommand.Tests.fsproj` (Expecto + FsCheck; ref `VerifyCommand`, `VerifyJson`, and the reused cores needed by fixtures — `Config`, `Snapshot`, `Ship`, `Enforcement`, `GateExecution`, `FreshnessSensing`) with `Main.fs` Expecto entry.
- [X] T005 Add the four new projects to `FS.GG.Governance.sln` (mirror the `ShipCommand` + `CacheEligibilityJson` solution-folder entries); confirm `dotnet build FS.GG.Governance.sln` resolves the new graph.

**Checkpoint**: Solution restores and builds with empty/stub modules.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All public `.fsi` contracts, the shared test fixture, and the surface-drift harness. **No story body may begin until the contracts compile and the fixtures exist.**

**⚠️ CRITICAL**: Blocks US1/US2/US3.

- [X] T006 [P] Author `src/FS.GG.Governance.VerifyJson/VerifyJson.fsi` — `val schemaVersion: string` (`"fsgg.verify/v1"`) and `val ofVerifyDecision: ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string` (data-model §7, verify.schema.md). Pure leaf — no MVU ceremony.
- [X] T007 [P] Author `src/FS.GG.Governance.VerifyCommand/Loop.fsi` — `OutputFormat` (`Text | Json`), `ScopeSelector` (`ExplicitPaths | Since | DefaultRange`), `RunRequest` (**no `Mode` field**; carries `Repo`/`Scope`/`Profile`/`Format`/`VerifyOut`/`StorePath`/`PersistStore` — data-model §1), `UsageError` (`UnknownFlag | MissingValue | PathsAndSinceTogether | EmptyPaths | UnrecognizedProfile` — **no `UnrecognizedMode`**, data-model §2), `ExitDecision` (5 cases — data-model §3), `Model`/`Msg`/`Effect` (the exact ShipCommand vocabulary, `ArtifactKind = VerifyArtifact` — data-model §4/§5), and `parse`/`init`/`update`/`render`/`exitCode`. **`applyExecution` is the F052 function called verbatim from `update`, not re-declared on `Loop`'s surface** (the "reused-verbatim shape" means same signature/semantics; it does not appear in `Loop.fsi` or the surface baseline).
- [X] T008 Author `src/FS.GG.Governance.VerifyCommand/Interpreter.fsi` — `ArtifactWriter`, `OutputSink`, the `Ports` bundle **IDENTICAL to ShipCommand's** (`Files`/`Git`/`Freshness`/`Store`/`Execute`/`Write`/`Out`, plus the opt-in store-persist port), `val realPorts: repo:string -> Ports`, `val step: Ports -> Loop.Effect -> Loop.Msg`, `val run: Ports -> Loop.RunRequest -> Loop.Model` (depends on T007; data-model §4).
- [X] T009 Exercise both command `.fsi` + the `VerifyJson.fsi` surface to prove they compile and compose before/with the `.fs` bodies (Constitution I). Depends on T006–T008. **Method used: `dotnet build` of both `src` projects (the F055 precedent) plus the semantic suites loading the packed public surface — `VerifyJson` built green first, then `VerifyCommand` against the curated `.fsi` contracts, then both `.Tests` suites compose `Loop.parse`/`Loop.update`/`Interpreter.run`/`VerifyJson.ofVerifyDecision` through the loaded surface.**
- [X] T010 [P] Add `tests/FS.GG.Governance.VerifyCommand.Tests/Support.fs` — a `withTempRepo` helper materializing a governed F014 catalog + governing source files in a temp dir and cleaning up (the F016/ShipCommand `withTempRepo` precedent); include fixture builders for: **clean** (all selected checks pass), **blocking** (a blocking-severity check unmet), **advisory-only** (only an advisory check unmet), **nothing-to-verify** (change touches no governed path), **stale-vs-fresh** (a prior evidence-reuse store so a re-run can reuse, plus an input flip that staleness one check), and **uncertain** (a blocking-severity selected check whose execution reports `Uncertain`/unrecoverable rather than pass or fail — to prove FR-005's no-coercion guarantee distinctly from a plain blocking-unmet check).
- [X] T011 [P] Add `tests/FS.GG.Governance.VerifyCommand.Tests/SurfaceDriftTests.fs` and `tests/FS.GG.Governance.VerifyJson.Tests/SurfaceDriftTests.fs` — load the public surface, compare to `surface/FS.GG.Governance.VerifyCommand.surface.txt` / `…VerifyJson.surface.txt`, honor `BLESS_SURFACE=1` (mirror the existing surface-drift test). Baselines committed in Phase 6 once `.fs` bodies stabilize.

**Checkpoint**: Contracts compile, FSI green, fixtures and surface harness in place — story work can begin.

---

## Phase 3: User Story 1 — Verify a change locally before opening a PR (Priority: P1) 🎯 MVP

**Goal**: One `fsgg verify` invocation that senses scope (F016), loads the F014 catalog, **selects** profile-appropriate checks (F015 `Routing.route` → F018 `Gates.buildRegistry` → F017 `Findings` → F019 `Route.select`), **runs** the stale checks and **reuses** the fresh ones (F046/F041/F051/F052), **rolls** the result up via F024 `Ship.rollup` + F052 `applyExecution` threaded with `RunMode.Verify`, prints a human verdict, and exits with the right code among the five.

**Independent Test**: Clean fixture ⇒ passing verdict listing ran/reused checks, exit 0 (US1.1/SC-001). Blocking fixture ⇒ failing verdict naming the unmet blocking check, exit 1 distinct from 2/3/4 (US1.2/SC-002). Advisory-only fixture ⇒ warning surfaced, verdict still passing, exit 0 (US1.3). Nothing-to-verify ⇒ "nothing to verify", exit 0 (US1.4/FR-012).

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T012 [P] [US1] `ParseTests.fs` — `Loop.parse`: valid argv → `Ok RunRequest` (defaults: `Profile=Standard`, `Format=Text`, `Scope=DefaultRange`, `VerifyOut=<repo>/readiness/verify.json`, `StorePath=<repo>/readiness/evidence-reuse.json`, `PersistStore=false`); unknown flag / missing value / `--paths`+`--since` together / empty `--paths` / unrecognized `--profile` → `Error UsageError`; **no I/O on rejection** (cli.md flag + invocation tables). Assert the **flags-only / leading-verb** contract: a leading bare `verify` token is tolerated and dropped, any other leading positional → `Error UsageError`. Assert **no `--mode`**: a `--mode` flag → `Error (UnknownFlag "--mode")` (FR-017 — verify cannot be escalated to the `Gate` verdict).
- [X] T013 [P] [US1] `LoopTests.fs` — pure transitions: `init` emits `SenseScope`; `Sensed(Ok)` emits `LoadCatalog`; `Loaded(valid)` runs the F015→F018→F017→F019 selection purely and emits `SenseFreshness` + `LoadStore`; the freshness/store join decides `ExecuteGates` for the must-recompute command-gates only; `GatesExecuted` computes `decision = Ship.rollup … RunMode.Verify` then `applyExecution`, sets `ExitDecision` from `ExitCodeBasis` (Clean→Success, Blocked→Blocked), and emits `EmitSummary`. **Assert the emitted-effect lists** (Constitution IV) and that `RunMode.Verify` (not `Gate`) is threaded.
- [X] T014 [P] [US1] `LoopTests.fs` — `exitCode`: `Success`→0, `Blocked`→1, `UsageError'`→2, `InputUnavailable`→3, `ToolError`→4 (cli.md exit-code table, SC-005).
- [X] T015 [P] [US1] `LoopTests.fs` — **empty selection**: a `Loaded(valid)` whose selection is empty short-circuits to a passing "nothing to verify" verdict, `ExitDecision=Success`, no `ExecuteGates`/`SenseFreshness` work beyond what an empty set needs (US1.4/FR-012).
- [X] T016 [P] [US1] `EndToEndTests.fs` — via `Interpreter.run` with `realPorts` over `withTempRepo` fixtures (reused F015–F052 cores **never** mocked; only a deterministic `Execute` fake + capturing `Write`/`Out`): clean ⇒ passing verdict, ran/reused checks listed, `ExitDecision=Success`; blocking ⇒ `Blocked`, the unmet check named under blockers, exit 1; advisory-only ⇒ advisory under warnings, verdict passing, exit 0; nothing-to-verify ⇒ "nothing to verify", exit 0; **uncertain** ⇒ a blocking-severity `Uncertain`/unrecoverable check result is surfaced as such, **never coerced to passing**, and drives `Blocked`/exit 1 through the reused `Ship.rollup` at `RunMode.Verify` (FR-005, spec edge case; distinct from a clean pass) (SC-001/SC-002/SC-006).

### Implementation for User Story 1

- [X] T017 [US1] `Loop.fs` — `parse` (argv → `RunRequest`/`UsageError`, fixed `RunMode.Verify`, leading-`verify` tolerated), `init`, pure `update` (scope sense → catalog → F015→F018→F017→F019 selection → freshness/cache-eligibility join → `ExecuteGates` for stale-only → `Ship.rollup` at `RunMode.Verify` + `applyExecution` → `Decision`; resolves `ExitDecision`; empty selection ⇒ "nothing to verify" Success), `render` (text verdict: pass/blocked, blockers w/ reason, warnings, ran/reused checks), `exitCode`. Makes T012/T013/T014/T015 pass.
- [X] T018 [US1] `Interpreter.fs` — `Ports`, `realPorts repo` (binds the **reused** edges verbatim: `Config.Loader.fileSystemReader repo`, `Snapshot.Interpreter.realPorts repo`, the F046 `FreshnessSensor`/`StoreReader`, the F051 `GateExecution` `ExecutionPort`, an atomic temp+rename `ArtifactWriter`, the opt-in store-persist writer, and a `Console.Out` sink), TOTAL+SAFE `step` dispatch over `Effect` (every port `Error`/exception reified to its `Msg`, never throws), `run` loop folding `Msg`→`update` to a terminal `Model`. Makes T016 pass. Depends on T017.
- [X] T019 [US1] `Program.fs` — thin `[<EntryPoint>]`: `Loop.parse argv` → on `Error` print usage to stderr + `exit 2`; on `Ok` build `realPorts` + `Interpreter.run` + emit + `exit (Loop.exitCode model.Exit)`. stderr diagnostics tagged `fsgg verify [<category>]: <message>`, distinguishing input vs tool defect (cli.md stderr, Constitution VI).

**Checkpoint**: MVP — local verification works end to end against real fixtures; selection + run/reuse + `RunMode.Verify` rollup wired; five exit codes distinguishable; currency findings and `verify.json` not yet surfaced.

---

## Phase 4: User Story 2 — Reuse fresh evidence and report what is stale (Priority: P2)

**Goal**: Surface **currency findings** as first-class output — per selected check: fresh/reused (carrying the opaque `EvidenceRef`) vs stale/recomputed (carrying `cause`: `NoPriorEvidence` or `InputsChanged categories`) vs recompute-by-default (carrying the missing freshness tokens); flag a stale generated view via its changed categories; carry each finding's enforcement-assigned severity. Computed **purely** from `Model.Sensed`/`Store`/`SelectedGates`/`Outcomes` (the inputs ship's `cacheLinesOf` uses) — no new sensing, no new severity path.

**Independent Test**: Run twice with no change ⇒ 100% of selected checks reported fresh/reused, none recomputed (SC-003); change one check's inputs ⇒ exactly that check recomputed and reported stale-then-recomputed with its changed `categories`; a stale generated view is flagged carrying its enforcement-assigned severity (US2.1–2.3).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T020 [P] [US2] `CurrencyTests.fs` — the pure currency computation over crafted `Sensed`/`Store`/`SelectedGates`/`Outcomes`: cache verdict `Reusable ref` ⇒ a **fresh/reused** finding carrying the verbatim `EvidenceRef`; `MustRecompute (InputsChanged categories)` ⇒ a **stale/recomputed** finding carrying the changed `categories`; `MustRecompute NoPriorEvidence` ⇒ stale with `noPriorEvidence`; freshness `Unresolved` ⇒ a **recompute-by-default** finding carrying the missing tokens. Each finding's severity equals the matching `EnforcedItem.Decision` severity in the `ShipDecision` partition (data-model §6, research D4) — verify builds **no** new `EnforcedItem`.
- [X] T021 [P] [US2] `ReuseTests.fs` — via `Interpreter.run` over the stale-vs-fresh fixture with `--persist-store`: run 1 populates the store; run 2 (no change) reuses **every** selected check (`Execute` invoked zero times for reused checks), all reported fresh; then flip one check's inputs and re-run ⇒ exactly that check recomputed and reported stale-then-recomputed with its `categories` (SC-003, US2.1/US2.2).
- [X] T022 [P] [US2] `DegradeTests.fs` — a freshness or store **sensing** `Error` degrades to a safe default + a non-fatal currency note and **never** changes the verdict or `ExitDecision` (sets `StoreDegraded`, appends a plain-string note; not an exit code — cli.md, FR-010/FR-013, research D7).
- [X] T023 [P] [US2] `CurrencyTests.fs` — a **stale generated view** (its declared sources changed) is flagged in the currency findings carrying the severity the enforcement dials assign; a blocking-severity currency finding rides the **existing** rollup to Blocked, an advisory one is a warning only (verify invents no second route to Blocked — US2.3, research D2, plan decision 3).

### Implementation for User Story 2

- [X] T024 [US2] `Loop.fs` — extend `update`/`Model` with the pure currency-findings computation (analogous to ship's `cacheLinesOf`, reading `Sensed`/`Store`/`SelectedGates`/`Outcomes`) and append non-fatal degrade notes; **the verdict and exit remain driven only by `Ship.rollup`/`applyExecution`** (no new severity path). Extend `render` to print the currency section (fresh/reused, stale/recomputed + categories, recompute-by-default + missing tokens, degrade notes). Makes T020/T021/T022/T023 pass.

**Checkpoint**: US1 + US2 work; currency is first-class in the text verdict; freshness/store failures degrade safely without perturbing the verdict.

---

## Phase 5: User Story 3 — Deterministic `verify.json` for tooling and pre-PR CI (Priority: P3)

**Goal**: `--json` / `--verify-out` produces a byte-deterministic `verify.json` projecting the verdict, per-check execution outcomes, the blocking/advisory split, and the currency section; `--json` stdout equals the persisted file verbatim (one source of truth); a tool error leaves no partial artifact.

**Independent Test**: Two runs over identical state + identical outcomes ⇒ byte-identical `verify.json` (SC-004); `--json` stdout == the file; the document carries `schemaVersion` `fsgg.verify/v1` and no timestamp/abs-path/username/machine-specific content (US3.1–3.3).

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T025 [P] [US3] `tests/FS.GG.Governance.VerifyJson.Tests/VerifyJsonTests.fs` — `ofVerifyDecision` shape: fixed top-level field order (`schemaVersion`,`verdict`,`exitCodeBasis`,`blockers`,`warnings`,`passing`,`currency`); each enforced item carries `id`/`enforcement{base,maturity,mode,profile,effective,reason}`/optional `cache`/optional `execution`; `currency` has `fresh`/`recomputed`/`unresolved` arrays with the data-model §7 shapes (`recomputed.cause` = `"noPriorEvidence"` or `{kind:"inputsChanged",categories:[…]}`) (verify.schema.md).
- [X] T026 [P] [US3] `tests/FS.GG.Governance.VerifyJson.Tests/DeterminismTests.fs` — `ofVerifyDecision` called twice on the same inputs is byte-identical; FsCheck property over reordered inputs yields identical bytes; assert no clock/abs-path/username/env content (FR-007/FR-008/SC-004).
- [X] T027 [P] [US3] `tests/FS.GG.Governance.VerifyJson.Tests/GoldenTests.fs` — `ofVerifyDecision` over a fixed fixture equals a committed golden baseline file (SC-004).
- [X] T028 [P] [US3] `tests/FS.GG.Governance.VerifyCommand.Tests/DeterminismTests.fs` — full `Interpreter.run` writing `verify.json` over a fixture twice ⇒ byte-identical artifact (SC-004); and the `--json` stdout string equals the persisted file verbatim (FR-007, one source of truth, US3.2).
- [X] T029 [P] [US3] `tests/FS.GG.Governance.VerifyCommand.Tests/PersistenceEdgeTests.fs` — atomic write via a faked failing `ArtifactWriter`: a failed/interrupted `Write` ⇒ `ToolError` (exit 4) and **no** partial `verify.json` left behind, distinct from a Blocked verdict (FR-013/US3, cli.md exit 4).

### Implementation for User Story 3

- [X] T030 [US3] `src/FS.GG.Governance.VerifyJson/VerifyJson.fs` — hand-driven compact `Utf8JsonWriter` walk (AuditJson/RouteJson/ReleaseJson precedent): `schemaVersion` literal `"fsgg.verify/v1"`, exhaustive token helpers for every enum (no wildcard), `blockers`/`warnings`/`passing` in the cores' fixed order, the `currency` section (`fresh`/`recomputed`/`unresolved`), no timestamp/abs-path/username/machine-specific content. Makes T025/T026/T027 pass.
- [X] T031 [US3] `Loop.fs` — extend `update`: after the decision is rolled, emit `WriteArtifact(VerifyArtifact, VerifyOut, VerifyJson.ofVerifyDecision decision cacheReport outcomes)`; handle `Wrote(Ok)`→Done and `Wrote(Error)`→`ToolError`; `render` honors `Text`/`Json` (`--json` prints the document verbatim and suppresses text). Depends on T030.
- [X] T032 [US3] `Interpreter.fs` — confirm the `Write` port is an atomic temp-then-rename `ArtifactWriter` returning `Result<unit,string>` (no partial file on failure) and the opt-in `PersistStore` write is non-fatal. Makes T028/T029 pass. Depends on T031.

**Checkpoint**: All three stories work; deterministic, CI-consumable `verify.json` emitted on request; `--json` stdout == the file.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: The failure-mode exit-code matrix, network-free guarantee, no-mutation guarantee, surface/golden baselines, docs, and the quickstart validation pass.

- [X] T033 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/FailureTests.fs` — the five-way exit matrix via `Interpreter.run` over fixtures + faked ports (cli.md exit table, SC-005): absent/invalid catalog ⇒ `InputUnavailable` (exit 3) with a tagged input diagnostic and **no** partial artifact; git-sensing unavailable ⇒ `InputUnavailable` (exit 3); bad argv ⇒ `UsageError'` (exit 2), no artifact; unwritable `--verify-out` ⇒ `ToolError` (exit 4), no partial artifact — each distinct, no fabricated passing verdict (FR-009/FR-010/FR-016).
- [X] T034 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/ScopeGuardTests.fs` — assert no `System.Net`/`HttpClient` reference in the command's reachable assembly surface (the F054/ShipCommand scope-guard precedent); reads are `System.IO`-only (FR-014/SC-007).
- [X] T035 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/NoMutationTests.fs` — snapshot the `withTempRepo` tree (relative paths + content hashes) before `Interpreter.run`; run writing `--verify-out` **outside** the repo and assert the tree is byte-for-byte unchanged; repeat with `--verify-out` *inside* the repo (and without `--persist-store`) and assert the only added/changed path is the requested `verify.json` (FR-013 — no repository mutation other than the requested artifact and the opt-in store).
- [X] T036 Bless and commit `surface/FS.GG.Governance.VerifyCommand.surface.txt` and `surface/FS.GG.Governance.VerifyJson.surface.txt` (`BLESS_SURFACE=1 dotnet test …`), then re-run the drift tests green (T011).
- [X] T037 [P] Commit the `verify.json` golden baseline file referenced by T027 under `tests/FS.GG.Governance.VerifyJson.Tests/` (or `specs/056-verify-command/contracts/`), generated from the stable `ofVerifyDecision`.
- [X] T038 [P] Update `CLAUDE.md` and the Phase 13 roadmap row "Define Governance `fsgg verify` and `fsgg release` schemas and exit codes": mark it **complete** (F056 lands the pending `fsgg verify` host + `verify.json`); note the F014–F052 cores untouched, no declaration surface, and verify = the ShipCommand pipeline at fixed `RunMode.Verify`.
- [X] T039 Run the `quickstart.md` validation checklist (Scenarios 1–8 / SC-001…SC-007) against built fixtures; record evidence (exit codes, `diff`/`cmp` byte-identical, `--json`==file, surface/golden green, scope-guard green) and tick the checklist boxes.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001–T004 parallel, T005 after them.
- **Foundational (Phase 2)** → after Setup. T006–T007 parallel; T008 after T007; T009 after T006–T008; T010–T011 parallel. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP.
- **US2 (Phase 4)** → after US1 (`Model`/`update` carry the `Sensed`/`Store`/`Outcomes` the currency computation reads); the pure currency computation (T020) is independently testable.
- **US3 (Phase 5)** → after Foundational; the `VerifyJson` library (T030) is independently testable, but T031/T032 integrate with US1's `Loop`/`Interpreter` and project US2's currency findings.
- **Polish (Phase 6)** → after the desired stories; T036/T037/T039 need the `.fs` bodies stable.

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- `.fsi` (Phase 2) before `.fs`; `Loop.fs` before `Interpreter.fs` before `Program.fs`.

### Parallel opportunities

- Phase 1: T001–T004 together.
- Phase 2: the `.fsi` (T006–T007, then T008-after-T007), and T010/T011, in parallel.
- Each story's `[P]` test tasks run together; once Foundational lands, the `VerifyJson` library work (T030 and its tests T025–T027) can be staffed in parallel with US1's command work.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL) → 3. Phase 3 US1 → **STOP & VALIDATE** (SC-001/SC-002/SC-005/SC-006 with text output) → demo local verification with the five exit codes.

### Incremental delivery

Setup + Foundational → US1 (MVP local verify gate) → US2 (currency findings) → US3 (`verify.json`) → Polish. Each story adds value without breaking the prior.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Reuse, don't reinvent: F015 `Routing.route`, F018 `Gates.buildRegistry`, F017 `Findings`, F019 `Route.select`, F024 `Ship.rollup`, F052 `applyExecution`, and the F046/F041/F051/F052 freshness/reuse/execution cores are called **verbatim** at `RunMode.Verify` — never mocked in end-to-end tests (Constitution V); only the edge `Files`/`Git`/`Freshness`/`Store`/`Execute`/`Write`/`Out` ports are faked for unit coverage.
- Verify never invents a second route to `Blocked` and never coerces an uncertain result to pass (plan decision 3, research D2): currency is a **projection**, the verdict is **only** `Ship.rollup`/`applyExecution`.
- Elmish/MVU applies (stateful, I/O-bearing): `.fsi` contract (T007/T008), pure transition tests (T013), emitted-effect assertions (T013), and real-interpreter evidence (T016) are explicit tasks. `VerifyJson` is a pure leaf — no MVU ceremony.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the task line.

---

## Implementation status (2026-06-24) — COMPLETE

All 39 tasks landed. **63 tests green** (19 `FS.GG.Governance.VerifyJson.Tests` + 44
`FS.GG.Governance.VerifyCommand.Tests`); the full solution builds clean (0 warnings,
`TreatWarningsAsErrors=true`).

- **Two new `src` projects** built to the exact ShipCommand/AuditJson shape:
  `FS.GG.Governance.VerifyJson` (pure `ofVerifyDecision` projection, `fsgg.verify/v1`) and
  `FS.GG.Governance.VerifyCommand` (MVU `Loop`/`Interpreter`/`Program`). The F014–F052 cores
  are reused **verbatim, not edited**; no `Declaration` adapter, no new sensing, no YamlDotNet,
  no AuditJson reference.
- **Verify = the ShipCommand pipeline at a fixed `RunMode.Verify`.** No `--mode` flag (a `--mode`
  argument is an `UnknownFlag`); `--profile` is the only enforcement lever. The verdict is
  **only** `Ship.rollup` + F052 `applyExecution`; currency is a projection; an uncertain
  (exit-125) blocking check is never coerced to pass (FR-005, proved distinctly from a clean pass).
- **Currency findings** are first-class in the text render (fresh/reused, stale/recomputed +
  categories, recompute-by-default + missing tokens), each carrying its owning gate's
  enforcement-assigned effective severity; freshness/store sensing failures degrade to a safe
  default + a non-fatal note that never perturbs the verdict or exit.
- **Surface baselines + golden committed**: `surface/FS.GG.Governance.VerifyCommand.surface.txt`,
  `surface/FS.GG.Governance.VerifyJson.surface.txt`, and
  `tests/FS.GG.Governance.VerifyJson.Tests/verify.golden.json` (blessed; drift tests green).
- **Vertical slice exercised**: the real `fsgg verify` host binary was driven against a real
  temp git repo through its public CLI — passing/clean verdict, currency section, `--json`
  document (== the persisted `verify.json` byte-for-byte), `verify.json` write, and the five-way
  exit codes all confirmed.
- **Synthetic disclosure (Principle V)**: the only synthetic substitutes are the faked F046
  freshness sensor (fixed literal digests, `// SYNTHETIC:` at the use site; the real sensor is
  proven over real bytes in `FreshnessSensing.Tests`) and the disclosed-synthetic `EvidenceRef`
  literals in fixtures — the F015–F052 cores are **never** mocked in the end-to-end tests.
