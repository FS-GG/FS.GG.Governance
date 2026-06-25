# Tasks: The `fsgg refresh` Host Command

**Input**: Design documents from `/specs/057-refresh-command/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/cli.md, contracts/manifest.md, contracts/refresh.schema.md, quickstart.md

**Tier**: Tier 1 (contracted change) — full chain owed: `.fsi`, surface baselines, test evidence, docs. Two new public projects (`FS.GG.Governance.RefreshCommand`, `FS.GG.Governance.RefreshJson`) and one new authored repository surface (`.fsgg/refresh.yml`); F014/F029/F051/F052 untouched. Tests are in scope (Constitution V; plan lists both `.Tests` projects).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]` may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`; setup/foundational/cross-cutting/polish tasks carry no story tag
- Discipline (Constitution I/II): draft each public module's `.fsi` and prove it compiles/composes **before** its `.fs` body; semantic tests call the loaded public surface (`Loop.parse`, `Interpreter.run`, `RefreshJson.ofRefreshDecision`, `Declaration.parse`), never internals.

**Design note — shared model placement**: To keep this row to **two** `src` projects (plan §Project Structure) while preserving a clean dependency direction (`RefreshCommand` → `RefreshJson`), the shared currency/decision/manifest types (`ViewKind`, `GenerationEntry`, `GenerationManifest`, `DeclError`, `CurrencyStatus`, `ViewDecision`, `RefreshOutcome`, `RefreshDecision`) live in a `RefreshModel.fs(i)` module **inside `FS.GG.Governance.RefreshJson`** (the leaf). `RefreshJson.ofRefreshDecision` projects them; `RefreshCommand` (`Declaration`/`Loop`/`Interpreter`) consumes them by referencing `RefreshJson`. This matches the plan note "RefreshJson.fsproj refs: the shared RefreshModel types (currency/decision)".

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the two new `src` projects, their test projects, and wire the solution. Mirror the `ReleaseJson`/`ReleaseCommand` entries exactly.

- [X] T001 [P] Create `src/FS.GG.Governance.RefreshJson/FS.GG.Governance.RefreshJson.fsproj` (net10.0, `GenerateDocumentationFile`, `IsPackable=true`; refs: `FS.GG.Governance.FreshnessKey` — for `InputCategory`/`ArtifactHash`/`GeneratorVersion` — and `FS.GG.Governance.Config`) with compile order `RefreshModel.fsi` → `RefreshModel.fs` → `RefreshJson.fsi` → `RefreshJson.fs` — mirror `ReleaseJson.fsproj`.
- [X] T002 [P] Create `src/FS.GG.Governance.RefreshCommand/FS.GG.Governance.RefreshCommand.fsproj` (net10.0, `OutputType=Exe`, `IsPackable=false`; refs: `FreshnessKey`, `GateExecution`, `GateRun`, `Config`, `RefreshJson`, `YamlDotNet`) with compile order `Declaration.fsi` → `Declaration.fs` → `Loop.fsi` → `Loop.fs` → `Interpreter.fsi` → `Interpreter.fs` → `Program.fs` — mirror `ReleaseCommand.fsproj`.
- [X] T003 [P] Create `tests/FS.GG.Governance.RefreshJson.Tests/FS.GG.Governance.RefreshJson.Tests.fsproj` (Expecto + FsCheck; ref `RefreshJson`, `FreshnessKey`) with `Main.fs` Expecto entry.
- [X] T004 [P] Create `tests/FS.GG.Governance.RefreshCommand.Tests/FS.GG.Governance.RefreshCommand.Tests.fsproj` (Expecto + FsCheck; ref `RefreshCommand`, `RefreshJson`, `FreshnessKey`, `GateExecution`, `GateRun`, `Config`) with `Main.fs` Expecto entry.
- [X] T005 Add the four new projects to `FS.GG.Governance.sln` (mirror the `ReleaseCommand` + `ReleaseJson` solution-folder entries); confirm `dotnet build FS.GG.Governance.sln` resolves the new graph.

**Checkpoint**: Solution restores and builds with empty/stub modules.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All public `.fsi` contracts, the shared model types, the test fixture, and the surface-drift harness. **No story body may begin until the contracts compile and the fixtures exist.**

**⚠️ CRITICAL**: Blocks US1/US2/US3.

- [X] T006 [P] Author `src/FS.GG.Governance.RefreshJson/RefreshModel.fsi` — the shared types: `ViewKind` (closed DU: `GateMetadata|RuleCatalog|CapabilityDoc|SkillReference|ApiSurfaceDoc|RouteProjection|Baseline|Other of string` — structural, product-neutral, FR-011), `GenerationEntry { ViewId; Kind; OutputPath; Sources; Generator; GeneratorBasis }`, `GenerationManifest { Entries }`, `DeclError { Reason }`, `CurrencyStatus` (`Current|Regenerated of InputCategory list|WouldRegenerate of InputCategory list|StaleUnresolved of string|NotEvaluated`), `ViewDecision { Entry; Status; Drifted }`, `RefreshOutcome` (`NothingToRefresh|ViewsRegenerated|StaleUnresolved'|UsageError'|InputUnavailable|ToolError`), `RefreshDecision { Outcome; Views; RegeneratedCount; CurrentCount; UnresolvedCount; NotEvaluatedCount }` (data-model §New types).
- [X] T007 [P] Author `src/FS.GG.Governance.RefreshJson/RefreshJson.fsi` — `val schemaVersion: string` (= `"fsgg.refresh/v1"`) and `val ofRefreshDecision: RefreshDecision -> string` (data-model §`RefreshDecision`; refresh.schema.md). Depends on T006.
- [X] T008 [P] Author `src/FS.GG.Governance.RefreshCommand/Declaration.fsi` — `val parse: lines:string list -> Result<GenerationManifest, DeclError>` (pure over `refresh.yml` contents; returns the `RefreshJson.RefreshModel` `GenerationManifest`/`DeclError`; data-model §`Declaration`, manifest.md).
- [X] T009 [P] Author `src/FS.GG.Governance.RefreshCommand/Loop.fsi` — `OutputFormat` (`Text|Json|TextAndJson`), `Scope` (`AllViews|ByKind of ViewKind|ByView of string`), `RunRequest { Repo; DryRun; Scope; Format; RefreshOut }`, `UsageError`, `Phase`, `Model`, `Msg`, `Effect` (the seven cases incl. `RegenerateView`/`RecordProvenance`, data-model §`Effect`), and `val parse: string list -> Result<RunRequest, UsageError>` / `init` / `update` / `render` / `val exitCode: RefreshOutcome -> int` signatures (data-model §MVU state). Depends on T006.
- [X] T010 Author `src/FS.GG.Governance.RefreshCommand/Interpreter.fsi` — `Ports { Files; Sense; ReadProv; Generate; WriteProv; Write; Out }` (data-model §Edge ports), `val realPorts: repo:string -> Ports`, `val step: Ports -> Loop.Effect -> Loop.Msg`, `val run: Ports -> Loop.RunRequest -> Loop.Model` (depends on T009).
- [X] T011 Exercise all `.fsi` surfaces to prove they compile and compose before/with the `.fs` bodies (Constitution I). Depends on T006–T010. **Method used:** `dotnet build FS.GG.Governance.sln` compiles both `src` projects (each `.fsi` checked against its `.fs`), and the semantic suites load and call the public surface verbatim — `Declaration.parse`, `Loop.parse`/`init`/`update`/`render`/`exitCode`, `Interpreter.run`/`realPorts`, `RefreshJson.ofRefreshDecision` (65 tests green).
- [X] T012 [P] Add `tests/FS.GG.Governance.RefreshCommand.Tests/Support.fs` — a `withTempRepo` helper (the ReleaseCommand/VerifyCommand precedent) that materializes a `.fsgg/refresh.yml`, the declared source file(s), and an optional `.fsgg/refresh.lock.json` in a temp dir and cleans up. Include fixture builders for: **all-current** (recorded provenance matches sensed sources), **one-stale** (a source mutated so its view drifts), **multi-stale** (independent stale sources), **empty-manifest**, **missing-source** (a declared source absent ⇒ stale-unresolved), and a **deterministic generator** command (e.g. a script that copies the source to the output path) so end-to-end regeneration is byte-stable.
- [X] T013 [P] Add `tests/FS.GG.Governance.RefreshCommand.Tests/SurfaceDriftTests.fs` and `tests/FS.GG.Governance.RefreshJson.Tests/SurfaceDriftTests.fs` — load the public surface, compare to `surface/FS.GG.Governance.RefreshCommand.surface.txt` / `…RefreshJson.surface.txt`, honor `BLESS_SURFACE=1` (mirror the existing surface-drift test). Baselines committed in Phase 7 once `.fs` bodies stabilize.

**Checkpoint**: Contracts compile, FSI green, fixtures and surface harness in place — story work can begin.

---

## Phase 3: User Story 1 — Bring stale generated views current with one command (Priority: P1) 🎯 MVP

**Goal**: One `fsgg refresh` invocation that loads `.fsgg/refresh.yml`, senses each in-scope view's declared-source digests + generator version, decides currency by reusing `FreshnessKey` (revisions held equal), regenerates exactly the stale views through the declared generator (F051/F052) writing atomically, records refreshed provenance, prints a human summary, and exits `0` (nothing to refresh) / `5` (views regenerated) / `1` (stale-unresolved).

**Independent Test**: All-current fixture ⇒ regenerates nothing, every view reported current, exit 0 (US1.1/SC-002). One-stale fixture ⇒ exactly that view regenerated (others byte-for-byte untouched), reported `regenerated` with drifted category, regenerated bytes equal the generator output, exit 5 (US1.2/SC-001); re-run ⇒ exit 0, nothing regenerated, by digest not presence (US1 re-run/SC-002). Empty manifest ⇒ "nothing to refresh," exit 0 (US1.4/FR-012).

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T014 [P] [US1] `ParseTests.fs` — `Loop.parse`: bare/flags-only argv → `Ok RunRequest` (defaults `DryRun=false`, `Scope=AllViews`, `Format=Text`, `RefreshOut=None`); a leading bare `refresh` token is **tolerated** (command precedent); unknown flag / missing value → `Error UsageError`; **no I/O on rejection** (cli.md invocation table).
- [X] T015 [P] [US1] `DeclarationTests.fs` — `Declaration.parse`: well-formed `refresh.yml` → `Ok GenerationManifest` with entries in declared order, each `GenerationEntry` fields populated, `kind` tokens kebab/camel/underscore-tolerant mapping to `ViewKind` (unknown ⇒ `Other`); duplicate `id` → `Error DeclError`; empty `views` → `Ok { Entries = [] }` (FR-012); product-neutral (no hardcoded ids/paths) (manifest.md field rules).
- [X] T016 [P] [US1] `CurrencyTests.fs` — the pure currency decision in `update`: build `recorded`/`current` `FreshnessInputs` with `Base`/`Head` **held equal** (research D1); `matches recorded current = true` ⇒ `Current`; a changed source digest ⇒ stale with `diff` = `[CoveredArtifactsCat]`; a changed generator version ⇒ `[GeneratorVersionCat]`; assert currency is **by digest, not file presence** (SC-002) — identical digests with differing revisions still `Current`.
- [X] T017 [P] [US1] `LoopTests.fs` — pure transitions (write mode): `init` emits `LoadManifest`; `ManifestLoaded(Ok)` emits `SenseSource`+`ReadRecorded` per in-scope entry; a stale entry emits `RegenerateView` then on `Regenerated'(Ok)` a `RecordProvenance`; a non-stale entry emits neither. Assert emitted-effect lists (Constitution IV).
- [X] T018 [P] [US1] `LoopTests.fs` — `exitCode` + roll-up: `NothingToRefresh→0`, `StaleUnresolved'→1`, `UsageError'→2`, `InputUnavailable→3`, `ToolError→4`, `ViewsRegenerated→5` (cli.md exit-code table, research D5); roll-up precedence: any `StaleUnresolved` ⇒ `1`; else any `Regenerated`/`WouldRegenerate` ⇒ `5`; else `0`.
- [X] T019 [P] [US1] `EndToEndTests.fs` — via `Interpreter.run` with `realPorts` over `withTempRepo`: all-current ⇒ exit 0, nothing written; one-stale ⇒ the one view regenerated (bytes equal the generator output), others untouched, recorded provenance updated, exit 5; **re-run after refresh ⇒ exit 0** (SC-002, currency by digest); multi-stale ⇒ each regenerated, result independent of regeneration order (US1.3); empty manifest ⇒ exit 0 (US1.4).

