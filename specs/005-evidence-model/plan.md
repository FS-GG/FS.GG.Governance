# Implementation Plan: Evidence Model & Synthetic Taint — Tracking What's Real and Propagating Doubt

**Branch**: `005-evidence-model` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-evidence-model/spec.md`

## Summary

Add the **evidence model** (F05) to the existing `FS.GG.Governance.Kernel` assembly: the
six-case `EvidenceState` (`Pending | Real | Synthetic | Failed | Skipped | AutoSynthetic`),
the abstract, cycle-rejecting `EvidenceGraph<'id>` of declared evidence over a dependency
DAG, and the pure `effective` taint closure that upgrades every `Real` node resting —
directly or transitively — on synthetic evidence to the computed-only `AutoSynthetic`,
leaving every other declared state untouched. The whole point is honesty about evidence:
"this passed, but only on simulated data" can never be silently presented as "this passed",
because the taint is *computed* by a transitive closure and **clears automatically** once
the root-cause `Synthetic` input is re-declared `Real`.

The approach adds **no bespoke rules engine and no new dependency**: `effective` is the
documented `effective(t)` formula (kernel.md "The evidence model") evaluated as a
memoized least-fixed-point over the DAG — the same monotone, deterministic dataflow F01's
`FixedPoint.evaluate` embodies, implemented directly so it is provably **total** over every
valid graph. The DAG invariant is made **unforgeable** by keeping `EvidenceGraph<'id>`
abstract: the only way to obtain one is `Evidence.build`, which rejects a node declared
`AutoSynthetic` (FR-002), a dependency edge to an undeclared node (totality), and any cycle
— self or multi-node (FR-004, **reinforces decision #4**) — so a graph over which the
closure could loop or be partial is unrepresentable. Node identity is generic (`'id`,
carrying no domain vocabulary), so the same model serves software, research, and writing,
and the F10 dogfood adapter's `TaskDependsOn` graph runs through *this* model.

The model is **pure and total**: it performs no I/O, reads no real artifacts, and dispatches
nothing — it operates over *declared* states supplied to it and returns each node's effective
state as data. Reading a node's true state from the filesystem/git, the disclosure logging
around a bypass, evidence-freshness predicates, and JSON rendering are deferred to the F08
edge interpreter and F06. The public surface is the curated
[`contracts/Evidence.fsi`](./contracts/Evidence.fsi), added to the kernel assembly. Zero new
dependencies (FSharp.Core `Map`/`Set`; no BCL beyond the base runtime). **Reinforces decision
#4** (GitHub issue #4: DAG only, no cycles — keeping the taint a monotone, terminating
least-fixed-point with no recursive negation or aggregation).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** — FSharp.Core only. `build`'s cycle detection and
`effective`'s closure use the standard `Map`/`Set`/`List`; no `System.*` is needed at all
(unlike F03/F04, which used `SHA256`). The kernel stays BCL+FSharp.Core, so the existing V12
dependency-hygiene test passes unchanged. Test project only: Expecto + FsCheck, already
pinned (F01 D5).

**Storage**: N/A — pure values; no filesystem, network, git, or agent. The model reads
*declared* states from its constructor arguments and reads no real artifacts (FR-013).

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through the built
library / `scripts/prelude.fsx` (Principle I). FsCheck properties for chain-depth taint over
arbitrary N (SC-002) and order-independence/determinism across node- and edge-ordering
permutations (SC-004); tests for transitive propagation (SC-001), auto-clear on `Synthetic →
Real` (SC-003), cycle rejection — self and multi-node (SC-005), `AutoSynthetic` un-declarable
+ synthetic-outranks-inherited (SC-006), real-only upgrade (SC-007), and totality incl. the
empty graph (SC-008). The reflective surface-drift test (V11) and dependency-hygiene test
(V12) extend to the `Evidence` surface for free once re-blessed.

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: single library (+ its test project) — additive change to the existing
`FS.GG.Governance.Kernel` — `library`.

