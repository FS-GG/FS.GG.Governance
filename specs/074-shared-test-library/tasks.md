---
description: "Task list for feature 074 — Shared test-support library"
---

# Tasks: Shared test-support library

**Input**: Design documents from `/specs/074-shared-test-library/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: This is a **behaviour-preserving consolidation**, accepted exactly as Phase A
(feature 073) was. The "tests" are (a) the **existing** per-project Expecto suites, whose
**per-project counts MUST stay identical** and whose **golden/snapshot fixtures MUST stay
byte-identical** — that is the acceptance gate (FR-004, FR-007, SC-001, SC-002, SC-006); and
(b) the **one** additive `FS.GG.Governance.Tests.Common.Tests` project — its
`SurfaceBaselineTests` (surface drift + the no-`src`-reference scope guard, FR-008/FR-009) and
`SmokeTests`. No new behavioural tests are authored for the migrated suites — the move is
proven by **absence of change** (count + golden byte-identity). The only legitimate total
count *increase* is `Tests.Common.Tests`, exactly as Phase A's `2237 → 2259` (research D3).

**Organization**: Tasks are grouped by the three priority slices (US1 → US2 → US3), preceded
by a Setup baseline and a Foundational slice that lands the shared library + its test harness.
Land them in priority order, **one concern / one batch per commit, suite green at every
commit** (research D5, FR-007, SC-006).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (one file
  / one project each).
- **[Story]**: US1 / US2 / US3 (US1 is the MVP).
- Tier annotation omitted on consumer migrations (each is behaviour-preserving / Tier-2 in
  spirit); the new library + harness is the **Tier 1** part of the feature (plan Summary).
- Exact file paths are given in every task.

## The acceptance invariant (re-run at the end of every slice, and after every migration commit)

```bash
dotnet build FS.GG.Governance.sln -m:2 -p:UseSharedCompilation=false   # warnings are errors
dotnet test  FS.GG.Governance.sln --nologo                             # full suite green
# Per-project counts unchanged vs the T001 baseline (only Tests.Common.Tests is additive — C2/A1):
#   compare against /tmp/074-baseline-counts.txt
git status --porcelain -- 'tests/**'   # MUST show ONLY: deleted local helper copies +
                                       # added <ProjectReference> lines. NO golden/snapshot byte changed (C3).