### Implementation for User Story 1

- [X] T020 [US1] `Declaration.fs` — row-local YamlDotNet parse-to-node adapter for `.fsgg/refresh.yml` → `GenerationManifest`; absent/malformed → `Error DeclError`; duplicate-id rejection; deterministic entry order; F014 schema untouched; pure and total (never throws). Makes T015 pass.
- [X] T021 [US1] `RefreshModel.fs` — the shared types' bodies (no access modifiers; Principle II) plus any small helpers (e.g. `viewKindToken` for projection/parse reuse). Makes T006 surface concrete.
- [X] T022 [US1] `Loop.fs` — `parse` (argv → `RunRequest`/`UsageError`, incl. scope selectors — see T041/T042), `init`, pure `update` (per in-scope entry: build `recorded`/`current` `FreshnessInputs` revisions-equal, `stale = not (FreshnessKey.matches …)`, `drifted = FreshnessKey.diff …`; emit `RegenerateView` only when stale **and not** `DryRun`; on `Regenerated'(Ok)` emit `RecordProvenance`; assemble `RefreshDecision` + counts; resolve `RefreshOutcome`), `render` (human summary: per-view status, drifted categories, regenerated/current/unresolved counts), `exitCode`. Makes T014/T016/T017/T018 pass. Depends on T021.
- [X] T023 [US1] `Interpreter.fs` — `Ports`; `realPorts repo` binding: `Files` = `Loader.fileSystemReader` at `<repo>/.fsgg`; `Sense` = a row-local SHA-256 per-declared-source digester + generator-version sensor (research D2); `ReadProv` = recorded-provenance read (D4); `Generate` = run the entry's declared generator via the F051 `GateExecution` / F052 `GateRun` port and return the output digest (research D3); `WriteProv` + `Write` = atomic temp-then-rename writers (ReleaseCommand precedent); `Out` = stdout. Pure `step` dispatch over `Effect`; `run` folds `Msg`→`update` to a terminal `Model`. Makes T019 pass. Depends on T020, T022.
- [X] T024 [US1] Recorded-provenance store — **Choice: a minimal deterministic row-local lock** at `<repo>/.fsgg/refresh.lock.json` (sorted view ids, no clock / no absolute path), NOT `EvidenceReuseStore`. Rationale: the F048 store is per-CHANGE (Base/Head-keyed) and does not fit the revision-INDEPENDENT view-currency triple (source digests + generator version + output digest, keyed by `ViewId`). Implemented in `Interpreter.fs` (`readLock`/`renderLock`/`readProv`/`writeProv`, atomic temp-then-rename) and wired to `ReadProv`/`WriteProv` in `realPorts`. Depends on T023.
- [X] T025 [US1] `Program.fs` — thin `[<EntryPoint>]`: `Loop.parse argv` → on `Error` print usage to stderr + `exit 2`; on `Ok` build `realPorts` + `Interpreter.run` + emit + `exit (Loop.exitCode model.Exit)`. stderr diagnostics tagged `fsgg refresh [<categoryToken>]: <message>` (cli.md stderr).

