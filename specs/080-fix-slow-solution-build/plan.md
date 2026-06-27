# Implementation Plan: Fix the Slow Governance Solution Build

**Branch**: `080-fix-slow-solution-build` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/080-fix-slow-solution-build/spec.md`

## Summary

The full-solution build (`dotnet build FS.GG.Governance.sln`, 162 projects) does **not
finish in 10 minutes** and aborts with resource-exhaustion errors, while a 43-project
subtree builds in 14 s. Empirical diagnosis (see [research.md](./research.md)) shows the
cause is **MSBuild node over-subscription**: `dotnet build` spawns one worker node per
logical core (24 here), and because F# has no shared compiler server (unlike C#'s
`VBCSCompiler`), each node launches its own `dotnet fsc` process. With ~82 test
executables sitting at the dependency-graph leaves, ~24 server-GC compiler processes fire
simultaneously, exhausting threads/heaps → `MSB6003` (`Resource temporarily
unavailable`, EAGAIN), `MSB6006` (exit 134 / SIGABRT), node crashes, retries, and thrash.

The fix is to **bound MSBuild parallelism** with an explicit `-m:N`. Measured: the same
clean 162-project rebuild completes in **33 s with 0 errors** at `-m:6`. The bound must be
delivered through a **checked-in build wrapper** (script) that becomes the documented
standard command, because the bound is only honored on the MSBuild **command line** — it
cannot be set from `Directory.Build.props` (nodes are spawned before props load) and is
**overridden** when placed in `Directory.Build.rsp` (verified). The wrapper computes `N`
from available hardware (scales up on bigger machines), applies uniformly to every
contributor and CI, leaves every compiled output byte-for-byte equivalent, and emits
before/after timing for the measurability requirement.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (SDK 10.0.301); build tooling is the change
surface, not product code.

**Primary Dependencies**: .NET SDK / MSBuild / `dotnet build` + `dotnet test`. No new
package dependency. (`FSharp.Core`, `YamlDotNet`, `Spectre.Console`, Expecto/YoloDev
test stack unchanged.)

**Storage**: N/A (build configuration + docs).

**Testing**: Expecto + YoloDev.Expecto (the repo's existing test stack, 82 test
projects). Validation for this feature is primarily **wall-clock measurement** of the
documented build/test commands plus a lightweight checked-in guard.

**Target Platform**: Linux / Windows / macOS developer workstations and CI runners with
the .NET 10 SDK and a warm NuGet cache.

**Project Type**: Single F#/.NET solution (162 projects: ~79 src + ~82 test + 1 sample),
flat `src/` + `tests/` layout under one `FS.GG.Governance.sln`.

**Performance Goals**: Clean full-solution build < 5 min and ≥ 4× baseline on an 8+ core
machine (SC-001, SC-006); no-op incremental < 30 s (SC-002); full `build + test` within a
single CI job budget, target < 20 min total, zero hand-excluded test projects (SC-003).
Measured here: clean rebuild **33 s** at `-m:6`, no-op incremental **3.3 s** (24 cores,
64 GB, warm cache).

**Constraints**: The faster build MUST be reachable through the standard documented
command with no per-developer machine tuning (FR-007); any configuration MUST be checked
into the repo and apply uniformly, not via per-machine env vars or local overrides
(FR-008); the same set of projects MUST still compile (FR-002) with functionally
equivalent outputs (FR-003) and an intact correctness signal (FR-009).

**Scale/Scope**: 162 projects, 928 ProjectReferences; deepest direct fan-in 42
(`VerifyCommand`); 82 test projects produce Expecto runner executables at the graph
leaves.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle / Constraint | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | No F# public surface is added or changed; there is no `.fsi` to sketch. The flow degrades to spec → plan → measured validation, which this feature follows. **PASS.** |
| **II. Visibility lives in `.fsi`** | No `.fs`/`.fsi` change; no surface-area baseline touched. **PASS (untouched).** |
| **III. Idiomatic Simplicity** | The fix is the plainest thing that works — a thin checked-in wrapper passing one `-m:N` flag to the existing `dotnet build`/`dotnet test`. No clever abstractions, no new dependency. **PASS.** |
| **IV. Elmish/MVU boundary** | The build is not a stateful F#-owned workflow; it shells out to the .NET SDK. A build wrapper script is not an MVU surface and needs none. **PASS (N/A, justified).** |
| **V. Test evidence is mandatory** | This is a performance fix; the oracle is wall-clock time (captured before/after, NFR-001) plus a small checked-in guard asserting the wrapper exists and carries an explicit bound. No assertion is weakened. **PASS.** |
| **VI. Observability & safe failure** | The wrapper echoes the chosen `-m:N`, the core count it derived from, and elapsed time; a real compile error still fails the build with the same MSBuild diagnostic detail (FR-009). **PASS.** |
| **Engineering: F#/.NET only, net10.0, no new deps, minimal** | No target change, no package added; an F# `build.fsx` (preferred, matches the repo's `dotnet fsi build.fsx` idiom) keeps the stack F#-exclusive. **PASS.** |

**Change Classification: Tier 2 (internal change).** Performance/build-infra only; no
public API, no new dependency, no inter-project contract change, build outputs
functionally equivalent (FR-003). `.fsi` files and surface-area baselines remain
untouched — consistent with the Tier 2 contract.

**Result: PASS — no violations.** Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/080-fix-slow-solution-build/
├── plan.md              # This file
├── research.md          # Phase 0 — empirical diagnosis + decisions
├── data-model.md        # Phase 1 — build-config "entities" (knobs + wrapper contract)
├── quickstart.md        # Phase 1 — how to reproduce/validate the speed-up
├── contracts/
│   └── build-command.md # Phase 1 — the documented build/test command contract
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
FS.GG.Governance.sln          # unchanged — same 162 projects (FR-002, SC-004)
Directory.Build.props         # unchanged by the core fix (cannot set -m; see research D3)
Directory.Packages.props      # unchanged — no new dependency

build.fsx                     # NEW — checked-in build entrypoint (dotnet fsi build.fsx)
                              #   computes -m:N from core count, sets the bounded build,
                              #   wraps `dotnet build`/`dotnet test FS.GG.Governance.sln`,
                              #   echoes chosen N + elapsed (NFR-001).
scripts/                      # existing helper scripts live here; thin OS shims MAY be
                              #   added if a non-fsi entry is wanted (decided in tasks)

docs/                         # the "standard documented command" updated to the wrapper
README.md                     # build instructions updated to the wrapper

tests/                        # 82 existing test projects UNCHANGED in count (SC-004);
                              #   the pathological SDD template-generation integration
                              #   test stays opt-in/isolated (FR-010, already gated by
                              #   FSGG_REAL_EVIDENCE — feature 078)
```

**Structure Decision**: No project is added or removed (FR-002 / SC-004). The change is a
new top-level **`build.fsx`** wrapper plus documentation updates. `build.fsx` is chosen
over a bash/cmd pair because it (a) keeps the toolchain F#-exclusive per the constitution,
(b) matches the repository's established `dotnet fsi build.fsx` convention (the reference
gate set), and (c) is trivially cross-platform. A thin `scripts/build.{sh,cmd}` shim that
just calls `dotnet fsi build.fsx` MAY be added for discoverability — deferred to tasks.

## Complexity Tracking

> No constitution violations — section intentionally empty.
