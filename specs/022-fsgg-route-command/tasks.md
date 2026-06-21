---
description: "Task list for F022 - 022-fsgg-route-command: the first HOST EDGE — the `fsgg route` command — that composes the Phase-2 pure cores (F014 Config, F015 Routing, F016 Snapshot, F017 Findings, F018 Gates, F019 Route, F020 RouteJson, F021 GatesJson) over a real repository and PERSISTS two deterministic documents. A new packable composition/edge project `FS.GG.Governance.RouteCommand` (OutputType Exe, PackAsTool, ToolCommandName fsgg) modeled through an Elmish/MVU boundary (Principle IV, load-bearing): a PURE `Loop` (parse/init/update/render/exitCode over Model/Msg/Effect) and an EDGE `Interpreter` (injected, fakeable `Ports` = reused F014 FileReader + F016 git Ports + new ArtifactWriter + OutputSink; realPorts/step/run) plus a thin `Program.fs`. Selects the changed-path scope (--paths | --since | default base/head), loads+validates the catalog, routes, builds the registry, computes findings, selects gates, projects gates.json (F021) + route.json (F020) BYTE-FOR-BYTE unchanged, writes them via temp+atomic-rename, and prints a deterministic text/JSON summary. Re-derives/re-sorts/re-classifies NOTHING; adds NO new third-party PackageReference (git/catalog/serialization all delegated to existing edges; only new I/O is a System.IO write); computes NO ship verdict — no merge decision, severity, profile, mode, enforcement, cache-eligibility verdict, blockers, warnings, or exit-code-from-blockers (those are `fsgg ship`/audit.json/Phase 5/Phase 11). Every failure (not-a-repo/unavailable git, unresolved rev, missing/invalid catalog, unwritable output) → distinct diagnostic + category-mapped non-zero exit code, NO partial artifact; the interpreter NEVER throws."
---

# Tasks: `fsgg route` Host Command

**Feature branch**: `022-fsgg-route-command` (active spec; git branch currently `main`)
**Spec**: [`specs/022-fsgg-route-command/spec.md`](./spec.md)
**Plan**: [`specs/022-fsgg-route-command/plan.md`](./plan.md)

**Input**: Design documents from `/specs/022-fsgg-route-command/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Loop.fsi](./contracts/Loop.fsi), [contracts/Interpreter.fsi](./contracts/Interpreter.fsi), [contracts/fsgg-route-command.md](./contracts/fsgg-route-command.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; two new public `.fsi` modules; a new user/CI command contract — flags, exit codes, on-disk artifact locations; a new surface baseline). Credible evidence is **public-surface** testing only (Principle V), at three levels: (1) the PURE boundary — drive `Loop.parse`/`Loop.init`/`Loop.update`/`Loop.render`/`Loop.exitCode` with literal `Model`/`Msg` values and assert the next `Model` + emitted `Effect`s (no I/O, no git, no clock); (2) the EDGE — run `Interpreter.run` against **faked ports** (in-memory `FileReader`, an in-memory git `Ports` over a literal `RawSensing`/fixed `RepoSnapshot`, a capturing `ArtifactWriter`, a capturing `OutputSink`) so the whole composition is exercised with **no real `git` process and no real filesystem** (FR-012, SC-007); (3) at least ONE **real-temp-git + real-catalog end-to-end** proof (the `Snapshot` `withTempRepo` fixture pattern), asserting the written bytes equal `RouteJson.ofRouteResult` / `GatesJson.ofGateRegistry` of the same typed inputs (SC-001, SC-007). Real evidence preferred; any synthetic literal standing in for an un-derivable case carries the `Synthetic` token in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — the eight cores (F014 Config, F015 Routing, F016 Snapshot, F017 Findings, F018 Gates, F019 Route, F020 RouteJson, F021 GatesJson) are referenced as-is via their existing public surfaces; the new baseline is `surface/FS.GG.Governance.RouteCommand.surface.txt`.

**Elmish/MVU (Principle IV)**: **APPLICABLE AND LOAD-BEARING** — this row is the first multi-step external-I/O host edge (git sensing, catalog reads, two artifact writes, a summary emit). It MUST be modeled as a pure `Model`/`Msg`/`Effect`/`init`/`update` core (`Loop`) plus an edge `Interpreter` that executes effects through injected, fakeable ports (`Host.Loop`/`Host.Interpreter` and `Snapshot`/`Config.Loader` shape) — NOT the heavier Elmish `Program` runtime (research D2). `parse`/`init`/`update`/`render`/`exitCode` are pure and total; ALL I/O is `Effect` data; interpretation happens only at the edge. The MVU tasks below therefore emit explicit work for the `.fsi` contracts (`Model`/`Msg`/`Effect`/`init`/`update`/interpreter boundary), pure transition tests, emitted-effect assertions, and real interpreter evidence (faked ports + one real temp git).

**Reuse minimums (FR-003/FR-004/FR-005, Reuse-don't-re-derive)**: the command COMPOSES the eight cores verbatim and re-derives/re-sorts/re-classifies **nothing**. The catalog read goes through `Config.Loader` (behind its `FileReader`), git sensing through `Snapshot.Interpreter` (behind its `Ports`), routing through `Routing.route`, the registry through `Gates.buildRegistry`, findings through `Findings.findUnknownGovernedPaths`, selection through `Route.select`, and the two documents through `RouteJson.ofRouteResult` / `GatesJson.ofGateRegistry`. The feature adds the composition `update`, the persistence edge (`ArtifactWriter`), the stdout edge (`OutputSink`), and the summary `render` — **no** new routing/selection/serialization logic and **no** new third-party `PackageReference` (the only new impure primitives are `System.IO` for the two writes and `System.Environment.Exit` at the `Program` edge — both BCL).

**Determinism & byte-stability minimums (FR-006/FR-013, SC-002/SC-005)**: the persisted artifacts inherit F020/F021 byte-stability **unchanged** — this feature serializes nothing of its own, so it cannot inject a clock, machine-absolute path, or environment value into the documents; its only obligation is to *not inject* one (it writes the projection strings verbatim). `parse`/`update`/`render`/`exitCode` are pure: identical `Msg`/`Model` in ⇒ identical `Model`/`Effect`s out, and `render` is byte-stable for a fixed `Model` (the `--json` summary too). Both document strings are computed **before either write** (research D9), each write is temp-file + atomic-rename, and a twice-run over fixed inputs yields byte-identical files and identical `--json` stdout.

**Safe-failure & totality minimums (FR-009/FR-010/FR-011/FR-013, SC-004/SC-006)**: the interpreter NEVER throws — `step` catches every port `Error` and thrown exception and reifies it to the matching `Msg`; `run` drives `update` to `Done` and returns the terminal `Model`. The four failure categories map to distinct exit codes via `ExitDecision`: `UsageError'` → 2 (bad argv, `--paths`+`--since`, empty `--paths`), `InputUnavailable` → 3 (not-a-repo/git unavailable, unresolved `--since` rev, missing/`Invalid` catalog), `ToolError` → 4 (unwritable output after a valid route, any unexpected reified failure), `Success` → 0. **No** `GovernedBlocking` decision exists (FR-008): any selected-gate count and any finding count ⇒ `Success` (the empty-change and empty-catalog edge cases included). Input/usage failures write **no** artifact; a write failure leaves **no** partial/truncated file (temp+rename, D9).

