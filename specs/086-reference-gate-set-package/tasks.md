---
description: "Task list for: Publish the Reference Gate Set as a Content Package"
---

# Tasks: Publish the Reference Gate Set as a Content Package

**Input**: Design documents from `/specs/086-reference-gate-set-package/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/reference-gate-set-package.contract.md ✅, quickstart.md ✅

**Tests**: Included and mandatory — this feature's value is *provable byte-identity / gated production*, so the guard test is real evidence (Principle V), not optional. All test tasks use real artifacts (actual packed `.nupkg`, actual on-disk YAML, actual script-emitted version) — no synthetic fixtures.

**Change classification**: **Tier 1** (new published package contract / cross-repo surface). Carries the registry + compatibility + ADR obligations (FR-008/SC-006), satisfied vacuously for `.fsi`/surface-baseline (the package ships no assembly — the contract surface is the package id + content layout + version rule). See Phase 6.

**Elmish/MVU applicability (Principle IV)**: **N/A.** No multi-step stateful/I/O F# workflow is authored. The pack script is a linear `read → derive → gate → pack` build step with `Process.Start` I/O only at the script edge, mirroring `build.fsx`. Recorded in T015.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in this phase)
- **[Story]**: US1 / US2 / US3 the task serves (omitted for shared/cross-cutting tasks)
- Tier annotation omitted — every phase matches the spec's Tier-1 classification

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Structural artifacts (packaging project, solution entry, script skeleton) that US1/US2/US3 all build on. No second copy of the four YAML files is created (FR-002).

- [X] T001 [P] Create the content-only packaging project `packaging/FS.GG.Governance.ReferenceGateSet/FS.GG.Governance.ReferenceGateSet.fsproj`: `IsPackable=true`, `PackageId=FS.GG.Governance.ReferenceGateSet`, `IncludeBuildOutput=false` (no `lib/`, FR-007), `SuppressDependenciesWhenPacking=true` (empty dependency group, FR-007), **no `Compile` items / no `PackageReference`**, and the content items `<None Include="../../samples/sdd-reference-gate-set/.fsgg/*.yml" Pack="true" PackagePath="contentFiles/any/any/.fsgg/" />` referencing the four files **in place** (single source — FR-002/FR-003).
- [X] T002 Add the packaging project to `FS.GG.Governance.sln` (after T001). Confirm it does **not** add any `PackageReference` to the locked-restore graph (085 must stay green).
- [X] T003 [P] Scaffold `pack-reference-gate-set.fsx` at repo root, mirroring the `build.fsx` idiom: arg parsing, a `--print-version` dry-run hook and a `--source <dir>` override, `Process.Start` confined to the script edge, and loud non-zero exit-code discipline (Principle VI). **`--source` convention**: `<dir>` is the directory that *contains* the `.fsgg/` folder; the script reads `<source>/.fsgg/*.yml` (default `<source>` = `samples/sdd-reference-gate-set`). This matches quickstart step 5, which passes `--source "$TMP"` where the files live at `$TMP/.fsgg/`. No derivation/pack logic yet — structure only.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The version-derivation spine. Both producing the package (US1, needs *a* version to pack) and the schema-versioning contract (US3) sit on this. **No US work can begin until this is done.**

⚠️ **CRITICAL**: Blocks US1, US2, US3.

- [X] T004 Implement the deterministic version-derivation in `pack-reference-gate-set.fsx` (after T003): read the `schemaVersion:` line from each of `governance.yml`, `capabilities.yml`, `policy.yml`, `tooling.yml` under `<source>/.fsgg/` (per T003's `--source` convention; `System.IO` + `System.Text.RegularExpressions`, BCL-only — no YamlDotNet); compose `Version = "{gov}.{caps}.{policy}.{tooling}"` (fixed order; current = `1.2.1.1`); **fail loud and closed** (non-zero, actionable message distinguishing malformed input from a tool defect, Principle VI) if any `schemaVersion:` line is missing/unparseable. Wire `--print-version` to emit the derived string and exit **without packing**.

**Checkpoint**: `dotnet fsi pack-reference-gate-set.fsx --print-version` prints `1.2.1.1`. Foundation ready — user stories can proceed.

---

## Phase 3: User Story 1 - Templates consumes one published source of truth (Priority: P1) 🎯 MVP

**Goal**: Produce the `FS.GG.Governance.ReferenceGateSet` content package so Templates#14 has one published, versioned source of truth to `git diff --exit-code` against — exactly the four config files at a documented, version-stable location, content-only.

**Independent Test**: Install the produced package into a clean consumer, materialize its `.fsgg` files, and run a drift comparison — an unmodified overlay passes (exit 0), a tampered one fails. Delivers value even before US2/US3.

### Tests for User Story 1 (write FIRST, ensure they FAIL before T006/T007) ⚠️

- [X] T005 [P] [US1] In `tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetPackageTests.fs` (new file in the **existing** test project; register it in `FS.GG.Governance.ReferenceGateSet.Tests.fsproj` before `Main.fs`): a **self-contained** guard that **produces** the `.nupkg` in its own setup (invoke `pack-reference-gate-set.fsx` once per run — real artifact, never assume a pre-staged `.nupkg`), then assert it contains **exactly** `contentFiles/any/any/.fsgg/{governance,capabilities,policy,tooling}.yml`, each **byte-identical** to the on-disk `samples/sdd-reference-gate-set/.fsgg/*.yml` (SC-002), and that the archive has **no `lib/` entry and an empty dependency group** (SC-005). Unzip the **real** produced `.nupkg` — no synthetic fixture. (The pack script's own pre-pack gate runs **only** G1–G7 — see T009 — so this fixture does not recurse into the package tests.)

### Implementation for User Story 1

- [X] T006 [US1] Implement the pack invocation in `pack-reference-gate-set.fsx` (after T001, T004): `dotnet pack packaging/FS.GG.Governance.ReferenceGateSet/...` at the derived version, output to `~/.local/share/nuget-local/` (constitution-mandated). Default run (no `--print-version`) produces `FS.GG.Governance.ReferenceGateSet.1.2.1.1.nupkg`.
- [X] T007 [US1] Validate quickstart steps 2/3/6 against the **real** artifact: produce the package, unzip and `diff` each file vs source (byte-identity), and `dotnet add package` + `restore` from a throwaway consumer to confirm the four files materialize at `<nuget-global-packages>/fs.gg.governance.referencegateset/1.2.1.1/contentFiles/any/any/.fsgg/` with **no** governance assembly reference (content-only). Record evidence on the task line.

**Checkpoint**: MVP — a consumable, content-only, byte-identical package exists at the documented location. Templates#14 has its source of truth (SC-001). **STOP and VALIDATE** here.

---

## Phase 4: User Story 2 - The shipped artifact is provably the validated set (Priority: P2)

**Goal**: Gate production on the existing G1–G7 reference-set guard so the shipped artifact is provably the tested artifact — same single-sourced files, no second copy, pack aborts when invariants are red.

**Independent Test**: Confirm the package draws from `samples/sdd-reference-gate-set/.fsgg/` (the directory G1–G7 loads), not a duplicate; mutate a sample and confirm both the tests and the pack guard react.

### Tests for User Story 2 (write FIRST) ⚠️

- [X] T008 [P] [US2] Extend `ReferenceGateSetPackageTests.fs`: assert that when an invariant is broken on a temp-dir copy passed via `--source`, the pack script exits **non-zero and writes no `.nupkg`** (FR-004/SC-004), and assert the `.fsproj` content items resolve to `samples/sdd-reference-gate-set/.fsgg/` (single source — no duplicated copy, FR-002). Real I/O on a mutated copy — no mock.

### Implementation for User Story 2

- [X] T009 [US2] Add the G1–G7 gate to `pack-reference-gate-set.fsx` (after T006): **before** packing, run **only** the G1–G7 reference-set guard (`ReferenceGateSetGuardTests` — filter by fully-qualified test name / trait via `dotnet test --filter`; do **NOT** run the whole `build.fsx test` suite, which now also contains the package tests T005/T008/T011 and would deadlock, since those produce the not-yet-packed `.nupkg`). On red, abort with a non-zero exit and an actionable message **before any `.nupkg` is written** (FR-004). Do not duplicate the G1–G7 assertions.
- [X] T010 [US2] Validate quickstart step 2's gate-fires path: break an invariant (e.g. blank a check in `capabilities.yml`), re-run the pack script, confirm it fails and writes no `.nupkg`, then restore the file. Record evidence.

**Checkpoint**: US1 + US2 — the artifact consumers receive is the artifact the G1–G7 tests validate (SC-004), produced only when invariants hold.

---

## Phase 5: User Story 3 - Schema-versioned package so consumers can pin coherently (Priority: P3)

**Goal**: Make the version-derivation rule legible and distinguishable so a schema bump is visible as a new pinnable package version, and record the numbering as a contract (ADR).

**Independent Test**: Inspect the produced package's version (`1.2.1.1`); simulate a `schemaVersion` bump and confirm the derived version changes to a distinguishable value.

### Tests for User Story 3 (write FIRST) ⚠️

- [X] T011 [P] [US3] Extend `ReferenceGateSetPackageTests.fs` (after T004): assert `--print-version` emits exactly `1.2.1.1` (deterministic, no clock/env input), and that a simulated single-segment bump on a temp-dir copy (e.g. `policy.yml` `schemaVersion: 1 → 2` via `--source`) yields a **distinguishable** version `1.2.2.1` (SC-003). Assert against the script's **actual emitted** version via `--print-version`, not a re-scraped copy of the rule.

### Implementation for User Story 3

- [X] T012 [US3] Author the version-derivation-rule ADR in `FS-GG/.github` (`docs/decisions/ADR-00NN-reference-gate-set-version-rule.md`) via the **cross-repo-coordination** skill: document `Version = "{gov}.{caps}.{policy}.{tooling}"`, the determinism + distinguishability guarantees, and the exact-pin recommendation (`[1.2.1.1]`). The numbering rule is itself a contract (FR-006). Bundle with the registry PR in T014.

**Checkpoint**: All three stories independently functional — published, gated, and schema-versioned.

---

## Phase 6: Cross-Cutting — Contract Registration, CI & Evidence (Tier-1 obligations)

**Purpose**: FR-008/SC-006 (registry + compatibility, spans the whole package contract), CI enforcement, and the Tier-1 evidence record. Not specific to one story.

- [X] T013 Add a `reference-gate-set-pack` job to `.github/workflows/gate.yml`: checkout → setup-dotnet `10.0.x` → run the `ReferenceGateSetPackageTests` guard (which self-produces the `.nupkg` via the pack script — T005 — and asserts byte-identity / content-only / version on the **CI-produced** artifact). A standalone `dotnet fsi pack-reference-gate-set.fsx` step is optional (the test already exercises it); if included, run it before the test. Keep the existing `gate` and `build-config-drift` jobs untouched.
- [X] T014 Register the package as a versioned cross-repo contract via the **cross-repo-coordination** skill: PR to `FS-GG/.github` adding `FS.GG.Governance.ReferenceGateSet` to `registry/dependencies.yml` (producer `FS.GG.Governance`, consumer `FS.GG.Templates` / Templates#14, kind = NuGet content package), regenerate `docs/registry/compatibility.md`, note the deferred org-feed status (`.github#21`), and link the PR (ADR-0001). Include the T012 ADR in the same PR.
- [X] T015 Record the Tier-1 / evidence obligations on `specs/086-reference-gate-set-package/plan.md` (or a short notes file): Principle IV (Elmish/MVU) **N/A** with rationale; `.fsi`/surface-baseline obligations vacuously satisfied (no assembly, contract surface = package id + layout + version rule); all guard evidence is real (no synthetic fixtures, Principle V).
- [X] T016 [P] Re-run the G1–G7 invariants against the **installed** copy materialized in T007 (SC-004) — confirm the shipped artifact still loads `Valid` through the real `Config → Gates → Routing → Route → Enforcement` pipeline. Record evidence.
- [X] T017 [P] Full quickstart pass (steps 1–7) end-to-end as the feature's done-check; confirm step 4's automated guard is green locally and in the new CI job.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately. T002 after T001.
- **Foundational (Phase 2)**: T004 after T003 — **blocks all user stories**.
- **US1 (Phase 3)**: after Foundational. MVP.
- **US2 (Phase 4)**: after Foundational; T009 builds on the T006 pack invocation (US1). Independently testable.
- **US3 (Phase 5)**: after Foundational; only needs T004 (version-derivation). Independent of US1/US2 implementation.
- **Cross-cutting (Phase 6)**: after the stories whose behavior it enforces — T013/T016/T017 after US1+US2; T014 after T012.

### Key cross-task dependencies

- T004 → T006, T011 (version-derivation spine)
- T001 → T002, T006 (the packaging project)
- T006 → T007, T009, T010 (a pack invocation must exist)
- T012 → T014 (ADR ships in the registry PR)
- T007 → T016 (installed copy to re-validate)

### Parallel Opportunities

- T001 ∥ T003 (different files).
- US3 (T011, then T012) can proceed in parallel with US2 once Foundational is done — they share no files (US3 touches the version-derivation tests + ADR; US2 touches the gate + its tests). Both extend `ReferenceGateSetPackageTests.fs`, so **serialize edits to that one file** (T005 → T008 → T011) even though the stories are otherwise independent.
- T016 ∥ T017 within Phase 6.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup (T001–T003).
2. Phase 2: Foundational version-derivation (T004) — `--print-version` → `1.2.1.1`.
3. Phase 3: US1 (T005–T007) — produce the content-only, byte-identical package; validate the consumer materialization path.
4. **STOP and VALIDATE**: Templates#14 now has an authoritative source of truth (SC-001). Demo-able.

### Incremental Delivery

- US1 → MVP (published source of truth).
- + US2 → gated production (shipped == validated, SC-004).
- + US3 → schema-versioned & contract-recorded (SC-003).
- + Phase 6 → CI enforcement + registry contract (SC-006) + full quickstart.

### Note on file contention

`ReferenceGateSetPackageTests.fs` and `pack-reference-gate-set.fsx` are each touched by multiple stories. They are small, single-source files by design (no duplicated rule) — keep edits to each serialized in task order rather than parallelizing within the same file.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in this phase.
- Tests use **real** artifacts only (Principle V): actual `.nupkg`, actual on-disk YAML, actual `--print-version` output. The bump test (T011) mutates a temp-dir **copy** — real I/O, no mock.
- Never mark a task `[X]` without real evidence; never weaken an assertion to green a build — narrow scope and document.
- No `src/`, no `.fsi`, no surface baseline, no existing sample file is modified (plan §Project Structure).
- The org-feed push (`.github#21`) is **out of scope** — local/CI `dotnet pack` to `~/.local/share/nuget-local/` is the done-definition (spec Distribution-scope assumption).

## Implementation evidence (all real, Principle V — no synthetic fixtures)

- **T001–T002**: `packaging/FS.GG.Governance.ReferenceGateSet/…fsproj` packs the four samples in place; the produced `.nupkg` contains exactly `contentFiles/any/any/.fsgg/*.yml`, **no `lib/`**, **no `<dependencies>`** (verified by `unzip -l` + nuspec inspection). Project added to the `.sln`; `dotnet restore FS.GG.Governance.sln --locked-mode` stays green (empty `packages.lock.json`, no new `PackageReference`).
- **T003–T004**: `pack-reference-gate-set.fsx --print-version` → `1.2.1.1`; a `--source` policy bump → `1.2.2.1`; a blanked `schemaVersion` → non-zero with an actionable message (malformed input vs tool defect, Principle VI).
- **T005/T008/T011**: `dotnet test …ReferenceGateSet.Tests` = **15/15 green** (8 G1–G7 + 7 package guards) — byte-identity vs on-disk samples, no `lib/`, empty dep group, gate-fires (broken `--source` ⇒ non-zero + no `.nupkg`), single-source assertion, `--print-version` = `1.2.1.1`, bump ⇒ `1.2.2.1`. All over the **real** packed `.nupkg` / real script output.
- **T006/T009**: full `dotnet fsi pack-reference-gate-set.fsx` runs the G1–G7 gate then packs to `~/.local/share/nuget-local/`. **T010**: flipping canonical `defaultProfile: light→strict` made G5 fail ⇒ pack refused (exit 1, **no `.nupkg`**), then `git checkout` restored the file.
- **T007/T016**: a throwaway consumer (`dotnet add package` + `restore`) materialized exactly the four files at `…/fs.gg.governance.referencegateset/1.2.1.1/contentFiles/any/any/.fsgg/` with **no DLL** (content-only); re-running G1–G7 against that **installed** copy (`FSGG_REFERENCE_GATE_SET_DIR`) = **8/8 green** (SC-004).
- **T012/T014**: cross-repo PR **[FS-GG/.github#35](https://github.com/FS-GG/.github/pull/35)** — ADR-0007 (version rule, Accepted), `governance-reference-gate-set@1.2.1.1` registered in `registry/dependencies.yml` + `compatibility.md` projection + new coherence id `reference-gate-set-published` (feed deferred `.github#21`); `validate-registry.py` green incl. `--expect`.
- **T013**: `reference-gate-set-pack` job added to `.github/workflows/gate.yml`. **T015**: `evidence-notes.md`. **T017**: quickstart steps 1–7 pass locally.
- **Two small design notes** (see `evidence-notes.md`): `--source` flows through the G1–G7 gate via `FSGG_REFERENCE_GATE_SET_DIR` (079 guard extended, default = canonical, behavior-preserving); test hooks `--output <dir>` + `FSGG_PACK_GATE_NO_BUILD` keep the automated guard hermetic and avoid a nested rebuild of the running test assembly.