**Performance Goals**: correctness/determinism, not throughput. `build` is one
`AutoSynthetic` scan + one undeclared-endpoint scan + one DFS cycle check (each linear in
nodes + edges); `effective` is a memoized DFS — each node and edge visited once, O(V + E).
No measured hot path. `build` and `effective` are **total** (FR-011).

**Constraints**: pure & deterministic; no I/O, no real-artifact reads (FR-013); the
`EvidenceGraph<'id>` is abstract so the DAG invariant cannot be bypassed (FR-004); `build`
refuses an `AutoSynthetic` declaration (FR-002, SC-006), an undeclared dependency endpoint
(totality), and any cycle (FR-004, SC-005); `effective` is a deterministic least-fixed-point,
order-independent and history-free (FR-010, SC-004), transitive to full depth (FR-006,
SC-002), upgrades only `Real` nodes (FR-007, SC-007), reports a declared `Synthetic` node as
`Synthetic` not `AutoSynthetic` (FR-008, SC-006), and is total over the empty graph (FR-011,
SC-008); zero heavy dependencies (FR-012, SC-009).

**Scale/Scope**: one new public union `EvidenceState` (6 cases) + one new public union
`GraphError<'id>` (3 cases) + the abstract `EvidenceGraph<'id>` + one `Evidence` module
(`build`, `nodes`, `dependencies`, `effective`), all in the existing kernel namespace. No
new project.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after
Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `contracts/Evidence.fsi` drafted first; FSI sketch extends `scripts/prelude.fsx` (quickstart); semantic tests against the public surface precede `Evidence.fs`. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `Evidence.fsi` is the sole surface; `Evidence.fs` carries no `private`/`internal`/`public` on top-level bindings; the abstract `EvidenceGraph<'id>` hides its representation through the `.fsi` (no access keywords in `.fs`); the reflective drift test (V11) re-blessed to include the F05 types + the `Evidence` module (FR-014). |
| III. Idiomatic simplicity | **PASS (justified)** | Plain unions (`EvidenceState`, `GraphError`) + an abstract record/DU + total functions; `Result` for the construction refusal (an allowed CE-free idiom). **No custom operators, no SRTP, no reflection, no type providers, no CEs.** A `let rec` memoized DFS is the idiomatic graph walk (Principle III explicitly endorses `let rec` for graph walks); the memo table is a single unaliased `Dictionary`/`Map` accumulator (disclosed at the use site if `mutable`). The one constraint — `'id : comparison` — is the standard requirement for `Map`/`Set` keys, not a clever trick. Simpler than F03/F04 (no hashing). |
| IV. Elmish/MVU boundary | **N/A (pure derivation)** | No multi-step state, no I/O, no retries, no agent call, no background work. `effective` is a pure function from a graph to a `Map` of states; `build` a pure validating constructor. There is no `Model`/`Msg`/`Cmd` because there is no workflow — exactly the "simple pure function" the principle exempts. Reading a node's true state and the disclosure logging are the F08 edge's job, modelled there. |
| V. Test evidence mandatory; prefer real | **PASS** | Real `EvidenceGraph` values built from real declared states throughout; FsCheck for the chain-depth and order-independence *properties* (SC-002/SC-004). Auto-clear proven by recomputing `effective` over a graph whose root is re-declared `Real` (real evidence, no mock). No synthetic evidence anticipated — the test inputs ARE real declared-state graphs. |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F05. `build` fails EXPLICITLY with a typed `GraphError` (carrying the offending node / cycle witness) rather than throwing or silently producing a bad graph; `effective` is total and returns a complete map for every valid graph — no silent partial result, no swallowed exception. |
| Change Classification | **Tier 1** | New public API surface (the evidence state, graph, and taint closure) + surface-baseline update; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs) (FR-014). |
| Engineering Constraints | **PASS** | `net10.0`; added to `FS.GG.Governance.Kernel`; `.fsi` per public module; surface baseline updated; **zero new deps** (FSharp.Core `Map`/`Set` only — lighter than F03/F04); no rendering/domain vocabulary; generic over `'id` (the operating rule — an adapter supplies its own node-id type). Kernel still packs at F06, not here. |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the
`let rec`/memo DFS and the `'id : comparison` constraint are standard idioms, not waived
complexity).**

