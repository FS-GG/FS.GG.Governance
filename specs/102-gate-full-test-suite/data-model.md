# Phase 1 Data Model: The gate runs the full test suite on every PR

Feature: 102-gate-full-test-suite. This feature has no product data model — it is CI configuration. The "entities" are the CI job, the required-status-check contract, and the invariant that binds them. They are documented here so the contract and quickstart can reference one authoritative shape.

## Entity 1 — Gate test job (`full-test-suite`)

The single new job added to `.github/workflows/gate.yml`.

| Field | Value | Source / constraint |
|---|---|---|
| `name` | `Full test suite (dotnet fsi build.fsx test)` | **Fixed** — must equal the registered required-check context (Entity 2). FR-008/FR-009, research D5. |
| trigger | inherits workflow `on:` (`push` → `main`, `pull_request` → `main`) | FR-001. Same triggers as every gate job. |
| `runs-on` | `ubuntu-latest` | Matches the other gate jobs. |
| `timeout-minutes` | `30` | FR-004, SC-003, research D4. Explicit — never the platform default. |
| restore | `dotnet restore FS.GG.Governance.sln --locked-mode` (+ actionable regenerate hint on failure) | FR-003. The single enforcement point; mirrors the `gate` job. |
| cache | `actions/setup-dotnet@v4` with `cache: true`, `cache-dependency-path: '**/packages.lock.json'` | FR-006, SC-005. Same convention as the three restoring gate jobs. |
| run | `dotnet fsi build.fsx test -c Debug --no-restore` | FR-002/FR-005, research D1/D2. Whole-solution, bounded, single restore. |
| shards | none (1 job) | FR-007 vacuously satisfied; research D3. |
| retries / continue-on-error | none | FR-010. A red assertion must fail the job. |

**Coverage obligation (FR-002)**: because the run is `build.fsx test` over `FS.GG.Governance.sln` (not an enumerated list), the executed set is *every* test project in the solution — 83 today, and any future `*.Tests.fsproj` automatically. Overlap with `reference-gate-set-pack` (runs `ReferenceGateSet.Tests`) and with publish.yml's `cli-tests` (`Cli.Tests`) is acceptable; the invariant is *no omission on the PR path*, not *no overlap*.

## Entity 2 — Required-status-check contract

The active repo ruleset — **already present**, not created by this feature.

| Field | Value |
|---|---|
| ruleset | `main branch protection` (id `18430843`), enforcement `active`, target `~DEFAULT_BRANCH` |
| required contexts (4) | `Deterministic gate (locked restore + build)` · `Build-config drift check (shared-build-config)` · `Reference gate set — pack guard (byte-identity + gated + versioned)` · **`Full test suite (dotnet fsi build.fsx test)`** |
| producing job today | first three ✅ (existing gate jobs) · **fourth ❌ — no producer** |
| `strict_required_status_checks_policy` | `false` (branch need not be up-to-date to merge) |

The fourth context is the binding target for Entity 1. Landing a job whose `name` equals it turns a perpetually-pending required check into a real gate — **no ruleset write is performed by this feature** (Tier-2 scope; FR-011 forbids touching org config, and the ruleset already holds the correct name).

## Entity 3 — CI invariant (what must stay true after the change)

- **I1 — Total PR coverage**: every `*.Tests.fsproj` in the solution runs on the PR path (was 2/83). SC-002.
- **I2 — Fail-closed**: a red assertion in any project fails the `full-test-suite` job; the required check goes red; the PR is not mergeable. SC-001/SC-004.
- **I3 — Bounded**: the job declares `timeout-minutes` and is killed at the bound, never the 360-min default. SC-003.
- **I4 — Lock preserved**: locked restore still fails on graph drift; cache warms but never bypasses the lock. FR-003/FR-006.
- **I5 — Org config untouched**: `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json` byte-identical; `build-config-drift` stays green. FR-011/SC-006.
- **I6 — Integrity**: no blanket retry / `continue-on-error` / wholesale skip; any quarantine is narrow, named, tracked. FR-010/SC-006.

## State transition (the guarantee this feature installs)

```text
BEFORE:  PR with a red assertion in project P (P ∉ {ReferenceGateSet.Tests, Cli.Tests})
         → gate builds green → required checks green → PR MERGEABLE   ← H1 gap

AFTER:   PR with a red assertion in project P (any P)
         → full-test-suite job runs build.fsx test → P fails
         → job red → required check "Full test suite (...)" red → PR BLOCKED
         → fix/revert → job green → PR MERGEABLE
```
