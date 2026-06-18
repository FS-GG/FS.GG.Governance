---
description: "Task list for F12 - 012-cli: optional CLI tool for route, explain, contract, and evidence reports over a repository snapshot."
---

# Tasks: The CLI Tool - Route, Explain, Contract, and Evidence Reports for a Repo Snapshot

**Feature branch**: `012-cli` (active spec; git branch currently `main`)  
**Spec**: [`specs/012-cli/spec.md`](./spec.md)  
**Plan**: [`specs/012-cli/plan.md`](./plan.md)

**Input**: Design documents from `/specs/012-cli/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Cli.fsi](./contracts/Cli.fsi), [contracts/Project.fsi](./contracts/Project.fsi), [contracts/command-schema.md](./contracts/command-schema.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature that creates the first end-user command surface and a new optional packable tool artifact. The credible evidence is public-surface testing, not private helper calls: parser normalization, CLI MVU transitions, emitted `Effect` assertions, real fixture snapshot sensing, built command runs, packaged-tool smoke runs, stable JSON checks, read-only checks, deterministic exit decisions, surface drift, and dependency hygiene.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations are needed; every task matches the feature tier.

**Elmish/MVU**: **APPLIES**. The CLI is I/O-bearing and must expose `Model` / `Msg` / `Effect`, `init`, `update`, and an interpreter boundary through `Cli.run` and `Program.fs`. Pure command orchestration stays in `src/FS.GG.Governance.Cli/Cli.fs`; filesystem, Host execution, output writing, and process exit are edge effects. The task list includes explicit `.fsi` contract tasks, pure transition tests, emitted-effect assertions, and real edge/package evidence.

**Synthetic-evidence discipline (Principle V)**: Real filesystem fixtures, this repository's `.specify` tree, the built CLI entry point, and the packaged tool are the default evidence. Fresh agent calls are not reproducible test oracles, so tests that fake judge/review responses must carry `Synthetic` in the test name and a `// SYNTHETIC: fake judge/review response - real agent calls are not reproducible test evidence` comment at the fake port or fixture construction use site. Cache-only and budget-exhaustion tests do not need real agent calls.

**Text-output minimums**: Every text renderer must use deterministic ordering and name the command, mode, root/scope summary, exit category/code, budget summary when relevant, failures when present, and command-specific facts. `route` must name light/advisory/blocking state; `explain` must name top verdicts/proof roots; `contract` must name rule ids/statements; `evidence` must name declared/effective/freshness/cache/safe-failure states.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe - no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US6]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional tool project, test project, public contracts, and readiness scaffolding so the feature can type-check before behavior lands.