Decisions locked / touched by this feature (roadmap §F05): **reinforces decision #4**
(issue #4) — the evidence dependency structure is a **DAG only**; `Evidence.build` rejects
cycles (self and multi-node), keeping the `effective` closure a monotone, terminating
least-fixed-point with no recursive negation or aggregation. F05 **decides no new locked
decision**; it consumes F01's identity/evaluation discipline and is consumed by F06
(evidence-freshness predicates + JSON explanation) and the F10 dogfood adapter. Reading
real evidence, disclosure logging, the bypass-never-changes-a-verdict rule, freshness, and
serialization are **out of scope** — F08, F07, and F06.

## Project Structure

### Documentation (this feature)

```text
specs/005-evidence-model/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D6
├── data-model.md        # Phase 1 — types, build/effective rules, invariants
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V21–V29
├── contracts/
│   └── Evidence.fsi     # Phase 1 — the curated public signature contract
├── checklists/
│   └── requirements.md  # spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Kernel/
├── FS.GG.Governance.Kernel.fsproj   # add Evidence.fsi + Evidence.fs to Compile (AFTER Kernel.*)
├── Verdict.fsi / Verdict.fs         # unchanged (F02)
├── Kernel.fsi  / Kernel.fs          # unchanged (F01) — F05 reuses its least-fixed-point discipline
├── Evidence.fsi                     # = contracts/Evidence.fsi (NEW, compiled after Kernel.*)
├── Evidence.fs                      # implementation against the stable signature (NEW)
├── Check.fsi   / Check.fs           # unchanged (F03)
├── CheckRule.fsi / CheckRule.fs     # unchanged (F04)

tests/FS.GG.Governance.Kernel.Tests/
├── FS.GG.Governance.Kernel.Tests.fsproj   # add EvidenceTests.fs to Compile (before Main.fs)
├── EvidenceTests.fs                        # NEW: V21–V29 (propagation/auto-clear/cycle/determinism/real-only/totality/accessors)
├── CheckRuleTests.fs                       # unchanged (F04)
├── CheckTests.fs                           # unchanged (F03)
├── VerdictTests.fs                         # unchanged (F02)
├── FixedPointTests.fs                      # unchanged (F01)
├── SurfaceDriftTests.fs                    # unchanged; V11 now also guards the Evidence surface, V12 still BCL-only
└── Main.fs                                 # unchanged

scripts/prelude.fsx                          # extend with a short Evidence/effective sketch (FSI design pass)
surface/FS.GG.Governance.Kernel.surface.txt  # RE-BLESSED to include the F05 types + Evidence module
```

**Structure Decision**: additive to the single existing kernel library. `Evidence` is a new
`Evidence.fsi`/`Evidence.fs` pair compiled **after** `Kernel.*` (and therefore after
`Verdict.*`) to reflect its only real predecessor — F05 is `[P after F01]` in the roadmap and
is **independent of F02/F03/F04** (it references none of `Verdict`/`Check`/`CheckRule`).
Placing it right after the F01 core, before the `Check`/`CheckRule` rule stack, documents that
relationship in the build order itself; `Check.*`/`CheckRule.*` continue to compile after it
and do not depend on it. No new project: the roadmap (§3) keeps the evidence model *in the
kernel* so every adapter (F09–F11) — notably the F10 self-dogfood adapter — reuses it over its
own generic node-id type with zero new dependencies. The `surface/`, `scripts/`, and central
build-props scaffolding stood up at F01 is reused unchanged; only the baseline *content* grows.

## Complexity Tracking

> No unjustified Constitution Check violations. The `let rec` memoized DFS and the
> `'id : comparison` constraint are standard F# idioms (Principle III explicitly endorses
> `let rec` for graph walks and `comparison` is the ordinary `Map`/`Set` key requirement) —
> no entries required here.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
