# Implementation Plan: Bound the scaffold real-evidence build test so the suite never hangs and the routine run stays fast

**Branch**: `078-fix-scaffold-build-test` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/078-fix-scaffold-build-test/spec.md`

## Summary

The SDD reference-provider worked-example suite shells out a real `dotnet build` of a
freshly-scaffolded `MyApp.sln` to prove the emitted skeleton compiles first-attempt
(072 FR-004 / SC-002). That step (`Support.dotnetBuild`) calls `WaitForExit()` with **no
timeout** *after* a synchronous `StandardOutput.ReadToEnd()` — so a stalled build hangs
the whole `dotnet test FS.GG.Governance.sln` run indefinitely (observed: a killed 25-minute
run during 077), it pays a cold full restore + compile on every routine run, and it fans
out MSBuild workers that contend with the parent test run.

The fix is entirely in the **test-support** of one project
(`FS.GG.Governance.Sample.SddReferenceProvider.Tests`): make the build step **bounded**
(async output capture + `WaitForExit(budget)` + `Kill(entireProcessTree=true)` on timeout
→ a new `TimedOut` named-skip outcome), keep the **routine run fast** (gate the heavyweight
build behind an explicit real-evidence opt-in so the default run reports a *named* opt-out
skip, while CI / opt-in still runs the real build), bound MSBuild fan-out
(`-maxcpucount:1 --disable-build-servers`) to kill the resource-contention pathology, and
**preserve the real-evidence guarantee** (a genuine non-zero build still fails; the
missing-SDK skip stays distinct). The two non-build worked-example tests and the committed
manifest golden stay byte-identical; no production code, no `.fsi`, and no surface baseline
is touched.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: Expecto + `Microsoft.NET.Test.Sdk` + `YoloDev.Expecto.TestSdk`
(unchanged); `System.Diagnostics.Process` from the BCL for the bounded subprocess.
`FS.GG.Governance.Tests.Common` (already referenced) for `repoRoot`. No new dependency
(constitution: dependencies minimized).

**Storage**: None. Real temp directories for the scaffold target (existing
`Support.freshTarget`/`cleanup`); the committed golden at
`fixtures/sdd-reference/scaffold-manifest.golden.json` (unchanged, FR-008).

**Testing**: Expecto via `dotnet test`. Both sides exercised with real evidence: a real
bounded process kill of a real sleeper (the US1 forced-stall test), and the real
`dotnet build` of the emitted skeleton under the opt-in/CI configuration.

**Target Platform**: Linux/macOS/Windows dev + CI (test-support must stay cross-platform;
the forced-stall sleeper is OS-selected or named-skipped — never a silent green).

**Project Type**: Test-support change inside an existing single test project of an F#
library/CLI monorepo. No project added (constitution: heavier capabilities layer in
separate projects, but this is a fix within an existing test project).

**Performance Goals**: SC-002 — the worked-example project completes in <30 s in the
default (build-gated-off) configuration. SC-001 — a stalled build is cut off within the
configured budget (~120 s default) plus a small margin, in 100 % of runs. The forced-stall
test itself runs in ~1–2 s (a ~500 ms budget against a real sleeper).

**Constraints**: No production behavior change; scaffold seam / SDD reference provider /
all shipped surfaces untouched. No golden re-bless (FR-008). No surface-baseline change
(test-support is not a reflected assembly). The pass/fail/skip classification must be
deterministic and self-contained — no wall-clock- or network-dependent decision beyond the
build itself (FR-010).

**Scale/Scope**: One test-support module (`Support.fs`) + one test module
(`WorkedExampleTests.fs`) + one new small test module for the forced-stall bound; ~3 new
`Support` bindings and one new `BuildAttempt` case. Net change is small and low-risk.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change classification — Tier 2 (internal/test change).** No public API surface is
added, removed, or modified; no new dependency; no inter-project/package contract change;
no production observable-behavior change. The change is confined to test-support code and
test run configuration. Per the constitution a Tier 2 change "requires spec and tests;
`.fsi` and baselines remain untouched" — which is exactly the shape here. The one new
contract is a *test-harness* env-var opt-in, documented in quickstart/contracts (not a
product surface). The reflective surface-drift test in this very project asserts the
generic-core baselines are byte-identical (SC-006 precedent) — this change keeps that true.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → tests → impl** | Test-support code; no public production module is added, so no `.fsi` is required (test projects carry none in this repo). The new `BuildAttempt.TimedOut` case lives in the test `Support` module. The bound is validated by use through a real forced-stall test before the `dotnet build` wiring relies on it. ✅ |
| **II. Visibility lives in `.fsi`** | Test assemblies have no `.fsi` and are not reflected by any surface baseline (the drift test reflects only `FS.GG.Governance.Scaffold`, `…ScaffoldManifestJson`, and the sample provider). No production surface changes. ✅ |
| **III. Idiomatic simplicity** | Plainest BCL pattern: async `OutputDataReceived`/`ErrorDataReceived` + `BeginOutputReadLine` + `WaitForExit(ms)` + `Kill(true)`. A `mutable`/`StringBuilder` accumulator for captured output is the simpler code and is disclosed at the use site (constitution: mutation allowed when plainer). No exotic F# features. ✅ |
| **IV. Elmish/MVU boundary** | The production scaffold workflow already lives behind its MVU seam (`Interpreter.run`, unchanged). This change is a *single bounded subprocess call at the test edge* — a pure I/O helper, not a stateful multi-step workflow — so it needs no MVU ceremony (constitution: "Simple pure functions … do not need Elmish ceremony"). ✅ |
| **V. Test evidence is mandatory** | Real evidence on both sides: a real process tree really killed within budget (forced-stall test, fails before the bound exists), and the real `dotnet build` under opt-in/CI. No new synthetic evidence introduced; the existing disclosed lifecycle-stand-in is untouched. ✅ |
| **VI. Observability & safe failure** | The headline requirement: every not-run/cut-off outcome is a *named* skip with actionable detail (`TimedOut` budget, missing-SDK, opt-out), each distinguishable; the build subprocess tree is killed on timeout (no orphans, no swallowed hang); a real failure still fails. ✅ |

**Engineering constraints**: `net10.0` ✅; no new dependency ✅; no rendering-specific
assumption ✅; no production `.fsi`/baseline touched ✅. **No violations — Complexity
Tracking left empty.**

## Project Structure

### Documentation (this feature)

```text
specs/078-fix-scaffold-build-test/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output — design decisions D1–D10
├── data-model.md        # Phase 1 output — BuildAttempt, Time budget, Run configuration
├── quickstart.md        # Phase 1 output — run-default / run-real / force-stall validation
├── contracts/
│   └── test-harness-contract.md   # env-var opt-in + BuildAttempt outcome contract
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/
├── Support.fs                  # CHANGED: bounded runner + TimedOut case + budget + opt-in helpers
├── WorkedExampleTests.fs       # CHANGED: build test gated by opt-in + handles TimedOut (2 sibling tests byte-identical)
├── BoundedBuildTests.fs        # NEW: US1 forced-stall test — real bounded kill within budget+margin
├── FailurePathTests.fs         # UNCHANGED
├── SurfaceDriftTests.fs        # UNCHANGED (asserts core baselines byte-identical — stays green)
├── Main.fs                     # UNCHANGED (Expecto entry)
└── FS.GG.Governance.Sample.SddReferenceProvider.Tests.fsproj  # CHANGED: <Compile Include="BoundedBuildTests.fs" />

fixtures/sdd-reference/scaffold-manifest.golden.json           # UNCHANGED (FR-008)
surface/FS.GG.Governance.{Scaffold,ScaffoldManifestJson,Sample.SddReferenceProvider}.surface.txt  # UNCHANGED
```

**Structure Decision**: Single-project, test-support-only change. The bounded
subprocess primitive is added to the existing `Support.fs` (where `dotnetBuild` already
lives) rather than a new shared library, because it is consumed by exactly one project;
promoting it into `FS.GG.Governance.Tests.Common` is an explicitly-deferred follow-up
(the cross-project parity noted in the spec — CLI `dotnet pack`, release `RealPackTests`).
The new `BoundedBuildTests.fs` keeps `WorkedExampleTests.fs`'s diff minimal so the two
non-build sibling tests stay obviously byte-identical (FR-008).

## Complexity Tracking

> No constitution violations. Section intentionally empty.
