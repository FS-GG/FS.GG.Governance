---
description: "Task list for fsgg ship host command (F026)"
---

# Tasks: `fsgg ship` Host Command (Protected-Branch Verdict)

**Input**: Design documents from `/specs/026-fsgg-ship-command/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (Loop.fsi, Interpreter.fsi, fsgg-ship-command.md)

**Tests**: INCLUDED — Principle V makes test evidence mandatory for this Tier-1
host edge (pure transition tests, faked-ports interpreter tests, and one
real-temp-git end-to-end proof). Write/assert tests against the packed public
surface (`parse`/`init`/`update`/`render`/`exitCode`, `Interpreter.run`), never
internals.

**Organization**: Tasks are grouped by user story. Phases run in sequence;
tasks within a phase marked `[P]` may run in parallel. Tier is Tier 1
throughout (matches the spec); no per-task tier annotation needed.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (distinct file)
- **[Story]**: `[US1]`..`[US4]`; unlabelled = shared setup/foundation/polish
- Exact file paths are given in each task.

## ⚠️ Same-file ordering note

`Loop.fs` and `Interpreter.fs` are each a single file touched by several user
stories (US1/US2/US3/US4 all land logic in `Loop.fs`). Those tasks are **not**
`[P]` with each other — they edit the same file and run in the listed order.
The story grouping below still lets each story be *tested* independently, but
the production edits are layered onto the one MVU core in priority order
(US1 → US2 → US4 → US3), as the plan's "F022 shape reused + one new behavior"
intends.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the project + test project skeleton and wire references — the `RouteCommand` shape, minus the two projections it does not use, plus the three Phase-5 cores it newly composes.

- [X] T001 Create `src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj` mirroring `RouteCommand.fsproj`: `<OutputType>Exe</OutputType>`, `RootNamespace` `FS.GG.Governance.ShipCommand`, **`<IsPackable>false</IsPackable>`** and **no** `PackAsTool`/`ToolCommandName`/`PackageId` (single-`fsgg`-tool unification is deferred — research D1, plan). `Compile` order: `Loop.fsi`, `Loop.fs`, `Interpreter.fsi`, `Interpreter.fs`, `Program.fs`. `PackageReference`: `FSharp.Core` only. **Incremental-build note**: only list a `Compile Include` once its file is authored (or commit minimal stub files in this phase) — the fsproj does not build green until the Phase-2/3 files exist; do not expect a green build at the Phase-1 checkpoint.
- [X] T002 Add the nine `ProjectReference`s to the fsproj (T001): `Config` (F014), `Snapshot` (F016), `Routing` (F015), `Findings` (F017), `Gates` (F018), `Route` (F019), `Enforcement` (F023), `Ship` (F024), `AuditJson` (F025). **Do NOT** reference `RouteJson`/`GatesJson` (this command projects neither — plan). No new third-party package.
- [X] T003 Create `tests/FS.GG.Governance.ShipCommand.Tests/FS.GG.Governance.ShipCommand.Tests.fsproj` mirroring `RouteCommand.Tests.fsproj` (Expecto + FsCheck via VSTest). `ProjectReference`s — the ShipCommand project **plus the same nine cores `RouteCommand.Tests` references** so tests can construct typed values (`Ship.rollup`, `AuditJson.ofShipDecision`, build catalog facts): `../../src/FS.GG.Governance.ShipCommand/…`, then `Config`, `Snapshot`, `Routing`, `Findings`, `Gates`, `Route`, `Enforcement`, `Ship`, `AuditJson`. `Compile` order `Support.fs`, `ParseTests.fs`, `LoopTests.fs`, `InterpreterTests.fs`, `FailureTests.fs`, `EndToEndTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (same incremental-include note as T001 — add each include as its file is authored, or stub).
- [X] T004 Register both projects in `FS.GG.Governance.sln` (mirror the existing `RouteCommand` / `RouteCommand.Tests` entries) and confirm `dotnet sln list` shows them.

**Checkpoint**: Both projects appear in `dotnet sln list` and their `ProjectReference` graph resolves. A green `dotnet build` is **not** expected here — it follows file authoring in Phase 2–3 (see the incremental-include note on T001/T003).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Author the public `.fsi` contracts into `src/` and stand up the FSI sketch + test fakes that every story depends on. Principle I (FSI before `.fs` body) and Principle II (`.fsi` is the sole surface) live here.