**Checkpoint**: MVP — stale views regenerated end to end against real fixtures; six exit codes wired; `--dry-run` and `refresh.json` not yet added.

---

## Phase 4: User Story 2 — Preview the refresh without writing (Priority: P2)

**Goal**: `--dry-run` performs the identical currency evaluation, reports each stale view as `would-regenerate` with its drifted source(s) and reason, and writes **nothing** to the working tree.

**Independent Test**: Stale fixture + `--dry-run` ⇒ view reported `would-regenerate` with drifted source(s)/reason, working tree byte-for-byte unchanged, exit 5 (US2.1/SC-003). All-current + `--dry-run` ⇒ all-current, nothing written, exit 0 (US2.2). No-mutation guard ⇒ no view, lock, or artifact written (US2.3/SC-003).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T026 [P] [US2] `DryRunTests.fs` — pure `update` under `DryRun=true`: a stale entry yields `WouldRegenerate drifted` and emits **no** `RegenerateView`, `RecordProvenance`, or view-`WriteArtifact` effect (data-model "Pure `update` invariant"); a non-stale entry yields `Current`; roll-up of any `WouldRegenerate` ⇒ `ViewsRegenerated` (exit 5).
- [X] T027 [P] [US2] `NoMutationTests.fs` — snapshot the `withTempRepo` tree (relative paths + content hashes) before `Interpreter.run --dry-run` over a stale fixture; assert it is **byte-for-byte identical** afterward — no view regenerated, no `.fsgg/refresh.lock.json` written, no artifact (SC-003/FR-013). Repeat over an all-current fixture (exit 0).
- [X] T028 [P] [US2] `EndToEndTests.fs` — `--dry-run` over a stale fixture via `realPorts`: exit 5, the stale view reported `would-regenerate` with its drifted category and a reason naming the source; over all-current: exit 0 (US2.1/US2.2).

