---
description: "Task list for feature 079 — Publish a Populated Reference `.fsgg` Gate Set"
---

# Tasks: Publish a Populated Reference `.fsgg` Gate Set

**Input**: Design documents from `/specs/079-reference-gate-set/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tier**: Tier 2 (additive data + test + docs). No new public F# surface, no `.fsi`, no
surface-area baseline change (research D7). The on-disk `.fsgg` artifact is the
deliverable; the regression guard freezes its invariants.

**Elmish/MVU applicability**: N/A. No new stateful/I/O workflow — the guard reads through
the **existing** config edge (`Config.Loader.loadAndValidate`) and calls pure cores
(`Gates`/`Routing`/`Route`/`Enforcement`). Constitution Principle IV is satisfied by
reuse, not a new MVU boundary (plan Constitution Check).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task
  in this phase)
- **[Story]**: `US1` / `US2` / `US3` — maps the task to a user story for traceability
- All tasks are Tier 2 (matches the spec); no per-task `[T1]/[T2]` annotation needed
- Exact file paths are given in every task

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the artifact directory and the regression-guard test project shell so
later phases have somewhere to author into.

- [X] T001 [P] Create the published-reference directory skeleton:
  `samples/sdd-reference-gate-set/` and its `samples/sdd-reference-gate-set/.fsgg/`
  subdirectory (empty for now; the four YAML files land in Phase 2).
- [X] T002 Create the guard project file
  `tests/FS.GG.Governance.ReferenceGateSet.Tests/FS.GG.Governance.ReferenceGateSet.Tests.fsproj`
  — `Microsoft.NET.Sdk`, `<IsPackable>false</IsPackable>`, xUnit packages
  (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`); `ProjectReference`s to
  `src/FS.GG.Governance.Config`, `.Gates`, `.Routing`, `.Route`, `.Enforcement`, and
  `tests/FS.GG.Governance.Tests.Common` (research D6 — this is the only project spanning
  all four upper layers + Enforcement). Leave the `<Compile>` item group empty until T008.
  **Deviation (recorded):** used **Expecto + YoloDev** (the repo-wide test convention under
  central package management), NOT xUnit — `xunit`/`xunit.runner.visualstudio` are absent from
  `Directory.Packages.props`, so the spec's literal xUnit packages would not resolve and would
  diverge from all 80 sibling test projects. The framework is incidental; the binding G1–G7
  real-evidence assertions hold regardless. Also added an explicit `Findings` ProjectReference
  (Route's `select` consumes a `FindingReport`).
- [X] T003 Register `FS.GG.Governance.ReferenceGateSet.Tests` in `FS.GG.Governance.sln`
  with a fresh project GUID under the F# project-type GUID
  `{F2A71F9B-5D33-465A-A702-920D77279786}` (match the existing `Route.Tests` /
  `Tests.Common` registration shape), and add its build/Debug/Release configuration rows.
  Depends on T002.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` resolves the new (empty) test project.

---

## Phase 2: Foundational (the reference artifact) — BLOCKS all user stories

**Purpose**: Author the four-file populated reference `.fsgg`. This artifact is the
feature's core deliverable; every user-story guard assertion loads it. **No guard
assertion can pass until these exist.**

**⚠️ CRITICAL**: All field names, enums, and required/optional status MUST follow
`src/FS.GG.Governance.Config/Model.fsi` + `Schema.fs`; concrete content is in
data-model.md §A. The directory MUST be copyable **unedited** (no absolute/host paths) so
the P4 overlay can adopt it (FR-009/SC-005) — the only adopter substitution is the
`<App>` placeholder in `tooling.yml` command strings.

- [X] T004 [P] Author `samples/sdd-reference-gate-set/.fsgg/project.yml` (schemaVersion 1):
  `id: sdd-reference-gate-set`, `governedRoot: .`, domains `build`/`test`/`evidence`,
  `packageSurfaces: [src]`, `policyRef`/`capabilitiesRef` → the sibling files
  (data-model §A).
- [X] T005 [P] Author `samples/sdd-reference-gate-set/.fsgg/capabilities.yml`
  (schemaVersion **2** — v1 is loader-rejected, research D2): the three `domains`; the
  four `pathMap` globs (`src/**`→build, `*.sln`→build, `tests/**`→test,
  `build.fsx`→evidence, research D3); the `public-api` package surface; and the **three
  checks** `build`/`test`/`evidence` with full metadata (owner/cost/environment/maturity,
  `tier` on build+test, omitted on evidence) per data-model §A / research D4. Each
  check's `command` MUST name a command authored in T007 (no dangling refs).
- [X] T006 [P] Author `samples/sdd-reference-gate-set/.fsgg/policy.yml` (schemaVersion 1):
  `defaultProfile: light`, profiles `light`/`standard`/`strict`/`release`, plus
  `branchPolicy` + `reviewBudget` (data-model §A). `light` default is the load-bearing
  non-blocking posture (FR-006).
- [X] T007 [P] Author `samples/sdd-reference-gate-set/.fsgg/tooling.yml` (schemaVersion 1):
  the three commands `dotnet-build` / `dotnet-test` / `build-evidence` (the last =
  `"dotnet fsi build.fsx -- evidence"`, the in-process EvidenceGraph/EvidenceAudit shape,
  research D8), `environmentClasses`, and the `dotnet` external tool (data-model §A). Every
  command an FR-005 check binds to MUST be declared here; no dead/unreferenced commands
  (0 orphan commands, SC-004).
- [X] T008 Manual load smoke-check before writing the guard: from the repo root, confirm
  `Config.Loader.loadAndValidate "samples/sdd-reference-gate-set"` returns
  `Validation.Valid` with an empty diagnostics list (use the CLI `fsgg route` against the
  copied dir, or `dotnet fsi`). If any `MissingRequiredField`/`UnknownField`/
  `DanglingReference` appears, fix the YAML in T004–T007 before proceeding. Depends on
  T004–T007. (This is a fast author-loop check; the durable assertion is G1 in T010.)

**Checkpoint**: The reference loads `Valid` with 0 diagnostics. Foundation ready — user
stories can begin.

---

## Phase 3: User Story 1 — An adopter gets gates that actually fire (Priority: P1) 🎯 MVP

**Goal**: The published reference loads clean, assembles into exactly 3 gates with no
dangling/orphan refs, and routes real build/test/evidence gates for governed paths.

**Independent Test**: `dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests
--filter "FullyQualifiedName~Loads|FullyQualifiedName~Routes"` is green — load returns
`Valid` (0 diagnostics), registry has the 3 gates, candidate paths select their gate.

> **Write each assertion to FAIL first** (before the artifact/guard exist) and pass after
> — Constitution Principle V (real evidence against the on-disk artifact, no synthetic
> facts, no mocks). These assertions implement guard contract G1–G4.

- [X] T009 [US1] Create `tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetGuardTests.fs`
  with the shared load fixture: resolve the repo root via
  `Tests.Common.RepositoryHelpers.repoRoot`, build the path to
  `samples/sdd-reference-gate-set`, and load it once through
  `Config.Loader.loadAndValidate` for the assertions to share. Add the file to the
  `.fsproj` `<Compile>` item group (T002). No assertions yet beyond the fixture compiling.
- [X] T010 [US1] **G1** — assert `loadAndValidate` returns `Validation.Valid facts` with
  an **empty** diagnostics list: 0 validation errors and 0 `UnknownField` findings
  (FR-007/SC-002). Name the test so `~Loads` selects it.
- [X] T011 [US1] **G2** — `Gates.buildRegistry facts` yields exactly **3** gates
  including `build:build`, `test:test`, `evidence:evidence`; assert the check set is
  non-empty and contains a build + test + evidence check (FR-002/FR-003/SC-001). Guards
  "rots to empty."
- [X] T012 [US1] **G3** — every gate's command prerequisite (`RequiresCommand …`)
  resolves to a command declared in `tooling.yml`: `dotnet-build`/`dotnet-test`/
  `build-evidence`; 0 dangling command references (FR-004/SC-001/SC-007).
- [X] T013 [US1] **G4** — drive `Routing.route` + `Route.select` over candidate paths
  `["src/App/Program.fs"; "App.sln"; "tests/App.Tests/Tests.fs"; "build.fsx"]`; assert
  build/test/evidence each selected by its path (the `src/**` *and* `*.sln` globs both
  route to `build`), and 0 orphan checks / 0 orphan commands / 0 unreachable domains
  (FR-005/FR-008/SC-004). Including `App.sln` exercises the `*.sln` path-map glob that
  `src/App/Program.fs` alone would leave untested. Name tests so `~Routes` selects them.

**Checkpoint**: MVP — the reference is a loadable, routable, copyable artifact with gates
that fire. US1 is independently testable and deliverable on its own.

---

## Phase 4: User Story 2 — First-touch is non-blocking by default (Priority: P2)

**Goal**: Prove `light` is the declared default and is a *deliberate* non-blocking choice
— advisory under `Light`, yet blockable under `Strict` on the same failing change.

**Independent Test**: `dotnet test … --filter "FullyQualifiedName~Profile"` is green —
`defaultProfile == light`; on a failing change (`BaseSeverity = Blocking`) at
`RunMode.Verify`, all selected gates are `Advisory` under `Light` and ≥1 is `Blocking`
under `Strict`.

> Implements guard contract G5–G7. **Document the `RunMode.Verify` choice at the use
> site** — Verify (ordinal 3) is the *only* mode where `block-on-ship` is Light-advisory
> yet Strict-blocking (research D5); leave a comment so the mode is not read as arbitrary.

- [X] T014 [US2] **G5** — assert `policy.defaultProfile == light` from the loaded facts
  (FR-006/SC-007). Guards "default profile drifts to blocking."
- [X] T015 [US2] **G6** — for each gate selected in T013, construct the
  `EnforcementInput` per `Enforcement.fsi` (the gate's own `Maturity` from the registry,
  `BaseSeverity = Blocking` to model the failing change, `RunMode = Verify`,
  `Profile = Light`) and call `Enforcement.deriveEffectiveSeverity`; assert **every**
  result is `EffectiveSeverity.Advisory` — 0 blocking outcomes (FR-006/SC-003). Name so
  `~Profile` selects it.
- [X] T016 [US2] **G7** — same selected gates, same `BaseSeverity = Blocking` /
  `RunMode = Verify`, but `Profile = Strict`; assert **≥1** result is
  `EffectiveSeverity.Blocking` on the same change (SC-006). Proves the gates *can* block —
  `light` is a chosen default, not an inability.

**Checkpoint**: US1 + US2 both pass independently. The "populated ≠ blocking by default"
constraint is frozen in both directions.

---

## Phase 5: User Story 3 — Evidence integrity is a declared, first-class gate (Priority: P3)

**Goal**: The evidence-integrity check is present as a normal governed gate bound to a
declared command, and stays advisory on first touch (no real evidence yet) under every
profile.

**Independent Test**: Inspect the registry/routing/enforcement results for
`evidence:evidence`: present, bound to `build-evidence`, `warn` maturity, and `Advisory`
under both `Light` and `Strict` at `RunMode.Verify`.

> Largely covered transitively by G2 (present), G3 (command bound), G4 (routed/selected),
> G6 (advisory under Light). This phase adds the **explicit first-touch advisory**
> assertion that distinguishes evidence from the block-capable build/test (research D4 —
> `evidence` is `warn`, advisory everywhere).

- [X] T017 [US3] Assert `evidence:evidence` is selected for `build.fsx` and bound to the
  declared `build-evidence` command with `warn` maturity (FR-003), and that
  `deriveEffectiveSeverity` returns `Advisory` for it under **both** `Light` and `Strict`
  at `RunMode.Verify` with `BaseSeverity = Blocking` — i.e. evidence never blocks on
  first touch even when the block-capable gates do (US3 scenario 2). Add to
  `ReferenceGateSetGuardTests.fs`.

**Checkpoint**: All three user stories pass independently; the guard contract G1–G7 is
fully realized.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Adopter documentation (FR-011), discoverability, and end-to-end validation.

- [X] T018 [P] Author `samples/sdd-reference-gate-set/README.md` (FR-011): a gate-by-gate
  explanation of `build`/`test`/`evidence`, the non-blocking-by-default (`light`) posture,
  and **how to ratchet strictness up deliberately** (switch profile to `strict`/`release`
  — tie back to the Verify-mode advisory→blocking behavior from research D5). Co-located so
  a downstream copy is self-documenting (research D9).
- [X] T019 [P] Add a short cross-link from `docs/tutorials/` (sibling to the existing 072
  `adopter-onboarding.md` / `provider-author.md` / `sdd-governance-handoff.md`) pointing to
  `samples/sdd-reference-gate-set/README.md` for onboarding discoverability (research D9).
- [X] T020 Downstream-reuse check (FR-009/SC-005): copy
  `samples/sdd-reference-gate-set/.fsgg/` **unedited** into a fresh scratch directory and
  `loadAndValidate` it — confirm it validates + routes with 0 edits (no absolute paths /
  repo-internal refs). Record the result in the quickstart-validation note.
- [X] T021 Run quickstart.md end-to-end: `dotnet test
  tests/FS.GG.Governance.ReferenceGateSet.Tests` green for all of G1–G7, then sanity-mutate
  the reference (empty the checks / break a command ref / flip `defaultProfile` off `light`)
  and confirm the guard turns **red** for each (SC-007) — revert the mutations after.
- [-] T022 Run the full solution suite `dotnet test FS.GG.Governance.sln` to confirm the
  new project compiles into the solution and no existing project regressed (the SDD
  template-generation integration test and the pre-existing CLI `dotnet pack` timeout flake
  are known-out-of-scope per recent feature notes).
  **Skipped (rationale, honest status — not `[X]`):** the full `dotnet test
  FS.GG.Governance.sln` did not complete in this environment — the solution **build alone**
  ran >22 min and was killed (it was progressing green through dozens of projects —
  GatesJson/CurrencyEnforcement/ReleaseJson/Route.Tests/EvidenceReuse.Tests/… — with no
  compile errors; the only log lines were the MSB5021/MSB4166 cancellation artifacts from the
  kill). The pathological slowness matches the recent feature notes (the SDD
  template-generation integration test + CLI `dotnet pack` flake are documented as
  out-of-scope/slow here). **Confirmation obtained by other means:** (a) `dotnet build` of the
  guard project compiled the entire upstream dependency closure green — Config, Gates,
  Routing, Findings, Route, Enforcement, Tests.Common and their transitive src/ closure
  (EvidenceCapture, CacheEligibility, GateExecution, Ship, GateRun, ReleaseRules,
  FreshnessSensing, PackEvidence, Attestation, ReleaseReport, HumanText, …); (b) the new guard
  suite is 8/8 green (G1–G7 + the evidence assertion); (c) the change set is additive-only — a
  new test project + YAML data + docs + additive `.sln` rows, **zero `src/` production code** —
  so no existing project can regress. A full-suite run on faster hardware/CI remains the
  durable confirmation.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 has no deps; T002→T003 are sequential (sln needs the project).
- **Foundational (Phase 2)**: T004–T007 depend only on the T001 directory and may run in
  parallel; T008 depends on T004–T007. **BLOCKS all user stories** — every guard
  assertion loads this artifact.
- **User Stories (Phase 3–5)**: all depend on Phase 2 (artifact exists, loads `Valid`) and
  on T009 (the guard file + shared load fixture). Given the fixture, US1/US2/US3 add
  assertions to the **same file** — sequence them or coordinate edits (not file-parallel).
- **Polish (Phase 6)**: T018–T019 depend only on the artifact (parallel with the guard
  work in principle); T020–T022 depend on the guard being complete.

### Within / across user stories

- US1 (P1) is the MVP and the floor: G1–G4 establish load/registry/routing. US2 (G5–G7)
  and US3 (T017) reuse US1's `select` results, so author T013 before T015/T016/T017.
- All three stories write into `ReferenceGateSetGuardTests.fs` → **not** `[P]` against each
  other. The artifact YAML files (T004–T007) and the two docs files (T018–T019) **are**
  `[P]` (distinct files).

### Parallel Opportunities

- **Phase 2**: T004, T005, T006, T007 — the four YAML files — in parallel.
- **Phase 6**: T018 (README) and T019 (tutorials cross-link) in parallel.
- The guard assertions share one file, so they parallelize as authoring effort only if the
  file is split; by default treat T010–T017 as sequential edits to one file.

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational (the artifact loads `Valid`).
2. Phase 3 US1 → **STOP and VALIDATE**: the reference is a loadable, routable, copyable
   gate set. This alone unblocks the P4 Templates overlay (it copies the artifact) — MVP
   shippable.

### Incremental Delivery

1. Setup + Foundational → the artifact exists and loads clean.
2. + US1 (G1–G4) → gates fire → **MVP** (P4 overlay can copy it).
3. + US2 (G5–G7) → non-blocking-by-default frozen in both directions.
4. + US3 (T017) → evidence first-touch advisory pinned.
5. + Polish → adopter README, discoverability, reuse + mutation-revert validation.

---

## Notes

- This feature touches **no `src/` production code** and adds no `.fsi` / surface-area
  baseline (Tier 2, research D7). If an assertion seems to need a new public surface, stop
  — the guard must exercise the **existing** public functions an adopter/CLI uses
  (`loadAndValidate`, `buildRegistry`, `route`, `select`, `deriveEffectiveSeverity`).
- Never mark a task `[X]` on a failing assertion; never weaken an assertion to green it —
  narrow scope and document instead.
- Commit one concern per task/logical group; keep the artifact YAML copyable-unedited at
  every commit.