**⚠️ CRITICAL**: No user-story `Loop.fs`/`Interpreter.fs` body work begins until this phase is complete.

- [X] T005 Author `src/FS.GG.Governance.ShipCommand/Loop.fsi` from `contracts/Loop.fsi` verbatim-in-intent: namespace `FS.GG.Governance.ShipCommand`; module `Loop`; types `ScopeSelector`, `OutputFormat`, `RunRequest` (incl. `Mode: RunMode`/`Profile: Profile`/`AuditOut`), `UsageError` (incl. `UnrecognizedMode`/`UnrecognizedProfile`), `ExitDecision` (incl. `Blocked`), `ArtifactKind` (`AuditArtifact` only), `Effect`, `Msg`, `Diagnostic`, `Phase` (incl. `Rolled`), `Model`; vals `parse`/`init`/`update`/`render`/`exitCode`.
- [X] T006 Author `src/FS.GG.Governance.ShipCommand/Interpreter.fsi` from `contracts/Interpreter.fsi`: module `Interpreter`; types `ArtifactWriter`, `OutputSink`, `Ports` (`Files: Loader.FileReader`, `Git: Snapshot.Ports`, `Write`, `Out`); vals `realPorts`/`step`/`run`. (Depends on T005 — `Interpreter` references `Loop` types.)
- [X] T007 [P] Extend `scripts/prelude.fsx` with the F026 FSI sketch (per quickstart "FSI sketch"): load the Ship surface and exercise `Loop.parse` (defaults + `UsageError` cases incl. `UnrecognizedMode`), `Loop.exitCode` (`0/1/2/3/4`), and the `Loaded(Valid)` `update` step shape — no I/O. This is the Principle-I design-first proof before any `.fs` body.
- [X] T008 Create `tests/FS.GG.Governance.ShipCommand.Tests/Support.fs` (port of `RouteCommand.Tests/Support.fs`): in-memory `Config.Loader.FileReader`, an in-memory `Snapshot.Ports` over a fixed `RepoSnapshot`, a capturing `ArtifactWriter` (records `path`+`content`, returns `Ok`/injectable `Error`) and capturing `OutputSink`; a minimal valid `.fsgg` catalog builder; helpers to build a change that selects a **base-blocking** gate and one that selects **passing-only** items; and a `withTempRepo` real-git + real-catalog fixture helper (F022 reuse) for the end-to-end proof; and expose `repoRoot` (consumed by `SurfaceDriftTests.fs` to locate the baseline — T033).
- [X] T009 Create `tests/FS.GG.Governance.ShipCommand.Tests/Main.fs` (Expecto entry point running all test lists).

**Checkpoint**: `.fsi` contracts compile; FSI sketch runs; test harness builds with empty/`pending` test lists.

---

## Phase 3: User Story 1 - Emit the protected-branch verdict and exit accordingly (Priority: P1) 🎯 MVP

**Goal**: Compose scope→load→route→registry→findings→select→**rollup→project**→persist→summarize→**exit-from-basis**, write `audit.json`, and exit `0` on `Clean` / the distinct `1` on `Blocked`.

**Independent Test**: In a temp repo with a minimal valid catalog, a base-blocking change under `--mode gate --profile standard` ⇒ `audit.json` `verdict:fail`/`exitCodeBasis:blocked`, exit 1, bytes = F025 projection of the same `Ship.rollup`; a passing-only change ⇒ `verdict:pass`/`clean`, exit 0.

### Tests for User Story 1 (write FIRST, ensure they FAIL)