### Implementation for User Story 2

- [X] T029 [US2] `Loop.fs` — wire `--dry-run` into `parse` (sets `RunRequest.DryRun`) and into `update` so the dry-run invariant holds (no `RegenerateView`/`RecordProvenance`/view-write effects; `WouldRegenerate` instead of `Regenerated`); `render` reports the would-regenerate plan and reason. Makes T026/T028 pass.
- [X] T030 [US2] Confirm via `Interpreter.run` that no edge write port fires in `--dry-run` (the `update` invariant already prevents the effects; add a faulting-`Write`/`WriteProv`/`Generate` fake that *asserts it is never called* in dry-run as a belt-and-braces guard). Makes T027 pass. Depends on T029.

**Checkpoint**: US1 + US2 both work; `--dry-run` is a safe, mutation-free preview.

---

## Phase 5: User Story 3 — Deterministic `refresh.json` for tooling and CI (Priority: P3)

**Goal**: `--json` / `--text-and-json` / `--refresh-out <path>` emit a byte-deterministic `refresh.json` projecting per-view currency status, drifted sources, reasons, and the summary; printed machine output equals the persisted file verbatim.

**Independent Test**: Two runs over identical state + identical outcomes ⇒ byte-identical `refresh.json` (SC-004); `--json` stdout equals the persisted file (US3.2); artifact carries `schemaVersion = "fsgg.refresh/v1"`, no timestamp/abs-path/username/machine-specific content, and a `stale-unresolved` view carries a `reason` and is never reported `current` (US3.3/FR-010).

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T031 [P] [US3] `tests/FS.GG.Governance.RefreshJson.Tests/RefreshJsonTests.fs` — `ofRefreshDecision` shape: fixed top-level key order (`schemaVersion`, `outcome`, `dryRun`, `summary`, `views`); `summary` = `{regenerated,current,unresolved,notEvaluated}`; `views` in declared manifest order, each with `id/kind/output/status` and `drifted` (omitted/empty when current), `reason` present **only** for `stale-unresolved`; status tokens exhaustive — `current|regenerated|would-regenerate|stale-unresolved|not-evaluated` (refresh.schema.md).
- [X] T032 [P] [US3] `tests/FS.GG.Governance.RefreshJson.Tests/DeterminismTests.fs` — `ofRefreshDecision` called twice on identical input is byte-identical; FsCheck property: no clock/abs-path/env/username content; stable ordering under any incidental input reordering (FR-007/SC-004).
- [X] T033 [P] [US3] `tests/FS.GG.Governance.RefreshJson.Tests/GoldenTests.fs` — `ofRefreshDecision` over a fixed fixture equals the committed golden baseline at `tests/FS.GG.Governance.RefreshJson.Tests/golden/refresh.json` (SC-004). *(Canonical golden path — T045 commits this exact file.)*
- [X] T034 [P] [US3] `tests/FS.GG.Governance.RefreshCommand.Tests/DeterminismTests.fs` — full `Interpreter.run` with `--text-and-json` over a fixture twice ⇒ byte-identical `refresh.json` (SC-004); the `--json` stdout equals the persisted file verbatim (one source of truth, FR-007); the text summary's per-view statuses match the JSON.