```

A green suite **with unchanged per-project counts** **and** an empty golden/snapshot diff is
the pass condition (contracts/migration-acceptance.md, C1–C6). A changed golden/snapshot means
the moved helper was **not** actually byte-identical → keep that helper **local** (intentional
variant, FR-006/D4); never re-baseline a golden to go green. A drifted per-project count means
a test was lost/duplicated/renamed by the move → reject and investigate (spec Edge Cases).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Capture the authoritative pre-migration signal the byte-identity / count gate
compares against, and the worklists the sweep consumes.

- [X] T001 Confirm a clean working tree and record the baseline `dotnet test
      FS.GG.Governance.sln --nologo` run into `/tmp/074-baseline-counts.txt`, capturing the
      **per-project** test counts (the C2/A1 baseline), the total, **and the authoritative
      test-project count** (`ls tests | wc -l` — the working tree has ~80, not the design
      report's 78; this T001 figure is authoritative). Note the total + project count in this
      file's Notes section so the additive `Tests.Common` + `Tests.Common.Tests` delta (two new
      projects) is distinguishable from drift, and so A1's total-count math uses the **real**
      baseline + 2, not the report's 78 (research D3, SC-001, analysis F4).
- [X] T002 [P] Capture the SC-004 / sweep worklists into this file's Notes (or a scratch file):
      (a) the `findRepoRoot` copies — `grep -rln "findRepoRoot" tests --include='Support.fs'`
      (≈68); (b) the real-`git` `ProcessStartInfo` helper copies (≈7) —
      `grep -rln "ProcessStartInfo" tests --include='Support.fs'`; (c) the **src-core `open`
      union** of the three command suites —
      `grep -hE "^open FS.GG.Governance" tests/FS.GG.Governance.{Verify,Ship,Route}Command.Tests/Support.fs | sort -u`
      — this is the `ProjectReference` set the library's `.fsproj` needs so `FakePorts` /
      `CatalogFixtures` / `SnapshotHelpers` can construct typed values (research D2).

**Checkpoint**: baseline per-project counts recorded; the duplication worklist + the src-core
reference union are pinned.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land `FS.GG.Governance.Tests.Common` + its test harness — the shared infrastructure
**every** slice builds on. No migration begins until the library compiles, its surface baseline
is blessed, and the no-`src`-reference scope guard is green.

**⚠️ CRITICAL**: The library + the surface-baseline/scope-guard mechanism must exist before any
project can reference it (FR-001, FR-009, FR-008).

- [X] T003 Create `tests/FS.GG.Governance.Tests.Common/FS.GG.Governance.Tests.Common.fsproj` —
      a plain F# library (**not** a test project): `<IsPackable>false</IsPackable>`, **no**
      `Expecto`/`Microsoft.NET.Test.Sdk` packages (research D6), **no** new third-party
      `PackageReference` (`Directory.Packages.props` unchanged). Add `<ProjectReference>`s to
      the src-core **union captured in T002** (FR-002, research D2). Place it under `tests/` so
      the test-only intent is structural (FR-008).
- [X] T004 Author `tests/FS.GG.Governance.Tests.Common/TestsCommon.fsi` from
      `contracts/TestsCommon.fsi`, `.fsi`-first (Principle I/II): the five module headers
      `RepositoryHelpers`, `FakePorts`, `CatalogFixtures`, `SnapshotHelpers`, `CaptureHelpers`.
      Fully specify **`RepositoryHelpers`** now (`findRepoRoot: DirectoryInfo|null -> string`,
      `repoRoot: string`); the heavier modules' exact member sets are pinned additively as the
      slices that extract them land (US1 → `FakePorts` git fakes; US2 → `CatalogFixtures` /
      `SnapshotHelpers` / `CaptureHelpers` / exec+sensor fakes). Only genuinely-shared,
      byte-identical helpers appear here (FR-006, research D4).
- [X] T005 Author `tests/FS.GG.Governance.Tests.Common/TestsCommon.fs` — implement
      `RepositoryHelpers.findRepoRoot` as the **`sln||slnx` superset** variant (checks both
      `FS.GG.Governance.sln` and `.slnx`, fails fast with `failwith` if neither found — research
      D4, FR-002) and `repoRoot`. Carry **no** `private`/`internal`/`public` modifiers
      (Principle II). Seed the other four modules with whatever dependency-free members are
      already byte-identical (e.g. `CaptureHelpers`); leave the rest to be extracted in US1/US2.
- [X] T006 Create the harness project
      `tests/FS.GG.Governance.Tests.Common.Tests/FS.GG.Governance.Tests.Common.Tests.fsproj`
      (Expecto, `IsPackable=false`, `GenerateProgramFile=false`, `ProjectReference` to the
      library) with `SurfaceBaselineTests.fs` (reflective surface-drift vs.
      `surface/FS.GG.Governance.Tests.Common.surface.txt`, blessed via `BLESS_SURFACE=1` —
      cloned from `tests/FS.GG.Governance.JsonText.Tests/SurfaceBaselineTests.fs`), `SmokeTests.fs`
      (exercise `RepositoryHelpers`/`CaptureHelpers` directly for real-evidence coverage), and
      `Main.fs`. Add the **scope-guard test** asserting **no `src/*.fsproj` references
      `FS.GG.Governance.Tests.Common`** (FR-008, contract C5).
- [X] T007 Register **both** new projects in `FS.GG.Governance.sln`; bless the surface baseline
      once (`BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.Tests.Common.Tests`); then run
      `dotnet test tests/FS.GG.Governance.Tests.Common.Tests` green (surface drift + scope guard
      + smoke). Confirm the library carries **no** `Expecto`/test-SDK reference.

**Checkpoint**: `FS.GG.Governance.Tests.Common` compiles and is referenceable; its surface is
blessed; the no-`src`-reference scope guard and smoke tests are green. Migration can now begin.

---

## Phase 3: User Story 1 — Single source of truth for cross-cutting helpers (Priority: P1) 🎯 MVP

**Goal**: The repo-root locator (`RepositoryHelpers`) and the real-`git`/git-fake helpers
(`FakePorts`) live **once** in the shared library, and **one** real project consumes them with
its local copies deleted — proving the architecture and the "edit-in-one-place" property
(spec US1 Acceptance Scenarios 1–3).

**Independent Test**: A chosen project references `Tests.Common`, deletes its local
`findRepoRoot` + git-fake copies, compiles, and passes with its **pre-migration count**; the
acceptance invariant is green and no golden moved. Editing
`RepositoryHelpers.findRepoRoot` in the library is then reflected in that project with **no**
edits to it (Acceptance Scenario US1-2).

> Wire/confirm the `ProjectReference` **before** deleting any local copy; the deletion and the
> reference land in the **same** commit so no project compiles with both (spec Edge Case
> "name collisions", contract C4).

- [X] T008 [US1] Extend `TestsCommon.fsi` + `.fs` with the **`FakePorts`** git surface — the
      real-`git` `ProcessStartInfo` helper and the git port fakes byte-identical across the
      ≈7 copies (e.g. `gitWithChanges`, `gitEmpty`, `gitNotRepo`, `gitUnavailable`, `portsGit`)
      — pinning exact signatures from the worklist (T002b). `SYNTHETIC:`-tagged fakes move
      **verbatim** with their disclosure comments intact (Principle V). Re-bless the surface
      baseline (additive growth, not consumer drift) and keep `Tests.Common.Tests` green.
      The **exec/sensor** members of `FakePorts` land additively in **US2 (T011)** — their exact
      signatures are pinned from the command suites that need them, so `FakePorts` grows across
      US1→US2 (spec US1 Independent Test).
- [X] T009 [US1] Migrate **one** bootstrap-proof target — a low-risk leaf suite whose
      `Support.fs` uses `findRepoRoot` (+ a git fake if convenient) with **no** divergent
      variant (e.g. `tests/FS.GG.Governance.CommandKind.Tests` or `FS.GG.Governance.Findings.Tests`
      — confirm the candidate has no suite-specific helper before picking). Add the
      `<ProjectReference>` to `tests/FS.GG.Governance.Tests.Common/...fsproj` in that project's
      `.fsproj`, `open FS.GG.Governance.Tests.Common`, and **delete the now-redundant local
      copies** from its `Support.fs` in the **same** change (FR-003). Keep any genuinely
      project-specific helpers local (FR-006).
      **Note on FR-005**: FR-005's "command suites first" governs the *sweep batches* — this
      single project is the deliberate **architecture proof** that lands before the US2 batch,
      not part of the remaining sweep (research D5). **Exclude** the chosen project from the
      US3 batch lists (T016–T018) so it is not migrated twice.
- [X] T010 [US1] Run the acceptance invariant for the migrated project: it is green with its
      **pre-migration count** (vs `/tmp/074-baseline-counts.txt`), `git status` shows only the
      deletion + the reference (no golden/snapshot byte changed), and exactly **one** compiled
      definition of each moved helper remains for that project (contract C2/C3/C4). Then prove
      Acceptance Scenario US1-2: edit `RepositoryHelpers.findRepoRoot` in the library, re-run
      that project's tests, confirm the change took with **no** edit to the project; revert the
      edit.

**Checkpoint**: US1 fully functional — `RepositoryHelpers` + git `FakePorts` shared once, one
project migrated green with byte-identical goldens. Independently shippable (MVP).

---

## Phase 4: User Story 2 — Command-suite fixtures consolidated (Priority: P2)

**Goal**: The three highest-duplication command suites (`VerifyCommand.Tests`,
`ShipCommand.Tests`, `RouteCommand.Tests` — 2,349 LOC, ~42% byte-identical) consume the shared
`CatalogFixtures`, `FakePorts` (exec/sensor), `SnapshotHelpers`, and `CaptureHelpers`, with
their local copies deleted and **every golden byte-identical** (FR-005, spec US2).

**Independent Test**: All three suites green with **identical per-suite counts**; every
`verify.json`/`audit.json`/`route.json` golden and every snapshot byte-identical (contract
C3); a shared fixture edited in one place is reflected across all three (Scenario US2-3).

**Depends on**: US1 (the library + the migration pattern). The three suites are the proving
ground for the full sweep (research D5).

- [X] T011 [US2] Extend `TestsCommon.fsi` + `.fs` with the helper groups that are
      **byte-identical across all three command suites**: `CatalogFixtures`
      (`projectYml`/`policyYml`/`toolingYml`, `validCatalog`/`emptyCatalog`/`invalidCatalog`,
      `readerOf`), the exec/sensor `FakePorts` (`fakeExecPortExiting`, `fakeSensor`/
      `throwingSensor`, `absentStoreReader`/`malformedStoreReader`, …), `SnapshotHelpers`
      (`writeFile`, `withTempRepo` + snapshot constructors driving **real** `git` — Principle V),
      and the full `CaptureHelpers` (stdout/stderr/exit-code capture). Pin exact signatures by
      diffing the three `Support.fs` files; **only** byte-identical members move — divergent
      ones stay local (FR-006, research D4, contract C3). Re-bless the surface baseline.
- [X] T012 [P] [US2] Migrate `tests/FS.GG.Governance.VerifyCommand.Tests`: add the
      `<ProjectReference>`, `open FS.GG.Governance.Tests.Common`, and delete the now-redundant
      catalog fixtures / port fakes / snapshot+capture copies from its `Support.fs` in the same
      change. Confirm each copy is byte-identical to the shared form **before** deleting (D4).
      Leave the suite's genuine variants local (e.g. a VerifyJson-style local writer).
- [X] T013 [P] [US2] Same for `tests/FS.GG.Governance.ShipCommand.Tests` — replace+delete the
      shared catalog/fakes/snapshot/capture copies; keep suite-specific helpers local.
- [X] T014 [P] [US2] Same for `tests/FS.GG.Governance.RouteCommand.Tests` — replace+delete the
      shared copies; keep suite-specific helpers local.
- [X] T015 [US2] Run the acceptance invariant across the three suites: all green with
      **identical per-suite counts** (C2) and **every golden/snapshot byte-identical** (C3,
      `git status --porcelain -- 'tests/**'` shows only deletions + reference lines). Prove
      Scenario US2-3: edit one shared `CatalogFixtures` member, confirm all three pick it up
      with no per-suite edit; revert.

**Checkpoint**: US1 AND US2 shippable — the command-suite duplication collapsed, every golden
byte-identical, suite green. The bulk of the LOC win is realised.

---

## Phase 5: User Story 3 — Whole-tree sweep with no test loss (Priority: P3)

**Goal**: The remaining ≈64 `Support.fs` files are swept — only the genuinely-duplicated
`findRepoRoot`/git/fixture copies deleted, intentional per-suite variants kept local and
documented — so the duplication is gone tree-wide with **no** test lost (spec US3, FR-010).

**Independent Test**: Full solution green; per-project counts equal the T001 baseline (only
`Tests.Common.Tests` additive); the duplicated helpers now resolve to **one** definition in
`Tests.Common` (plus documented variants) — `grep -rl "findRepoRoot" tests --include='Support.fs'`
prints only the documented variants (contract A4, SC-004).

**Depends on**: US1 + US2 (the library is fully populated). Sweep in **small batches, one
commit each, acceptance invariant green between batches** (research D5) so any failure isolates
its cause. Within a batch the project migrations are `[P]` (one `Support.fs` each).

> **Completeness authority**: the batch lists below are **illustrative groupings**, not an
> exhaustive enumeration. The **T002 worklist** (`grep -rln "findRepoRoot" tests --include='Support.fs'`,
> minus the US1 project and the three US2 suites) is the authoritative set that must be fully
> consumed; T020 proves nothing was silently dropped. Reconcile any `Support.fs` not named in a
> batch against the T002 list before declaring US3 complete (analysis F5).

- [X] T016 [P] [US3] **Batch — `*Json` projection test suites** (`AttestationJson.Tests`,
      `AuditJson.Tests`, `CacheEligibilityJson.Tests`, `CostBudgetJson.Tests`,
      `EvidenceJson.Tests`, `GatesJson.Tests`, `ProvenanceJson.Tests`, `RefreshJson.Tests`,
      `ReleaseJson.Tests`, `RouteJson.Tests`, `ScaffoldManifestJson.Tests`, `VerifyJson.Tests`):
      add the reference, `open` the library, and delete each suite's redundant `findRepoRoot` /
      git / capture copies in the same change. Keep VerifyJson-style local writers and any
      divergent fixture **local** (FR-006). Run the acceptance invariant; commit the batch.
- [X] T017 [P] [US3] **Batch — command / host / adapter suites** (`CacheEligibilityCommand.Tests`,
      `EvidenceCommand.Tests`, `RefreshCommand.Tests`, `ReleaseCommand.Tests`, `Host.Tests`,
      `Sample.SddReferenceProvider.Tests`, and the other I/O-bearing suites): replace+delete the
      shared `findRepoRoot`/git/snapshot/capture copies; keep suite-specific port fakes local
      where they diverge. Run the acceptance invariant; commit.
- [X] T018 [P] [US3] **Batch — domain-core leaf suites** (the remaining `*.Tests` with a
      `Support.fs`, e.g. `Config.Tests`, `Gates.Tests`, `Snapshot.Tests`, `Enforcement.Tests`,
      `Route.Tests`, `Ship.Tests`, … — those typically needing only `findRepoRoot`): replace+
      delete the redundant repo-root/git copies via the shared module. Run the acceptance
      invariant; commit. Split into sub-batches if a single commit's diff grows unwieldy.
- [X] T019 [US3] **Document the surviving variants**: for any `Support.fs` that keeps a
      genuinely project-specific helper (a divergent catalog/writer/fake), leave a one-line
      comment on it stating it is an intentional per-suite variant kept local (FR-006/FR-010,
      spec US3 Acceptance Scenario 3, research D4). These are the only `findRepoRoot`/git
      definitions allowed to remain outside `Tests.Common`.
- [X] T020 [US3] Prove the terminal invariant (SC-004 / FR-010 / A4):
      `grep -rl "findRepoRoot" tests --include='Support.fs'` and the git-`ProcessStartInfo`
      grep print **only** the documented variants from T019; the shared definitions are the
      single home. Run the full acceptance invariant; confirm per-project counts equal the T001
      baseline (only `Tests.Common.Tests` additive — A1).

**Checkpoint**: All three stories functional — duplication removed tree-wide, every golden
byte-identical, full suite green, no test lost.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Whole-feature acceptance, the LOC-reduction measurement, scope-guard confirmation,
and the Tier 1 agent-context obligation.

- [X] T021 Final full-feature acceptance run of `quickstart.md`: build + test green on
      `FS.GG.Governance.sln`; `git status` shows **no** changed golden/snapshot fixture (A2);
      per-project counts equal the T001 baseline with **only** `Tests.Common.Tests` added (A1).
- [X] T022 [P] Measure the net test-support LOC reduction (A3, SC-003): compare the swept
      `Support.fs` total against the 11,845-LOC baseline; confirm a net reduction of **≥ ~1,000
      LOC** (up to ~3,500), with the three command suites carrying the bulk. Record the figure
      in this file's Notes.
- [X] T023 [P] Confirm FR-008 / SC-005 end-to-end: the scope-guard test is green (no `src`
      project references `Tests.Common`); a change to any one consolidated helper requires
      editing **exactly one** file to take effect across all consumers (contract C5/A5). Confirm
      the library still carries no `Expecto`/test-SDK package and no new third-party reference.
- [X] T024 Run the Tier 1 agent-context update (`/speckit-agent-context-update`) so the managed
      Spec Kit section / CLAUDE.md reflects the new `FS.GG.Governance.Tests.Common` library +
      its `.Tests` harness and the migrated test tree.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately (baseline + worklists).
- **Foundational (Phase 2)**: depends on Setup (needs the src-core reference union from T002).
  The library + its surface/scope-guard harness BLOCKS all three slices.
- **US1 (Phase 3)**: depends on Foundational. The MVP — proves the architecture on one project.
- **US2 (Phase 4)**: depends on US1 (library + migration pattern). The largest measured win.
- **US3 (Phase 5)**: depends on US1 + US2 (library fully populated). The completing sweep.
- **Polish (Phase 6)**: depends on all three slices.

### Within Each Slice

- New-project creation order (Foundational): `.fsproj` → `.fsi` → `.fs` → `.Tests` harness →
  `.sln` registration → bless surface → green.
- Per migration: **confirm/add the `ProjectReference` before deleting any local copy**; the
  deletion + the reference land in the **same** commit (contract C4, spec Edge Case "name
  collisions").
- **Validate goldens before deleting the last copy** of a near-identical helper — a golden
  byte change proves the helper was a variant; keep it local (research D4, FR-006).
- Slice complete (acceptance invariant green, counts unchanged, goldens byte-identical) before
  moving to the next priority.

### Parallel Opportunities

- **Setup**: T002 is `[P]` (read-only worklist capture) alongside T001.
- **US2**: T012–T014 are all `[P]` — one command suite each — after T011 lands the shared
  fixtures/fakes/helpers.
- **US3**: T016–T018 are `[P]` batches (one `Support.fs` per project within a batch); commit
  one batch at a time and re-run the acceptance invariant between them.
- **Polish**: T022 and T023 are `[P]` (independent measurements).

---

## Parallel Example: User Story 2

```bash
# After T011 lands the shared CatalogFixtures / exec-sensor FakePorts / SnapshotHelpers /
# CaptureHelpers, migrate the three command suites in parallel (one Support.fs each):
Task: "Migrate tests/FS.GG.Governance.VerifyCommand.Tests — reference + delete shared copies"
Task: "Migrate tests/FS.GG.Governance.ShipCommand.Tests  — reference + delete shared copies"
Task: "Migrate tests/FS.GG.Governance.RouteCommand.Tests — reference + delete shared copies"
# Then T015: all three green, identical counts, every golden/snapshot byte-identical.
```

---

## Implementation Strategy

### MVP First (Foundational + User Story 1)

1. Phase 1 Setup → baseline per-project counts + worklists.
2. Phase 2 Foundational → land `Tests.Common` + its harness (surface baseline + scope guard).
3. Phase 3 US1 → share `RepositoryHelpers` + git `FakePorts`; migrate one project.
4. **STOP and VALIDATE**: acceptance invariant + the edit-in-one-place proof. Ship the MVP.

### Incremental Delivery

1. Setup + Foundational → library ready, scope guard green.
2. US1 → one project migrated; validate; ship.
3. US2 → three command suites migrated (the bulk win); validate; ship.
4. US3 → remaining sweep in small batches; validate each batch; ship.
   Each slice keeps the suite green, per-project counts unchanged, and every golden byte-identical.

---

## Notes

- **Byte-identity + unchanged counts are the hard gate.** Never re-baseline a golden and never
  accept a count change to go green — a moved golden or a count drift means the move altered
  behaviour; keep the helper local (FR-006) or investigate the lost test (spec Edge Cases).
- The only legitimate total count increase is the additive `Tests.Common.Tests` project
  (like Phase A's `2237 → 2259`, research D3) — it is never counted as drift.
- The shared `findRepoRoot` is the **`sln||slnx` superset** (research D4); it is
  behaviour-identical in this tree (only `.sln` exists) and safe for either marker.
- `SYNTHETIC:`-tagged fakes move **verbatim** with their disclosure comments intact (Principle V).
- The library is **test-only**: `IsPackable=false`, lives under `tests/`, and **no `src`
  project references it** — a tested invariant via the scope-guard (FR-008, contract C5).
- Elmish/MVU (Principle IV) is **N/A**: the helpers are inert port *values* / pure temp-dir+git
  builders the consuming suites drive — no `Model`/`Msg`/`update` is warranted (research D6).
- Commit one concern / one batch at a time so each test run isolates any golden drift or count
  change (FR-007, SC-006).

### Recorded during implementation
- Baseline total test count (T001): **2259** across **78** test assemblies (all green, tip 48c8cfc). tests/ project dirs: 78 (+2 golden-fixture). Support.fs files: 68; findRepoRoot copies: 60; real-`git` ProcessStartInfo copies: 6. Full per-project table in /tmp/074-baseline-counts.txt.
- src-core reference union for the library (T002c): Config, Snapshot, Routing, Findings, Gates, Route, Enforcement, Ship, HumanText, FreshnessKey, FreshnessResolution, FreshnessSensing, CacheEligibility, EvidenceReuse, EvidenceReuseStore, CommandRecord, GateExecution, GateRun, EvidenceCapture (19 refs in the .fsproj).
- Net test-support LOC reduction (T022): **−1,677 LOC** across the swept Support.fs (11,845 → 10,168); the single shared library adds 481 LOC (.fs 373 + .fsi 108) ⇒ **~−1,196 net**, exceeding the ≥~1,000 target (SC-003). Final suite: 2259 → 2265 (+6 additive Tests.Common.Tests only); every per-project count identical; every golden/snapshot byte-identical.
- Surviving documented per-project variants (T019): **none for findRepoRoot** — all 60 copies consolidated (grep for a `findRepoRoot` definition in any tests/**/Support.fs returns 0). The 3 remaining real-`git` ProcessStartInfo suites (Sample.SddReferenceProvider, ReleaseCommand, CacheEligibilityCommand) keep their local `git`/`withTempRepo` builders (their fixtures diverge from the command-suite form); the command suites keep their local Capture/fakePorts/verifyExpected/withTempRepo/expectedCacheReport variants (FR-006).
</content>
</invoke>