- [X] T001 Create `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` targeting `net10.0`, with `OutputType` `Exe`, `PackAsTool` `true`, `ToolCommandName` `fsgg-governance`, `PackageId` `FS.GG.Governance.Cli`, `IsPackable` `true`, no new runtime library dependency (the centrally pinned `FSharp.Core` language runtime is referenced so the executable/tool has a valid `.deps.json`), and `ProjectReference`s to `src/FS.GG.Governance.Kernel`, `src/FS.GG.Governance.Host`, `src/FS.GG.Governance.Adapters.Spi`, `src/FS.GG.Governance.Adapters.SpecKit`, and `src/FS.GG.Governance.Adapters.DesignSystem`.
- [X] T002 Copy `specs/012-cli/contracts/Project.fsi` to `src/FS.GG.Governance.Cli/Project.fsi` and `specs/012-cli/contracts/Cli.fsi` to `src/FS.GG.Governance.Cli/Cli.fsi` verbatim as the curated public surface.
- [X] T003 Add minimal `failwith "F12"` stub bodies in `src/FS.GG.Governance.Cli/Project.fs`, `src/FS.GG.Governance.Cli/Cli.fs`, and `src/FS.GG.Governance.Cli/Program.fs` that satisfy the `.fsi` contracts and keep `Program.fs` as the thin argv/stdout/stderr/exit edge.
- [X] T004 Create `tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj` with centrally pinned Expecto/FsCheck/VSTest packages, `IsPackable=false`, `GenerateProgramFile=false`, and a `ProjectReference` to `src/FS.GG.Governance.Cli`.
- [X] T005 [P] Add empty Expecto test modules `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs`, `MvuTests.fs`, `SnapshotTests.fs`, `OutputTests.fs`, `PackagingTests.fs`, `ReadOnlyTests.fs`, `SurfaceDriftTests.fs`, and `Main.fs` in compile order, with `Main.fs` running the assembly tests.
- [X] T006 Add `src/FS.GG.Governance.Cli` and `tests/FS.GG.Governance.Cli.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Create fixture directories under `tests/FS.GG.Governance.Cli.Tests/fixtures/` for `light`, `advisory`, `blocking`, `missing-input`, `stale-synthetic`, `review-miss`, `review-store-unavailable`, and `review-dispatch-failed` snapshots, with README notes tying each fixture to the relevant acceptance scenario.
- [X] T008 [P] Create `specs/012-cli/readiness/README.md` describing required transcripts: FSI session, built CLI smoke output, packaged tool smoke output, stable JSON comparison, budget run, and read-only diff evidence.
- [X] T009 [P] Extend `scripts/prelude.fsx` with an F12 design sketch that references `FS.GG.Governance.Cli.Project` and `FS.GG.Governance.Cli.Cli`, constructs a representative `RunRequest`, calls `Cli.init` / `Cli.update` shapes, and records the intended command-runner flow before real bodies land.

**Checkpoint**: `dotnet build src/FS.GG.Governance.Cli` and `dotnet test tests/FS.GG.Governance.Cli.Tests` compile against stubs; the solution lists the two new projects.

---

## Phase 2: Foundation

**Purpose**: write failing semantic tests for the shared composition root, parser contract, MVU shell, fixtures, and evidence obligations first; then implement the shared bodies every command story depends on. No user-story implementation should start before this phase is complete.

### Tests First

- [X] T010 Add foundational parser contract tests in `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs` for all shared options and defaults, including `--root` syntactic validation only; filesystem existence/readability MUST be deferred to snapshot loading and `InputUnavailable`.
- [X] T011 [P] Add foundational Project composition tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` that exercise `Project.identify`, `Project.bridge`, `Project.senseArtifact`, `Project.readContent`, `Project.compose`, `Project.toLoopConfig`, and `Project.evidenceReport` through the public `.fsi` shape against fixture facts.
- [X] T012 [P] Add foundational CLI MVU tests in `tests/FS.GG.Governance.Cli.Tests/MvuTests.fs` for `Cli.init`, `Cli.update`, emitted `LoadSnapshot` / `RunHost` / `WriteOutput` / `Finish` effects, parse-failure completion, snapshot-failure completion, and output-write failure completion.
- [X] T013 [P] Add foundational Program edge tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` or `ReadOnlyTests.fs` that drive injected edge ports for snapshot loading, Host execution, output writing, stdout/stderr behavior, and process exit selection without relying on private helpers.
- [X] T014 Add an evidence-obligations note at the top of `tests/FS.GG.Governance.Cli.Tests/MvuTests.fs` recording where Principle IV is verified: pure transition tests, emitted-effect assertions, real fixture snapshot evidence, packaged-tool evidence, and any `Synthetic` fake-judge disclosures.
- [X] T015 [P] Add shared fixture loading helpers inside `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` for the fixture roots from T007 and this repository's `.specify` tree, ensuring helper code does not mutate the governed root.
- [X] T016 Capture the Foundation FSI transcript from `scripts/prelude.fsx` to `specs/012-cli/readiness/fsi-session.txt`, including representative `Cli.init` / `Cli.update` paths and noting that the transcript is shape evidence before full story behavior.

### Implementation

- [X] T017 Implement all `Project.fsi` declarations in `src/FS.GG.Governance.Cli/Project.fs`: project types, active patterns, `identify`, `bridge`, `senseArtifact`, `readContent`, `compose`, `toLoopConfig`, and `evidenceReport`, preserving F01 identity semantics and adding no second router/evaluator/evidence engine.
- [X] T018 Implement the public CLI data types, `Cli.defaultJudge`, `Cli.exitCode`, `Cli.renderParseError`, and `Cli.parse` in `src/FS.GG.Governance.Cli/Cli.fs` for `route|explain|contract|evidence`, all shared options, syntactic root validation only, fixed exit codes `0`, `2`, `64`, `66`, and `70`, and no filesystem I/O.
- [X] T019 Implement the CLI MVU shell in `src/FS.GG.Governance.Cli/Cli.fs`: `Phase`, `Model`, `Msg`, `Effect`, `CliPorts`, `Cli.init`, `Cli.update`, and `Cli.run`, keeping `update` pure and emitting `LoadSnapshot`, `RunHost`, `WriteOutput`, and `Finish` effects instead of touching I/O.
- [X] T020 Implement the edge in `src/FS.GG.Governance.Cli/Program.fs`: build real `CliPorts`, normalize stdout/stderr/report-file output, call `Cli.run`, terminate with `Cli.exitCode`, and classify root existence/readability failures as snapshot/input failures rather than parse errors.

**Checkpoint**: Foundation semantic tests fail before implementation and pass after the shared bodies land; parser and MVU shell exist; fixture and evidence discipline is documented; stories can begin independently after Foundation.

---

## Phase 3: User Story 1 - `route` returns a short, explainable routing decision (Priority: P1) MVP

**Goal**: `route` reports light, advisory, and blocking decisions in text and deterministic JSON, naming mode, stakes, fences, rules, severities, and rendered checks.

**Independent Test**: run `route` against light/advisory/blocking fixture snapshots and this repository's `.specify` tree in `Inner` and `Gate` modes; verify text and JSON carry the same route facts deterministically.

### Tests First

- [X] T021 [P] [US1] Add parser tests in `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs` for `route` defaults, `--root`, `--mode sandbox|inner|gate`, `--format text|json`, `--json`, `--scope`, `--review-budget`, and normalized judge identity.
- [X] T022 [P] [US1] Add pure MVU tests in `tests/FS.GG.Governance.Cli.Tests/MvuTests.fs` asserting `Cli.init ["route"; "--root"; "."]` emits `LoadSnapshot`, successful `SnapshotLoaded` emits `RunHost`, successful `HostCompleted` emits `WriteOutput`, and `OutputWritten (Ok ())` emits `Finish`.
- [X] T023 [P] [US1] Add route output tests in `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` for light/no-gates, advisory, and blocking route payloads; assert text names command, mode, root/scope, exit category/code, route state, matched fence/rule/severity/rendered check where present, and budget/failure summaries; assert JSON includes stable `command`, `mode`, `exit`, `budget`, `failures`, and `payload` fields.
- [X] T024 [P] [US1] Add snapshot route tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` that load `fixtures/light`, `fixtures/advisory`, `fixtures/blocking`, and this repository's `.specify` tree through the public built command runner path, not private helper-only evaluation.