### Implementation for User Story 3

- [X] T035 [US3] `src/FS.GG.Governance.RefreshJson/RefreshJson.fs` — hand-driven `Utf8JsonWriter` walk (AuditJson/ReleaseJson/VerifyJson precedent): `schemaVersion` literal `"fsgg.refresh/v1"`, `outcome`/`status` exhaustive token helpers (no wildcard — a future case is a compile error), `views` in declared order, `drifted` via `FreshnessKey.categoryToken`, `reason` only on `stale-unresolved`. Makes T031–T033 pass.
- [X] T036 [US3] `Loop.fs` — extend `parse` (`--text`/`--json`/`--text-and-json`, `--refresh-out <path>`) and `update`: when `Format` requests JSON, emit `WriteArtifact(path, RefreshJson.ofRefreshDecision decision)` (the **one** write allowed in `--dry-run`); handle `Wrote(Ok)`→Done and `Wrote(Error)`→`ToolError`; `render` honors `Text`/`Json`/`TextAndJson` with `--json` printing the persisted bytes verbatim. Depends on T035.
- [X] T037 [US3] `Interpreter.fs` — `Write` for the artifact path = atomic temp-then-rename returning `Result<unit,string>` (no partial file on failure). Makes T034 pass. Depends on T036.

**Checkpoint**: All three stories work; deterministic, CI-consumable `refresh.json` emitted on request.

