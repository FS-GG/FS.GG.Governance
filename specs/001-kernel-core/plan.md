# Implementation Plan: Kernel Core — Facts, Rules, Fixed-Point Derivation, Provenance

**Branch**: `001-kernel-core` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-kernel-core/spec.md`

## Summary

Deliver `FS.GG.Governance.Kernel` (F01) — a pure, domain-neutral reasoner that takes
asserted facts plus monotonic rules and forward-chains to a fixed point, recording
why each derived fact holds. The approach (see [research.md](./research.md)):
**synchronous (naïve) round-based forward chaining** so the least fixed point *and*
each fact's provenance are independent of rule order; a caller-supplied
`identify : 'fact -> FactId` as the sole authority on identity (dedup + provenance);
a deterministic `(FactId, RuleId)` tie-break for multi-chain provenance. The public
surface is the curated [`contracts/Kernel.fsi`](./contracts/Kernel.fsi). Zero heavy
dependencies (BCL only); F# `net10.0`; no packing yet (the Kernel packs at F06).
This feature **locks decision #4** to the extent of monotonicity + stratification of
supplied negated/aggregated facts, and stands up the API surface-drift baseline
mechanism reused by every later public module.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (SDK 10.0.301 present)

**Primary Dependencies**: **None for the kernel** — BCL only (it needs no
`System.Text.Json` in F01). Test project only: Expecto + FsCheck (D5).

**Storage**: N/A (pure in-memory reasoner; no filesystem, network, or git — FR-010)

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through
the built library / `scripts/prelude.fsx` (Constitution Principle I). Property-based
order-independence via FsCheck; reflective API surface-drift test (D6).

**Target Platform**: cross-platform .NET library (Linux dev host)

**Project Type**: single library (+ its test project) — `library`

**Performance Goals**: correctness/determinism, not throughput. Bounded fact space
per run; naïve iteration is sufficient (SC-005 "light by default"). No measured hot
path in F01; semi-naïve optimization explicitly deferred (D1).

**Constraints**: pure & deterministic — byte-for-byte reproducible across runs and
rule orderings (SC-001); terminates on any bounded monotone input (SC-003); zero
heavy dependencies (SC-005). Monotonicity is a documented precondition, not
runtime-enforced (FR-012).

**Scale/Scope**: one public namespace (`FS.GG.Governance.Kernel`) — 7 types + one
`FixedPoint.evaluate` function; small toy/property fact sets in tests.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after
Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `contracts/Kernel.fsi` drafted first; FSI sketch via `scripts/prelude.fsx` (quickstart); semantic tests against the public surface before `Kernel.fs`. `tasks.md` will order tasks accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `Kernel.fsi` is the sole surface; `Kernel.fs` carries no `private`/`internal`/`public` on top-level bindings; reflective drift test vs `surface/…surface.txt` (D6, FR-011). |
| III. Idiomatic simplicity | **PASS** | Plain records/unions/functions. One justified `mutable` accumulator for the fixed-point pass — explicitly blessed by Principle III, disclosed at the use site. No SRTP/reflection/custom operators in the kernel (reflection lives only in the surface-drift **test**). |
| IV. Elmish/MVU boundary | **N/A** | Pure reasoner — no state machine, I/O, retries, or user interaction (roadmap: MVU not applicable to F01–F07). The boundary arrives at F08. |
| V. Test evidence mandatory; prefer real | **PASS** | Real facts/rules/evaluation throughout; property tests for order-independence. No synthetic evidence needed in F01 (so no `// SYNTHETIC:` disclosures expected). |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F01; `Rounds` is the convergence signal a consumer watches to detect non-termination (FR-008). Empty inputs return empty results, never errors (edge cases) — no silent failure. Structured-logging selection is deferred to an ADR before F08 (plan roadmap §5), an accepted bounded deferral. |
| Change Classification | **Tier 1** | New public API surface + first surface baseline; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs). |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.Kernel` identity; `.fsi` per public module; surface baseline; zero heavy deps; no rendering/domain vocabulary (FR-009/010). Kernel packs at F06, not F01 (D7). |

**Gate result: PASS — no violations. Complexity Tracking left empty.**

Decisions locked / touched by this feature (roadmap §6): **Locks #4** (kernel
preconditions — monotonic; negated/aggregated facts supplied from a lower stratum,
never derived in the same fixed point; commutative-node hash canonicalization is
locked later at F03).

## Project Structure

### Documentation (this feature)

```text
specs/001-kernel-core/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D7
├── data-model.md        # Phase 1 — entities, invariants, behavioral contract
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V1–V12
├── contracts/
│   └── Kernel.fsi       # Phase 1 — the curated public signature contract
├── checklists/
│   └── requirements.md  # spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/
└── FS.GG.Governance.Kernel/
    ├── FS.GG.Governance.Kernel.fsproj   # net10.0, no package refs
    ├── Kernel.fsi                       # = contracts/Kernel.fsi
    └── Kernel.fs                        # implementation against the stable signature

tests/
└── FS.GG.Governance.Kernel.Tests/
    ├── FS.GG.Governance.Kernel.Tests.fsproj   # Expecto + FsCheck (test-only deps)
    ├── FixedPointTests.fs                      # V1–V10: derivation, provenance, order-independence, dedup, rounds
    └── SurfaceDriftTests.fs                    # V11: reflective API surface baseline (FR-011)

scripts/
└── prelude.fsx          # FSI entry: #r the built kernel, open the namespace

surface/
└── FS.GG.Governance.Kernel.surface.txt   # committed API surface baseline

Directory.Build.props    # shared net10.0 / lang settings
Directory.Packages.props # central package versions (test deps pinned here)
```

**Structure Decision**: single library + its test project (the roadmap's downward
dependency layout, with the kernel at the bottom depending on nothing above it). No
`cli/`, `services/`, or `models/` subfolders — F01 is one cohesive pure module, so a
flat `Kernel.fsi`/`Kernel.fs` pair is the plainest fit (Principle III). The
`surface/`, `scripts/`, and central-build-props scaffolding is stood up now and
reused by every later feature (plan roadmap §5).

## Complexity Tracking

> No Constitution Check violations — no entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