**Scope-guard minimums (FR-008, slice boundary)**: this row ships the `route` verb ONLY. **No** `fsgg ship`, **no** ship/merge verdict, severity, profile, mode, enforcement state, cache-eligibility verdict (freshness-key inputs are carried by F020/F021, never evaluated — Phase 11), blockers, warnings list, exit-code-from-blockers, or `audit.json`, and **no** branch-protection guidance — those are later Phase-2/Phase-5 rows. The command lands as a NEW project `FS.GG.Governance.RouteCommand`; it does **not** reuse or extend the kernel-era `FS.GG.Governance.Host`/`FS.GG.Governance.Cli` (distinct lineage, research D1).

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US4]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new packable composition/edge project `FS.GG.Governance.RouteCommand` (the `Host`/`Cli` shape — Exe referenced by its test project), its test project, the two public contracts copied verbatim, the faked-port + real-temp-git support helpers, the prelude sketch, and the readiness note. **No new third-party dependency** — the project references the **eight cores** and adds only `FSharp.Core`; git/catalog/serialization are delegated, the only new I/O is a `System.IO` write.

- [X] T001 Create `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj` targeting `net10.0`, `OutputType=Exe`, `PackAsTool=true`, `ToolCommandName=fsgg`, `PackageId=FS.GG.Governance.RouteCommand`, `IsPackable=true`, `RootNamespace=FS.GG.Governance.RouteCommand`, with a `FSharp.Core` `<PackageReference>` (mirroring `FS.GG.Governance.Cli`) and **no other** `<PackageReference>`, and exactly **eight** `<ProjectReference>`s — `Config`, `Snapshot`, `Routing`, `Findings`, `Gates`, `Route`, `RouteJson`, `GatesJson` (relative `../FS.GG.Governance.<X>/…fsproj`). Compile order: `Loop.fsi` → `Loop.fs` → `Interpreter.fsi` → `Interpreter.fs` → `Program.fs` (Interpreter references Loop's `Effect`/`Msg`/`Model`/`RunRequest`; Program references both). Add an fsproj header comment (mirroring the `Host` fsproj) noting this is the Phase-2 **host edge** — the composition/edge tier that wires the eight pure/edge cores end-to-end and persists `gates.json`/`route.json`; it is a NEW project (not the kernel-era `Host`/`Cli`, research D1), adds **no** new third-party dependency (git/catalog/serialization delegated; only new I/O is a `System.IO` write + `Environment.Exit` at `Program`), and reaches no clock and injects no nondeterministic value into the artifacts.
- [X] T002 [P] Copy `specs/022-fsgg-route-command/contracts/Loop.fsi` → `src/FS.GG.Governance.RouteCommand/Loop.fsi` verbatim as the curated public surface of the PURE MVU core (Principle II — this `.fsi` is the SOLE public surface: `ScopeSelector`/`OutputFormat`/`RunRequest`/`UsageError`/`ExitDecision`/`ArtifactKind`/`Effect`/`Msg`/`Diagnostic`/`Phase`/`Model` + `parse`/`init`/`update`/`render`/`exitCode`; the matching `Loop.fs` carries no top-level access modifiers and keeps every helper hidden, the `Host.Loop` precedent).
- [X] T003 [P] Copy `specs/022-fsgg-route-command/contracts/Interpreter.fsi` → `src/FS.GG.Governance.RouteCommand/Interpreter.fsi` verbatim as the curated public surface of the EDGE (Principle II — SOLE public surface: `ArtifactWriter`/`OutputSink`/`Ports` + `realPorts`/`step`/`run`; the matching `Interpreter.fs` carries no top-level access modifiers and keeps the temp+rename writer plumbing hidden, the `Host.Interpreter`/`Snapshot.Interpreter` precedent).
- [X] T004 Add `failwith "F022"` stub bodies in `src/FS.GG.Governance.RouteCommand/Loop.fs` and `src/FS.GG.Governance.RouteCommand/Interpreter.fs` that satisfy the two `.fsi` contracts (every `val` bound to a stub returning the declared type, e.g. `parse`/`init`/`update`/`render`/`exitCode` and `realPorts`/`step`/`run`), so the project compiles against the contracts before any real composition logic lands (Principle I). Add a minimal `src/FS.GG.Governance.RouteCommand/Program.fs` whose `[<EntryPoint>]` parses argv, builds `Interpreter.realPorts`, calls `Interpreter.run`, prints diagnostics, and returns `Loop.exitCode model.Exit` — initially over the stubs.
- [X] T005 Create `tests/FS.GG.Governance.RouteCommand.Tests/FS.GG.Governance.RouteCommand.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/YoloDev.Expecto.TestSdk packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.RouteCommand` plus the cores the tests assemble real inputs from — at minimum `Config`, `Snapshot`, `Route`, `RouteJson`, `GatesJson`, `Gates` (the tests assemble real `TypedFacts`/`RepoSnapshot`, drive `run` through faked ports, and compare written bytes against the real F020/F021 projections).
- [X] T006 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.RouteCommand.Tests/`: `Support.fs`, `ScopeParseTests.fs`, `LoopTests.fs`, `InterpreterTests.fs`, `FailureTests.fs`, `EndToEndTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T007 Add `src/FS.GG.Governance.RouteCommand` and `tests/FS.GG.Governance.RouteCommand.Tests` to `FS.GG.Governance.sln`.
- [X] T008 [P] Implement the **faked-port + real-temp-git** support helpers in `tests/FS.GG.Governance.RouteCommand.Tests/Support.fs` over real values (no mocks of the cores): (a) an in-memory `Config.Loader.FileReader` backed by a `path → content` map (a valid minimal `.fsgg` catalog with ≥2 declared domains/capability checks spanning ≥2 cost tiers; a **valid-but-empty** catalog variant — declaring no capability checks, for the empty-registry case T019(3); plus a missing-file and an invalid-content variant for FailureTests); (b) a faked `Snapshot.Interpreter.Ports` that returns a literal `RawSensing`/fixed `RepoSnapshot` (a changed path under a declared surface, an unclassified routine path, and an unknown-governed path — plus a **no-changed-paths/empty-diff** variant for T019(2) and an `Error` variant), so no real `git` runs; (c) a capturing `ArtifactWriter` (records `(path, content)` writes in order; a failing variant returning `Error` for the unwritable-output case) and a capturing `OutputSink` (collects emitted summary strings); (d) a `fakePorts` assembling `{ Files; Git; Write; Out }` from the above; (e) a `withTempRepo` helper (the `Snapshot` fixture pattern) that initializes a real temp git repo with a real `.fsgg` catalog and a real edit, for the ONE end-to-end proof; (f) small assertion helpers — read a captured artifact by `ArtifactKind`, and compare a captured string to `RouteJson.ofRouteResult`/`GatesJson.ofGateRegistry` of the same typed inputs. These produce/inspect REAL outputs through the faked edges, never fakes of the cores.
- [X] T009 [P] Extend `scripts/prelude.fsx` with an F022 design sketch that `#r`s the built `FS.GG.Governance.RouteCommand` assembly, opens the namespace, and walks the PURE boundary with no I/O: `Loop.parse [ "route"; "--paths"; "src/Lib/Thing.fs" ]`, then `Loop.init` the request, then feed literal `Sensed`/`Loaded`/`Wrote`/`Emitted` `Msg`s through `Loop.update` printing the running `Model.Phase`/`Model.Exit` and the emitted `Effect`s at each step, render the summary both ways (`Loop.render model Text` / `Loop.render model Json`), and print `Loop.exitCode` for each `ExitDecision`. **Design-first, like F020/F021's prelude task**: written as the design record, it throws while the bodies are the `failwith "F022"` stubs (T004) and only runs green once Foundation/US1 land; T035 re-runs it end-to-end against the real bodies (and adds a faked-port `Interpreter.run` smoke).
- [X] T010 [P] Create `specs/022-fsgg-route-command/readiness/README.md` listing the required transcripts/evidence (the pure `update` walk Parsed→…→Done with emitted effects per step; a faked-port `Interpreter.run` writing both artifacts whose bytes equal the F020/F021 projections; a twice-run byte-identical determinism check; the four failure categories with their diagnostics + exit codes; the one real-temp-git end-to-end run) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.RouteCommand` and `dotnet test tests/FS.GG.Governance.RouteCommand.Tests` compile against the stubs; the solution lists the two new projects; the eight core references resolve; `Support.fs` assembles faked ports and a real temp repo; the project packs as `fsgg` (Exe/PackAsTool) without error.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the pure MVU scaffolding and the edge loop skeleton everything specializes — `parse`, `exitCode`, `init`, the `update` transition skeleton (the `Phase` advance + short-circuit shape), the `render` skeleton, and the `Interpreter` `step`/`run`/`realPorts` dispatch. **No user-story work begins until this phase is complete.**

- [X] T011 Implement `Loop.exitCode` in `src/FS.GG.Governance.RouteCommand/Loop.fs` as the total closed-DU map `Success`→0, `UsageError'`→2, `InputUnavailable`→3, `ToolError`→4 (research D6; **no** `GovernedBlocking` code, FR-008) — exhaustive `match`, no wildcard.
- [X] T012 Implement `Loop.parse` in `src/FS.GG.Governance.RouteCommand/Loop.fs` as a pure, total argv matcher (research D8, [contracts/fsgg-route-command.md](./contracts/fsgg-route-command.md)): recognize the `route` verb and the flags `--repo`, `--paths <p>…`, `--since <rev>`, `--json`, `--gates-out`, `--route-out`; build a `RunRequest` with defaults (`Repo="."`, `Format=Text`, `GatesOut=<repo>/.fsgg/gates.json`, `RouteOut=<repo>/readiness/route.json` — paths derived from `Repo`, research D5); and return `Error` for the four `UsageError` cases — `UnknownFlag`, `MissingValue` (a flag missing its value), `PathsAndSinceTogether` (both `--paths` and `--since`), `EmptyPaths` (`--paths` with no path). No exceptions — usage problems are values (FR-013). (US2 leans on this; the matcher itself is Foundation.)
- [X] T013 Implement `Loop.init` in `src/FS.GG.Governance.RouteCommand/Loop.fs`: build the initial `Model` (`Phase=Parsed`, all `option` fields `None`, empty `Diagnostics`, `Exit=Success` as the running default) and emit the first effect — `LoadCatalog repo` directly for `ExplicitPaths` (bypasses git diff, research D4), `SenseScope scope` for `Since`/`DefaultRange` (data-model §3 `init`).
- [X] T014 Implement the `Loop.update` **transition skeleton** in `src/FS.GG.Governance.RouteCommand/Loop.fs`: the total `Msg`×`Model`→`Model`×`Effect list` dispatch advancing `Phase` (Parsed→Sensed'→Loaded'→Selected→Projected→Persisted→Done) and the short-circuit shape — any `Sensed (Error _)` / `Loaded (Invalid _)` / `Wrote (_, Error _)` records the matching `Diagnostic` + sets `Exit` (`InputUnavailable`/`ToolError`) and emits **no** further effects, reaching `Done` (FR-010/FR-013). Leave the in-process composition (route/registry/findings/select/project) and the dual-write/summary wiring as explicit TODO seams US1 fills. TOTAL — exhaustive `match`, never throws.
- [X] T015 Implement the `Loop.render` **skeleton** in `src/FS.GG.Governance.RouteCommand/Loop.fs`: a pure `Model→OutputFormat→string` dispatching `Text`/`Json` (research D7) — for now rendering the diagnostics/exit and a placeholder body US3 fills with the selected-gate list, route trace, cost rollup, findings, and written paths. PURE: no clock/abs-path/env (FR-006/SC-005).
- [X] T016 Implement the `Interpreter` `step`/`run`/`realPorts` **dispatch skeleton** in `src/FS.GG.Governance.RouteCommand/Interpreter.fs` against `Loop.Effect` (data-model §4): `step ports effect` matches each `Effect` (`SenseScope`/`LoadCatalog`/`WriteArtifact`/`EmitSummary`) and returns its result `Msg`, wrapping the body in a total catch that reifies any port `Error`/thrown exception to the matching `Msg` (a sensing failure ⇒ `Sensed (Error _)`, an invalid/failed catalog ⇒ `Loaded (Invalid _)`, a write failure ⇒ `Wrote (_, Error _)`) — NEVER throws (FR-010/FR-013); `run ports request` does `Loop.init`, threads each emitted `Effect` through `step`, feeds every result `Msg` back into `Loop.update`, and stops at `Done`, returning the terminal `Model` (the `Host.Interpreter.run` shape); `realPorts repo` binds `Config.Loader.fileSystemReader repo`, `Snapshot.Interpreter.realPorts repo`, a temp+atomic-rename `ArtifactWriter`, and a `Console.Out` `OutputSink`. Leave the SenseScope/LoadCatalog/Write bodies as thin pass-throughs to the reused edges (US1/US2 specialize); the loop + safety wrapper are Foundation.

**Checkpoint**: the project builds with real `parse`/`init`/`exitCode`/`render`-skeleton/`update`-skeleton and the `step`/`run`/`realPorts` dispatch; `run` over faked ports drives `init`→`update` to `Done` for a trivial path (even if the composition body is a TODO); the surfaces compile against both `.fsi`. No user-story behavior yet.

---

## Phase 3: User Story 1 - Route the current change and persist the artifacts (Priority: P1) 🎯 MVP

**Goal**: pointed at a repository with a valid catalog and a changed file under a declared surface, the command senses scope, loads+validates the catalog, runs `Routing.route` → `Gates.buildRegistry` → `Findings.findUnknownGovernedPaths` → `Route.select`, projects `gates.json` (F021) + `route.json` (F020), writes both to disk, prints the summary, and exits 0 — the selected-gate set in `route.json` being exactly the gates the changed path's domain declares, matching the projection of the same typed inputs byte-for-byte. Routine-only changes, an empty changed-path set, and a valid empty catalog all succeed (exit 0) with an empty selected set / empty gate list (FR-009/FR-011, SC-006).

**Independent Test**: in a faked-port (and one real-temp-git) run with a minimal valid catalog and a small edit under a declared surface, confirm both files are written, `route.json`'s selected set equals the gates the edited path's domain declares, `gates.json` lists the full declared catalog, the written bytes equal `RouteJson.ofRouteResult`/`GatesJson.ofGateRegistry` of the same inputs, the summary lists each selected gate by id with its selecting path and per-tier cost, and the exit is 0.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T017 [P] [US1] In `tests/FS.GG.Governance.RouteCommand.Tests/LoopTests.fs`, add pure-`update` composition tests: feed `Sensed (Ok snapshot)` then `Loaded (Valid facts)` into a real-input `Model` and assert (1) `Candidates` is set from the snapshot's changed paths on `Sensed Ok`; (2) on `Loaded Valid`, `Result` is set to `Route.select (Gates.buildRegistry facts) (Routing.route facts candidates) (Findings.findUnknownGovernedPaths facts route)` (re-derived from the real cores — assert equality against directly calling the cores, FR-004), and `GatesDoc`/`RouteDoc` are set to `GatesJson.ofGateRegistry`/`RouteJson.ofRouteResult` of those values (research D9 — both computed before any write), and exactly two `WriteArtifact` effects are emitted (one `GatesArtifact`, one `RouteArtifact`) with the request's `GatesOut`/`RouteOut` paths and those document strings (US1 AS1, **SC-001**); (3) on both `Wrote (_, Ok ())` an `EmitSummary` effect is emitted, and on `Emitted` the `Model` reaches `Phase=Done` with `Exit=Success` (US1 AS1). Use real `TypedFacts`/`RepoSnapshot` from `Support.fs`, not hand-built `RouteResult`s.
- [X] T018 [P] [US1] In `tests/FS.GG.Governance.RouteCommand.Tests/InterpreterTests.fs`, add the faked-port end-to-end-through-fakes test: `Interpreter.run fakePorts request` over a catalog + snapshot where the changed path routes to a declared domain, and assert (1) the capturing `ArtifactWriter` recorded exactly two writes — to `GatesOut` and `RouteOut` — whose contents are **byte-for-byte** `GatesJson.ofGateRegistry registry` / `RouteJson.ofRouteResult result` of the same typed inputs (US1 AS1, **SC-001**, **SC-007**); (2) the `OutputSink` captured a summary listing each selected gate by id with its selecting path and the per-tier cost rollup, plus the findings line and the two written paths (US1 AS2); (3) `model.Exit = Success` and `Loop.exitCode model.Exit = 0` (US1 AS1).
- [X] T019 [P] [US1] In the same `InterpreterTests.fs`, add the **empty-result / empty-input success** tests covering all three "nothing to select, still a success" shapes (US1 AS3, FR-009/FR-011, **SC-006**, spec Edge Cases "No changes in scope" / "Empty catalog"): (1) **routine-only change** — a snapshot whose changed paths touch only routine, unclassified paths routes to an **empty** selected-gate set; assert `route.json` is written with an empty selected set, `gates.json` still lists the full declared catalog, the summary says no gates were selected, and the run exits 0 — routine unclassified files never produce a failure or default-deny; (2) **empty changed-path set** — a snapshot with **no** changed paths (the "no changes in scope" / since-rev-equals-head case) routes nothing and selects nothing; assert a valid `route.json` with an empty selected set, the full `gates.json`, and exit 0 (never an error for an empty diff); (3) **valid empty catalog** — a valid but **empty** `.fsgg` catalog yields an empty `GateRegistry`; assert `gates.json` is a valid document with an **empty gate list**, `route.json` selects nothing, both are written, and the run exits 0 (the empty catalog is a success, never an error — the second clause of SC-006). Use the empty-change and empty-catalog fixtures from `Support.fs` (T008).

### Implementation for User Story 1

- [X] T020 [US1] Complete the in-process composition inside `Loop.update` (`src/FS.GG.Governance.RouteCommand/Loop.fs`, building on T014): on `Loaded (Valid facts)`, run `Routing.route` → `Gates.buildRegistry` → `Findings.findUnknownGovernedPaths` → `Route.select` over the resolved `Candidates` and `facts` (re-deriving/re-sorting/re-classifying nothing, FR-004), set `Result`, set `GatesDoc = GatesJson.ofGateRegistry registry` and `RouteDoc = RouteJson.ofRouteResult result` (both **before** any write, research D9), advance `Phase` Selected→Projected, and emit `WriteArtifact (GatesArtifact, req.GatesOut, gatesDoc)` and `WriteArtifact (RouteArtifact, req.RouteOut, routeDoc)`. On both `Wrote Ok`, advance to `Persisted` and emit `EmitSummary (render model req.Format)`; on `Emitted`, reach `Done`/`Success`. Carry the cores' values verbatim (FR-005).
- [X] T021 [US1] Complete the `WriteArtifact` and `EmitSummary` bodies in `Interpreter.step` (`src/FS.GG.Governance.RouteCommand/Interpreter.fs`, building on T016): `WriteArtifact (kind, path, content)` calls `ports.Write path content` and returns `Wrote (kind, result)` (the real port creating parent dirs as needed and writing via temp-file + atomic rename so a failure leaves no partial file, research D9/FR-010); `EmitSummary text` calls `ports.Out text` and returns `Emitted`. And complete the `LoadCatalog` body: call `Config.Loader.load`/equivalent over `ports.Files` and return `Loaded validation`. (SenseScope is finished in US2.) All wrapped in the total catch (T016).

**Checkpoint**: a real (and faked) repo with a valid catalog and a classified change writes both artifacts whose bytes equal the F020/F021 projections, prints the selected-gate summary, and exits 0; a routine-only change writes an empty selected set and still exits 0 — the MVP. US1 stands alone.

---

## Phase 4: User Story 2 - Scope the change to route (Priority: P1)

**Goal**: the changed-path set is determined by exactly one of `--paths` (explicit, bypasses git diff), `--since <rev>`, or the default sensed base/head — and the routed/selected set reflects the requested scope. `--paths` + `--since` together, and an empty `--paths`, are usage errors.

**Independent Test**: parse and run the command three ways — explicit path list, since-revision, and neither — and confirm the candidate set is, respectively, exactly the given paths (ignoring the working tree), the paths changed since the rev, and the default sensed base/head change; confirm `--paths`+`--since` and empty `--paths` parse to `UsageError`.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T022 [P] [US2] In `tests/FS.GG.Governance.RouteCommand.Tests/ScopeParseTests.fs`, add `parse` tests over argv lists (US2, [contracts/fsgg-route-command.md](./contracts/fsgg-route-command.md)): (1) `--paths a b` ⇒ `Ok { Scope = ExplicitPaths [a; b]; … }`; (2) `--since HEAD~3` ⇒ `Ok { Scope = Since "HEAD~3"; … }`; (3) neither ⇒ `Ok { Scope = DefaultRange; … }`; (4) `--paths a --since X` ⇒ `Error PathsAndSinceTogether`; (5) `--paths` with no value ⇒ `Error EmptyPaths`; (6) `--repo`/`--gates-out`/`--route-out` set the request fields, and the defaults derive `GatesOut=<repo>/.fsgg/gates.json` / `RouteOut=<repo>/readiness/route.json` from `Repo` (research D5); (7) `--json` ⇒ `Format=Json`, absent ⇒ `Text`; (8) an unknown flag ⇒ `Error (UnknownFlag …)` and a flag missing its value ⇒ `Error (MissingValue …)`. PURE/TOTAL — never throws (**SC-003** parse side).
- [X] T023 [P] [US2] In `tests/FS.GG.Governance.RouteCommand.Tests/InterpreterTests.fs`, add the three-scope routing tests through faked ports (**SC-003**): (1) `ExplicitPaths [p1; p2]` routes/selects exactly those paths and the faked git `Ports` is **not** consulted for a diff (the `init` emits `LoadCatalog` directly, research D4) — the working tree's other changes are ignored (US2 AS1); (2) `Since rev` drives `SenseScope (Since rev)` so the candidate set is the faked snapshot's changed-since paths (US2 AS2); (3) `DefaultRange` drives `SenseScope DefaultRange` so the candidate set is the faked default base/head change (US2 AS3). Assert the selected-gate set in the written `route.json` differs appropriately across the three scopes.

### Implementation for User Story 2

- [X] T024 [US2] Confirm/complete the scope wiring across `Loop.init`/`Loop.update`/`Interpreter.step` (`Loop.fs`, `Interpreter.fs`): `init` emits `LoadCatalog` for `ExplicitPaths` (candidates = the given paths, set without sensing) and `SenseScope` for `Since`/`DefaultRange` (research D4); on `Sensed (Ok snapshot)` `update` sets `Candidates` from the snapshot and emits `LoadCatalog`; `Interpreter.step` finishes the `SenseScope` body by calling `Snapshot.Interpreter` (`senseSnapshot`/`Since`/default) through `ports.Git` and returning `Sensed result` (Foundation already wired the catch). Note explicitly if `ExplicitPaths` requires setting `Candidates` in `init` rather than via a `Sensed` message. Note explicitly if no change was needed beyond Phase 2/US1.

**Checkpoint**: the same change routes three ways with the correct candidate set each time, `--paths` bypasses git entirely, and the two mutually-exclusive/empty cases are usage errors — US1 + US2 are the co-equal P1 pairing.

---

## Phase 5: User Story 3 - Deterministic, machine-readable result for CI and agents (Priority: P2)

**Goal**: the persisted `route.json`/`gates.json` and the `--json` stdout summary are byte-for-byte identical for identical repository inputs and carry no wall-clock, machine-absolute path, or environment-derived value; `--json` suppresses the text form; the artifacts carry their declared schema versions and none of the excluded verdict/severity/profile/mode/enforcement/cache/blockers tokens (inherited from F020/F021, not re-introduced here).

**Independent Test**: run the command twice over fixed faked inputs and assert the captured `route.json`/`gates.json` (and the `--json` summary) are byte-identical across runs; assert `--json` emits a deterministic JSON summary and suppresses text; run the exclusion sweep over the written bytes and the summary.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T025 [P] [US3] In `tests/FS.GG.Governance.RouteCommand.Tests/InterpreterTests.fs`, add the **twice-run determinism** test: `Interpreter.run fakePorts request` twice over the same fixed inputs and assert the captured `gates.json`, the captured `route.json`, AND the captured `--json` summary are each byte-for-byte identical between the two runs (US3 AS1, **SC-002**). Add the **render purity** companion in `LoopTests.fs`: `Loop.render model Json = Loop.render model Json` and `Loop.render model Text = Loop.render model Text` for a fixed `Model`.
- [X] T026 [P] [US3] In the same `InterpreterTests.fs`, add the **format** test: a `Json` request captures a parseable JSON summary on the sink and the human text form is **not** emitted; a `Text` request captures the human text and no JSON summary (US3 AS2, FR-007).
- [X] T027 [P] [US3] In the same `InterpreterTests.fs`, add the **exclusion sweep** over the written `route.json`/`gates.json` bytes AND the summary (US3 AS3, **SC-005**, FR-006/FR-008): assert the text contains none of `verdict`, `severity`, `profile`, `mode`, `enforcement`, `cacheEligib`, `blockers`, `warnings`, `exitCode`, a ship/merge decision, a wall-clock timestamp, a machine-absolute path, or an environment-derived value — confirming this feature injects no such value (the artifact bytes equal the F020/F021 projections, which already exclude them).

### Implementation for User Story 3

- [X] T028 [US3] Complete `Loop.render` (`src/FS.GG.Governance.RouteCommand/Loop.fs`, building on T015): `Text` lists each selected gate by id with its selecting path and per-tier cost, the cost rollup (`cheap`/`medium`/`high`/`exhaustive` counts), the unknown-governed-path findings (or "findings: none"), and the two written artifact paths — a no-gate run says so explicitly (US1 AS3); `Json` is the machine-readable form of the same summary. PURE and byte-stable for a fixed `Model` — no clock, machine-absolute path (render the configured relative output paths, not resolved absolutes), or environment value (FR-006/FR-007/SC-002/SC-005). Confirm the artifact writes (T020) introduce no value of their own — they write the F020/F021 strings verbatim; note explicitly that byte-stability is inherited.

**Checkpoint**: identical inputs produce byte-identical artifacts and `--json` summary across runs; `--json` suppresses text; no excluded/nondeterministic token appears — the result is a stable CI/agent contract (the P2 hardening of the P1 MVP).

---

## Phase 6: User Story 4 - Clear, safe failure (Priority: P2)

**Goal**: each of the four failure categories — not-a-repo/unavailable git, missing-or-invalid catalog, unresolved `--since` revision, unwritable output after a valid route — yields a distinct, descriptive diagnostic and a category-mapped non-zero exit code, with **no** partial/malformed artifact written and **no** false blocking/passing verdict; the interpreter never throws.

**Independent Test**: run the command (through faked ports + the real CLI for the usage case) against (a) a non-git directory, (b) a missing required `.fsgg` file, (c) an invalid `.fsgg` file, (d) a non-resolving `--since` rev, and (e) an unwritable output location after a valid route, and confirm each yields a distinct diagnostic and the correct exit code (3 for a–d input-unavailable, 4 for e tool-error, 2 for argv usage errors) with no artifact written for the input/usage cases and no partial file for (e).

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T029 [P] [US4] In `tests/FS.GG.Governance.RouteCommand.Tests/FailureTests.fs`, add the four-category failure tests through faked ports (**SC-004**, FR-010/FR-013): (1) the faked git `Ports` reports not-a-repo/unavailable ⇒ `Sensed (Error _)` ⇒ a "git sensing unavailable" diagnostic, `Exit=InputUnavailable`, `exitCode=3`, **no** write recorded (US4 AS1); (2) the in-memory `FileReader` is missing the required `.fsgg` / returns invalid content ⇒ `Loaded (Invalid diags)` ⇒ a validation diagnostic naming the file/locator/reason, `Exit=InputUnavailable`, `exitCode=3`, **no** write (US4 AS2); (3) the faked git `Ports` reports an unresolved `--since` rev ⇒ `Sensed (Error _)` ⇒ an "unknown revision" diagnostic, `Exit=InputUnavailable`, `exitCode=3`, **no** write (US4 AS3); (4) the capturing `ArtifactWriter` returns `Error` for a write after a valid route ⇒ `Wrote (_, Error _)` ⇒ a write-failure diagnostic, `Exit=ToolError`, `exitCode=4`, and **no** partial/second artifact left (temp+rename, research D9) (US4 AS4). Assert the four diagnostics are **distinct** and category-appropriate.
- [X] T030 [P] [US4] In the same `FailureTests.fs`, add the **usage-error → exit 2 + no artifact** cases driving the public surface: `Loop.parse` returns `Error` for `--paths`+`--since`, empty `--paths`, unknown flag, and missing value, and the `Program`/run path maps each to `exitCode=2` writing **no** artifact (FR-010, [contracts/fsgg-route-command.md](./contracts/fsgg-route-command.md) exit table). Add a **totality** assertion that `Interpreter.run`/`step` never throw for any of the failure inputs above — every failure is a `Msg`/`Diagnostic`/`ExitDecision`, never an exception (FR-013, **SC-004**).

### Implementation for User Story 4

- [X] T031 [US4] Confirm/complete the failure mapping in `Loop.update` and `Interpreter.step` (`Loop.fs`, `Interpreter.fs`, building on T014/T016): each short-circuit records a distinct, actionable `Diagnostic { Category; Message }` (no clock/abs-path/env in the message) and sets `Exit` to the mapped `ExitDecision`; `step`'s total catch reifies a not-a-repo/unresolved-rev sensing failure to `Sensed (Error _)`, a missing/invalid catalog to `Loaded (Invalid _)`, and a write failure to `Wrote (_, Error _)`; the real `ArtifactWriter` writes via temp+atomic-rename so a failure leaves no partial file (research D9); `Program.fs` prints the diagnostics and returns `Loop.exitCode model.Exit` (a `parse` `Error` ⇒ exit 2 before any port is built). Verify input/usage failures emit **no** `WriteArtifact`. Note explicitly if no change was needed beyond Phase 2.

**Checkpoint**: all four failure categories (plus usage errors) produce distinct diagnostics and category-mapped non-zero exit codes with no partial artifact, and the interpreter never throws — the host edge is shippable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: lock the public surface, prove the dependency boundary, run the one real-git end-to-end proof, and finish the docs/evidence.

- [X] T032 [P] Generate `surface/FS.GG.Governance.RouteCommand.surface.txt` capturing exactly the public `Loop` and `Interpreter` modules (the two `.fsi` surfaces), nothing private (no argv-matcher helpers, no in-process composition helpers, no temp+rename writer plumbing, no `Program` entry).
- [X] T033 In `tests/FS.GG.Governance.RouteCommand.Tests/SurfaceDriftTests.fs`, add the surface-drift test asserting the built public surface matches `surface/FS.GG.Governance.RouteCommand.surface.txt` (Principle II, with `BLESS_SURFACE=1` regen path), assert "exactly the `Loop` + `Interpreter` modules, nothing private," and assert the **dependency boundary**: `RouteCommand` references only the eight cores (Config, Snapshot, Routing, Findings, Gates, Route, RouteJson, GatesJson) + `FSharp.Core` — **no** new third-party `PackageReference`, and **no** edge into the kernel-era `Host`/`Cli` (research D1) — mirroring the F020/F021 `SurfaceDriftTests` dependency assertion.
- [X] T034 [US1] In `tests/FS.GG.Governance.RouteCommand.Tests/EndToEndTests.fs`, add the ONE **real-temp-git + real-catalog** end-to-end proof (the `withTempRepo` fixture, T008): build `Interpreter.realPorts repo`, `Interpreter.run` it over a `DefaultRange` (and one `--since`) request, and assert (1) both artifacts exist on disk and their bytes equal `RouteJson.ofRouteResult`/`GatesJson.ofGateRegistry` of the same typed inputs (SC-001, **SC-007** — the full composition through the *real* git + filesystem boundary, no fakes); (2) a re-run produces byte-identical files (**SC-002** on disk); (3) the exit is 0. This is the real-evidence backstop for the faked-port suite (Principle V); no `Synthetic` token expected.
- [X] T035 [P] Verify [quickstart.md](./quickstart.md) end-to-end: run the documented `dotnet test`, `dotnet run -- route …` against a real repo (default/`--paths`/`--since`/`--json`), the determinism diff (SC-002), and the prelude FSI smoke (T009) re-run against the real bodies; confirm the acceptance→evidence map holds and fill `specs/022-fsgg-route-command/readiness/README.md` (T010) with the real transcripts and the SC-001…SC-007 traceability note.
- [X] T036 [P] Update [`specs/022-fsgg-route-command/plan.md`](./plan.md) with an **Implementation Progress** header (status table + evidence summary, mirroring the F020/F021 plans) once the suite is green, and confirm `CLAUDE.md`'s SPECKIT block points at this plan.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundation (Phase 2)** — depends on Setup; **BLOCKS all user stories** (`parse`/`exitCode`/`init`, the `update` transition skeleton + short-circuit shape, the `render` skeleton, and the `step`/`run`/`realPorts` dispatch everything specializes).
- **User Stories (Phases 3–6)** — all depend on Foundation. US1 (P1) is the MVP composition (route→persist). US2 (P1) is the scope selection that feeds US1's composition. US3 (P2) hardens determinism/format over the artifacts US1 writes. US4 (P2) proves safe failure across the whole edge.
- **Polish (Phase 7)** — depends on all desired user stories being complete (the surface baseline, the real-git proof, and the docs).

### User-story dependencies

- **US1 (P1)** — after Foundation; the core composition + dual write. The MVP.
- **US2 (P1)** — after Foundation; scope parse + sense wiring. Independently testable (parse is pure; the three scopes are distinct faked runs). Pairs with US1 as co-equal P1.
- **US3 (P2)** — after US1 (it asserts properties of the document/summary US1 produces): determinism, `--json` format, exclusion. Independently testable.
- **US4 (P2)** — after Foundation (the short-circuit shape) and US1 (a valid route to fail the *write* of); proves distinct diagnostics + exit codes + no-partial-write across all four categories. Independently testable.

### Within each user story

- Tests are written first and MUST FAIL before implementation (Principle I/V).
- `parse`/`exitCode`/`init` + the `update`/`render` skeletons + the `step`/`run` dispatch (Foundation) before any story.
- The MVU split holds throughout: pure `Loop` transition tests assert `Model`+`Effect`s; edge `Interpreter` tests assert written bytes/captured summary through faked ports; one real temp git proves the whole composition (Principle IV/V).
- Each story is independently completable and testable; complete a story before moving to the next priority.

### Parallel opportunities

- **Setup**: T002, T003 are `[P]` (distinct `.fsi` copies); T006, T008, T009, T010 are `[P]` once T001/T004/T005 exist.
- **Tests across stories**: T017–T019 (US1), T022–T023 (US2), T025–T027 (US3), T029–T030 (US4) are `[P]` across the distinct test files (`LoopTests`/`ScopeParseTests`/`InterpreterTests`/`FailureTests`) — but note T018/T019/T023/T025/T026/T027 all live in `InterpreterTests.fs`, so co-authors editing that one file serialize their edits (or one owner sweeps them).
- **Implementation**: the `Loop.fs`/`Interpreter.fs` implementation tasks (T020/T021 US1, T024 US2, T028 US3, T031 US4) edit the two source files — serialize them in phase order; several are "confirm/complete," since the Foundation skeletons (T011–T016) already wire most of the machine.
- **Polish**: T032, T035, T036 are `[P]`; T033 depends on T032; T034 depends on the full composition (US1–US4).

---

## Parallel Example: cross-story test authoring

```bash
# After Foundation (Phase 2), launch the per-story test files together (distinct files):
Task: "LoopTests.fs      — US1 pure update composition: Sensed/Loaded → Result + 2 WriteArtifact + Done (T017); render purity (T025)"
Task: "ScopeParseTests.fs — US2 parse: --paths/--since/default, exclusivity, defaults, flags, errors (T022)"
Task: "FailureTests.fs   — US4 four categories → distinct diag + exit 3/4, usage → exit 2, never throws (T029–T030)"
# InterpreterTests.fs (T018/T019 US1, T023 US2, T025–T027 US3) is one file — one owner sweeps it in phase order.
```

---

## Implementation Strategy

### MVP first (User Story 1; co-equal P1 with User Story 2)

The **minimum** shippable increment is **US1** — route → project → persist → summarize → exit 0 (it stands alone on the default scope). **US2** is its co-equal P1 partner (the scope selector) and should land with it for a useful CLI; treat US1+US2 together as the delivered P1 MVP.

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1 (route → project → persist → summarize → exit 0) — the standalone MVP.
4. Complete Phase 4: User Story 2 (scope: `--paths` / `--since` / default) — completes the co-equal P1.
5. **STOP and VALIDATE**: a real (and faked) repo with a valid catalog and a classified change writes both artifacts whose bytes equal the F020/F021 projections and exits 0, and the same change routes correctly under all three scopes.

### Incremental delivery

1. Setup + Foundation → the MVU machine is wired (pure boundary + edge loop).
2. US1 → the cores compose, both artifacts persist, the summary prints → the MVP.
3. US2 → the change can be scoped three ways → the co-equal P1.
4. US3 → the artifacts/summary are deterministic, versioned, exclusion-clean → CI/agent-ready.
5. US4 → every failure category is distinct, safe, and non-zero → shippable host edge.
6. Polish → surface baseline + dependency assertion + the one real-git proof + readiness/quickstart.

---

## Notes

- `[P]` = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- The MVU boundary is **load-bearing** here (Principle IV): keep `Loop` pure (no I/O, no git, no clock) and confine all I/O to `Interpreter` effects executed through injected, fakeable ports — the `Host`/`Snapshot` pattern, not the heavy Elmish `Program` runtime (research D2).
- The two `Loop.fs`/`Interpreter.fs` implementation tasks per story (T020/T021, T024, T028, T031) edit two files — serialize them in phase order; most later ones are "confirm/complete," since the Foundation skeletons (T011–T016) already wire the dispatch.
- Tests inspect the **written bytes / captured summary** through the public surface (`Loop.update`/`render` and `Interpreter.run` over faked ports), never private helpers; one real-temp-git proof backstops the fakes (Principle V). **No synthetic evidence is anticipated** — every case is reachable from real `TypedFacts`/`RepoSnapshot` through faked ports plus the one real repo; any unavoidable literal carries the `Synthetic` token + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- The artifacts are the F020/F021 projections **byte-for-byte** — this feature serializes nothing of its own and injects no value; byte-stability and the exclusion sweep are inherited (SC-005), and the only obligation is to not inject a clock/abs-path/env value, which `render`/the writer must also honor.
- Scope guards (FR-008, slice boundary): no `fsgg ship`, ship/merge verdict, severity, profile, mode, enforcement, cache-eligibility verdict, blockers, warnings, exit-code-from-blockers, `audit.json`, or branch-protection guidance — the command stops at the two documents + summary + exit code, adds no third-party dependency, and does not touch the kernel-era `Host`/`Cli`.