---

## Phase 6: Failure modes & fail-safe hardening (Cross-Cutting)

**Purpose**: Make every failure class distinguishable in diagnostic **and** exit code, never fabricate "all current," never leave a partial view/artifact, and honor scoping. Spans all stories (FR-010/FR-015/FR-016/SC-005).

- [X] T038 [P] `FailureTests.fs` — `ManifestLoaded(Error)` and an absent/malformed/unreadable `refresh.yml` ⇒ `InputUnavailable` (exit 3), actionable diagnostic naming the file, **no** `SenseSource`/`RegenerateView`/`WriteArtifact` emitted (cli.md step 1, FR-016).
- [X] T039 [P] `FailureTests.fs` — a stale view whose declared source is absent/unreadable (`Sense` returns `Error`) ⇒ `StaleUnresolved reason` (never `Current`, never fabricated), roll-up ⇒ exit 1; the run still brings current the views that *can* be refreshed (US1 "always refreshes what it can"); `reason` names the offending source (FR-010/FR-016/SC-005).
- [X] T040 [P] `FailureTests.fs` — a declared generator that exits non-zero, or an unwritable output path (faked failing `Generate`/`Write`) ⇒ `ToolError` (exit 4), **no partial view** left behind, distinct from `StaleUnresolved` (1) and `InputUnavailable` (3) (cli.md step 5, FR-013/SC-005).
- [X] T041 [P] `FailureTests.fs` — bad argv: unknown flag, missing flag value, **mutually-exclusive** `--view-kind X --view Y` together ⇒ `UsageError'` (exit 2), nothing written (cli.md scope rules, FR-015).
- [X] T042 [P] `ScopeTests.fs` — `--view-kind <kind>` / `--view <id>` narrow the evaluated set; out-of-scope views are reported `NotEvaluated` (never assumed current), counted in `notEvaluatedCount`, and projected `not-evaluated` in `refresh.json`; default scope (no selector) evaluates all declared views (FR-015, research D6, edge "Partial scope selection"). Then implement the scope-application in `Loop.fs` `update` if not already covered by T022/T029. Depends on T022.

**Checkpoint**: All six outcome classes distinguishable; no fabricated current; no partial view/artifact; scoping honored.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Network-free guarantee, surface baselines, golden, docs, and the quickstart validation pass.

