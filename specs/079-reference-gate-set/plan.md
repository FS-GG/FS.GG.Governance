# Implementation Plan: Publish a Populated Reference `.fsgg` Gate Set

**Branch**: `079-reference-gate-set` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/079-reference-gate-set/spec.md`

## Summary

Publish a single curated, **populated** reference `.fsgg` gate set (full
project/policy/capabilities/tooling) at `samples/sdd-reference-gate-set/.fsgg/`, shaped
to govern the SDD reference worked-example skeleton. It declares three first-class checks
— `build`, `test`, and an in-process `evidence`-integrity check — each bound to a
declared tooling command, routed through declared domains/path-map, with `light` as the
non-blocking default profile. A new regression-guard test (`FS.GG.Governance.
ReferenceGateSet.Tests`) loads the on-disk artifact through the **existing** config →
gates → routing → enforcement pipeline and freezes its invariants (loads clean, 3
gates, no dangling/orphan refs, `light` default, advisory-under-Light /
blocking-under-Strict at `RunMode.Verify`). An adopter-facing README documents each gate
and the ratchet posture. **No new F# public surface** — this is a data + test + docs
feature (Tier 2). It unblocks Coordination board P4 (the Templates overlay copies this
artifact unedited).

Technical approach is fully resolved in [research.md](./research.md); the concrete YAML
is in [data-model.md](./data-model.md) §A; the contracts in [contracts/](./contracts/).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (artifact files are YAML; the guard is F#/xUnit).

**Primary Dependencies**: None new. Reuses existing
`FS.GG.Governance.Config` (Loader/Schema/Model), `.Gates`, `.Routing`, `.Route`,
`.Enforcement`, and `FS.GG.Governance.Tests.Common`. FSharp.Core + xUnit only for the
guard project.

**Storage**: Files on disk — a `.fsgg/` directory of four YAML files under
`samples/sdd-reference-gate-set/`.

**Testing**: xUnit, real-evidence (loads the actual on-disk `.fsgg` and exercises the
real domain cores; no synthetic facts, no mocks). New project
`tests/FS.GG.Governance.ReferenceGateSet.Tests/`.

**Target Platform**: Linux/CI and local dev (same as repo).

**Project Type**: Single repository — governance tooling library + samples + tests.

**Performance Goals**: N/A (guard loads/routes one small config; sub-second).

**Constraints**: The artifact MUST load + route through the existing pipeline with 0
errors and be copyable **unedited** by the downstream P4 overlay (no absolute/host paths).
The `light` default MUST stay non-blocking on the everyday inner/verify loop while
remaining blockable under a stricter profile (see research D5).

**Scale/Scope**: 4 YAML files + 1 README + 1 test project (one guard test file) + a
docs cross-link + `.sln` registration. No production `src/` code change.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ PASS | No new public API ⇒ no new `.fsi` to draft. The guard exercises **existing** public surfaces (`loadAndValidate`, `buildRegistry`, `route`, `select`, `deriveEffectiveSeverity`) the way a CLI/adopter would. |
| II. Visibility lives in `.fsi` | ✅ PASS / N/A | No new public `.fs` module ⇒ no `.fsi`, no access modifiers introduced. |
| III. Idiomatic simplicity | ✅ PASS | Plain YAML + a straightforward data-driven test; no clever F# features. |
| IV. Elmish/MVU boundary | ✅ PASS / N/A | No new stateful/I/O workflow. The guard reads via the existing config edge (`loadAndValidate`) and calls pure cores; no new MVU needed. |
| V. Test evidence mandatory | ✅ PASS | Real-evidence guard against the on-disk artifact; fails before the artifact exists, passes after. No synthetic evidence. |
| VI. Observability & safe failure | ✅ PASS / N/A | No new runtime/critical-path code. |
| Change Classification | ✅ Tier 2 | Additive data artifact + test + docs; no public API surface, no new dependency, no change to existing observable behavior ⇒ **no `.fsi`, no surface-area baseline changes** (research D7). |

**Engineering-constraints check**: net10.0 ✅; no new dependency ✅; core rule/evidence
library untouched (the heavyweight stays out of the core) ✅; genericity operating rule
✅ (the reference is an external-customer-shaped sample, supplied as data, assuming no
rendering identity); does **not** re-introduce the deleted evidence-audit/DAG machinery
✅ (research D8).

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/079-reference-gate-set/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D10
├── data-model.md        # Phase 1 — the concrete .fsgg YAML + reused typed values
├── quickstart.md        # Phase 1 — validation/run guide
├── contracts/           # Phase 1
│   ├── reference-gate-set.contract.md
│   └── regression-guard.contract.md
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
samples/
├── FS.GG.Governance.Sample.SddReferenceProvider/   # existing (feature 072)
└── sdd-reference-gate-set/                          # NEW — the published reference
    ├── .fsgg/
    │   ├── project.yml            # schemaVersion 1
    │   ├── capabilities.yml       # schemaVersion 2 — domains, pathMap, surfaces, 3 checks
    │   ├── policy.yml             # schemaVersion 1 — defaultProfile: light
    │   └── tooling.yml            # schemaVersion 1 — dotnet-build / dotnet-test / build-evidence
    └── README.md                  # NEW — adopter-facing gate-by-gate + ratchet posture (FR-011)

tests/
└── FS.GG.Governance.ReferenceGateSet.Tests/         # NEW — the FR-010 regression guard
    ├── FS.GG.Governance.ReferenceGateSet.Tests.fsproj   # IsPackable=false; refs Config/Gates/Routing/Route/Enforcement/Tests.Common
    └── ReferenceGateSetGuardTests.fs                # G1–G7 assertions

docs/
└── tutorials/                                       # existing 072 tutorials
    └── (cross-link to samples/sdd-reference-gate-set/README.md)   # discoverability (FR-011)

FS.GG.Governance.sln                                 # register the new test project
```

**Structure Decision**: Single-repo. The reference is a copyable adopter **sample** under
`samples/` (user-confirmed location, research D1), co-located with its README so a
downstream copy is self-documenting. The regression guard is a dedicated test project
because it must span four upper pipeline layers plus `Enforcement` — no existing test
project references all of them (research D6). No `src/` production code changes.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
