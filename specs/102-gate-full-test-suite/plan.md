# Implementation Plan: The gate runs the full test suite on every PR

**Branch**: `102-gate-full-test-suite` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/102-gate-full-test-suite/spec.md`

## Summary

Close review finding **H1** (issue #45, epic #44): the per-PR gate (`.github/workflows/gate.yml`) restores and builds the solution but never runs tests, so only 2 of the 83 test projects execute anywhere in CI and a logic regression in the other 81 merges green as long as it compiles.

The fix is a single new `gate.yml` job that runs the whole Expecto suite through the repo's bounded entrypoint — `dotnet fsi build.fsx test -c Debug --no-restore` — after the same locked restore + lockfile-keyed cache the existing gate jobs use, bounded by an explicit `timeout-minutes`. Measured wall-time (below) shows the full suite fits comfortably in one job, so **no sharding** is adopted.

A decisive discovery during planning: the repo's active branch ruleset (`main branch protection`, id `18430843`) **already lists a required status check named exactly `Full test suite (dotnet fsi build.fsx test)`** — a context that no current job produces. The other three required contexts map one-to-one to the three existing `gate.yml` job names. So the branch-protection side of this feature is already wired and waiting: the job name is a fixed contract, and FR-008/FR-009 are satisfied the moment a job with that exact `name:` lands. No ruleset edit is required (and none is attempted in this Tier-2 change).

This is CI-configuration-only. No product API, `.fsi`, contract, or test assertion changes; the set of assertions is unchanged — only *where they are allowed to run red* changes.

## Measured evidence (drives the single-job decision)

Local run on this machine (12 logical cores → `build.fsx` bounds MSBuild to `-m:3`), latest `main`:

| Step | Command | Wall-time | Result |
|---|---|---|---|
| Locked restore | `dotnet restore FS.GG.Governance.sln --locked-mode` | ~6 s (warm) | exit 0 |
| Bounded build | `dotnet fsi build.fsx --no-restore` | ~89 s | exit 0 |
| Full suite | `dotnet fsi build.fsx test --no-restore --no-build` | ~102 s | **all green**, 83 projects |

Tall pole: `ReleaseCommand.Tests` ~33 s. The whole suite is green locally with **zero** failures/quarantines needed. Even allowing a 2–3× slowdown on a 4-core `ubuntu-latest` runner and folding build+test into one `build.fsx test` invocation (build then test, no `--no-build`), the job lands well under ~15 min — comfortably inside a `timeout-minutes: 30` bound. **Decision: one job, no shards** (FR-007 becomes vacuously satisfied; sharding stays an unused option documented in research D3).

## Technical Context

**Language/Version**: GitHub Actions YAML (one new job in `.github/workflows/gate.yml`); the job shells the existing F# `build.fsx` (net10.0). No F# source, `.fsi`, or test code is touched.

**Primary Dependencies**: `actions/checkout@v4`, `actions/setup-dotnet@v4` (native NuGet cache, already used by the three restoring gate jobs), the checked-in `build.fsx` bounded entrypoint (spec 080), the committed `**/packages.lock.json` lockfiles. No new dependency on any project.

**Storage**: N/A.

**Testing**: Real-evidence CI. The change is validated by (a) the new job going green on this branch's PR while executing all 83 test projects, and (b) a demonstrated RED when an assertion in a previously-un-run project is deliberately broken, then GREEN again on revert (SC-001/SC-003). The suite's own pass/fail is the evidence; no new tests are authored.

**Target Platform**: GitHub Actions `ubuntu-latest` + local `dotnet fsi build.fsx test`.

**Project Type**: CI workflow configuration for an F# library/CLI monorepo.

**Performance Goals**: The new job completes within its declared `timeout-minutes` on a warm cache; a second run with unchanged lockfiles restores from cache (SC-005). No product perf surface.

**Constraints**:
- **No edits to org-synced build config** — `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` stay byte-identical (guarded by the `build-config-drift` job; FR-011).
- **Job name is a fixed contract** — the new job's `name:` MUST be exactly `Full test suite (dotnet fsi build.fsx test)` to satisfy the already-registered required status check. A typo here means the required check never reports and every PR blocks.
- **Locked-restore enforcement preserved** — the job restores `--locked-mode` and runs `build.fsx test --no-restore`, so a graph drift still fails and caching never weakens the lock (FR-003/FR-006).
- **Integrity of the guarantee** — no blanket retry / `continue-on-error` / wholesale skip; a red assertion blocks the merge (FR-010).

**Scale/Scope**: 1 new job (~15 lines) in 1 repo-owned workflow file. 0 F# files. 0 org-synced files. 0 ruleset edits (the required check pre-exists).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — Tier 2 (internal CI hygiene).** No product public API, no `.fsi`, no external contract, no observable product-behavior change. This wires existing tests into the merge path.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | N/A to product surface — no `.fsi` is designed or changed. The feature *strengthens* the principle's enforcement: it makes the mandated semantic tests actually gate the merge instead of only running locally. |
| **II. Visibility lives in `.fsi`** | Untouched. The surface-drift tests that guard Principle II are among the 81 projects this feature finally runs in CI — so the principle's own automated check now blocks merges instead of being build-only. |
| **III. Idiomatic simplicity** | Central. One job that calls the *existing* `build.fsx test` — no new script, no bespoke test runner, no enumerated project allow-list. The CI command equals the local command. Sharding (added complexity) is explicitly rejected on measured evidence. |
| **IV. Elmish/MVU boundary for I/O** | N/A — declarative CI YAML shelling a build script; no stateful/I-O workflow introduced. |
| **V. Test evidence is mandatory** | This *is* the principle, operationalized: the constitution requires test evidence, and this feature makes that evidence a merge gate. Real evidence throughout — the suite's actual verdicts, a demonstrated broken-baseline RED. No synthetic evidence. |
| **VI. Observability & safe failure** | The job fails loudly and specifically (the failing project + assertion is named in `dotnet test` output). Fail-closed: an unreconcilable graph (locked-restore drift) or a red assertion stops the merge rather than passing quietly. |

**Engineering Constraints**: net10.0 via the existing `build.fsx` ✅; no edits to org-synced props ✅; dependency-minimalism — no project gains a dependency; the job adds only workflow YAML ✅; genericity — nothing rendering-specific ✅; `FS.GG.Governance.*` identity preserved ✅.

**Repo-owned-check justification (Development Workflow clause)** — *what contract it protects*: that every committed test actually gates `main` (H1); *when it runs*: every PR and push to `main`; *who owns it*: the Governance repo maintainers; *what it costs*: ~one runner slot for ≤~15 min per PR, warm-cached. Pays for itself by converting thousands of advisory assertions into enforced ones.

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/102-gate-full-test-suite/
├── plan.md              # This file
├── spec.md              # 3 prioritized stories + FR/SC
├── research.md          # Phase 0 — decisions D1–D6
├── data-model.md        # Phase 1 — the gate-test-job shape + required-check contract + CI invariant
├── contracts/
│   └── gate-test-job.md # Contract A (the new job) / B (required-check binding)
├── quickstart.md        # Phase 1 — runnable validation per story
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root)

```text
.github/workflows/
└── gate.yml    # ADD one job:
                #   full-test-suite:
                #     name: Full test suite (dotnet fsi build.fsx test)   # EXACT — matches the required check
                #     runs-on: ubuntu-latest
                #     timeout-minutes: 30
                #     steps: checkout → setup-dotnet(cache) → restore --locked-mode → build.fsx test -c Debug --no-restore

# Unchanged, but relevant:
#   build.fsx                         # the bounded entrypoint the job calls (no edit)
#   FS.GG.Governance.sln              # restored --locked-mode by the job (no edit)
#   Repo ruleset "main branch protection" (id 18430843)
#     already requires context "Full test suite (dotnet fsi build.fsx test)" — no edit needed
```

**Structure Decision**: The entire change is one added job in the repo-owned `gate.yml`, mirroring the existing `gate` job's checkout → cached setup-dotnet → locked-restore shape and then calling the existing `build.fsx test`. Nothing else in the repo changes. The required-status-check contract already exists in the ruleset, so the job name — not any ruleset edit — is the binding. See [research.md](./research.md) for D1–D6 and [contracts/gate-test-job.md](./contracts/gate-test-job.md) for the authoritative job + binding shapes.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
