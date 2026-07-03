# Feature Specification: The gate runs the full test suite on every PR

**Feature Branch**: `102-gate-full-test-suite`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "start the next governance item on the board" → resolves to FS.GG.Governance#45 · *ci(gate): run the full test suite on every PR* (2026-07-02 review H1, epic #44)

## Overview

The per-PR gate (`.github/workflows/gate.yml`) restores and **builds** the 162-project solution but never **runs** the test suite. Across the whole of CI, exactly two test projects execute anywhere:

- `FS.GG.Governance.ReferenceGateSet.Tests` — run by the `reference-gate-set-pack` job in `gate.yml` (a targeted package/bundle guard, not the general suite).
- `FS.GG.Governance.Cli.Tests` — run only by `publish.yml`, and only on a publish event (tag / release / dispatch), never on a PR.

The `api-compatibility-gate` job runs `dotnet fsi build.fsx` — a build, not `test`.

The result: **the other ~81 test projects (thousands of assertions) never execute in CI.** A logic regression in any core, adapter, JSON-projection, CLI, or release module merges green as long as it *compiles*. The repository asserts its own correctness through an extensive Expecto suite (the constitution makes test evidence mandatory), yet the gate that protects `main` only checks that the code type-checks and links. This is the single largest enforcement gap in the review (H1): the tests exist and pass locally, but nothing prevents a red suite from reaching `main`.

This feature closes that gap: `gate.yml` gains a job that runs the full test suite under a locked restore, with an explicit wall-clock bound, and branch protection is updated so the new job is required to merge. It is enforcement wiring, not new product behaviour or new tests — the set of assertions is unchanged; what changes is *where* they are allowed to run red.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A failing test blocks the merge (Priority: P1)

A contributor opens a pull request that introduces a logic regression which compiles cleanly but breaks an existing assertion in any test project (core kernel, an adapter, a JSON projection, the CLI, release facts — anything other than the two projects already run). The gate runs the full suite, the affected test goes red, and the PR's required checks fail — the regression cannot merge to `main` until it is fixed.

**Why this priority**: This *is* the finding (H1) and the entire value of the feature. Without it, the repo's thousands of assertions are advisory-at-best on the merge path — they protect nothing that a compile wouldn't already catch. Every other part of this feature (bounding, sharding, branch protection) exists only to make this one guarantee real and durable. It is independently the whole MVP.

**Independent Test**: Open a PR that deliberately breaks one assertion in a project *other than* ReferenceGateSet.Tests / Cli.Tests (e.g. flip an expected value in a kernel or JSON test). Confirm the new gate job goes red and the PR is blocked. Revert; confirm the job goes green and the PR is mergeable.

**Acceptance Scenarios**:

1. **Given** a PR whose diff makes an existing assertion in a previously-un-run test project fail, **When** the gate runs, **Then** the test job fails and the PR's required checks are not all green.
2. **Given** a PR with a green test suite, **When** the gate runs, **Then** the test job passes and (together with the other required jobs) the PR is mergeable.
3. **Given** the full suite runs in the gate, **When** the executed test projects are enumerated, **Then** every test project in the solution is exercised (not only the two run today) — no core/adapter/JSON/CLI/release project is silently excluded.

---

### User Story 2 - The test job is bounded and cannot hang the gate (Priority: P2)

A maintainer relies on the gate finishing in a predictable time. The test job carries an explicit `timeout-minutes`, so a hung or deadlocked test is killed in minutes rather than burning the platform-default 360-minute ceiling; and the suite is run through the repo's bounded build/test entrypoint so the 162-project solution does not thrash the runner. If wall-time demands it, the suite is split across a small number of parallel shard jobs, each independently bounded and required.

**Why this priority**: Correctness of the guarantee is P1; keeping the guarantee *affordable and reliable* is P2. An unbounded or thrashing test job would either time out at 360 minutes or fail flakily under over-subscription, which would pressure maintainers to drop the requirement — defeating US1. This makes the new job a good CI citizen so it survives.

**Independent Test**: Inspect the rendered job definition(s): confirm each declares an explicit `timeout-minutes` and runs the suite via the bounded entrypoint (not a raw unbounded `dotnet test` over the full solution). If sharded, confirm the shards partition the projects with no overlap and no gaps.

**Acceptance Scenarios**:

