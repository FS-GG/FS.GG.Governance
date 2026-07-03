---

description: "Task list for feature 100 — repair the repository's dependency fences"
---

# Tasks: Repair the repository's dependency fences

**Input**: Design documents from `/specs/100-dependency-fences/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/dependency-fences.md ✅

**Tier**: overall **Tier 2** (internal refactor + hygiene + guards; no product public API / JSON contract / package-ID change). Per-task Tier annotations omitted where they match.

**Tests**: guard tests ARE the deliverable for this feature (the fences), so test tasks are first-class, not optional.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different files).
- **[Story]**: US1 (YAML fence) · US2 (exe-leaf) · US3 (fsgg) · US4 (P3 add-ons).
- Exact file paths are given in each task.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: create the one guard-test project that hosts all three fence assertions, modeled on `tests/FS.GG.Governance.RenameGuard.Tests` (Tier 2, no `.fsi`, private bindings, scans `git ls-files '*.fsproj'`).

- [X] T001 Create `tests/FS.GG.Governance.DependencyFences.Tests/FS.GG.Governance.DependencyFences.Tests.fsproj` (Expecto test project; net10.0; no `.fsi`; add to the solution). Mirror the packages/structure of `tests/FS.GG.Governance.RenameGuard.Tests/FS.GG.Governance.RenameGuard.Tests.fsproj`.
- [X] T002 Add `tests/FS.GG.Governance.DependencyFences.Tests/Main.fs` — the Expecto `[<EntryPoint>]` runner (copy the RenameGuard `Main` shape).
- [X] T003 [P] Add `tests/FS.GG.Governance.DependencyFences.Tests/ProjectGraph.fs` — the shared, private read-only model + parser: locate repo root (git), enumerate `git ls-files '*.fsproj'`, parse each into a `ProjectNode` (`Name`, `OutputType`, `PackAsTool`, `ToolCommandName`, `PackageReferences`, `ProjectReferences`) per `data-model.md`, and expose pure matchers (`yamlOwnerViolations`, `exeReachesExe`, `fsggClaimants`). Include the transitive `ProjectReference` reachability used by the exe-leaf rule.

**Checkpoint**: an empty-but-compiling guard project + a parsed project-graph the three stories assert against.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: establish ground truth before writing allowlists — audit the real graph so the documented sets are correct.

- [X] T004 Audit the five direct-`YamlDotNet` projects (Config, CurrencySensing, RefreshCommand, ReleaseDeclaration, ReleaseCommand): for each, confirm a `YamlDotNet` type is actually *used* in its `.fs` sources. Record which references are genuine vs. dead in a short note appended to `specs/100-dependency-fences/research.md` (§D1). This decides the final YAML-owner allowlist. **Blocks T010.**
- [X] T005 [P] Confirm the exe→exe edges are exactly `Cli → RouteCommand` and `EvidenceCommand → Cli` (no others) by inspecting all eight exe `.fsproj` files; confirm the exact symbols to move match research §D2 (`RouteCommand.Interpreter`/`Loop`; Cli `Project.*` + `defaultJudge`). **Blocks T014, T018.**

**Checkpoint**: ground-truth allowlists confirmed; the guards and README can now be written against reality.

---

## Phase 3: User Story 1 — YAML fence (Priority: P1) 🎯 MVP

**Goal**: the direct-`YamlDotNet` owner set matches documentation exactly, guarded.

**Independent Test**: `dotnet test tests/FS.GG.Governance.DependencyFences.Tests` passes the YAML case; adding a `YamlDotNet` ref to `Findings` turns it red (quickstart Scenario 2).

- [X] T006 [US1] Remove any *dead* `<PackageReference Include="YamlDotNet" />` found in T004 from the offending `src/FS.GG.Governance.<Name>/*.fsproj` (only where no YamlDotNet type is used). If all five are genuine, this task is `[-]` with that rationale.
- [X] T007 [US1] Update `README.md` (lines ~117 and ~146): replace "YamlDotNet isolated here / isolated internal detail" with the true owner set from T004/T006 and a one-line reason per owner (mirror the table in `contracts/dependency-fences.md` §Fence 1).
- [X] T008 [US1] Finalize the owner allowlist constant in `contracts/dependency-fences.md` §Fence 1 (strike any owner removed in T006).
- [X] T009 [US1] Add `tests/FS.GG.Governance.DependencyFences.Tests/YamlFenceTests.fs` — assert the direct-`YamlDotNet` set (from `ProjectGraph`) equals the documented allowlist exactly (undocumented member → fail; documented owner that dropped it → fail), with a file-level diagnostic. Add a red-path unit test over literal `ProjectNode`s. **Depends on T003, T008.**

**Checkpoint**: YAML fence green and guarded; MVP deliverable complete and independently shippable.

---

## Phase 4: User Story 3 — single `fsgg` owner (Priority: P2)

> Sequenced before US2 because it is a pure metadata change (low risk) and unblocks a quick second win.

**Goal**: at most one project claims `ToolCommandName=fsgg`, guarded.

**Independent Test**: the `fsgg` guard passes; setting a second project's `ToolCommandName` to `fsgg` turns it red.

- [X] T010 [P] [US3] In `src/FS.GG.Governance.EvidenceCommand/FS.GG.Governance.EvidenceCommand.fsproj` change `<ToolCommandName>fsgg</ToolCommandName>` → `fsgg-evidence`. (PackageId unchanged.)
- [X] T011 [P] [US3] In `src/FS.GG.Governance.CacheEligibilityCommand/FS.GG.Governance.CacheEligibilityCommand.fsproj` change `<ToolCommandName>fsgg</ToolCommandName>` → `fsgg-cache-eligibility`. (PackageId unchanged.)
- [X] T012 [US3] Update `README.md` CLI/commands section to list the new invocation names (`fsgg-evidence`, `fsgg-cache-eligibility`) alongside `fsgg` (RouteCommand) and `fsgg-governance` (Cli).
- [X] T013 [US3] Add `tests/FS.GG.Governance.DependencyFences.Tests/FsggOwnerTests.fs` — assert `|{ p : ToolCommandName = "fsgg" }| ≤ 1`, naming the second claimant on failure. Add a red-path unit test. **Depends on T003.**

**Checkpoint**: `fsgg` collision removed and guarded; two P2-metadata + one P1 fences now green.

---

## Phase 5: User Story 2 — every executable is a leaf (Priority: P2) ⚠️ highest-risk slice

**Goal**: no executable references another executable; the two shared payloads live in internal libraries. **May ship as its own commit/PR after Phases 1–4.**

**Independent Test**: the exe-leaf guard passes; `RouteCommand`/`Cli`/`EvidenceCommand` semantic + `SurfaceDriftTests` stay green; `fsgg route` and `fsgg-evidence` output is unchanged.

> **De-risking notes (from the US2 pre-flight investigation):**
> - **Keep the `FS.GG.Governance.RouteCommand` namespace** on the moved `Loop`/`Interpreter` (they live in RoutePipeline.dll). Cli references them **fully-qualified** (`Program.fs:141–164`), and the `RouteCommand` tokens in `Cli.fs`/`Cli.fsi`/`CliRender.fs` are a DU case in Cli's own `Command` type — *not* the project. ⇒ **T016 needs no Cli source change, only the project-ref swap.** `Loop`/`Interpreter` reference no other RouteCommand-local module (only `Program` uses them), so the move is clean.
> - **RouteCommand.Tests/SurfaceDriftTests.fs encodes two invariants that must RE-HOME to RoutePipeline** (it now owns the surface + the real dependency closure): (1) "public surface is exactly Loop+Interpreter (+ Program entry)" and (2) "references only the cores/BCL, no kernel/host/cli/adapters/Spectre". Plan: add a new **`RoutePipeline.Tests`** with those two tests + `surface/FS.GG.Governance.RoutePipeline.surface.txt` (bless via `BLESS_SURFACE=1`); reduce RouteCommand.Tests' surface test to the thin `Program` entry and re-bless `surface/FS.GG.Governance.RouteCommand.surface.txt`.
> - Surface baselines are per-project files under `surface/`, regenerated with `BLESS_SURFACE=1 dotnet test`.
> - **ProjectSensing (Extraction 2) is the harder half**: `defaultJudge` is embedded in the large `Cli` module (`Cli.fs:121`, exposed via `Cli.fsi`), so it must be relocated alongside the `Project` module and Cli's own uses re-pointed. Do RoutePipeline first, validate green, then tackle this.

### Extraction 1 — RoutePipeline (breaks `Cli → RouteCommand`)

- [X] T014 [US2] Create `src/FS.GG.Governance.RoutePipeline/` library (`IsPackable=false`, net10.0): **move** `Interpreter.fs`/`.fsi` and `Loop.fs`/`.fsi` from `src/FS.GG.Governance.RouteCommand/` here, keeping module/type/member names identical (`Interpreter.Ports/realPorts/run`, `Loop.RunRequest/DefaultRange/Text/humanView`). Carry over the `ProjectReference`s those modules need (Config, Snapshot, Routing, Findings, Gates, Route, RouteJson, GatesJson, HumanText/HumanRender, Freshness*, EvidenceReuse*, GateRun/GateExecution, CommandHost, EvidenceCapture, Adapters.SddHandoff). Add to solution. **Depends on T005.**
- [X] T015 [US2] Slim `src/FS.GG.Governance.RouteCommand/` to a `main` leaf: reference `RoutePipeline`, drop the moved files, keep `<ToolCommandName>fsgg</ToolCommandName>` + `PackageId`. Re-baseline its `SurfaceDriftTests` for the surface that moved out. **Depends on T014.**
- [X] T016 [US2] In `src/FS.GG.Governance.Cli/`: replace the `RouteCommand` project reference with a `RoutePipeline` reference; update `Program.fs:141–164` `open`/usages to the RoutePipeline namespace. Remove `<ProjectReference Include="../FS.GG.Governance.RouteCommand/...">`. **Depends on T014.**

### Extraction 2 — ProjectSensing (breaks `EvidenceCommand → Cli`)

- [X] T017 [US2] Create `src/FS.GG.Governance.ProjectSensing/` library (`IsPackable=false`, net10.0): **move** `Project.fs`/`Project.fsi` (module `Project`: `identify`/`compose`/`toLoopConfig`/`evidenceReport` + `ProjectFact`/`ProjectOptions`/`ProjectEvidenceReport`/`ProjectSnapshot`) and the `defaultJudge : JudgeId` constant out of `src/FS.GG.Governance.Cli/`. Carry the `ProjectReference`s they need (Host, Adapters.SpecKit, Adapters.DesignSystem, Kernel, EvidenceJson, …). Add to solution. **Depends on T005.**
- [X] T018 [US2] In `src/FS.GG.Governance.Cli/`: reference `ProjectSensing`, drop the moved files, update internal `open`/usages, and re-baseline `Cli.Tests` `SurfaceDriftTests` for the moved surface. **Depends on T017.**
- [X] T019 [US2] In `src/FS.GG.Governance.EvidenceCommand/`: replace the `Cli` project reference with a `ProjectSensing` reference; update `Interpreter.fs`/`Loop.fs` (+ `.fsi`) `open FS.GG.Governance.Cli` → the ProjectSensing namespace; keep `<ToolCommandName>fsgg-evidence</ToolCommandName>` from T010. Remove `<ProjectReference Include="../FS.GG.Governance.Cli/...">`. **Depends on T017, T010.**

### Guard

- [X] T020 [US2] Add `tests/FS.GG.Governance.DependencyFences.Tests/ExeLeafTests.fs` — assert no `Exe` node reaches another `Exe` node via the transitive `ProjectReference` closure; on failure list every offending `Exe → Exe` edge. Add a red-path unit test over a literal graph containing an exe→exe edge. **Depends on T003; verifies T015/T016/T019.**

**Checkpoint**: all eight executables are leaves; both edges gone; behavior unchanged; fence guarded.

---

## Phase 6: User Story 4 — P3 add-ons (Priority: P3, cheap-only)

**Goal**: low-severity hygiene where cheap and zero-risk; any non-trivial item becomes a follow-up issue under epic #44 and is marked `[-]` here.

- [-] T021 [P] [US4] **Deferred to a follow-up under epic #44** — verifying "no packable tool's effective version changes" across the ~13 baseline-only projects is a build-config change warranting review; split off per the cheap-only clause. Add a single `<VersionPrefix>` to `Directory.Build.local.props` (repo-owned, drift-exempt — NOT `Directory.Build.props`) so the ~13 baseline-only `.fsproj` files without `<Version>` inherit it. Verify via `dotnet msbuild -getProperty:Version` that no packable tool's effective version changes unexpectedly (quickstart Scenario 6).
- [X] T022 [P] [US4] Add an edge-tier reference-convention note to `README.md` (or `docs/`): command/edge-tier projects may carry broad reference lists by convention (example: `VerifyCommand`, 43 refs / 32 reachable).
- [X] T023 [P] [US4] Add `docs/adr/README.md` (or equivalent) — a local index/pointer for the org-level ADRs this repo cites (0007/0012/0013).

**Checkpoint**: P3 items done or explicitly deferred with rationale.

---

## Phase 7: Polish & Validation (Cross-Cutting)

- [X] T024 All three fences proven red on a real-tree break with actionable diagnostics (SC-004): YAML (Findings gains a ref), fsgg (2nd claimant), exe-leaf (ShipCommand→RouteCommand). Ran the full guard project red-path proof: temporarily break each of the three fences (quickstart Scenario 2) and confirm each guard fails with an actionable diagnostic; revert. Record as SC-004 evidence.
- [X] T025 Verify FR-007/008 / SC-005: `git diff --name-only origin/main... -- Directory.Build.props Directory.Packages.props .config/dotnet-tools.json` is empty; no `PackageId` and no JSON contract file changed (quickstart Scenario 4).
- [X] T026 Run `dotnet build && dotnet test` — full suite green (FR-009, quickstart Scenario 5). This is the real acceptance evidence.

---

## Dependencies & Execution Order

### Phase order
- **Phase 1 (Setup)** → **Phase 2 (Foundational audit)** → user-story phases → **Phase 7 (Validation)**.
- Story sequencing: **US1 (P1)** → **US3 (fsgg, quick P2)** → **US2 (exe-leaf, risky P2)** → **US4 (P3)**. US1/US3/US2 are independently testable and could be reordered, but US2 is deliberately last (highest risk; can be its own PR).

### Key cross-task dependencies
- T004 (YAML audit) blocks T006–T009 (the allowlist must be true first).
- T005 (symbol confirmation) blocks T014/T017 (extraction).
- T014 blocks T015/T016; T017 blocks T018/T019; T010 feeds T019.
- All guard tests (T009/T013/T020) depend on the shared `ProjectGraph.fs` (T003).

### Parallel opportunities
- T010 ‖ T011 (different `.fsproj`s). T021 ‖ T022 ‖ T023 (P3, different files).
- The two extractions (T014-chain ‖ T017-chain) touch mostly disjoint projects and can proceed in parallel, converging only where both edit `src/FS.GG.Governance.Cli/`.

---

## Implementation Strategy

- **MVP = Phase 1 + 2 + US1** (YAML fence): smallest honest, guarded win; independently shippable.
- **Incremental**: land US1 + US3 + guards as one low-risk commit; land US2 (extraction) as a second commit/PR after the exe semantic tests confirm no regression; P3 last or deferred.
- **Never** green a build by weakening a guard assertion — narrow scope and document (`[-]`) instead.

## Notes

- `[P]` = different files, no incomplete-dependency in-phase.
- Principle IV (Elmish/MVU): US2 preserves the route pipeline's `Interpreter.Ports` boundary intact during relocation — no new stateful workflow is introduced, so no new `Model`/`Msg`/`Effect` contract is authored; the existing boundary moves verbatim.
- Commit after each checkpoint; keep the org-synced props untouched throughout.