- [X] T010 [P] [US1] In `tests/FS.GG.Governance.ShipCommand.Tests/LoopTests.fs`: pure `update` tests over literal `Model`/`Msg` — `Loaded(Valid facts)` runs route→registry→findings→select→`Ship.rollup`→`AuditJson.ofShipDecision` and emits exactly one `WriteArtifact(AuditArtifact, request.AuditOut, doc)` whose `doc` equals `AuditJson.ofShipDecision (Ship.rollup result Mode Profile)`; terminal `Emitted` maps `ExitCodeBasis.Clean → Success` and `Blocked → Blocked` (US1 AS1/AS2). Plus `exitCode` total mapping `Success 0 | Blocked 1 | UsageError' 2 | InputUnavailable 3 | ToolError 4`.
- [X] T011 [P] [US1] In `LoopTests.fs`: `render model Text` states the verdict + exit-code basis, lists blockers/warnings/passing each with identity and base/effective severity, lists unknown-governed-path findings, and reports the written path (US1 AS3). Deterministic for a fixed `Model`.
- [X] T012 [P] [US1] In `tests/FS.GG.Governance.ShipCommand.Tests/InterpreterTests.fs`: faked-ports `Interpreter.run` over a base-blocking change (`--paths` selecting the blocking gate) asserts captured `audit.json` bytes = `AuditJson.ofShipDecision (Ship.rollup …)`, terminal `Exit = Blocked` (US1 AS1, SC-001); and a passing-only change ⇒ `Exit = Success`, `verdict:pass` (US1 AS2, SC-001).
- [X] T013 [P] [US1] In `tests/FS.GG.Governance.ShipCommand.Tests/EndToEndTests.fs`: one real-temp-git + real-catalog proof through `Interpreter.realPorts` (the `withTempRepo` fixture) — full composition writes `readiness/audit.json`, asserts the verdict, the persisted bytes, and the exit code (SC-007). Real evidence; no synthetic.

### Implementation for User Story 1

- [X] T014 [US1] Create `src/FS.GG.Governance.ShipCommand/Loop.fs` `init`: build the initial `Model` (Phase `Parsed`) and emit `LoadCatalog` for `ExplicitPaths` (candidates set directly) or `SenseScope` first for `Since`/`DefaultRange` (data-model §6).
- [X] T015 [US1] In `Loop.fs` `update`: handle `Sensed(Ok snap)` (set `Candidates`, emit `LoadCatalog`) and the `Loaded(Valid facts)` composition — `Routing.route` → `Gates.buildRegistry` → `Findings.findUnknownGovernedPaths` → `Route.select` → `Ship.rollup result Mode Profile` → `AuditJson.ofShipDecision`; set `Phase = Rolled`, `Decision`, `AuditDoc`, emit one `WriteArtifact(AuditArtifact, AuditOut, doc)` (data-model §4). Reuse cores verbatim — re-derive/re-sort/re-serialize nothing (FR-004/FR-005).
- [X] T016 [US1] In `Loop.fs` `update`: handle `Wrote(AuditArtifact, Ok())` → `Phase = Persisted`, emit `EmitSummary(render model Format)`; and `Emitted` → `Phase = Done`, `Exit` mapped from `Decision`'s `ExitCodeBasis` (`Clean → Success`, `Blocked → Blocked`) (data-model §4, FR-008). (After T015.)
- [X] T017 [US1] Implement `Loop.render` (Text branch) and `Loop.exitCode` per the contract: Text summary (verdict, basis, partition w/ identity + base/effective severity, findings, written path); `exitCode` `0/1/2/3/4`. (Json branch lands in US3 T031.)
- [X] T018 [US1] Create `src/FS.GG.Governance.ShipCommand/Interpreter.fs` `step` + `run` for the success path: execute `SenseScope`/`LoadCatalog`/`WriteArtifact`/`EmitSummary` against `Ports` and reify results to `Msg`; `run` threads `init`→`step`→`update` to `Done` returning the terminal `Model`. (Failure reification hardened in US4 T028.)
- [X] T019 [US1] Create `src/FS.GG.Governance.ShipCommand/Program.fs` (thin edge): argv → `Loop.parse` → on `Ok` build `Interpreter.realPorts` + `Interpreter.run` → `exitCode model.Exit`; on `Error` print usage diagnostic and exit 2. Mirror `RouteCommand/Program.fs`.

**Checkpoint**: US1 is fully functional — `dotnet run -- ship --mode gate --profile standard` writes `audit.json`, prints the verdict, and exits 0/1; T010–T013 pass.

---

## Phase 4: User Story 2 - Choose the run mode and profile (Priority: P1)

**Goal**: Parse `--mode`/`--profile` in pure `parse` via the F023 recognizers (default `gate`/`standard`), thread them into the rollup, and record them in the audit document; an unrecognized lever is a `UsageError` decided before any port is built.

