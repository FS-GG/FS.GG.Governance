# Feature Specification: Shared test-support library

**Feature Branch**: `074-shared-test-library`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-203146-architecture-quality-deduplication-design.md" — resolved to **Phase D — Shared test library** (the roadmap's Suggested-sequencing next step after Phase A / feature 073).

## Overview

The repository has **no shared test-support project**. All test projects hand-roll their own `Support.fs`: across 68 such files there are **11,845 LOC**, of which a large fraction is copy-paste. The repo-root locator (`findRepoRoot`), the real-`git` process helper, YAML catalog fixtures, port fakes, snapshot/capture builders, and temp-repo helpers are duplicated project-by-project, with the three largest command suites (`VerifyCommand.Tests` 857, `ShipCommand.Tests` 769, `RouteCommand.Tests` 723 = 2,349 LOC) measured at **~42% byte-identical**.

This is the single largest maintenance liability in the test tree: a one-line catalog-fixture change requires synchronized edits across 4+ files, and a repo-root-helper change touches dozens. This feature introduces a single shared test-only library — `FS.GG.Governance.Tests.Common` — that the existing test projects reference, after which each project deletes its now-redundant local copies. The behaviour-preserving acceptance bar is that the **full suite stays green with identical per-project test counts** (no tests lost or altered in the move).

## Change Classification

**Tier 1** (per constitution *Change Classification*). *Introducing* the library is a contracted
change: it adds a curated public `.fsi` surface, a surface-area baseline
(`surface/FS.GG.Governance.Tests.Common.surface.txt`), and new inter-project (test→test)
dependency edges — so it carries the full artifact chain (spec, plan, `.fsi`, baseline, test
evidence, doc/agent-context update). **Every consumer migration is behaviour-preserving**
(Tier-2 in spirit: no observable output, golden, snapshot, or per-project test-count change).
It touches **no** production (`src`) project, artifact, or contract (FR-008).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single source of truth for cross-cutting test helpers (Priority: P1)

A maintainer changes a cross-cutting test concern — for example, how the repository root is located, or how a real `git` subprocess is invoked — and needs the change to take effect everywhere at once.

**Why this priority**: This is the core value of the feature. The repo-root locator and git helper are duplicated across nearly every test project; making them a single referenced definition removes the largest synchronized-edit burden and is the prerequisite that every later migration builds on.

**Independent Test**: Land the `FS.GG.Governance.Tests.Common` library containing `RepositoryHelpers` (repo-root locator) and the git fakes of `FakePorts` (the real-`git` `ProcessStartInfo` helper + git port fakes — the exec/sensor fakes land additively with the command suites in US2, since their exact signatures are pinned from those suites), migrate at least one test project to reference it and delete its local copies, and confirm that project's suite passes with an unchanged test count. Editing the shared helper is then observably reflected in the migrated project without touching that project's files.

**Acceptance Scenarios**:

1. **Given** the shared library exists and exposes the repo-root locator, **When** a test project references the library and deletes its local copy, **Then** that project compiles and its tests pass with the same test count as before the migration.
2. **Given** a migrated test project, **When** the shared repo-root locator is modified in one place, **Then** the migrated project picks up the change with no edits to that project's own files.
3. **Given** the shared library, **When** the full test solution is built and run, **Then** there is exactly one compiled definition of each shared helper in the migrated set (no duplicate local definitions remain in those projects).

---

### User Story 2 - Command-suite fixtures consolidated (Priority: P2)

A maintainer updates a YAML catalog fixture (project / policy / tooling YAML, valid/empty/invalid catalogs) or a port fake used by the high-traffic command test suites and needs the update to apply consistently across them.

**Why this priority**: The command suites (`VerifyCommand.Tests`, `ShipCommand.Tests`, `RouteCommand.Tests`) carry the largest measured duplication (~42% byte-identical, ~2,349 LOC) and the highest churn. Consolidating their shared `CatalogFixtures`, `FakePorts`, `SnapshotHelpers`, and `CaptureHelpers` yields the biggest single reduction and is the proving ground for the full sweep.

