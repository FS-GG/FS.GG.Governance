# Implementation Plan: Shared test-support library

**Branch**: `074-shared-test-library` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/074-shared-test-library/spec.md`

## Summary

Remove the test-support duplication identified as **Phase D** of the architecture/quality/
de-duplication roadmap (`docs/reports/2026-06-26-203146-architecture-quality-deduplication-design.md`).
There is **no shared test-support project today**: all 68 `Support.fs` files (11,845 LOC)
hand-roll the same helpers — `findRepoRoot` is copied into 68 files, the real-`git`
`ProcessStartInfo` helper into 7, and the three largest command suites
(`VerifyCommand.Tests` 857, `ShipCommand.Tests` 769, `RouteCommand.Tests` 723 = 2,349 LOC)
are ~42% byte-identical (YAML catalog fixtures, port fakes, temp-repo/snapshot builders).

Introduce one **test-only** library — `FS.GG.Governance.Tests.Common` — that aggregates the
five duplicated helper groups behind a curated `.fsi`, then migrate the existing test
projects to reference it and delete their now-redundant local copies, **one concern / one
batch at a time, suite green at every commit**:

1. **Land `FS.GG.Governance.Tests.Common`** (`.fsi` + `.fs`) with the five modules —
   `RepositoryHelpers`, `FakePorts`, `CatalogFixtures`, `SnapshotHelpers`, `CaptureHelpers` —
   plus a minimal `FS.GG.Governance.Tests.Common.Tests` carrying the reflective
   `SurfaceBaselineTests` and a no-src scope guard, and migrate **one** project (US1).
2. **Migrate the three command suites first** (largest measured win) —
   `VerifyCommand.Tests`, `ShipCommand.Tests`, `RouteCommand.Tests` — deleting their local
   catalog fixtures / port fakes / snapshot+capture helpers (US2).
3. **Sweep the remaining `Support.fs` files**, deleting only the genuinely-duplicated
   `findRepoRoot`/git/fixture copies and keeping intentional per-suite variants local (US3).

**Acceptance is behaviour-preserving and byte-exact**, mirroring how Phase A (feature 073)
was accepted: every migrated project's tests pass with the **same per-project test count**,
and **every golden and snapshot fixture stays byte-identical**. The only test-count *increase*
is the additive `FS.GG.Governance.Tests.Common.Tests` project (exactly as Phase A's
`2237 → 2259` increase was solely its three new leaf-test projects).

This is a **Tier 1** change *for introducing the library* — it adds a curated public `.fsi`
surface, a surface-area baseline, and new inter-project (test→test) dependency edges — but
every consumer migration is **behaviour-preserving** (Tier-2 in spirit: no observable output,
golden, snapshot, or test-count change). It touches **no** production (`src`) project,
artifact, or contract (FR-008).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (solution-wide via `Directory.Build.props`;
`TreatWarningsAsErrors=true`, `Nullable=enable`, `WarnOn=3390;1182`).

**Primary Dependencies**: existing `src` micro-libraries only, referenced by the new
library so its fakes/fixtures can construct typed port and catalog values (`Config`,
`Snapshot`, `GateExecution`, `GateRun`, `FreshnessSensing`, `FreshnessResolution`,
`CacheEligibility`, `EvidenceReuse`, `EvidenceReuseStore`, `CommandRecord`,
`EvidenceCapture`, … — the precise union is enumerated during implementation from the three
command suites' `open` sets). **No new third-party `PackageReference`** is introduced
(`Directory.Packages.props` unchanged). The library itself needs **no** test-runner packages
(`Expecto`/`Microsoft.NET.Test.Sdk`) — it is a plain library; only its `.Tests` project does.

**Storage**: N/A. `SnapshotHelpers` writes into caller-provided temp directories and drives
**real** `git` (Principle V); it owns no durable state.

**Testing**: per-project Expecto suites under `tests/` driven by `dotnet test`, including
golden/snapshot `*Json`/command fixtures and per-project `SurfaceDriftTests.fs`. The new
library gets its own `SurfaceBaselineTests` validating its public surface against
`surface/FS.GG.Governance.Tests.Common.surface.txt` (blessed via `BLESS_SURFACE=1`), matching
the sibling leaf-test convention.

**Target Platform**: Linux / cross-platform CLI + libraries.

**Project Type**: Single solution of one-concern-per-project F# micro-libraries
(75 `src` + 78 `tests`). This feature adds **one** test-only library + **one** test project
to `tests/` and registers both in `FS.GG.Governance.sln`.

**Performance Goals**: N/A (test-support consolidation; no runtime hot path).

**Constraints**: behaviour-preserving — identical per-project test counts and byte-identical
goldens/snapshots at **every committed step** (FR-004, FR-007, SC-002, SC-006). The library
is **test-only** and MUST NOT be referenced by any `src` project (FR-008), enforced by a
scope-guard test.

**Scale/Scope**: 68 `Support.fs` files / 11,845 LOC in scope; net reduction target
≥ ~1,000 LOC (conservative), up to ~3,500 across the full tree (SC-003). The 10 projects
without a `Support.fs` are out of scope for deletion (may opt in, not required).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ | The new library's public surface is drafted **`.fsi`-first** ([contracts/TestsCommon.fsi](./contracts/TestsCommon.fsi)) and exercised through its packed surface; consumers `open` exactly that surface, the same audience a human uses. |
| II. Visibility lives in `.fsi` | ✅ | `Tests.Common` gets a curated `.fsi`; the `.fs` carries **no** `private`/`internal`/`public` modifiers; a reflective `SurfaceBaselineTests` + `surface/FS.GG.Governance.Tests.Common.surface.txt` baseline pin the surface (research D3). |
| III. Idiomatic simplicity | ✅ | Plain helpers — YAML string literals, record/function fakes, temp-dir + real-`git` builders, stdout/stderr capture. No SRTP/reflection/type-providers/custom CEs (reflection stays in the `.Tests` surface check only, as in every leaf). |
| IV. Elmish/MVU boundary | ✅ N/A | The library models **no** stateful/I-O workflow — the fakes are inert port *values* the consuming suites drive; `SnapshotHelpers` is a pure temp-dir/`git` builder. No `Model`/`Msg`/`update` is warranted (research D6). |
| V. Test evidence | ✅ | Acceptance = full suite green + identical per-project counts + byte-identical goldens (real evaluation, not fixtures). `SnapshotHelpers` uses **real** `git`. Pre-existing `SYNTHETIC:`-tagged fakes move verbatim, disclosures intact. |
| VI. Observability & safe failure | ✅ N/A | Test-support code; `RepositoryHelpers.findRepoRoot` fails fast (`failwith`) when no marker is found, exactly as the copies do today. |
| Tier classification | ✅ | **Tier 1** for the new library (`.fsi` + surface baseline + new test→test edges); behaviour-preserving for all consumers. Declared in spec & Summary. |
| net10.0; `.fsi` per public module; surface baseline; deps minimized; **test-only / not packable** | ✅ | `IsPackable=false`; **no** new third-party package; **no** `src` reference to the library (FR-008, scope-guard test). The src-core references it pulls are the union its fakes already require — justified, not new dependencies for the repo. |

**Gate result: PASS** — no violations; Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/074-shared-test-library/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6
├── data-model.md        # Phase 1 — the five helper groups + migration-state model
├── contracts/
│   ├── TestsCommon.fsi          # the curated public surface (design-first sketch)
│   └── migration-acceptance.md  # the byte-identity / test-count acceptance contract
├── quickstart.md        # Phase 1 — runnable validation (green suite, identical counts)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
tests/
├── FS.GG.Governance.Tests.Common/                 # NEW test-only library
│   ├── FS.GG.Governance.Tests.Common.fsproj       #   IsPackable=false; ProjectReferences the
│   │                                              #   union of src cores its fakes construct
│   ├── TestsCommon.fsi                             #   curated public surface (5 modules)
│   └── TestsCommon.fs                              #   RepositoryHelpers / FakePorts /
│                                                   #   CatalogFixtures / SnapshotHelpers / CaptureHelpers
├── FS.GG.Governance.Tests.Common.Tests/           # NEW — surface baseline + scope guard + smoke
│   ├── FS.GG.Governance.Tests.Common.Tests.fsproj
│   ├── SurfaceBaselineTests.fs                     #   reflective surface-drift + "no src references it"
│   ├── SmokeTests.fs                               #   RepositoryHelpers/CaptureHelpers exercised directly
│   └── Main.fs
│
├── FS.GG.Governance.VerifyCommand.Tests/          # migrated (US2): local copies deleted,
├── FS.GG.Governance.ShipCommand.Tests/            #   reference added, Support.fs keeps only
├── FS.GG.Governance.RouteCommand.Tests/           #   genuinely suite-specific helpers
└── …                                              # remaining 64 Support.fs files swept (US3)

surface/
└── FS.GG.Governance.Tests.Common.surface.txt      # NEW blessed surface baseline

FS.GG.Governance.sln                               # both new projects registered
```

**Structure Decision**: A single **test-only** library under `tests/`
(`FS.GG.Governance.Tests.Common`), referenced via `<ProjectReference>` by the migrated test
projects — **not** a `Directory.Build.props` shared-`Compile` link (research **D1**). The
five helper groups are aggregated into **one** assembly behind one `.fsi`, honoring spec
FR-002/Key-Entities (research **D2**). Placing it under `tests/` (rather than `src/`) makes
the test-only intent structural and keeps it out of the production dependency graph (FR-008).

## Complexity Tracking

> No constitution violations — section intentionally empty.