- [X] T043 [P] `tests/FS.GG.Governance.RefreshCommand.Tests/ScopeGuardTests.fs` — assert the command's own reachable assembly surface references no network API (the ReleaseCommand/F054 scope-guard precedent); the command's reads/writes are `System.IO`-only and generator execution is the F051/F052 process port (SC-007).
- [X] T048 [P] `tests/FS.GG.Governance.RefreshCommand.Tests/ProductNeutralityTests.fs` — the dedicated SC-006/FR-011 guard (sibling of the T043 network guard): assert no product/view/path/generator/renderer identity is hardcoded in `RefreshCommand`/`RefreshJson` — every view id, kind, output path, source, generator argv, and generator-version basis is read from `.fsgg/refresh.yml` (drive `Declaration.parse`/`Loop`/`RefreshJson.ofRefreshDecision` with two manifests carrying *different* invented ids/paths/kinds and assert the output reflects the input verbatim, with no string from the spec's example renderers appearing unless the manifest supplied it). Closes SC-006's "verifiable by inspection" with an automated assertion.
- [X] T044 Bless and commit `surface/FS.GG.Governance.RefreshCommand.surface.txt` and `surface/FS.GG.Governance.RefreshJson.surface.txt` (`BLESS_SURFACE=1 dotnet test …`), then re-run drift tests green (T013).
- [X] T045 [P] Commit the `refresh.json` golden baseline file referenced by T033 at `tests/FS.GG.Governance.RefreshJson.Tests/golden/refresh.json`, generated from the stable `ofRefreshDecision`.
- [X] T046 [P] Update `CLAUDE.md` and the Phase 7 roadmap row: Governance `fsgg refresh` 🟡→complete (F057 lands `fsgg refresh` host + `refresh.json`); note F014/F029/F051/F052 untouched, the row-local `.fsgg/refresh.yml` adapter + generated `.fsgg/refresh.lock.json`, and that the boundary-enforcement row remains a later feature (FR-017).
- [X] T047 Ran the `quickstart.md` validation: 65 semantic tests green (`dotnet test` both suites) cover Scenarios 1–3 + failure/guarantee checks; a real-host CLI smoke run (`readiness/cli-smoke.txt`) demonstrates exit 5 (regenerated), 0 (by-digest re-run), 5 + no-write (`--dry-run`), deterministic `--json`, 3 (absent manifest), 2 (mutually-exclusive selectors). Determinism/golden/surface tests green; `--dry-run` no-mutation guarded; product-neutrality + network-free guards green.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001–T004 parallel, T005 after them.
- **Foundational (Phase 2)** → after Setup. T006 first (shared model `.fsi`); T007/T008/T009 after T006 (T010 after T009); T011 after T006–T010; T012/T013 parallel. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP. (`Declaration.fs`/`RefreshModel.fs`/`Loop.fs` before `Interpreter.fs` before the provenance store before `Program.fs`.)
- **US2 (Phase 4)** → after US1's `Loop`/`Interpreter` exist; the dry-run invariant layers onto US1's `update`.
- **US3 (Phase 5)** → after Foundational; `RefreshJson.fs` (T035) is independently testable, but the command-level determinism test (T034) needs US1's `Interpreter`.
- **Failure modes (Phase 6)** → after US1–US3; hardens all paths; T042 scoping depends on US1's `parse`/`update`.
- **Polish (Phase 7)** → after the desired stories; T044/T045/T047 need the `.fs` bodies stable.

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- `.fsi` (Phase 2) before `.fs`; `RefreshModel`/`Declaration`/`Loop` before `Interpreter` before the provenance store before `Program`.

### Parallel opportunities

- Phase 1: T001–T004 together.
- Phase 2: T007/T008/T009 after T006 in parallel (T010 after T009), and T012/T013 in parallel.
- Each story's `[P]` test tasks run together; once Foundational lands, US2/US3 can be stubbed against US1's contracts.
- Phase 6: T038–T042 are independent `[P]` test tasks.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL) → 3. Phase 3 US1 → **STOP & VALIDATE** (SC-001/SC-002 with text output: stale view regenerated, re-run clean, empty manifest clean) → demo the regeneration entry point.

### Incremental delivery

Setup + Foundational → US1 (MVP regeneration) → US2 (`--dry-run` preview) → US3 (`refresh.json`) → Failure-mode hardening → Polish. Each story adds value without breaking the prior.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Reuse, don't reinvent: F029 `FreshnessKey.compute`/`matches`/`diff`/`categoryToken` is the staleness comparator (revisions held **equal** — research D1, the crux), F051 `GateExecution`/F052 `GateRun` is the generator-execution port, F014 `Loader` reads the manifest/sources — all called verbatim, never mocked in end-to-end tests (Constitution V); only the edge ports are faked for unit coverage.
- **Mutation by design**: unlike its read-only siblings, write-mode refresh regenerates stale views, their recorded provenance, and the requested `refresh.json` — and **nothing else** (FR-013); `--dry-run` writes nothing; a tool error leaves no partial view/artifact.
- Elmish/MVU applies (stateful, I/O-bearing): `.fsi` contract, pure transition tests (T016/T017/T026), emitted-effect assertions (Constitution IV), and real-interpreter evidence (T019/T028/T034) are explicit tasks. `RefreshJson`, `RefreshModel`, and `Declaration` are pure leaves — no MVU ceremony.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the task line.