### Implementation

- [X] T025 [US1] Implement snapshot sensing for Spec Kit and design-system inputs in the real `CliPorts.LoadSnapshot` in `src/FS.GG.Governance.Cli/Program.fs`, producing `ProjectSnapshot` values from a read-only repository root and requested scope.
- [X] T026 [US1] Implement route payload selection in `src/FS.GG.Governance.Cli/Cli.fs`, deriving `RoutePayload` from the Host model and preserving light/no-gates routes as explicit output.
- [X] T027 [US1] Implement route text rendering in `src/FS.GG.Governance.Cli/Cli.fs`, using existing kernel route rendering where available and adding only CLI-owned compact summaries for command metadata and budget/failure facts.
- [X] T028 [US1] Implement route JSON rendering in `src/FS.GG.Governance.Cli/Cli.fs` with fixed field order and deterministic ordering for blocking/advisory entries.
- [X] T029 [US1] Record the independent US1 validation transcript in `specs/012-cli/readiness/route.txt` from `dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner` and fixture runs in `Inner` and `Gate` modes.

**Checkpoint**: US1 is fully functional and testable independently. This is the MVP.

---

## Phase 4: User Story 2 - CI receives deterministic exit decisions (Priority: P1)

**Goal**: the same command surface gives CI unambiguous process results: advisory findings exit `0`, failing blocking gates in `Gate` exit `2`, malformed usage exits `64`, unavailable input exits `66`, and unexpected tool defects exit `70`.

**Independent Test**: drive the command runner over advisory-only, blocking-failure, malformed invocation, unavailable-root, and output-write-failure cases; assert exit category, code, and output classification.

### Tests First

