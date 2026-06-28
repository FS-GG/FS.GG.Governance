---
description: "Task list for 087 — Re-type Config loader/schema onto FS.GG.Contracts"
---

# Tasks: Re-type Config loader/schema onto FS.GG.Contracts

**Input**: Design documents from `/specs/087-retype-config-onto-contracts/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅,
contracts/fsgg-contracts-consumption.md ✅, quickstart.md ✅

**Tests**: This feature's gate is the **existing** test suite staying green (no new
behavior, no new tests authored). The "test" tasks below are *evidence-capture* tasks
that run the existing suites and assert byte-identical parity — they are mandatory here
because this is a Tier 1 change whose entire promise is "zero observable change."

**Overall tier**: Tier 1 (introduces a new dependency, `FS.GG.Contracts`). Tasks inherit
Tier 1; no per-task tier annotation needed.

**Elmish/MVU applicability**: Not applicable as a *new* boundary. The pure core
(`Schema.validate`) stays pure/total and the I/O edge (`Loader` `FileReader` port) keeps
its signature (FR-007, Constitution IV). No `Model`/`Msg`/`Effect`/`update` is added or
changed; the change is a pure constant-resolution swap inside `Schema.fs`. The
evidence-obligations are parity (determinism + interpreter-unchanged), captured below.

**Implementation status**: the mechanical edits already exist in the working tree (see
plan.md "Implementation status"): the central `PackageVersion`, the `PackageReference`,
`open Fsgg` + the `Schemas.*Version` swap in `Schema.fs`, and the regenerated lockfiles.
The tasks below therefore *verify-and-evidence* that change rather than author it from
scratch. Each `[ ]` becomes `[X]` only when its real evidence is captured.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: the user story (US1/US2/US3) the task serves; omitted for shared phases.

---

## Phase 1: Setup — dependency plumbing (Shared Infrastructure)

**Purpose**: Wire `FS.GG.Contracts@1.0.1` into Config under central package management and
locked restore. (FR-001, FR-010, SC-005; research D4/D5)

- [ ] T001 Confirm central pin exists: `<PackageVersion Include="FS.GG.Contracts" Version="1.0.1" />` in `Directory.Packages.local.props`. (FR-001; already present — verify value is exactly `1.0.1`.)
- [ ] T002 [P] Confirm bare reference (no version, per CPM convention): `<PackageReference Include="FS.GG.Contracts" />` in `src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj`. (FR-001)
- [ ] T003 Restore the solution in locked mode and confirm `FS.GG.Contracts` resolves at `1.0.1` from the org GitHub Packages feed: `dotnet restore FS.GG.Governance.sln`. A restore failure here is the correct loud signal (research D6) — do not add any fallback. (FR-001, SC-005)
- [ ] T004 Confirm the regenerated lockfiles carry the new edge: `src/FS.GG.Governance.Config/packages.lock.json` lists `FS.GG.Contracts` (Direct, resolved `1.0.1`) and `FSharp.Core` (CentralTransitive), and every project in Config's dependency closure has a lockfile reflecting the new graph (the broad `packages.lock.json` churn in the working tree). Regenerate with `dotnet restore FS.GG.Governance.sln --force-evaluate` if locked restore in T003 fails on a stale lockfile. (FR-010, SC-005; research D5)

**Checkpoint**: `FS.GG.Contracts 1.0.1` restores in locked mode; lockfiles are coherent.

---

## Phase 2: Foundational — the constant swap (Blocking Prerequisite)

**Purpose**: Replace the four hard-coded supported-version literals with the package
constants. This is the single production-code change and it underpins **every** user
story, so it precedes US1/US2/US3 verification. (FR-002, FR-003/FR-004 boundary; research
D1/D2, data-model §A/§B)

**⚠️ CRITICAL**: No user-story verification is meaningful until this is in place.

- [ ] T005 Confirm `open Fsgg` is present and `Schema.supportedVersionFor` maps each `FsggFile` case to its package constant in `src/FS.GG.Governance.Config/Schema.fs`: `Project -> SchemaVersion Schemas.governanceVersion`, `Policy -> SchemaVersion Schemas.policyVersion`, `Capabilities -> SchemaVersion Schemas.capabilitiesVersion`, `Tooling -> SchemaVersion Schemas.toolingVersion`. The return type stays Governance's own `SchemaVersion` newtype (no surface move). (FR-002; research D2, data-model §A)
- [ ] T006 [P] Confirm FR-003/FR-004 boundary held: **no** Contracts `*Schema` record is adopted and **no** Governance type moved. `Model.fsi`/`Model.fs` (facts, identity newtypes, `Cost`/`Maturity`/`SurfaceClass`/`GeneratedProductTier`/`EnvironmentClass` enums, `Diagnostic`/`DiagnosticId`/`Validation`) and the `Schema.fsi`/`Loader.fsi` edge types are unchanged. `git diff --stat` over `src/FS.GG.Governance.Config/` shows only `Schema.fs` (+ `.fsproj`) edited. (FR-003, FR-004, FR-007; data-model §B/§C)

**Checkpoint**: the four literals are gone; the only edited production source is `Schema.fs`.

---

## Phase 3: User Story 1 — Behavior parity, no observable change (Priority: P1) 🎯 MVP

**Goal**: The full build and the complete existing test suite pass with the same counts as
before; valid fixtures yield identical typed facts, malformed fixtures yield identical
diagnostics, determinism is preserved, downstream goldens are byte-identical.

**Independent Test**: Build + full suite green; `git diff` empty over command/projection
goldens and snapshots.

**Depends on**: Phase 2 (the swap must be in place).

- [ ] T007 [US1] Full build of the solution: `dotnet fsi build.fsx`. Every project compiles, including the 50+ downstream consumers of `Config`. (FR-011, SC-001, SC-007 compile evidence)
- [ ] T008 [US1] Full test suite — the delivery gate: `dotnet fsi build.fsx test`. Same project + test-pass counts as before the change. (SC-001, FR-011)
- [ ] T009 [P] [US1] Capture Config **valid-fixture** parity evidence: the validation tests in `tests/FS.GG.Governance.Config.Tests/SchemaTests.fs` / `LoaderTests.fs` pass — identical typed facts for the valid `.fsgg` fixtures. (SC-002, FR-005, US1 scenario 2)
- [ ] T010 [P] [US1] Capture Config **diagnostic** parity evidence: `tests/FS.GG.Governance.Config.Tests/DiagnosticTests.fs` passes — identical diagnostic id/locator/message for every malformed/edge fixture (unknown field, missing required, malformed value, duplicate id, dangling ref, path-escape, unsupported/missing/malformed version, empty file, missing required file). (SC-003, FR-005, US1 scenario 3)
- [ ] T011 [P] [US1] Capture **determinism** evidence: `tests/FS.GG.Governance.Config.Tests/DeterminismTests.fs` passes — reordered YAML input yields byte-identical output. (FR-006, SC-002, US1 scenario 4)
- [ ] T012 [US1] Capture **downstream golden/snapshot** parity: run the command/projection/gate/ship/verify golden + snapshot suites and confirm `git diff --exit-code` is empty over those fixtures. No golden is regenerated. (FR-008, SC-006)

**Checkpoint**: build + full suite green; every behavior fixture byte-identical. **This is
the MVP** — User Story 1 fully delivers the safe re-typing on its own.

---

## Phase 4: User Story 2 — Versions sourced from the shared package, not literals (Priority: P2)

**Goal**: `capabilities → 2` and `project/policy/tooling → 1` are resolved from
`Fsgg.Schemas.*`, with no Governance-local literal remaining; the unsupported-version
diagnostic (incl. the `capabilities` v1→v2 migration pointer) is unchanged.

**Independent Test**: Inspect the resolved supported version per file and its source; load
`capabilities.yml` with `schemaVersion: 1` → still `UnsupportedSchemaVersion` + migration
pointer.

**Depends on**: Phase 2.

- [ ] T013 [P] [US2] No-local-literal guard (SC-004): `grep -n "Schemas\.\(governance\|policy\|capabilities\|tooling\)Version" src/FS.GG.Governance.Config/Schema.fs` shows all four package reads, and `grep -n "SchemaVersion [12]" src/FS.GG.Governance.Config/Schema.fs` returns **no** matches in `supportedVersionFor`. (SC-004, FR-002, US2 scenarios 1–2; quickstart §5)
- [ ] T014 [US2] Confirm value parity by resolution: the supported version resolves to `2` for `Capabilities` and `1` for `Project`/`Policy`/`Tooling`, each via `Schemas.*Version` (covered by the version-handling assertions in `tests/FS.GG.Governance.Config.Tests/SchemaTests.fs`). (SC-004, contract C2, US2 scenarios 1–2)
- [ ] T015 [US2] Confirm the unsupported-version path is byte-identical: a `capabilities.yml` declaring `schemaVersion: 1` is rejected as `UnsupportedSchemaVersion` with the existing migration-doc pointer (the relevant `DiagnosticTests.fs` case passes unchanged). (FR-005, US2 scenario 3, contract C4)

**Checkpoint**: the four versions are single-sourced from the package; the drift class that
forced the SDD#18 `1.0.1` correction is closed.

---

## Phase 5: User Story 3 — Future bumps flow in by re-pinning the package (Priority: P3)

**Goal**: A future supported-version change reaches Governance by advancing the
`fsgg-contracts` pin + central `PackageVersion` and re-restoring, with no edit to
`Schema.fs`.

**Independent Test**: By inspection — the supported version flows from `Fsgg.Schemas.*`, so
re-pinning `FS.GG.Contracts` and re-restoring changes the resolved value with no Governance
source edit.

**Depends on**: Phase 2; reuses US2's sourcing.

- [ ] T016 [US3] Forward-compat inspection (quickstart §6): confirm the *only* edit point for a future version bump is the `fsgg-contracts` registry pin + the central `PackageVersion` in `Directory.Packages.local.props` — `Schema.fs` reads `Schemas.*Version` and never a literal, so no Config source edit is needed to intake a new value. Record this as the FR-001/US3 evidence. (FR-001, US3 scenario 1, contract "Forward-compatibility note")

**Checkpoint**: coherence with the org schema authority is now a pin-bump, not a code edit.

---

## Phase 6: Surface parity & wrap-up (Cross-Cutting)

**Purpose**: Prove the public Config surface did not move and close out the quickstart.

- [ ] T017 [P] Surface parity (FR-009, SC-007, contract C5): run `dotnet fsi pack-and-apicheck.fsx --json` (or the Config `SurfaceDriftTests` in the suite) and confirm `git diff --exit-code surface/FS.GG.Governance.Config.surface.txt` reports **no change** — byte-identical surface, no re-export, no moved symbol. (research D3, data-model §"Surface impact")
- [ ] T018 Run the full quickstart end-to-end (`specs/087-retype-config-onto-contracts/quickstart.md` steps 1–5) and confirm every "Expected" holds; tick its "Done when" checklist. (overall verification)
- [ ] T019 [P] Update the Coordination board: mark **FS-GG/FS.GG.Governance#14** complete once T007/T008/T012/T017 evidence is captured. (epic FS-GG/.github#16 tracking — out-of-code, informational)

**Checkpoint**: surface byte-identical; quickstart green; board updated.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; restore/lockfile plumbing first.
- **Phase 2 (Foundational swap)** → after Setup restores; **blocks all user stories**.
- **Phase 3 (US1)**, **Phase 4 (US2)**, **Phase 5 (US3)** → each depends only on Phase 2;
  may run in parallel (different evidence, no shared mutable files).
- **Phase 6 (Surface & wrap-up)** → after the user-story evidence exists (T017 can run any
  time after Phase 2; T018/T019 after US1/US2 evidence).

### Within phases

- Phase 1: T001/T002 are inspection (parallel); T003 then T004 are sequential (restore then
  lockfile check).
- Phase 3: T007 (build) precedes T008 (test). T009–T011 are sub-results of T008 and are
  parallel-safe reads of distinct test files. T012 after T008.
- Phases 4 and 5 read-only checks may run alongside Phase 3.

### Parallel opportunities

- T001 ‖ T002 (Setup inspections).
- T009 ‖ T010 ‖ T011 (distinct Config test files) once T008 has run.
- T013 (US2 guard) ‖ T017 (surface) ‖ Phase 3 evidence — independent read-only checks.
- US1, US2, US3 verification can proceed in parallel after Phase 2.

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1: Setup — restore at the pin, lockfiles coherent.
2. Phase 2: Foundational — the constant swap is in place (already in the working tree).
3. Phase 3: US1 — build + full suite + behavior-parity evidence green.
4. **STOP and VALIDATE**: US1 alone delivers the safe re-typing with zero observable change.

### Incremental delivery

1. Setup + Foundational → dependency wired, literals swapped.
2. US1 → behavior parity proven → MVP.
3. US2 → single-source guard + version-resolution parity proven.
4. US3 → forward-compat by pin-bump confirmed by inspection.
5. Phase 6 → surface byte-identical, quickstart closed, board updated.

---

## Notes

- This is a verify-and-evidence task list: the production edit already exists; `[X]` requires
  the *real* command output / passing test / empty diff, never a presumption.
- Never regenerate a golden or weaken an assertion to green the build — a non-empty golden
  diff is a real regression to investigate (FR-008).
- A restore/feed failure is a loud, expected signal (research D6) — fix the feed/pin/lockfile,
  never add a literal fallback.
- **Task counts** — US1: 6 (T007–T012); US2: 3 (T013–T015); US3: 1 (T016). Setup: 4; Foundational: 2; Surface/wrap-up: 3. **Total: 19.**
- **Suggested MVP scope**: User Story 1 (Phases 1–3).