1. **Given** the new test job (or each shard), **When** its definition is inspected, **Then** it declares an explicit `timeout-minutes` bound and does not rely on the platform default.
2. **Given** a test that hangs, **When** the job runs, **Then** it is terminated at the declared bound with a failing (not indefinitely-running) result.
3. **Given** the test job restores packages, **When** it runs, **Then** it uses the same locked restore + lockfile-keyed cache convention as the existing gate jobs (a graph drift still fails; an unchanged graph restores warm).
4. **Given** the suite is split into shards (if adopted), **When** the shard set is taken together, **Then** it covers exactly the full set of test projects — every project in exactly one shard, none omitted, none double-run.

---

### User Story 3 - The requirement is durable, not just present once (Priority: P3)

A maintainer configures branch protection so the new test job is a **required** status check for merging to `main`. A future PR cannot merge with the test job red, skipped, or removed — the enforcement is anchored in the protected-branch configuration, not merely in the presence of a job that could be bypassed.

**Why this priority**: A job that runs but is not *required* is advisory — a maintainer could merge over a red suite, which silently reopens H1. Making it required is what converts "the tests run" into "the tests must pass to merge." It is P3 only because it depends on US1 existing first and is a configuration step rather than a workflow change; its value is guarding against regression of the guarantee itself.

**Independent Test**: Inspect the branch-protection required-checks list for `main` and confirm the new job (or all shard jobs) appear. Confirm a PR with the test job red reports the branch as not-mergeable via required checks.

**Acceptance Scenarios**:

1. **Given** the branch-protection configuration for `main`, **When** the required status checks are listed, **Then** the new test job (every shard, if sharded) is among them.
2. **Given** a PR with the test job failing, **When** merge is attempted, **Then** it is blocked by the required-check requirement (not merely by convention).
3. **Given** the job name might change (rename/shard-count change), **When** such a change lands, **Then** the required-checks list is updated in the same change so the requirement is never left pointing at a job that no longer runs (no silently-dropped requirement).

---

### Edge Cases

- **Tests that assume they never run in CI.** Some suites may currently pass locally but touch process spawning, redirected stdin, the filesystem, or git in ways that behave differently on a headless `ubuntu-latest` runner (this is the same headless-fragility class as review H2/H3 and 091). Turning the suite on in CI may surface genuine, pre-existing environment-dependent failures. Those are real defects the gap was hiding — they are fixed or explicitly quarantined with a tracking issue, not masked by weakening the gate. The feature must make such a failure *visible*, not silently skip it.
- **Restore/build reuse across jobs.** The test job needs the solution restored and built before it can run `test`. It must obey the same single-locked-restore + `--no-restore`/`--no-build` discipline the existing jobs use, so the graph-drift enforcement is not duplicated or weakened, and the job does not silently re-restore in an unlocked mode.
- **Wall-time growth.** If the full suite in one job approaches the declared bound, the split into shards must partition by project with no overlap and no gap — a shard scheme that drops a project would reopen H1 for that project while looking green.
- **Flaky vs. genuine failure.** A newly-enabled suite must not be made "green" by adding blanket retries or `continue-on-error`; the job's whole purpose is that a red assertion blocks the merge. Any retry/quarantine must be narrow, named, and tracked, never a blanket suppression.
- **The two already-run projects.** ReferenceGateSet.Tests and Cli.Tests are covered today (by the pack-guard job and by publish.yml respectively). The new full-suite job may re-cover them; the requirement is total coverage on the PR path, so overlap with those jobs is acceptable as long as nothing is *omitted* from the PR gate.

## Requirements *(mandatory)*

### Functional Requirements

**Full-suite execution on every PR (US1)**

- **FR-001**: The per-PR gate MUST run the repository's full test suite on every pull request targeting `main` (and on pushes to `main`), such that a failing assertion in *any* test project fails the gate.
- **FR-002**: The set of test projects executed by the gate MUST be the complete set in the solution — not limited to `ReferenceGateSet.Tests` and `Cli.Tests`. No core, adapter, JSON-projection, CLI, or release test project may be silently excluded from the PR path.
- **FR-003**: The suite MUST run under the repository's locked-restore convention: restore in `--locked-mode` against the committed `packages.lock.json` (a graph drift still fails), then run tests without an implicit unlocked re-restore.

**Bounding & reliability (US2)**

- **FR-004**: The test job (and every shard, if the suite is split) MUST declare an explicit `timeout-minutes` bound; no test job may rely on the platform-default ceiling.
- **FR-005**: The suite MUST be executed through the repository's bounded build/test entrypoint (`build.fsx`) rather than a raw unbounded invocation, so the 162-project solution does not over-subscribe the runner.
- **FR-006**: The test job MUST use the same lockfile-keyed NuGet caching convention as the existing gate jobs (warm cache on unchanged lockfiles; cache miss + re-restore on a lockfile change), without weakening locked-restore enforcement.
- **FR-007**: If the suite is split into shards for wall-time, the shards MUST partition the test projects with no overlap and no gap — every test project runs in exactly one shard, and the shard set together covers the whole suite.