- [X] T030 [P] [US2] Add parser error tests in `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs` for missing command, unknown command, unknown option, missing option value, invalid mode, invalid format, invalid review budget, and invalid root syntax/spelling only; inaccessible or missing filesystem roots are snapshot/input failures, not parse failures.
- [X] T031 [P] [US2] Add pure MVU failure tests in `tests/FS.GG.Governance.Cli.Tests/MvuTests.fs` asserting parse failures finish with `UsageError`, snapshot load failures finish with `InputUnavailable`, and output-write failures finish with `ToolError`.
- [X] T032 [P] [US2] Add exit decision tests in `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` covering success/advisory, governed blocking, usage error, input unavailable, and tool error JSON/text renderings.
- [X] T033 [P] [US2] Add built command exit tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` for `route --mode gate` over `fixtures/advisory` and `fixtures/blocking`, plus an unavailable root path, asserting process exit codes through the public entry point.

### Implementation

- [X] T034 [US2] Implement deterministic exit selection in `src/FS.GG.Governance.Cli/Cli.fs`, mapping Host route facts and CLI failures to `Success`, `GovernedBlocking`, `UsageError`, `InputUnavailable`, or `ToolError` without collapsing governed failures into tool errors.
- [X] T035 [US2] Implement malformed-invocation and unavailable-input rendering in `src/FS.GG.Governance.Cli/Cli.fs`, keeping usage/tool diagnostics distinct from governed blocking output.
- [X] T036 [US2] Implement stdout/stderr/report-file behavior in `src/FS.GG.Governance.Cli/Program.fs`: normal command output to stdout or `--out`, usage/tool diagnostics to stderr when no report envelope can be produced, and no default report file inside the governed root.
- [X] T037 [US2] Record the exit-code validation transcript in `specs/012-cli/readiness/exit-codes.txt` for unknown command (`64`), unavailable root (`66`), advisory gate (`0`), and blocking gate (`2`).

**Checkpoint**: CI can consume the process contract without ambiguity.

---

## Phase 5: User Story 3 - `explain` and `contract` audit why and what the tool enforces (Priority: P1)

**Goal**: `explain` reports evaluated proof information whose top verdicts agree with route, and `contract` reports statements folded from the same composed rules used by evaluation. Both support text and stable JSON.

**Independent Test**: run `explain` and `contract` against fixtures; parse JSON; confirm repeated runs over the same snapshot are byte-for-byte identical and contract statements match evaluated checks.

### Tests First

- [X] T038 [P] [US3] Add parser tests in `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs` for `explain` and `contract` with shared options, including `--json` and `--format`.
- [X] T039 [P] [US3] Add explain/contract output tests in `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` asserting text names command, mode, root/scope, exit category/code, top verdicts or rule ids/statements, and failure/budget summaries; assert explanation top verdicts agree with a route over the same Host model and contract statements are derived from composed rule checks.
- [X] T040 [P] [US3] Add stable JSON repeat tests in `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` for `explain --json` and `contract --json`, rendering the same `CommandResult` three times and asserting byte-for-byte equality.
- [X] T041 [P] [US3] Add built command tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` running `explain` and `contract` against fixture roots and this repository's `.specify` tree, parsing JSON with `System.Text.Json`.

### Implementation

- [X] T042 [US3] Implement explain payload selection in `src/FS.GG.Governance.Cli/Cli.fs`, using existing kernel explanation values from evaluated rules rather than a second explanation engine.
- [X] T043 [US3] Implement contract payload selection in `src/FS.GG.Governance.Cli/Cli.fs`, folding `ContractEntry list` from the composed F09/F10/F11 rule catalog used by the run.
- [X] T044 [US3] Implement deterministic text and JSON rendering for `ExplainPayload` and `ContractPayload` in `src/FS.GG.Governance.Cli/Cli.fs`, embedding kernel JSON folds where available and keeping field order stable.
- [X] T045 [US3] Record stable JSON evidence in `specs/012-cli/readiness/stable-json.txt` using three repeated `route`, `explain`, `contract`, and `evidence` JSON runs over unchanged explicit inputs.

**Checkpoint**: `explain` and `contract` are auditable and deterministic through the public command surface.

---

## Phase 6: User Story 4 - `evidence` reports taint, freshness, cache, and safe-failure state (Priority: P1)

**Goal**: `evidence` distinguishes declared evidence, effective `AutoSynthetic`, freshness, cache hits/misses, pending reviews, disclosures, safe failures, stale records, skipped/failed/pending states, and missing inputs in text and JSON.

**Independent Test**: run `evidence` against fixtures with real evidence, synthetic roots, auto-synthetic downstream nodes, stale records, cache hits, cache misses, failed reads, and missing artifacts; verify no distinct state collapses.