**Independent Test**: Over one repo + one change, two mode/profile combinations produce the two expected verdicts/partitions/exit codes, matching `Ship.rollup` under each lever set; a relaxed profile lands a base-blocking finding in `warnings` with base `Blocking`/effective `Advisory` (no-hide), pass/clean.

### Tests for User Story 2 (write FIRST, ensure they FAIL)

- [X] T020 [P] [US2] Create `tests/FS.GG.Governance.ShipCommand.Tests/ParseTests.fs`: `parse` normalizes argv → `RunRequest` (tolerates leading `ship` verb); `--mode`/`--profile` recognized via F023 (`gate`/`standard` etc.); omitted levers default to `Gate`/`Standard` (US2 AS4); unrecognized `--mode`/`--profile` ⇒ `UnrecognizedMode`/`UnrecognizedProfile` (US2 AS3); scope flags `--paths`/`--since`/default and exclusivity (`PathsAndSinceTogether`, `EmptyPaths`), `--audit-out` override, `--json` ⇒ `Format = Json`.
- [X] T021 [P] [US2] In `InterpreterTests.fs`: run the same change under two lever sets — a strict set lands the base-blocking finding in `blockers` (fail/blocked, exit 1) and a relaxing profile lands it in `warnings` (pass/clean, exit 0) carrying base `Blocking` + effective `Advisory` (US2 AS1/AS2, SC-003, no-hide FR-011); assert the audit bytes carry `mode`/`profile` per item.

### Implementation for User Story 2

