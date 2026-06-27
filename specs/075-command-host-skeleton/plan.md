# Implementation Plan: CommandHost skeleton extraction

**Branch**: `075-command-host-skeleton` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/075-command-host-skeleton/spec.md`

## Summary

Extract the verbatim and near-verbatim MVU command-host skeleton — the small
helpers (`under`, `fail`, `describeInvalid`, `emptySensedFacts`, `revOfCommit`,
`baseHeadOf`, `persistedContent`, `awaitingPersist`), the exit-code mapper
(`exitCode` over a canonical `ExitDecision`), the gate-classification type
(`GateClassification`), the snapshot/kinded-run helpers (`buildSnapshot`,
`kindedRunsOf`, `kindOf`), the gate-execution driver (`tryExecute`), and the
parameterized `executionPlan` — out of the seven command `Loop.fs` hosts and into
**one new pure leaf library** `FS.GG.Governance.CommandHost`, deleting the local
copies. This mirrors the delivered Phase A pattern (new pure leaves placed *below*
the existing layers, `.fsi`-first, with a surface-area baseline + drift test) and
the Phase D discipline (only genuinely-shared members move; type-divergent members
stay local with the reason recorded). The acceptance test is **byte-identical**
golden/snapshot output: every command (`route.json`, `audit.json`, `verify.json`,
refresh, cache-eligibility, release, evidence) and projection fixture is unchanged.

**Change classification:** Tier 1 — new project, new public `.fsi` surface, a new
surface-area baseline, and new inter-project dependency edges from the command
hosts to the leaf. No observable behavior change.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`;
`Nullable=enable`, `TreatWarningsAsErrors=true`, `--nowarn:57`).

**Primary Dependencies**: `FSharp.Core` + BCL only for the leaf body (purity). The
leaf takes ProjectReferences on the already-shared domain-type projects whose values
the helpers walk (e.g. `Snapshot`, `Gates`, `GateRun`, `GateExecution`, `Config`,
`FreshnessSensing`, `FreshnessResolution`, `CacheEligibility`, `EvidenceReuse`,
`FreshnessKey`, `CommandKind`, `CostBudget`) — final reference set confirmed in
[research.md](./research.md) §Leaf dependency set. Tests use `Expecto`,
`Expecto.FsCheck`, `FsCheck`, `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk`,
plus `FS.GG.Governance.Tests.Common` for `findRepoRoot`/`repoRoot`.

**Storage**: N/A — the leaf is pure; it performs no I/O. (The `tryExecute` driver
returns effect data; the host interpreter executes it, exactly as today.)

**Testing**: Expecto. The behavior gate is the existing per-command golden/snapshot
suites (must stay byte-identical). The new leaf adds one additive test project
(`FS.GG.Governance.CommandHost.Tests`): semantic tests over the public surface with
real, literally-constructed domain values, plus a reflective `SurfaceBaselineTests`
(surface-drift equality + dependency-hygiene scope guard) matching the Phase A leaves.

**Target Platform**: Cross-platform .NET; developed on Linux.

**Project Type**: F# microproject — one new pure *leaf* library inside the existing
multi-project solution (`FS.GG.Governance.sln`), consumed by the seven MVU command
hosts.

**Performance Goals**: N/A — behavior-preserving refactor; no hot path touched.

**Constraints**:
- **Byte-identical** golden/snapshot output across every command and projection
  (the acceptance test for behavior preservation — FR-009, SC-002).
- The leaf MUST be **pure**: no host/filesystem/git/process dependency; enforced by
  a scope-guard test (FR-002, SC-005).
- The dependency graph stays **acyclic** and the pure-core/impure-host split is
  preserved (FR-011); `TreatWarningsAsErrors=true` means any non-exhaustive match
  introduced by adopting a canonical DU is a build break, not a warning.
- `.fsi`-first; visibility lives only in the `.fsi`; surface-area baseline + drift
  test required (Constitution II/V, FR-003/FR-004).