### Tests First

- [X] T046 [P] [US4] Add parser tests in `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs` for `evidence` with `--review-store`, `--review-budget`, `--json`, and `--out`.
- [X] T047 [P] [US4] Add evidence report tests in `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` for text and JSON coverage of declared/effective states, `AutoSynthetic`, freshness, disclosures, Host failures, cache hits/misses, pending reviews, budget-exhausted keys, `ReviewStoreUnavailable`, and `ReviewDispatchFailed`.
- [X] T048 [P] [US4] Add snapshot evidence tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` for `fixtures/stale-synthetic`, `fixtures/review-miss`, `fixtures/review-store-unavailable`, `fixtures/review-dispatch-failed`, `fixtures/missing-input`, and this repository's `.specify` tree.
- [X] T049 [P] [US4] Add safe-failure tests in `tests/FS.GG.Governance.Cli.Tests/MvuTests.fs` asserting missing or unreadable governed input, unavailable review store, and failed review dispatch become evidence/input safe failures when a report can be produced, and `InputUnavailable` only when snapshot construction cannot proceed.

### Implementation

- [X] T050 [US4] Implement `Project.evidenceReport` details in `src/FS.GG.Governance.Cli/Project.fs`, preserving declared state, effective state, freshness, dependencies, disclosures, and Host failures without collapsing `Synthetic`, `AutoSynthetic`, `Stale`, `Skipped`, `Failed`, `Pending`, or `Real`.
- [X] T051 [US4] Implement evidence payload selection and text/JSON rendering in `src/FS.GG.Governance.Cli/Cli.fs`, including budget state and safe failures in the stable envelope.
- [X] T052 [US4] Implement missing-artifact, unreadable-root, review-store-unavailable, and review-dispatch-failed handling in `src/FS.GG.Governance.Cli/Program.fs`, preserving governed safe failures separately from unexpected tool defects.
- [X] T053 [US4] Record the evidence validation transcript in `specs/012-cli/readiness/evidence.txt` for real, stale/synthetic, missing-input, review-miss, review-store-unavailable, and review-dispatch-failed fixture runs.

**Checkpoint**: evidence output is explainable and state-preserving across normal and safe-failure cases.

---

## Phase 7: User Story 5 - run mode and fresh-review budget are explicit (Priority: P2)

**Goal**: users choose `Sandbox`, `Inner`, or `Gate`; fresh agent review dispatch is cache-only by default and never exceeds the caller-granted `--review-budget`.

**Independent Test**: run commands with cache-only budget, nonzero budget, exhausted budget, and all three modes; assert no run dispatches more fresh reviews than allowed and mode semantics match F07 routing.

### Tests First

- [X] T054 [P] [US5] Add budget parser and normalization tests in `tests/FS.GG.Governance.Cli.Tests/ParserTests.fs` for default cache-only, `--review-budget 0`, positive budgets, and invalid negative/non-numeric budgets.
- [X] T055 [P] [US5] Add budget edge tests in `tests/FS.GG.Governance.Cli.Tests/MvuTests.fs` asserting fake review requests are counted as requested/cache-hit/cache-miss/fresh-dispatch/pending/budget-exhausted without exceeding the granted budget; fake judge/review responses must carry the `Synthetic` token and use-site comment.
- [X] T056 [P] [US5] Add mode semantics tests in `tests/FS.GG.Governance.Cli.Tests/SnapshotTests.fs` for `Sandbox`, `Inner`, and `Gate` over the same fenced change, asserting only `Gate` can produce `GovernedBlocking`.
- [X] T057 [P] [US5] Add budget output tests in `tests/FS.GG.Governance.Cli.Tests/OutputTests.fs` confirming requested review count, cache hits, cache misses, fresh dispatch attempts, pending reviews, and budget exhaustion are visible in deterministic text and JSON output.

### Implementation

- [X] T058 [US5] Implement fresh-review budget gating in the real `CliPorts.RunHost` path in `src/FS.GG.Governance.Cli/Program.fs`, allowing cache hits for free, dispatching fresh reviews only while `freshDispatches < budget`, and leaving over-budget reviews pending/uncertain.
- [X] T059 [US5] Implement `BudgetState` accumulation and deterministic ordering in `src/FS.GG.Governance.Cli/Cli.fs`, ensuring `FreshDispatches.Length <= granted budget` for every completed run.
- [X] T060 [US5] Implement mode normalization and mode-sensitive route/exit selection in `src/FS.GG.Governance.Cli/Cli.fs`, preserving `Sandbox` loud/non-enforcing, `Inner` advisory, and `Gate` enforcing semantics.
- [X] T061 [US5] Record budget and mode evidence in `specs/012-cli/readiness/budget-and-modes.txt` using `fixtures/review-miss` with budgets `0` and `1`, plus `Sandbox`/`Inner`/`Gate` runs over a fenced fixture.

**Checkpoint**: cost/latency consent is explicit and enforced at the CLI boundary.

---

## Phase 8: User Story 6 - optional packaged tool is dogfooded read-only (Priority: P2)

**Goal**: the CLI is packable as an optional .NET tool, can be installed/run from `~/.local/share/nuget-local/`, dogfoods this repository and fixtures, and does not become a dependency for consumers or mutate governed repositories.

**Independent Test**: pack to the local feed, install/run the tool from that artifact against fixtures and this repository, and verify inspected trees have no governed content changes.

### Tests First

- [X] T062 [P] [US6] Add packaging tests in `tests/FS.GG.Governance.Cli.Tests/PackagingTests.fs` that pack `src/FS.GG.Governance.Cli` to `~/.local/share/nuget-local/`, install `FS.GG.Governance.Cli` to `.tmp/f12-tool`, and run installed `fsgg-governance route`, `explain`, `contract`, and `evidence` against fixtures from the packaged artifact.
- [X] T063 [P] [US6] Add read-only tests in `tests/FS.GG.Governance.Cli.Tests/ReadOnlyTests.fs` that snapshot hashes for governed fixture files and this repository's `.specify` tree, run all four commands without `--out`, and assert zero governed file changes.
- [X] T064 [P] [US6] Add `--out` tests in `tests/FS.GG.Governance.Cli.Tests/ReadOnlyTests.fs` proving report-file output writes only the caller-selected path and never writes default files under the governed root.
- [X] T065 [P] [US6] Add dependency optionality tests in `tests/FS.GG.Governance.Cli.Tests/SurfaceDriftTests.fs` asserting Kernel, Host, SPI, SpecKit adapter, and design-system adapter assemblies do not reference `FS.GG.Governance.Cli`, and ordinary `dotnet test` does not require installing the tool.

### Implementation

- [X] T066 [US6] Finalize packaging metadata in `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj`, including local tool packaging compatibility, executable entry point, stable package identity, and local-feed pack output behavior documented in `specs/012-cli/quickstart.md`.
- [X] T067 [US6] Harden `Program.fs` read-only behavior in `src/FS.GG.Governance.Cli/Program.fs`: do not rewrite Spec Kit artifacts, adapter fixtures, source files, generated issue/task lists, or consumer repository content; write only stdout/stderr or caller-selected `--out`.
- [X] T068 [US6] Record packaged-tool smoke evidence in `specs/012-cli/readiness/packaged-tool.txt` from `dotnet pack`, `dotnet tool install --tool-path .tmp/f12-tool --add-source ~/.local/share/nuget-local`, and installed `fsgg-governance route`, `explain`, `contract`, and `evidence` fixture runs.
- [X] T069 [US6] Record read-only dogfood evidence in `specs/012-cli/readiness/read-only.txt` from running `route`, `explain`, `contract`, and `evidence` against this repository's `.specify` tree and confirming `git diff --quiet -- .specify specs src tests surface`.

**Checkpoint**: the optional tool artifact runs from the local feed and remains one-way/read-only with respect to governed repositories.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: surface baselines, dependency hygiene, docs, quickstart validation, and final evidence across the complete CLI.

- [X] T070 Add the CLI public surface drift test in `tests/FS.GG.Governance.Cli.Tests/SurfaceDriftTests.fs`, bless `surface/FS.GG.Governance.Cli.surface.txt`, and assert `src/FS.GG.Governance.Cli/Cli.fsi` and `src/FS.GG.Governance.Cli/Project.fsi` are the sole visibility declarations.
- [X] T071 Add dependency hygiene assertions in `tests/FS.GG.Governance.Cli.Tests/SurfaceDriftTests.fs`: CLI may reference BCL/FSharp.Core/Kernel/Host/SPI/SpecKit/DesignSystem only; no new runtime package; Kernel/Host/SPI/adapters must not reference CLI.
- [X] T072 Run the quickstart validation in `specs/012-cli/quickstart.md` end-to-end and append command outputs or summaries to `specs/012-cli/readiness/quickstart.txt`; include an explicit SC-001 matrix proving `route`, `explain`, `contract`, and `evidence` each run against this repository's `.specify` tree and representative fixtures in both text and JSON forms.
- [X] T073 Run `dotnet build src/FS.GG.Governance.Cli`, `dotnet test tests/FS.GG.Governance.Cli.Tests`, and full `dotnet test`; record the final test summary in `specs/012-cli/readiness/test-summary.txt`.
- [X] T074 [P] Update `README.md` with the F12 optional CLI usage summary, installation command, and read-only boundary.
- [X] T075 [P] Run `speckit-agent-context-update` so `CLAUDE.md` points to the current 012 plan/task context and record the command in `specs/012-cli/readiness/agent-context.txt`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup** has no dependencies.
- **Phase 2 Foundation** depends on Setup and blocks all user-story implementation.
- **US1 Route (Phase 3)** depends on Foundation and is the MVP.
- **US2 Exit Decisions (Phase 4)** depends on Foundation and can proceed after US1 route payload shape is available for blocking/advisory fixtures.
- **US3 Explain/Contract (Phase 5)** depends on Foundation and can proceed in parallel with US2 after composed catalog access exists.
- **US4 Evidence (Phase 6)** depends on Foundation and `Project.evidenceReport` scaffolding.
- **US5 Modes/Budget (Phase 7)** depends on Foundation and Host run wiring; it can proceed in parallel with US3/US4 where file edits do not collide.
- **US6 Packaging/Read-only (Phase 8)** depends on the command stories being runnable through `Program.fs`.
- **Polish (Phase 9)** depends on all desired stories being complete.

### User Story Dependencies

- **US1 (P1)**: MVP; establishes the public route command and primary fixture path.
- **US2 (P1)**: uses route outcomes from US1 for governed blocking/advisory exit decisions, but usage/input/tool errors are independently testable.
- **US3 (P1)**: shares composed-catalog/Host model foundation with US1, no dependency on US2 except common renderer envelope.
- **US4 (P1)**: shares renderer envelope and Project evidence report foundation, no dependency on US3.
- **US5 (P2)**: depends on Host run edge wiring and affects all commands through shared budget/mode fields.
- **US6 (P2)**: requires runnable command behavior from US1-US5 before packaged dogfood evidence is meaningful.

### Parallel Opportunities

- Setup tasks T005, T007, T008, and T009 are parallel-safe after T001-T003 are scoped.
- Foundation tests T010-T016 run before implementation; T010-T013 can be authored in parallel across test files, then T017-T020 implement `Project.fs`, `Cli.fs`, and `Program.fs`.
- Tests marked `[P]` within each story target different test files or independent cases and can be authored before the implementation tasks.
- US3 explain/contract tests and US4 evidence tests can be authored in parallel once Foundation exists.
- US6 packaging and read-only tests can be authored in parallel before final packaging/read-only hardening.
- Polish tasks T074 and T075 are parallel-safe after the implementation shape is stable.

### File-Coupling Notes

- `src/FS.GG.Governance.Cli/Cli.fs` is the central parser/MVU/rendering file. Implementation tasks touching it should be sequenced to avoid conflicting edits: T018-T019 before T026-T028, T034-T035, T042-T044, T051, and T059-T060.
- `src/FS.GG.Governance.Cli/Program.fs` owns real I/O. Sequence T020 before T025, T036, T052, T058, and T067.
- `src/FS.GG.Governance.Cli/Project.fs` owns composition/evidence. Sequence T017 before T050.

## Implementation Strategy

### MVP First

1. Complete Phase 1 Setup.
2. Complete Phase 2 Foundation.
3. Complete Phase 3 US1 (`route`).
4. Validate US1 independently with fixture and repository `.specify` runs.

### Incremental Delivery

1. Add US2 exit semantics so CI can consume `route`.
2. Add US3 `explain` and `contract` for auditability.
3. Add US4 `evidence` for trust and safe-failure visibility.
4. Add US5 budget/mode consent.
5. Add US6 packaging and read-only dogfood evidence.
6. Finish surface, dependency, docs, quickstart, and final test evidence.
