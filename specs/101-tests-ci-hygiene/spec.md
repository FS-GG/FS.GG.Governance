# Feature Specification: Tests & CI hygiene — consolidate SurfaceDrift, bound and cache CI, harden publish

**Feature Branch**: `101-tests-ci-hygiene`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "start the next governance item on the board" → resolves to FS.GG.Governance#54 · *tests/ci hygiene: consolidate SurfaceDriftTests into Tests.Common; NuGet caching; job timeouts; tag guard* (2026-07-02 review M-CI-3 / M-CI-4 + lows, epic #44)

## Overview

The 2026-07-02 code-quality & architecture review surfaced a cluster of test-suite and CI hygiene findings that are individually low-risk but collectively costly. Three concrete problems:

1. **Duplicated surface-drift tests (M-CI-3).** Seventy-four near-identical copies of `SurfaceDriftTests.fs` (≈7,300 lines total) live one-per-test-project, plus a parallel family under other names (six `SurfaceBaselineTests.fs`, one `HumanRenderSurfaceDriftTests.fs`) doing the identical reflect-and-compare dance. Every copy re-derives the same public-surface renderer, the same normalization, and the same `BLESS_SURFACE` bless path; the low-level core suites even re-inline a `findRepoRoot` that already exists in `FS.GG.Governance.Tests.Common.RepositoryHelpers` (most suites already use the shared one). Every new project pastes the same ~40–100 lines; a fix or improvement to the drift check has to be applied ~80 times or silently diverges — and the failure messages already have drifted into four different templates. The check itself is sound — the *packaging* of it is the debt.

2. **Unbounded, un-cached CI (M-CI-3).** The repo commits 166 `packages.lock.json` lockfiles yet no CI job caches the NuGet restore, so every job re-downloads the full graph. No workflow job sets `timeout-minutes`, so a hung job burns the GitHub-default 360 minutes before it is killed.

3. **A publish workflow that can mislabel a release (M-CI-4).** `publish.yml` fires on any `v*` tag. A tag that *is* semver is cross-checked against the CLI project's evaluated `<Version>` and fails on mismatch — but a tag that is **not** semver (e.g. `vNext`) falls straight through the check to `push=true`, publishing whatever version the fsproj happens to declare with no tag↔fsproj agreement. Separately, the hardcoded fallback NuGet user `Paradigma11` is duplicated across two publish jobs.

This feature closes those gaps: one shared parameterised surface-drift helper that each project instantiates in a few lines, CI jobs that are cached and time-bounded, and a publish workflow that refuses to act on a tag it cannot reconcile — with the fallback user single-sourced. It is consolidation and enforcement hardening, not new product behaviour: the set of things actually asserted, built, and published is unchanged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Surface-drift is asserted from one shared definition (Priority: P1)