**Scale/Scope**: 7 consuming command hosts; ~400–500 LOC net source reduction
(SC-004). One leaf `.fs`+`.fsi`, one additive test project, one new surface baseline.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ PASS | The leaf is drafted `.fsi`-first ([contracts/command-host.fsi.md](./contracts/command-host.fsi.md)); semantic tests exercise the public surface before the `.fs` body is finalized; the host edits follow. |
| II. Visibility Lives in `.fsi` | ✅ PASS | Leaf gets a curated `.fsi` exposing exactly the shared helpers; `.fs` carries no access modifiers; a surface-area baseline (`surface/FS.GG.Governance.CommandHost.surface.txt`) + reflective drift test are added (FR-003/FR-004). |
| III. Idiomatic Simplicity Is the Default | ✅ PASS | Helpers are plain functions/records. `executionPlan` is parameterized by a **plain record of optional folds** (no SRTP, no generics, no active patterns, no custom operators) — see research D4. Any deviation would be justified here; none is needed. |
| IV. Elmish/MVU Is the Boundary | ✅ PASS | The leaf holds **pure** helpers that sit *below* `update`; it adds no MVU ceremony of its own. The hosts keep their existing `Model`/`Msg`/`Effect`/`init`/`update`/interpreter boundary unchanged — exactly the Phase A leaf shape. |
| V. Test Evidence Is Mandatory | ✅ PASS | Behavior preserved is proven by the existing golden/snapshot suites staying byte-identical (real evaluation, no mocks). The leaf adds real-value semantic tests + surface-drift. No synthetic evidence introduced. |
| VI. Observability and Safe Failure | ✅ PASS | No I/O or failure paths move semantics; diagnostics/exit-code mapping is preserved byte-for-byte (the canonical `exitCode` reproduces every host's current codes). |
| Change Classification | ✅ Tier 1 declared | New project, new `.fsi`, new baseline, new edges — full artifact chain produced. |

**Result: PASS — no violations. Complexity Tracking left empty.**

Re-check after Phase 1 design: still PASS. The one design risk (canonical DU
adoption forcing non-exhaustive matches under `TreatWarningsAsErrors`) is handled
by adding never-taken arms in the non-budget hosts, which is behavior-preserving and
caught immediately by the compiler — see research D2/D3.

## Project Structure

### Documentation (this feature)

```text
specs/075-command-host-skeleton/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — per-helper move/keep decisions + hard cases
├── data-model.md        # Phase 1 — leaf entities (ExitDecision, GateClassification,
│                        #            ExecutionPlanParams, GateClassification map)
├── quickstart.md        # Phase 1 — how to build/test and verify byte-identity
├── contracts/
│   └── command-host.fsi.md   # Phase 1 — the proposed curated leaf .fsi surface
├── checklists/
│   └── requirements.md  # (pre-existing) spec quality checklist
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.CommandHost/                 # NEW pure leaf
│   ├── FS.GG.Governance.CommandHost.fsproj       #   IsPackable=true, PackageId, Version 0.1.0
│   ├── CommandHost.fsi                            #   curated public surface (.fsi-first)
│   └── CommandHost.fs                             #   the moved helpers + parameterized executionPlan
├── FS.GG.Governance.RouteCommand/Loop.fs         # consume leaf; delete local copies
├── FS.GG.Governance.ShipCommand/Loop.fs          # consume leaf; delete local copies
├── FS.GG.Governance.VerifyCommand/Loop.fs        # consume leaf; delete local copies
├── FS.GG.Governance.RefreshCommand/Loop.fs       # consume the subset it uses (under/exitCode/fail)
├── FS.GG.Governance.CacheEligibilityCommand/Loop.fs
├── FS.GG.Governance.ReleaseCommand/Loop.fs
└── FS.GG.Governance.EvidenceCommand/Loop.fs

tests/
└── FS.GG.Governance.CommandHost.Tests/           # NEW additive test project
    ├── FS.GG.Governance.CommandHost.Tests.fsproj
    ├── CommandHostTests.fs                        # semantic tests over the public surface
    ├── SurfaceBaselineTests.fs                    # reflective drift + scope-guard
    └── Main.fs

surface/
└── FS.GG.Governance.CommandHost.surface.txt      # NEW surface-area baseline

FS.GG.Governance.sln                              # add 2 Project entries (src + test)
```

**Structure Decision**: Single new pure-leaf microproject under `src/` plus one
additive test project under `tests/`, registered in `FS.GG.Governance.sln`, with a
new baseline under `surface/`. This is the exact shape the Phase A leaves
(`JsonText`/`JsonTokens`/`JsonWriters`) and Phase D library established; reusing it
keeps the dependency graph legible and the pure-core/impure-host split intact. The
leaf is placed **below** the command hosts and **above** the domain-type projects it
references.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.

## Phase 2 note

`/speckit-tasks` will sequence the work as one-concern-per-commit (FR-013):
scaffold the leaf + baseline + test project; move the byte-identical micro-helpers
first; then the canonical `ExitDecision`/`exitCode`; then `GateClassification` +
the parameterized `executionPlan` (the gated, highest-risk move) for Route/Ship/
Verify; then sweep each host's deletions; full suite green at every commit.
