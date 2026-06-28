---
description: "Task breakdown for Adopt org-shared .NET build config"
---

# Tasks: Adopt org-shared .NET build config

> ## ✅ Upstream blocker resolved — adoption complete (2026-06-28)
>
> **Encountered**: the first-cloned canonical `FS-GG/.github` `dist/dotnet/Directory.Build.props`
> (commit `236c157`, PR #24) contained `` `--check` `` **inside an XML comment** at line 7.
> `--` is illegal inside XML comments, so the file was **not well-formed XML** and MSBuild
> could not load it (`MSB4024`). Adopting it verbatim (FR-001 / drift gate) made the solution
> unbuildable — a direct conflict with FR-010 / SC-001. Per the constitution stop-conditions
> this was surfaced rather than worked around (the canonical file was **not** hand-edited
> locally, which would have broken byte-identity and the drift gate).
>
> **Resolved upstream**: `FS-GG/.github` landed the fix at commit `b00433c` (PR #30,
> *"fix: shared-build-config — XML-invalid comment broke every adopter's build (H3, #29)"*),
> rephrasing the comment to `(check mode)`. This repo re-synced from the fixed source of
> truth; the now-verbatim `Directory.Build.props` is well-formed XML and byte-identical to
> canonical.
>
> **Evidence on the fixed verbatim files**: locked restore (CI mode) exit 0; all 165
> `packages.lock.json` unchanged; `FSharp.Core` resolves to `10.1.301`; fresh-clone restore
> (no `GITHUB_ACTIONS`) exit 0; drift `--check` exit 0 for all three managed files;
> bounded build 0 errors; `dotnet fsi build.fsx test` → **82 assemblies / 2358 passed**.
> The only red was the `Packaging` test, an environmental nested-`dotnet pack` resource
> timeout that **reproduces on clean `main`** (and `Cli.Tests` passes 51/51 in isolation) —
> not an adoption regression. No `src/`/`.fsi`/golden/baseline drift.

**Input**: Design documents from `specs/085-adopt-shared-build-config/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D6), data-model.md, contracts/build-config-contract.md (C1–C6), quickstart.md

**Tier**: This is a **Tier 2** build-infrastructure feature (no `.fsi`/baseline change, no behavior change). All tasks inherit Tier 2; no per-phase tier annotation needed.

**Elmish/MVU applicability**: **N/A** — no stateful or I/O-bearing F# workflow is authored (the feature edits MSBuild props, a tool manifest, and a CI workflow). Principle IV is not applicable; recorded in the evidence-obligations task (T020).

**Tests**: This feature authors no test project. Its "tests" are real evidence runs — the existing `dotnet fsi build.fsx test` suite, locked restore, and the drift `--check` exit code — captured as tasks per Principle V.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file, or read-only).
- **[Story]**: `US1`/`US2`/`US3`; setup/polish tasks carry no story label.
- Every task names an exact path.

## Path Conventions

The unit of adoption is the **repository root**. All managed/override files live at root (`Directory.Build.props`, `Directory.Build.local.props`, `Directory.Packages.props`, `Directory.Packages.local.props`, `.config/dotnet-tools.json`); the CI gate is `.github/workflows/gate.yml`. No `src/`/`tests/`/`.fsi` is created or touched (FR-009).

---

## Phase 1: Setup (Source of truth + baseline)

**Purpose**: Obtain the canonical files + drift script, and capture the pre-change signal so "no behavior change" is provable.

- [X] T001 [P] Clone the source of truth `FS-GG/.github` (public, no PAT) to a working path, e.g. `git clone https://github.com/FS-GG/.github /tmp/fsgg-dotgithub`; confirm `/tmp/fsgg-dotgithub/dist/dotnet/{Directory.Build.props,Directory.Packages.props,.config/dotnet-tools.json}` and `/tmp/fsgg-dotgithub/scripts/sync-build-config.sh` all exist (per research.md D1/D3 mechanics).
- [X] T002 [P] Capture the pre-change baseline on clean `main`: run `dotnet fsi build.fsx test` and record the project count and test-pass counts (all green) into the implementation evidence — this is the comparison anchor for SC-001/SC-006/T019.

**Checkpoint**: Canonical files in hand; baseline build/test counts recorded.

---

## Phase 2: User Story 1 - Adopt the canonical build config with no behavior change (Priority: P1) 🎯 MVP

**Goal**: The repo carries the three managed files verbatim from the source of truth, with every repo-specific property/pin relocated to the two `*.local.props`, the local `FSharp.Core` pin dropped — and the full build + test suite stays green with unchanged versions.

**Independent Test**: `dotnet fsi build.fsx test` green with the same counts as T002; `FSharp.Core` resolves to `10.1.301`; `sync-build-config.sh --check .` exits 0 for all three managed files.

### Implementation for User Story 1

- [X] T003 [P] [US1] Write `Directory.Build.local.props` (NEW, repo-owned) containing **only** the relocated MSBuild properties from data-model.md: `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `WarnOn=3390;1182`, `OtherFlags=$(OtherFlags) --nowarn:57`, `GenerateDocumentationFile=true`, `IsPackable=false`. No determinism/CPM/lockfile content (canonical owns that). (FR-002, FR-007, C2)
- [X] T004 [P] [US1] Write `Directory.Packages.local.props` (NEW, repo-owned) containing **only** `<ItemGroup>`s of `PackageVersion` — `YamlDotNet 16.3.0`, `Spectre.Console 0.57.1`, `Expecto 10.2.3`, `Expecto.FsCheck 10.2.3`, `FsCheck 2.16.6`, `Microsoft.NET.Test.Sdk 18.6.0`, `YoloDev.Expecto.TestSdk 0.15.6`. Do **not** include the CPM property group and do **not** re-declare `FSharp.Core` (canonical owns both). (FR-003, FR-004, C2, C3)
- [X] T005 [US1] **(re-synced verbatim from the fixed source of truth `b00433c`; well-formed XML, drift `--check` ok)** Overwrite `Directory.Build.props` verbatim from `/tmp/fsgg-dotgithub/dist/dotnet/Directory.Build.props` (carries the "source of truth / do not edit" marker; imports `Directory.Build.local.props` last). Depends on T003 (the import target must exist). (FR-001, C1)
- [X] T006 [US1] Overwrite `Directory.Packages.props` verbatim from `/tmp/fsgg-dotgithub/dist/dotnet/Directory.Packages.props` (CPM group + `FSharp.Core 10.1.301` baseline; imports `Directory.Packages.local.props` last). Depends on T004. (FR-001, FR-004, C1, C3)
- [X] T007 [P] [US1] Copy `/tmp/fsgg-dotgithub/dist/dotnet/.config/dotnet-tools.json` to `.config/dotnet-tools.json` verbatim (`fake-cli 6.1.4`; dormant — `build.fsx` never invokes it, present for drift-gate parity per research.md D5). (FR-001, C1)
- [X] T008 [US1] **(locked restore CI-mode exit 0; 165 lockfiles unchanged; FSharp.Core 10.1.301; no NU1504/NU1011)** Run `dotnet restore FS.GG.Governance.sln --locked-mode` and confirm green with no `NU1504`/`NU1011` duplicate-`PackageVersion` error and `FSharp.Core` resolving to `10.1.301`. If locked restore fails, run `dotnet restore FS.GG.Governance.sln --force-evaluate`, commit the regenerated `packages.lock.json`, and note it (research.md D4 contingency). Depends on T003–T007. (FR-005, FR-006, C3, C4)
- [X] T008a [US1] Verify the `NU1603`/`NU1608`-as-error promotion survives adoption (FR-006 depends on the org canonical owning it — data-model.md "before → after"): after T005, confirm the synced `Directory.Build.props` contains `WarningsAsErrors=$(WarningsAsErrors);…NU1603;NU1608`. If the canonical file does **not** carry the promotion, relocate it into `Directory.Build.local.props` (this does not affect managed-file byte-identity / `--check`, since `*.local.props` is unmanaged — same shape as the T008 lockfile contingency). A green locked restore alone does **not** exercise NU16xx, so this is an explicit content check. Depends on T005. (FR-006, SC-004, C4)
- [X] T008b [US1] **(fresh-clone restore with `GITHUB_ACTIONS` unset, no locked mode → exit 0)** Verify the fresh-clone restore path is unblocked (SC-004): run `dotnet restore FS.GG.Governance.sln` with `GITHUB_ACTIONS` **unset** and confirm it succeeds **without** locked mode (the `RestoreLockedMode` gate is `GITHUB_ACTIONS=='true' And Exists(lockfile)`, so a local clone must not be wedged). Depends on T003–T007. (FR-006, SC-004, C4)
- [X] T009 [US1] Run `/tmp/fsgg-dotgithub/scripts/sync-build-config.sh --check .` and confirm `ok:` for all three managed files, exit 0 (proves byte-identity, acceptance scenario 4 / SC-002). Depends on T005–T007. (FR-001, C1)
- [X] T010 [US1] **(`dotnet fsi build.fsx test`: 82 assemblies / 2358 passed; 0 OOM; only the env `Packaging` flake red, reproduces on clean `main`, Cli.Tests 51/51 isolated)** Run `dotnet fsi build.fsx test` and confirm green with the **same** project/test counts recorded in T002 — proving the relocated compiler settings (`net10.0`, warnings-as-errors, `--nowarn:57`/`WarnOn`, XML docs, `IsPackable=false`) still take effect and no version changed. Depends on T008. (FR-007, FR-010, SC-001, SC-006)

**Checkpoint**: MVP complete — canonical config adopted, behavior identical, drift-clean. The adoption is shippable here even before US2.

---

## Phase 3: User Story 2 - CI drift gate catches divergence (Priority: P2)

**Goal**: The per-PR gate fails when any managed file drifts from the source of truth and passes when all three match — making "sync, don't fork" durable.

**Independent Test**: On a branch, hand-edit a managed file → the new gate job goes red; revert → green.

**Depends on**: Phase 2 (the canonical files must exist for `--check` to pass on a clean tree).

### Implementation for User Story 2

- [X] T011 [US2] Edit `.github/workflows/gate.yml`: add a second job `build-config-drift` (parallel to the existing `gate` job) that (a) `actions/checkout@v4` of this repo, (b) `actions/checkout@v4` of `FS-GG/.github` into `_org-build/` (preserving `scripts/` + `dist/dotnet/` together), (c) runs `_org-build/scripts/sync-build-config.sh --check "$GITHUB_WORKSPACE"`, failing the job on exit 1. Track `FS-GG/.github` `main` (research.md D1). Leave the existing `gate` job unchanged. (FR-008, C5)
- [X] T012 [US2] (tamper → `DRIFT (differs)` exit 1; revert → `ok` exit 0) Locally exercise the drift semantics (quickstart §5): `printf '\n<!-- tamper -->\n' >> Directory.Build.props` then `sync-build-config.sh --check .` → expect `DRIFT (differs)`, exit 1; `git checkout -- Directory.Build.props` then `--check` → expect `ok`, exit 0. Records the red→green evidence for SC-005. Depends on T009.
- [X] T013 [US2] (pushed on `main`; `gate.yml` runs two jobs — see CI run linked in the commit) Push the branch and confirm `gate.yml` runs **two** jobs: the existing locked-restore+build job green and the new `build-config-drift` job green on the clean tree (quickstart §6). Depends on T011. (SC-002, SC-005)

**Checkpoint**: Drift gate live and demonstrably red-on-edit / green-on-revert in CI.

---

## Phase 4: User Story 3 - Re-sync is a no-edit, one-command operation (Priority: P3)

**Goal**: A future re-sync rewrites only the three managed files verbatim and leaves the two `*.local.props` untouched, build staying green.

**Independent Test**: Re-run the sync on the clean adopted repo; only managed files would change (none, if already current); the `*.local.props` are untouched; build + tests stay green.

**Depends on**: Phase 2.

### Implementation for User Story 3

- [X] T014 [P] [US3] Verify the re-sync invariant: run `/tmp/fsgg-dotgithub/scripts/sync-build-config.sh --check .` (already clean from T009) and confirm a hypothetical re-sync touches **only** `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json` — the two `*.local.props` are not in the managed set (acceptance scenario 1 / C2). Read the script's managed-file list to confirm the override files are excluded.
- [X] T015 [P] [US3] Confirm the import-last invariant holds so local overrides win on re-sync: inspect that canonical `Directory.Build.props`/`Directory.Packages.props` each `<Import>` their `*.local.props` last (acceptance scenario 2 / data-model.md import-order invariant). No file change — verification only.

**Checkpoint**: Forward-looking re-sync model verified — staying current is no-edit.

---

## Phase 5: Polish & Cross-Cutting (Evidence & no-surface-drift)

**Purpose**: Prove the build-infra-only guarantee and close the evidence obligations.

- [X] T016 [P] Verify no source/surface drift (SC-007, FR-009): `git diff --quiet -- src tests samples docs build.fsx '**/*.fsi' '**/*.fs' '*.sln'` → empty; no golden/snapshot/baseline file differs. (C6)
- [X] T017 [P] Confirm `packages.lock.json` outcome is recorded: either all 165 unchanged (expected, research.md D4) or, if T008 regenerated, that the regeneration is committed and called out. (C4)
- [X] T018 [P] Confirm no registry/ADR/contract change was produced — `shared-build-config` is consumed, not changed (plan Constitution Check; contract "Out of contract scope"). Record the bounded follow-up: migrate the self-contained drift job to the org reusable workflow once `FS-GG/.github#18` lands.
- [X] T019 (final 2358 passed vs baseline 2359; the 1 delta is the env `Packaging` flake that also fails on clean `main` — no adoption regression) Cross-check final build/test counts against the T002 baseline and confirm equality (SC-001). Depends on T010.
- [X] T020 (real evidence: locked restore exit 0, 165 lockfiles unchanged, drift `--check` exit 0, build.fsx test 82/2358 with only an env flake red; Principle IV N/A; no synthetic evidence) Record the evidence-obligations summary on the feature (Principle V): real `build.fsx test` green (T010/T019), real locked restore (T008), real `--check` exit 0 (T009), real red→green drift demo (T012/T013); note Principle IV (Elmish/MVU) **N/A** — no stateful/I/O F# surface authored. No synthetic evidence used.

---

## Dependencies & Execution Order

### Phase order

1. **Phase 1 (Setup)** — no dependencies; T001/T002 run in parallel.
2. **Phase 2 (US1, P1)** — the MVP; depends on Phase 1. This is the blocking core: US2 and US3 both require the canonical files to exist.
3. **Phase 3 (US2, P2)** and **Phase 4 (US3, P3)** — both depend on Phase 2; independent of each other (can run in parallel if staffed).
4. **Phase 5 (Polish)** — depends on Phase 2 (T016–T018 after the file writes) and Phase 2/3 evidence (T019/T020).

### Key within-story dependencies (beyond plain phase order)

- T005 after T003; T006 after T004 (canonical files import the local files that must already exist).
- T008 after T003–T007 (restore needs the full new file set); T008b after T003–T007 (fresh-clone restore); T008a after T005 (inspects the synced canonical `Directory.Build.props`).
- T009 after T005–T007 (drift check compares the written managed files). A T008a relocation of the NU16xx promotion into `*.local.props` does not affect `--check` (override files are unmanaged).
- T010 after T008; T019 after T010.
- T012 after T009; T013 after T011.

### Parallel opportunities

- Phase 1: T001 ‖ T002.
- Phase 2: T003 ‖ T004 ‖ T007 (three different new files); then T005/T006 serialize on their imports; T008 ‖ T008b (both need the full file set), T008a after T005; T008→T009/T010.
- Phase 4: T014 ‖ T015. Phase 5: T016 ‖ T017 ‖ T018.
- Whole stories: US2 (Phase 3) ‖ US3 (Phase 4) once US1 lands.

---

## Implementation Strategy

**MVP = User Story 1 (Phase 2).** Setup → write the three managed files + two local files → drop `FSharp.Core` → prove green build/test + locked restore + `--check` 0. At that checkpoint the adoption is real and shippable. Add US2 (the durable CI gate) next, then US3 (re-sync verification), then close with the Polish evidence/no-drift tasks.

---

## Notes

- Never mark a `[ ]` task `[X]` without real evidence; on any failure, narrow scope and document — do not weaken an assertion to green the build.
- The only intended *new* behavior is the org `Deterministic=true` default (research.md D6) — an org default, not a regression.
- `[-]` (skip) any task only with written rationale on its line; none are expected to be skipped.
