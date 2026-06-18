# Implementation Plan: Verdicts — Three-Valued Kleene Composition

**Branch**: `002-verdicts-kleene` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-verdicts-kleene/spec.md`

## Summary

Add `Verdict` (F02) to the existing `FS.GG.Governance.Kernel` assembly: a small,
pure, three-valued (Kleene "strong") judgement type — `Pass | Fail of string |
Uncertain of string` — plus the order-independent combinators `Verdict.all`
(conjunction), `Verdict.any` (disjunction), and `Verdict.negate`. The approach (see
[research.md](./research.md)): collection reductions over `Verdict list` so the
empty-list identities (`Pass` for `all`, `Fail ""` for `any`) and associativity fall
out naturally; the **outcome** is commutative + associative by the Kleene truth
tables; the **combined reason** is made byte-for-byte reproducible (FR-006, SC-001)
by normalising the dominating-kind reasons into components on a reserved `"; "`
separator, de-duplicating, **ordinal**-sorting (culture-invariant), and re-joining —
the Hazard-2 mitigation from `docs/governance-design/theory-and-composition.md`. The
public surface is the curated [`contracts/Verdict.fsi`](./contracts/Verdict.fsi),
added to the kernel assembly. Zero new dependencies (BCL only); the algebra lives in
the kernel (roadmap §3) so the F03 `Check` interpreters reuse it for free. This
feature **completes the Kleene operator set** the reified `Check` algebra (F03)
evaluates against.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** — BCL only (`System.String.Split` /
`String.CompareOrdinal` for reason normalisation). The kernel assembly stays
BCL+FSharp.Core only (FR-010, SC-005; enforced by the existing V12 dependency-hygiene
test). Test project only: Expecto + FsCheck, already pinned (D5 from F01).

**Storage**: N/A — pure values, no filesystem, network, or git (FR-010).

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through
the built library / `scripts/prelude.fsx` (Principle I). FsCheck properties for
commutativity, associativity, and `Uncertain`-preservation; reflective API
surface-drift test (the existing V11) now also covers the `Verdict` surface.

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: single library (+ its test project) — additive change to the
existing `FS.GG.Governance.Kernel` — `library`.

**Performance Goals**: correctness/determinism, not throughput. Operations are
O(n) (or O(n log n) for the reason sort) over a typically tiny verdict list; no
measured hot path. `all`/`any`/`negate` are total over all inputs (FR-008).

**Constraints**: pure & deterministic — byte-for-byte reproducible across orderings
and re-nestings (SC-001); total for every input including the empty list (SC-003);
`Uncertain` never silently coerced (SC-002); zero new dependencies (SC-005).

**Scale/Scope**: one new public type (`Verdict`, 3 cases) + one module with 3
functions (`all`, `any`, `negate`) in the existing kernel namespace. No new project.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after
Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `contracts/Verdict.fsi` drafted first; FSI sketch extends `scripts/prelude.fsx` (quickstart); semantic tests against the public surface precede `Verdict.fs`. `tasks.md` orders accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `Verdict.fsi` is the sole surface; `Verdict.fs` carries no `private`/`internal`/`public` on top-level bindings; the existing reflective drift test (V11) re-blessed to include `Verdict` + the `Verdict` module (FR-011). |
| III. Idiomatic simplicity | **PASS** | Plain union + three total functions; pattern matches and `List` pipelines, no mutation, no recursion-for-state. `[<CompilationRepresentation(ModuleSuffix)>]` on the `Verdict` module is the standard F# idiom for a type+companion-module of the same name (cf. `List`/`Option`) — not a flagged "complex feature". No SRTP/reflection/custom operators in the kernel. |
| IV. Elmish/MVU boundary | **N/A** | Pure value algebra — no state machine, I/O, retries, or user interaction. The MVU boundary first appears at F08. |
| V. Test evidence mandatory; prefer real | **PASS** | Real verdict values throughout; FsCheck properties for the headline order-independence guarantees. No synthetic evidence needed (so no `// SYNTHETIC:` disclosures expected). |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F02; every operation is total and returns a verdict for all inputs (FR-008) — no silent failure, no partial results, no throw. |
| Change Classification | **Tier 1** | New public API surface (`Verdict` + combinators) + surface-baseline update; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs). |
| Engineering Constraints | **PASS** | `net10.0`; added to `FS.GG.Governance.Kernel`; `.fsi` per public module; surface baseline updated; zero new deps; no domain vocabulary / I/O (FR-010). Kernel still packs at F06, not here. |

**Gate result: PASS — no violations. Complexity Tracking left empty.**

Decisions locked / touched by this feature (roadmap §6): pins the **Kleene "strong"
truth tables** (`pass`=true / `fail`=false / `uncertain`=unknown) and the
**reason-aggregation rendering** (reserved `"; "` separator → split, dedup,
ordinal-sort, re-join) that every later interpreter inherits. The commutative-node
*hash* canonicalisation remains an F03 concern (Hazard 3), not locked here.

## Project Structure

### Documentation (this feature)

```text
specs/002-verdicts-kleene/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D5
├── data-model.md        # Phase 1 — Verdict cases, combinator truth tables, invariants
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V1–V11
├── contracts/
│   └── Verdict.fsi      # Phase 1 — the curated public signature contract
├── checklists/
│   └── requirements.md  # spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Kernel/
├── FS.GG.Governance.Kernel.fsproj   # add Verdict.fsi + Verdict.fs to the Compile list
├── Verdict.fsi                      # = contracts/Verdict.fsi (NEW, compiled before Kernel.*)
├── Verdict.fs                       # implementation against the stable signature (NEW)
├── Kernel.fsi                       # unchanged (F01)
└── Kernel.fs                        # unchanged (F01)

tests/FS.GG.Governance.Kernel.Tests/
├── FS.GG.Governance.Kernel.Tests.fsproj   # add VerdictTests.fs to the Compile list
├── VerdictTests.fs                         # NEW: V1–V10 Kleene/order-independence/negation
├── FixedPointTests.fs                      # unchanged (F01)
├── SurfaceDriftTests.fs                    # unchanged; V11 now also guards the Verdict surface
└── Main.fs                                 # unchanged

scripts/prelude.fsx                          # extend with a short Verdict sketch (FSI design pass)
surface/FS.GG.Governance.Kernel.surface.txt  # RE-BLESSED to include Verdict + the Verdict module
```

**Structure Decision**: additive to the single existing kernel library — `Verdict`
is a pure value algebra with zero dependency on the F01 fact/rule types, so it gets
its own `Verdict.fsi`/`Verdict.fs` pair compiled **before** `Kernel.*` (dependency-free
first). No new project: the design (roadmap §3) keeps the algebra in the kernel so the
F03 interpreters reuse it without a new dependency, and `System.Text.Json`/the BCL are
the only things either file may touch. The `surface/`, `scripts/`, and central
build-props scaffolding stood up at F01 is reused unchanged; only the baseline *content*
grows.

## Complexity Tracking

> No Constitution Check violations — no entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