A maintainer improves or fixes the surface-drift check (how a project's public `.fsi` surface is compared against its committed baseline). Today they would have to edit up to 75 copies; after this change they edit **one** shared helper and every test project picks it up. Adding surface-drift coverage to a new project becomes a few-line instantiation, not a ~100-line paste that re-derives repo-root discovery.

**Why this priority**: This is the headline of the finding (M-CI-3) and the largest concrete debt — ~7,300 lines collapsing to a shared helper plus thin call-sites, and the elimination of 72 redundant `findRepoRoot` re-implementations. It also removes the most likely source of silent divergence (one copy drifting from the rest). It is independently valuable even if CI caching and publish hardening never ship.

**Independent Test**: Run the full test suite before and after; every surface-drift assertion that ran before still runs and still passes (or fails identically on a deliberately broken baseline). Confirm each per-project `SurfaceDriftTests.fs` is reduced to a short instantiation of the shared helper and that no project re-implements `findRepoRoot`.

**Acceptance Scenarios**:

1. **Given** the shared surface-drift helper in `Tests.Common`, **When** a test project instantiates it for its own assembly and baseline, **Then** it produces the same drift verdicts (pass on a matching surface, fail on a mismatched one) that its previous hand-written copy produced.
2. **Given** a project whose committed `.fsi` surface is deliberately mutated to diverge from its baseline, **When** its surface-drift test runs via the shared helper, **Then** the test fails with a diagnostic naming the offending project and the surface difference.
3. **Given** the consolidation is complete, **When** the repository is scanned, **Then** no test project contains a private re-implementation of repo-root discovery — every call-site uses `Tests.Common.RepositoryHelpers`.
4. **Given** two library surfaces (`HumanRender`, `SurfaceChecks.Dispatch`) whose drift is currently asserted from a *non-standard* location (inside `Cli.Tests` and `SurfaceChecks.Tests` respectively, not a dedicated test file), **When** the consolidation lands, **Then** each is either normalized onto the shared helper or recorded as a deliberate, documented placement — not left as an unexplained inconsistency.

---

### User Story 2 - CI jobs are cached and time-bounded (Priority: P2)

A contributor opens a pull request. Every CI job that restores NuGet packages reuses a warm package cache keyed on the committed lockfiles instead of re-downloading the whole graph, and every job carries an explicit wall-clock ceiling so a hang is killed in minutes rather than burning the 360-minute default.

**Why this priority**: Pure throughput and cost hygiene with no behavioural risk — it makes the existing gates faster and bounds runaway jobs. It depends on nothing in Story 1 and delivers value on its own, but it is lower-consequence than removing the duplication debt, hence P2.

**Independent Test**: Trigger CI on a branch; on a second run with unchanged lockfiles observe a cache hit that shortens restore. Inspect every job definition and confirm each restoring job declares NuGet caching keyed on the lockfiles and every job declares an explicit `timeout-minutes`.

**Acceptance Scenarios**:

1. **Given** a CI job that restores packages, **When** it runs a second time with unchanged `packages.lock.json` files, **Then** it restores from cache rather than re-downloading the full dependency graph.
2. **Given** any job in any workflow, **When** its definition is inspected, **Then** it declares an explicit `timeout-minutes` bound (no job relies on the platform default).
3. **Given** the caching change, **When** the deterministic locked restore runs, **Then** it still fails on genuine dependency-graph drift exactly as before (caching does not weaken the locked-restore enforcement).
4. **Given** a job that does not restore packages, **When** the change lands, **Then** it is not made to depend on a cache it never populates (only restoring jobs opt into the package cache).

---

### User Story 3 - The publish workflow refuses to act on a tag it cannot reconcile (Priority: P3)

A maintainer pushes a `v*` tag or cuts a release. If the tag is semver, it must equal the CLI project's evaluated `<Version>` (already enforced). If the tag is **not** semver, the workflow no longer silently publishes — it fails with a clear diagnostic (or produces a no-push dry run) instead of pushing an artifact whose label was never checked. The fallback NuGet user is declared once, not duplicated per job.

**Why this priority**: This is a low-probability but high-consequence safety gap — a mislabeled or unintended publish is hard to undo (version immutability). It is scoped and self-contained, and it protects the release path rather than adding capability, so P3.

**Independent Test**: Simulate the version-resolution logic with a non-semver `v*` tag (e.g. `vNext`) and confirm it does **not** resolve to a pushing publish; simulate a matching semver tag and confirm it still resolves to a push. Confirm the fallback user appears in exactly one place.

**Acceptance Scenarios**:

1. **Given** a non-semver `v*` tag (e.g. `vNext`), **When** the publish workflow resolves the version, **Then** it fails loudly with a diagnostic explaining the tag could not be reconciled against the fsproj `<Version>`, and no package is pushed to any feed.
2. **Given** a semver tag equal to the fsproj `<Version>`, **When** the workflow resolves the version, **Then** it resolves to a normal push exactly as it does today (no regression to the working path).
3. **Given** a semver tag that does **not** equal the fsproj `<Version>`, **When** the workflow resolves the version, **Then** it fails with the existing mismatch diagnostic (unchanged behaviour).
4. **Given** the two publish jobs that authenticate to nuget.org, **When** their definitions are inspected, **Then** the fallback NuGet user is sourced from a single declaration rather than a value hardcoded independently in each job.

---

### Edge Cases

- **A surface-drift copy that legitimately differs**: if any of the 75 copies encodes a per-project deviation (e.g. an intentionally excluded type), the shared helper must express that deviation as a parameter, not force-flatten it — the consolidation must preserve every project's *actual* assertion, including any bespoke one, or fail the migration for that project.
- **Misplaced surface-drift coverage**: `HumanRender` and `SurfaceChecks.Dispatch` are *covered* today but from a non-standard file (a hand-rolled `HumanRenderSurfaceDriftTests.fs` inside `Cli.Tests`, and a second test inside `SurfaceChecks.Tests`), and a parallel family of `SurfaceBaselineTests.fs` files does the same dance under a different name. The consolidation must resolve these explicitly — normalize them onto the shared helper or document the placement as deliberate — rather than leaving several naming conventions for one check.
- **Non-`v*` / lightweight vs annotated tags**: only `v*` tags trigger publish; the hardening concerns tags that match the `v*` trigger but not semver. A tag that does not match `v*` at all is out of scope (it never triggers the workflow).
- **Cache poisoning / staleness**: a cache keyed on the lockfiles must not serve stale packages after a legitimate dependency change — a lockfile change must miss the cache and re-restore, and the locked-restore gate remains the correctness backstop regardless of cache state.
- **Manual dispatch dry run**: a `workflow_dispatch` with no version input must still resolve to a no-push dry run (the existing safe default) — the tag hardening must not turn the intentional dry-run path into a failure.

## Requirements *(mandatory)*

### Functional Requirements

**Surface-drift consolidation (US1)**

- **FR-001**: A single shared, parameterised surface-drift test helper MUST live in `FS.GG.Governance.Tests.Common`, taking the per-project inputs (target assembly / surface and its committed baseline) needed to produce that project's drift assertions.
- **FR-002**: Each test project's `SurfaceDriftTests.fs` MUST be reduced to a thin instantiation of the shared helper (a few lines), carrying no logic that is duplicated across projects.
- **FR-003**: No test project MAY re-implement repository-root discovery; every call-site MUST use the existing `Tests.Common.RepositoryHelpers`.
- **FR-004**: The consolidation MUST preserve each project's actual surface-drift verdicts — a passing surface stays passing and a mutated surface still fails with an actionable, project-identifying diagnostic. Any per-project deviation MUST be expressed as a parameter of the shared helper, never dropped.
- **FR-005**: The surfaces asserted from a non-standard location or file name (`HumanRender` via `Cli.Tests`; `SurfaceChecks.Dispatch` via `SurfaceChecks.Tests`; the `SurfaceBaselineTests.fs` family) MUST be resolved explicitly — normalized onto the shared helper or recorded as a documented, deliberate placement. Every `surface/*.surface.txt` baseline MUST remain asserted by exactly one test after consolidation (no baseline left unguarded, none double-guarded).

**CI bounding & caching (US2)**

- **FR-006**: Every CI job that restores NuGet packages MUST enable package caching keyed on the committed `packages.lock.json` files, so an unchanged graph restores from cache.
- **FR-007**: Every job in every workflow MUST declare an explicit `timeout-minutes` bound; no job may rely on the platform default.
- **FR-008**: Package caching MUST NOT weaken the deterministic locked-restore enforcement — a genuine dependency-graph drift MUST still fail restore, and a lockfile change MUST invalidate the cache.

**Publish hardening (US3)**

- **FR-009**: The publish workflow MUST NOT push when the triggering `v*` tag is not valid semver; it MUST instead fail loudly (or resolve to a no-push dry run) with a diagnostic that the tag could not be reconciled against the CLI project's evaluated `<Version>`.
- **FR-010**: The existing reconcilable-tag paths MUST be unchanged: a semver tag equal to the fsproj `<Version>` still publishes; a semver tag unequal to it still fails with the current mismatch diagnostic; a dispatch with no version input still resolves to a no-push dry run.
- **FR-011**: The fallback NuGet user currently hardcoded in two publish jobs MUST be declared once (a single workflow/job-level source) and referenced by both jobs.

**Cross-cutting constraint**

- **FR-012**: No org-synced build-config file (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) may be edited — they are drift-locked to the org baseline. All CI changes land in the repo-owned workflow files; caching is configured at the workflow level, not in shared build props.

### Key Entities

- **Surface-drift helper**: the single shared definition in `Tests.Common` that, given a project's assembly/surface and its committed baseline, yields that project's surface-drift test(s). Replaces the per-project logic; parameterised over any legitimate per-project deviation.
- **CI job**: a unit of the CI workflows carrying (a) an explicit time bound and (b), when it restores packages, a lockfile-keyed package cache.
- **Version-resolution decision**: the publish workflow's mapping from a trigger (release / `v*` tag / manual dispatch) to `{version, push?}`, hardened so an unreconcilable tag never maps to a push.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The aggregate line count of the surface-drift test family drops substantially — the duplicated renderer / normalize / repo-root / bless logic collapses into one shared ~80-line helper, and each per-project file shrinks to its genuine inputs (assembly binding, label, baseline name) plus any preserved bespoke guard — with zero net change in the number of surface-drift assertions that execute. (Achieved: 7,738 → 3,214 lines, ~58% removed. The residual is per-project bindings and bespoke leak/deny-list/module-count guards that were never duplication, so a full order-of-magnitude drop was not attainable without deleting real assertions.)
- **SC-002**: Zero surface-drift-family files inline repository-root discovery (down from the ~11 that do today); every surface-drift call-site resolves the repo root through `Tests.Common.RepositoryHelpers`. (Non-surface-drift files that inline `findRepoRoot` for unrelated reasons are out of scope.)
- **SC-003**: The full test suite produces the same pass/fail outcome after consolidation as before on both a clean tree (all green) and a deliberately broken baseline (the affected project's drift test fails).
- **SC-004**: Every job across all workflows declares an explicit `timeout-minutes`, and every restoring job restores from a warm cache on a second run with unchanged lockfiles.
- **SC-005**: A non-semver `v*` tag never results in a package push; a matching semver tag still publishes and a dry-run dispatch still pushes nothing.
- **SC-006**: The fallback NuGet user appears exactly once in the publish workflow; the two org-synced build-config files and the tools manifest are byte-identical to their pre-feature state.

## Assumptions

- **Fail-closed over dry-run for a bad tag**: where the finding offered "fail or dry-run," this feature chooses to **fail loudly** for a non-semver `v*` tag, consistent with the repo's established fail-safe pattern (the publish path already errors on a semver mismatch rather than guessing).
- **Caching is a workflow-level concern**: NuGet caching is configured through the CI runner's setup step (keyed on `**/packages.lock.json`), not by editing the drift-locked shared build props — so the org-baseline coherence guard stays green.
- **"Seven restoring jobs" is a review estimate**: the exact set of jobs that restore packages is whatever the current workflows actually contain; the requirement is "every restoring job," resolved against the real job list at implementation time, not a fixed count.
- **The drift check's semantics are correct**: only the packaging of the surface-drift check changes; its comparison logic (what counts as drift, the baseline format) is preserved as-is and re-homed, not redesigned.
- **Surface baselines that move with the helper are re-baselined in place**: if relocating the drift logic changes only *where* a baseline lives (not its content contract), the baseline is re-homed without a product-API change.
- **`HumanRender` / `SurfaceChecks.Dispatch` are covered, not uncovered**: both have committed baselines that are asserted today — just from a non-standard file. The requirement is to normalize the *placement*, not to add missing coverage; this spec only requires the outcome be explicit.
- **Two files stay fully local**: `Cli.Tests/SurfaceDriftTests.fs` (a hardcoded expected-surface list under a `"Surface"` test list) and `Sample.SddReferenceProvider.Tests` (a cross-baseline no-delta guard with a bespoke bless path) are genuinely non-uniform and are out of scope for the shared helper — they keep their own logic.