**Independent Test**: Migrate the three command suites to consume the shared fixtures/fakes/snapshot/capture helpers, delete the local copies, and confirm all three suites pass with identical test counts and unchanged golden/snapshot outputs.

**Acceptance Scenarios**:

1. **Given** the shared library exposes the catalog fixtures and port fakes, **When** the three command suites reference them and delete local copies, **Then** all three suites pass with identical per-suite test counts.
2. **Given** the command suites consume shared snapshot/capture helpers, **When** the suites run, **Then** every golden and snapshot fixture is byte-identical to before the migration.
3. **Given** a shared catalog fixture, **When** it is edited in one place, **Then** the change is reflected across all consuming command suites without per-suite edits.

---

### User Story 3 - Whole-tree sweep with no test loss (Priority: P3)

A maintainer completes the migration across the remaining test projects so the duplication is removed tree-wide, with confidence that no test was silently dropped during the move.

**Why this priority**: The remaining projects (beyond the three command suites) each contribute smaller savings, but completing the sweep is what realises the full ~1,000–3,500 LOC reduction and leaves a single consistent test-support surface. It is lower priority because the architecture and the bulk of the value are already proven by Stories 1–2.

**Independent Test**: After sweeping the remaining `Support.fs` files, run the full solution and compare the total and per-project test counts against the pre-migration baseline; they must be identical, and the full suite must be green.

**Acceptance Scenarios**:

1. **Given** the shared library is in place, **When** the remaining test projects are migrated and their redundant local copies deleted, **Then** the full suite is green and the total test count equals the pre-migration baseline.
2. **Given** the completed sweep, **When** the test tree is searched for the previously duplicated helpers (repo-root locator, git process helper), **Then** the only remaining definitions are in the shared library (plus any deliberately project-specific variants, which are explicitly documented).
3. **Given** a project whose `Support.fs` retains genuinely project-specific helpers, **When** it is migrated, **Then** only the duplicated helpers are removed and the project-specific ones remain, with the project still green.

---

### Edge Cases

- **Divergent "copies" that are not actually identical.** Some per-project helpers may look shared but have small intentional differences (e.g. a suite-specific catalog variant). These MUST stay local rather than being forced into the shared surface; the byte-identity of that suite's goldens is the guard. (Mirrors Phase A, where divergent token/writer copies were correctly kept local.)
- **Helper name collisions on `open`.** When a project opens the shared module, a remaining local helper of the same name would shadow or clash. The migration MUST remove the local copy in the same step it adds the reference, so no project compiles with both.
- **A project that has no `Support.fs`** (10 of 78 projects). These are simply not in scope for deletion; they may still opt into referencing the shared library if useful, but are not required to.
- **Test-count drift.** If a project's test count changes after migration, that signals a test was lost, duplicated, or renamed by the move — this MUST fail acceptance and be investigated, not accepted.
- **Golden/snapshot drift.** Any change to a golden or snapshot fixture during migration indicates the shared fixture diverged from the local one — the local one MUST be preserved (kept local) rather than overwriting the golden.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single test-only shared library, `FS.GG.Governance.Tests.Common`, referenced by test projects rather than copied into them.
- **FR-002**: The shared library MUST expose the cross-cutting test helpers identified as duplicated: a repository-root locator (`RepositoryHelpers`), real-`git`/exec/sensor port fakes (`FakePorts`), YAML catalog fixtures (`CatalogFixtures`: project/policy/tooling YAML and valid/empty/invalid catalogs), snapshot builders (`SnapshotHelpers`), and output-capture helpers (`CaptureHelpers`).
- **FR-003**: Each shared helper MUST have exactly one definition for the migrated projects; migrating a project MUST delete its now-redundant local copy in the same change that adds the reference.
- **FR-004**: Migration MUST be behaviour-preserving: every migrated project's tests MUST pass with the **same test count** as before migration, and **every golden and snapshot fixture MUST remain byte-identical**.
- **FR-005**: The three command suites (`VerifyCommand.Tests`, `ShipCommand.Tests`, `RouteCommand.Tests`) MUST be migrated as the first batch (largest measured win) before the remaining sweep.
- **FR-006**: Helpers that are NOT byte-identical across projects (intentional per-suite variants) MUST remain local; only genuinely duplicated helpers move to the shared library.
- **FR-007**: The full test suite MUST remain green at every committed step of the migration (one concern / one batch at a time, so any failure isolates its cause).
- **FR-008**: The shared library MUST be test-only and MUST NOT be referenced by, or alter, any production (`src`) project or production artifact.
- **FR-009**: The shared library MUST follow the repo's existing discipline for a shared component: a signature boundary that exposes exactly the shared helpers and nothing more, consistent with how other shared leaves are surfaced.
- **FR-010**: After the sweep, the only remaining definitions of the previously duplicated helpers MUST be in the shared library, except for explicitly documented project-specific variants.