- [X] T022 [US2] In `Loop.fs` `parse`: implement the argv matcher (mirror F022's explicit matcher) capturing raw `--mode`/`--profile` strings, then call `Enforcement.recognizeMode`/`recognizeProfile`, mapping `Unrecognized s` → `UnrecognizedMode`/`UnrecognizedProfile`; default omitted levers to `Gate`/`Standard`; build `RunRequest` (`Repo` default `"."`, `AuditOut` default `<repo>/readiness/audit.json`). Total/pure — usage problems are `UsageError` values, never exceptions. (Builds on the `RunRequest`/`init` from US1.)
- [X] T023 [US2] In `InterpreterTests.fs`: add a focused assertion that `update`'s `Ship.rollup` call (T015) threads `model.Request.Mode`/`Profile` end to end — run the same change under two distinct lever sets and assert each item's six-field enforcement detail in the persisted `audit.json` records the applied `mode`/`profile` (the levers flow through F024→F025 unchanged), confirming `Loop.fs` reads the request levers rather than a hardcoded default. (Depends on T021, T022.)

**Checkpoint**: US1 + US2 work — levers parsed, defaulted, applied, and recorded; T020–T021 pass.

---

## Phase 5: User Story 4 - Clear, safe failure distinct from a blocked verdict (Priority: P1)

**Goal**: Every tool failure (not-a-repo / unavailable git, missing-or-invalid catalog, unrecognized lever, unwritable output) surfaces a distinct actionable diagnostic and a tool-failure exit code in `{2,3,4}` — each distinct from the blocked code `1` — writing no partial/malformed `audit.json` and never a false verdict; the interpreter never throws.

**Independent Test**: Drive (a) non-git dir, (b) missing required `.fsgg`, (c) invalid `.fsgg`, (d) unrecognized `--mode`, (e) unwritable `--audit-out` — each yields a distinct diagnostic and an exit in `{2,3,4}` ≠ `1`; no artifact for the usage/input cases.

### Tests for User Story 4 (write FIRST, ensure they FAIL)

- [X] T024 [P] [US4] Create `tests/FS.GG.Governance.ShipCommand.Tests/FailureTests.fs`: `Sensed(Error _)` (non-git / unresolved rev) ⇒ `Done`/`InputUnavailable` (exit 3), no `WriteArtifact` emitted (US4 AS1); `Loaded(Invalid diags)` (missing/invalid catalog) ⇒ `Done`/`InputUnavailable` (exit 3), no write (US4 AS2).
- [X] T025 [P] [US4] In `FailureTests.fs`: unrecognized `--mode`/`--profile` via `parse` ⇒ `UsageError'` (exit 2), no ports built, no artifact (US4 AS3); and a faked `ArtifactWriter` returning `Error` ⇒ `Wrote(_, Error)` ⇒ `Done`/`ToolError` (exit 4), **never** `Blocked` (US4 AS4).
- [X] T026 [P] [US4] In `FailureTests.fs`: assert the four codes are mutually distinct and none equals the blocked code `1` (FR-009, SC-004), and that the interpreter never throws across all failure paths (FR-014).

### Implementation for User Story 4

- [X] T027 [US4] In `Loop.fs` `update`: handle the failure transitions — `Sensed(Error r)` and `Loaded(Invalid diags)` short-circuit to `Phase = Done` with a `Diagnostic { Category = InputUnavailable; Message = … }` and `Exit = InputUnavailable`, emitting no further effects; `Wrote(_, Error r)` ⇒ `Done`/`ToolError` (never `Blocked`). Diagnostics carry no clock/abs-path/env. (Builds on US1 `update`.)
- [X] T028 [US4] In `Interpreter.fs` `step`: make every effect total/safe — catch every port `Error` and every thrown exception and reify to the matching `Msg` (`Sensed(Error)`/`Loaded(Invalid)`/`Wrote(_, Error)`); never throw, never leave a partial artifact (the real `ArtifactWriter` writes via temp-file + atomic rename). (Hardens T018.)

**Checkpoint**: US1 + US2 + US4 work — blocked verdict (1) provably distinct from tool failures (2/3/4); T024–T026 pass; the interpreter is total.

---

## Phase 6: User Story 3 - Deterministic, machine-readable verdict for CI and agents (Priority: P2)

**Goal**: `--json` stdout = the F025 `audit.json` document verbatim (text suppressed) and equals the persisted file; the artifact + stdout + exit code are byte/value-identical across runs with fixed inputs and levers; the document carries the schema version and no clock/abs-path/env.

**Independent Test**: Run twice over identical inputs+levers ⇒ byte-identical `audit.json`, identical `--json` stdout, identical exit code; inspect the document for `schemaVersion` and absence of timestamp/absolute-path/env.

### Tests for User Story 3 (write FIRST, ensure they FAIL)

- [X] T029 [P] [US3] In `InterpreterTests.fs`: twice-run determinism — same faked inputs + levers ⇒ captured `audit.json` bytes identical, `--json` stdout identical, `Exit` identical (US3 AS1, SC-002); and `--json` stdout equals the persisted artifact content exactly (US3 AS2).
- [X] T030 [P] [US3] In `InterpreterTests.fs`: inspect the persisted document for the declared `schemaVersion` and assert it contains no wall-clock, machine-absolute path, or environment-derived value (US3 AS3, SC-005 — inherited from F025); and the empty-scope / empty-catalog clean-pass cases (SC-006) yield a valid empty-partition `audit.json` and exit 0.
- [X] T030a [P] [US3] In `InterpreterTests.fs` (SC-006 / FR-012 — routine-paths case, distinct from empty scope): run a change that touches **only routine unclassified paths** (paths the catalog does not govern, so routing reaches no gate) and assert a clean-pass run — valid empty-partition `audit.json`, `verdict:pass`/`exitCodeBasis:clean`, exit 0 — confirming routine paths are treated as information, never a default-deny. Add a complementary assertion that an unknown-governed-path finding's blocking-or-not is decided entirely by `Ship.rollup` under the levers (compare against the cores' output for the same finding), proving the host edge (`Loop.fs`) never itself decides blocking (FR-012).

### Implementation for User Story 3

- [X] T031 [US3] In `Loop.fs` `render`: implement the `Json` branch — emit `model.AuditDoc` (the F025 document text) verbatim so `--json` stdout equals the persisted file and inherits F025 byte-stability; the Text form is suppressed under `Json` (research D8, FR-007). (Builds on T017.)

**Checkpoint**: All user stories independently functional; T029–T030a pass; determinism + JSON contract proven.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Lock the public surface, finish docs, and run full validation.