**Durable enforcement (US3)**

- **FR-008**: The new test job (every shard, if sharded) MUST be added to the branch-protection required status checks for `main`, so a PR cannot merge with the test job red or absent.
- **FR-009**: Any change to the test job's name or shard count MUST update the required-checks list in the same change, so the requirement never points at a job that no longer runs.

**Integrity of the guarantee**

- **FR-010**: The gate MUST NOT be made green by blanket retries, `continue-on-error`, or wholesale skipping of tests. A red assertion MUST block the merge; any narrow quarantine of a genuinely environment-fragile test MUST be explicit, named, and tracked by a follow-up issue rather than silently masked.
- **FR-011**: No org-synced build-config file (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) may be edited — they are drift-locked to the org baseline (guarded by the `build-config-drift` job). All changes land in the repo-owned `gate.yml` and the branch-protection configuration.

### Key Entities

- **Gate test job**: the new `gate.yml` job (or set of shard jobs) that restores locked, builds, and runs the full Expecto suite via `build.fsx test`, bounded by `timeout-minutes` and keyed to the lockfile cache.
- **Test project set**: the ~83 `*.Tests.fsproj` projects in the solution — the complete population the gate must cover, of which only two run in CI today.
- **Required-check set**: the branch-protection list of status checks that must be green to merge to `main`; the new job(s) join it so the guarantee is enforced, not advisory.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a PR that breaks one assertion in a test project *other than* the two run today, the gate reports a failing required check and the PR is not mergeable; reverting the break returns the gate to green. (The regression is caught on the PR path, where today it would merge green.)
- **SC-002**: The number of test projects exercised on the PR path rises from 2 to the full solution set (~83) — every `*.Tests.fsproj` runs in the gate, verified by enumerating executed projects.
- **SC-003**: Every gate test job (or shard) declares an explicit `timeout-minutes`, and a deliberately hung test is terminated at that bound rather than at the 360-minute platform default.
- **SC-004**: The new test job(s) appear in the branch-protection required checks for `main`; a PR with the test job red is reported not-mergeable by required checks.
- **SC-005**: The full-suite gate completes within its declared wall-clock bound on a warm cache (single job or the slowest shard), and a second run with unchanged lockfiles restores from cache rather than re-downloading the full graph.
- **SC-006**: The two org-synced build-config files and the tools manifest remain byte-identical to their pre-feature state (the `build-config-drift` job stays green), and no test is suppressed via blanket retry or `continue-on-error`.

## Assumptions

- **Run the whole solution, don't curate a subset.** The requirement is total coverage; the gate runs `build.fsx test` over the full solution rather than an enumerated allow-list of projects, so a newly-added test project is covered automatically and cannot be forgotten.
- **`build.fsx test` is the right entrypoint.** The repo already exposes `dotnet fsi build.fsx test` as the bounded whole-suite command (specs/080); the gate uses it rather than inventing a second invocation, keeping the local and CI commands identical.
- **Single job first; shard only if wall-time demands.** The acceptance criteria permit 2–3 shards "if wall-time demands." The default is one bounded job; sharding is adopted only if the measured suite time approaches the bound, and if adopted it partitions by project (FR-007). The plan will confirm the measured time before choosing.
- **Debug configuration.** The suite runs under `-c Debug` (matching the existing gate build and the local test command); a Release test pass is out of scope for this feature.
- **Branch protection is configurable by the maintainer.** FR-008/009 assume the repo admin can set required checks for `main` (via the API/UI). If required-check configuration cannot be applied in this change, the job still runs on every PR (FR-001 holds) and the required-check step is recorded as an explicit, tracked follow-up rather than silently dropped.
- **Pre-existing headless failures are in scope to surface, not necessarily to fix here.** Enabling the suite may reveal genuine environment-dependent failures (H2/H3 class). This feature must make them visible; fixing each is tracked by its own issue (several already exist: #46, #47). A minimal, named quarantine to get the gate green is acceptable if — and only if — each quarantined test is tracked, so the gate protects everything else immediately rather than waiting on every headless fix.
- **No new tests, no changed assertions.** This feature wires existing tests into the merge path; it does not add coverage or alter what any test asserts. The `.fsi`/product surface is untouched (Tier 2 / CI-only change).