### Key Entities *(include if feature involves data)*

- **`FS.GG.Governance.Tests.Common`**: the new shared test-only library. Aggregates the five helper groups below behind a signature boundary; referenced by the existing test projects.
- **`RepositoryHelpers`**: locates the repository root (today's per-project `findRepoRoot`) and related path helpers.
- **`FakePorts`**: real-`git` `ProcessStartInfo` helper plus git/exec/sensor port fakes used by command and adapter suites.
- **`CatalogFixtures`**: the shared YAML catalog inputs — project/policy/tooling YAML and the valid/empty/invalid catalog builders.
- **`SnapshotHelpers` / `CaptureHelpers`**: temp-repo + file-writing snapshot builders and stdout/stderr/exit-code capture utilities used to assert against goldens.
- **Migrated test project**: an existing `*.Tests` project that references the shared library and has had its redundant local helper copies removed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After the full sweep, the full test suite is green and the **total test count is identical** to the pre-migration baseline (no tests gained or lost by the move).
- **SC-002**: **Every golden and snapshot fixture is byte-identical** before and after migration.
- **SC-003**: Net test-support line count is reduced by **at least ~1,000 LOC** (conservative target), with the three command suites alone accounting for the bulk; up to ~3,500 LOC across the full tree.
- **SC-004**: The repository-root locator and the real-`git` process helper each have **exactly one shared definition** consumed by the migrated projects (down from the dozens / 7 copies respectively measured today), except for any explicitly documented project-specific variant.
- **SC-005**: A change to any consolidated shared helper requires editing **exactly one file** to take effect across all consuming projects (down from 4+ synchronized edits for a fixture change today).
- **SC-006**: The suite remains green at **every committed step** of the migration, not only at the end.

## Assumptions

- "Next item" resolves to **Phase D — Shared test library** per the user's selection and the design report's Suggested-sequencing ("A and D first → B → C → E"); Phase A shipped as feature 073.
- The baseline figures (≈78–80 test projects — the design report says 78, the working tree currently has 80 test-project directories; 68 with `Support.fs`, 11,845 LOC, ~42% byte-identical command suites, `findRepoRoot` duplicated widely, 7 git-helper copies) are taken from the design report and confirmed against the working tree; the **authoritative** count and exact LOC removed are captured at implementation (tasks T001/T002), and the total-count acceptance (SC-001) uses that real number **+ the two new projects**, not the report's 78.
- Byte-identical goldens/snapshots and unchanged per-project test counts are the authoritative acceptance signal, consistent with how Phase A was accepted.
- Shared linking is feasible via the existing root `Directory.Build.props` and/or a referenced project; the exact mechanism (project reference vs. shared-compile linking) is an implementation/planning decision, not a spec constraint.
- The 10 projects without a `Support.fs` are out of scope for deletion and need not be migrated.
- This feature is confined to the test tree; no production (`src`) behaviour, artifact, or contract changes.

## Out of Scope

- Any change to production (`src`) projects, MVU host boundaries, projection contracts, or deterministic-JSON output.
- The other roadmap phases: CommandHost skeleton extraction (B), god-module split (C), and CLI decomposition (E).
- Rewriting or expanding test coverage — this is a behaviour-preserving consolidation, not a test-authoring effort.
- Forcing intentionally divergent per-suite helpers into the shared surface.