- [X] T032 Create `tests/FS.GG.Governance.ShipCommand.Tests/SurfaceDriftTests.fs` (port of `RouteCommand.Tests/SurfaceDriftTests.fs`): the reflective surface renderer + dependency/scope-hygiene checks (reflection lives ONLY in the test, never the library — Principle III). It loads the `FS.GG.Governance.ShipCommand` assembly, renders the public surface of `Loop` + `Interpreter`, and asserts it equals `surface/FS.GG.Governance.ShipCommand.surface.txt` (via `Support.repoRoot` — T008); the dependency check asserts the boundary is the nine cores + BCL + FSharp.Core with NO edge into the kernel-era `Host`/`Cli` (research D1). **There is no external surface generator** — this test *is* the renderer.
- [X] T033 Capture `surface/FS.GG.Governance.ShipCommand.surface.txt` from the renderer in T032 (run the drift test once and write its rendered surface output as the committed baseline), matching the format of `surface/FS.GG.Governance.RouteCommand.surface.txt`. (After T032 — the baseline is produced *by* the test, then committed so the test is green and guards drift thereafter.)
- [X] T034 [P] Verify the shipped `Loop.fs`/`Interpreter.fs`/`Program.fs` carry **no** `private`/`internal`/`public` modifiers on top-level bindings (Principle II — visibility is presence/absence in the `.fsi`).
- [X] T035 [P] Run the `specs/026-fsgg-ship-command/quickstart.md` validation (build, `dotnet test` for the new test project, the CLI smoke, and the determinism/safe-failure `diff` checks); record evidence against the acceptance→evidence map.
- [X] T036 Whole-solution regression: `dotnet test FS.GG.Governance.sln` — confirm green and that no other project changed (the row is additive). Mark any synthetic evidence with the `Synthetic` token + use-site disclosure (Principle V).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1; `.fsi` (T005→T006) and `Support.fs` (T008) BLOCK all story bodies.
- **US1 (Phase 3)**: depends on Phase 2 — the MVP; everything else layers on its `Loop.fs`/`Interpreter.fs`.
- **US2 (Phase 4)**: depends on Phase 2; its `parse` (T022) is independent of US1's `update`, but its interpreter test (T021) needs US1's `run` (T018). Bodies edit the same `Loop.fs`, so run after US1.
- **US4 (Phase 5)**: depends on US1 `update`/`Interpreter` (T015–T018) — it hardens the same files with failure transitions.
- **US3 (Phase 6)**: depends on US1 `render`/`Interpreter` (T017–T018) — adds the `Json` branch + determinism assertions.
- **Polish (Phase 7)**: depends on the public surface being final (after US1–US4).

### Within each story

- Tests are written FIRST and must FAIL before the implementation tasks.
- `parse`/`init` before `update`; `update` before `render`/`exitCode`; `Loop` before `Interpreter` before `Program` (compile order).

### Parallel opportunities

- **Phase 1**: T001→T002 sequential (same fsproj); T003 [P] with them; T004 after T001/T003.
- **Phase 2**: T005→T006 sequential (`.fsi` dependency); T007, T008, T009 [P].
- **Tests within a story** (T010/T011/T012/T013; T020/T021; T024/T025/T026; T029/T030/T030a) are `[P]` — distinct files / distinct assertions.
- **Implementation within a story** edits the single `Loop.fs`/`Interpreter.fs` — **sequential, not `[P]`** (see same-file note).
- **Phase 7**: T034 [P], T035 [P] alongside the surface work; T032 (drift test) → T033 (capture baseline from it) sequential; T036 last.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (`.fsi` + fakes).
2. Phase 3 US1 → **STOP and VALIDATE**: temp repo, base-blocking ⇒ exit 1 / passing ⇒ exit 0, bytes = F025 of F024 rollup.

### Incremental delivery

1. Setup + Foundational → foundation ready.
2. US1 (MVP) → verdict + blocking exit code.
3. US2 → levers parsed/applied/recorded (defaults + no-hide).
4. US4 → safe failure provably distinct from the blocked code.
5. US3 → JSON contract + determinism hardening.

Each story is independently testable; production edits layer onto the one MVU core in priority order.

---

## Notes

- `[P]` = different file / independent assertion, no dependency on another incomplete task in the phase.
- This row reuses the F022 `RouteCommand` shape verbatim; the **one new behavior** is the `Blocked` exit category and the `Ship.rollup` + `AuditJson.ofShipDecision` composition appended after `Route.select`.
- It references **nine** cores (not `RouteJson`/`GatesJson`) and adds **no** new third-party package; `IsPackable=false` this slice (tool unification deferred).
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document it (`[-]` with rationale).
