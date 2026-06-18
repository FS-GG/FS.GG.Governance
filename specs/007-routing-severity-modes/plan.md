# Implementation Plan: Routing, Stakes & Run Modes — Light by Default, Always Explained

**Branch**: `007-routing-severity-modes` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-routing-severity-modes/spec.md`

## Summary

Add the **light routing layer** (F07) to the existing `FS.GG.Governance.Kernel` assembly —
the pure decision that sits *above* the F04 `CheckRule` bridge and answers, for a concrete
proposed change, **which requirements apply, whether they merely advise or actually block,
and why**. This **starts Milestone M2** (light routing + the effects edge). It introduces one
new public module, `Route`, exposing four shapes and three folds:

1. **Stakes & fences (light by default).** An abstract change is classified by declared
   **fences** — named classifiers — into `Stakes = Routine | Fenced of string`. `stakesOf`
   combines fences by **deterministic precedence — forbid trumps permit**: `Fenced` if *any*
   fence trips (carrying the tripped names de-duplicated, ordinal-sorted, `"; "`-joined),
   `Routine` otherwise. Because the carried name is a function of the *set* of tripped fences,
   the result is **identical under any fence ordering** — combination is never positional,
   which **closes hazard 5 / reinforces decision #4**. An unclassified change (and the empty
   fence set) defaults to `Routine` with no gates.
2. **Run modes (when a stake blocks).** `RunMode = Sandbox | Inner | Gate` names the lifecycle
   position. A blocking-severity requirement on a `Fenced` change surfaces as **advisory** in
   `Sandbox`/`Inner` and as a **blocking gate** only in `Gate`; the `Stakes` classification is
   identical across all three (run mode changes enforcement, not classification).
3. **The explainable route.** `route` computes the stakes, folds the **applicable** rules into
   drift-proof `ContractEntry` requirements (reusing **F06** `Contract.ofRules`, so each
   `Statement` *is* `Check.render` — no drift), and partitions them: a requirement is a
   blocking gate **iff** `Severity = Blocking ∧ stakes = Fenced ∧ mode = Gate`, else advisory.
   Every `Route` carries a **non-empty reason**; `renderRoute` produces a deterministic,
   execution-free explanation naming the stakes, the fence, and each rendered check.

The approach adds **no new dependency**: routing reuses the in-assembly F06 `ContractEntry` /
`Contract.ofRules`, F04 `CheckRule` / `Severity`, F01 `RuleId`, and (transitively) F03 `Check`
/ F02 `Verdict` — so the V12 BCL/`System.*`-only hygiene test passes unchanged with **zero**
`PackageReference`. The whole feature is **pure and total**: it performs no I/O, runs no probe,
dispatches no agent review, and reads no clock — fences, rules, run mode, and the abstract
change are all supplied as values. Executing the route (sensing facts, running probes,
dispatching reviews, recording verdicts, logging disclosures) remains the **F08** edge
interpreter's job (FR-016). All output is **domain-neutral**: the change is the generic
`'change` type parameter (the abstract `ChangeSet`); `Route` drops `'change` and `'fact`,
carrying only `Stakes`, `ContractEntry` requirements, and a reason (FR-017).

This feature **decides no new locked roadmap decision**; it **reinforces decision #4 and closes
hazard 5** (stakes combination is deterministic by precedence, never positional). It consumes
F04 (and through it F03/F02) and F06, all already merged, and is consumed by F08 (the effects
loop that *acts* on a route) and F12 (the CLI `route` command). The public surface is one
curated `.fsi` contract — [`contracts/Route.fsi`](./contracts/Route.fsi) — added to the kernel
assembly with the surface-area baseline re-blessed (FR-018).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new.** The kernel stays BCL + FSharp.Core. `Route` reuses
F06 `Contract.ofRules`/`ContractEntry` (drift-proof statements), F04 `CheckRule`/`Severity`,
F01 `RuleId`, and the standard library only (`List.partition`, `Set`/`List.sort` for the
order-independent name combination). No `PackageReference` is added, so V12
(`name.StartsWith "System."` / FSharp.Core) keeps passing (research D6). Test project only:
Expecto + FsCheck, already pinned (F01 D5).

**Storage**: N/A — pure values; no filesystem, network, git, clock, or agent. `stakesOf`/`route`
operate on a supplied abstract `'change`, declared `Fence<'change>` predicates, a supplied
`CheckRule<'fact>` list, and a `RunMode` (FR-016).

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through the built
library / `scripts/prelude.fsx` (Principle I). FsCheck properties for **order-independence**
(permute the fence list ⇒ identical stakes and identical route, SC-003), **determinism**
(byte-for-byte identical route + `renderRoute` across repeated evaluation, SC-008), and
**light-by-default** (no matching fence ⇒ `Routine` + empty blocking set in any mode, SC-001).
Targeted tests for: a single matching fence ⇒ `Fenced` (SC-002); the run-mode matrix — the same
blocking rule is advisory in `Sandbox`/`Inner`, blocking only in `Gate`, with identical stakes
across modes (SC-004); every route carries a non-empty reason (SC-005); a blocking gate's
statement is byte-for-byte `Check.render` of the rule's check (no drift, SC-006); the blocking
subset equals exactly the blocking gates and is bounded by the applicable rules (SC-007);
totality over the empty fence set and the empty rule set (SC-009); zero probe/review executed
across the suite (SC-010); zero-dependency hygiene via the re-blessed V11/V12 reflective tests
(SC-011).

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: single library (+ its test project) — additive change to the existing
`FS.GG.Governance.Kernel` — `library`.

**Performance Goals**: correctness/determinism, not throughput. `stakesOf` is one pass over the
fences; `route` is one `Contract.ofRules` fold plus one `List.partition` over the applicable
rules; `renderRoute` is one pass over the route — each O(size of input). No measured hot path.
All functions are **total** (FR-015).

**Constraints**: pure & deterministic; no I/O, no clock, no probe, no agent (FR-016, SC-010).
Stakes combination is **order-independent** — the `Fenced` name is the set of tripped fence
names (de-duplicated, ordinal-sorted, `"; "`-joined), so permuting `fences` never changes the
stakes or the route (FR-005, SC-003). Light by default — no matching fence (or empty fence set)
⇒ `Routine` + empty blocking set in every run mode (FR-006, SC-001). A blocking gate requires
`Severity = Blocking ∧ stakes = Fenced ∧ mode = Gate` (FR-008); the stakes are identical across
modes (FR-009). Every `Route` carries a non-empty reason (FR-011, SC-005); a gate's statement is
`Check.render` (drift-proof, FR-012, SC-006). `renderRoute` is execution-free (FR-014). Routing
is total over the empty fence set and empty rule set (FR-015, SC-009) and adds zero dependency
(FR-017, SC-011).

**Scale/Scope**: one new public module in the existing kernel namespace — `Route` (the DUs
`Stakes` and `RunMode`, the records `Fence<'change>` and `Route`, and the `Route` module with
`stakesOf` / `route` / `renderRoute`). No new project.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The single `contracts/Route.fsi` is drafted first; the FSI sketch extends `scripts/prelude.fsx` (quickstart); semantic tests against the public surface precede the `.fs` body. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `Route.fsi` is the sole surface; the `.fs` carries no `private`/`internal`/`public` on top-level bindings; the reflective drift test (V11) is re-blessed to include the F07 types + module; V12 still BCL/`System.*`-only (FR-018). |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs + total folds: `stakesOf` is `List.choose`+sort+dedup, `route` is `Contract.ofRules` then `List.partition`, `renderRoute` is one fold to a string. No custom operators, no SRTP, no reflection in the kernel, no type providers, no non-trivial CEs. The generic `'change`/`'fact` parameters are ordinary F# (the kernel's house style: `Check<'fact>`, `EvidenceGraph<'id>`). The order-independent `Fenced`-name combination deliberately **reuses the F02 reason-combination convention** (split / dedup / ordinal-sort / re-join) rather than inventing a new one. |
| IV. Elmish/MVU boundary | **N/A (pure derivation)** | No multi-step state, no I/O, no retries, no agent call, no clock, no background work. Routing maps `(fences, rules, mode, change)` to a `Route` value as a pure function — exactly the "simple pure function" the principle exempts, and exactly what the spec Assumptions and the dated plan (F07 = MVU N/A) state. Acting on the route (read facts, run probes, dispatch reviews, log disclosures) is the **F08** boundary feature, modelled there. |
| V. Test evidence mandatory; prefer real | **PASS** | Real `Fence`/`CheckRule`/`Check` values built from real checks and declared fences throughout; FsCheck for the order-independence, determinism, and light-by-default *properties*. No synthetic evidence anticipated — the inputs ARE real kernel values. |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F07. `stakesOf`/`route`/`renderRoute` are total and deterministic; there is no partial result and no throwing path (FR-015). Distinguishing tool defect from malformed input is N/A — there is no external input to parse (the change is a typed value; fences are typed predicates). |
| Change Classification | **Tier 1** | New public API surface (`Stakes`, `Fence`, `RunMode`, `Route`, `stakesOf`, `route`, `renderRoute`) + surface-baseline update; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs) (FR-018). |
| Engineering Constraints | **PASS** | `net10.0`; added to `FS.GG.Governance.Kernel`; `.fsi` per public module; surface baseline updated; **zero new deps** (reuses in-assembly F02/F03/F04/F06, standard library only); no rendering/domain vocabulary (the change is the generic `'change`; `Route` drops `'change`/`'fact`) — the operating rule. This feature **starts M2**; no packing/milestone action (M1 already packed the kernel at F06). |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the generic
`'change`/`'fact` parameters and the `List.partition` / set-based name combination are ordinary
F# idioms, not waived complexity).**

Decisions locked / touched by this feature (roadmap §F07): **decides no new locked decision.**
It **reinforces decision #4 and closes hazard 5** — stakes combination over multiple fences is
deterministic by precedence (forbid trumps permit), **never positional**: reordering or
re-evaluating fences never changes the outcome (the design-doc `stakesOf` sketch used
`List.tryFind`, i.e. *first-match positional*; this surface deliberately strengthens it to
order-independence — research D2). It consumes F04 `CheckRule`/`Severity` (and through F04 the
F03 `Check`/F02 `Verdict`) and F06 `ContractEntry`/`Contract.ofRules` (the drift-proof rendered
statement). It is consumed by **F08** (the effects loop that senses facts, runs probes,
dispatches reviews, and acts on the route) and **F12** (the CLI `route` command). Acting on a
route — sensing facts, running probes, dispatching reviews, recording verdicts, logging
disclosures, and the `Gate`-recomputes-from-base guarantee — is **out of scope**, F08's job. A
JSON serialization of `Route` is **deferred** (renderRoute is text-only; JSON is F08/F12's
concern, mirroring how F06 deferred its own edge emission).

## Project Structure

### Documentation (this feature)

```text
specs/007-routing-severity-modes/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D7
├── data-model.md        # Phase 1 — types, partition/precedence rules, render shape, invariants
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V40–V47
├── contracts/
│   └── Route.fsi        # Phase 1 — the routing/stakes/run-mode surface
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Kernel/
├── FS.GG.Governance.Kernel.fsproj   # add Route.* to Compile (AFTER Contract.*; Json.* unaffected)
├── Verdict.fsi / Verdict.fs         # unchanged (F02)
├── Kernel.fsi  / Kernel.fs          # unchanged (F01) — Route reuses RuleId
├── Evidence.fsi / Evidence.fs       # unchanged (F05)
├── Check.fsi   / Check.fs           # unchanged (F03) — gate statement IS Check.render (via Contract)
├── CheckRule.fsi / CheckRule.fs     # unchanged (F04) — Route reuses CheckRule/Severity
├── Freshness.fsi / Freshness.fs     # unchanged (F06)
├── Contract.fsi / Contract.fs       # unchanged (F06) — Route reuses ContractEntry/ofRules
├── Json.fsi / Json.fs               # unchanged (F06)
├── Route.fsi                        # = contracts/Route.fsi (NEW)
└── Route.fs                         # implementation against the stable signature (NEW)

tests/FS.GG.Governance.Kernel.Tests/
├── FS.GG.Governance.Kernel.Tests.fsproj   # add RouteTests.fs to Compile (before Main.fs)
├── RouteTests.fs                            # NEW: light-by-default / precedence / run-mode matrix /
│                                            #      drift-proof gate / reason mandatory / determinism (V40–V47)
├── CheckRuleTests.fs / CheckTests.fs / EvidenceTests.fs / VerdictTests.fs / FixedPointTests.fs
│                                            # unchanged
├── ContractTests.fs / FreshnessTests.fs / JsonTests.fs   # unchanged (F06)
├── SurfaceDriftTests.fs                     # unchanged; V11 now also guards the F07 surface, V12 still BCL/System.*
└── Main.fs                                  # unchanged

scripts/prelude.fsx                          # extend with a short stakesOf / route (light + fenced) / renderRoute sketch
surface/FS.GG.Governance.Kernel.surface.txt  # RE-BLESSED to include the F07 types + module
```

**Structure Decision**: additive to the single existing kernel library. The `Route` module
compiles **after** `Contract.*` because it references F06 `ContractEntry`/`Contract.ofRules`
(and through them F04 `CheckRule`/`Severity` and F03 `Check.render`) — the simplest correct
build position is immediately after `Contract.*` (before or after `Json.*` is equivalent, as
`Route` and `Json` are independent; placing it after `Json.*` keeps the edit append-only). No
new project: the roadmap (§3) keeps routing **in the kernel** so the F08 edge and F12 CLI
consume the exact `Route`/`renderRoute` with zero new dependencies. The `surface/`, `scripts/`,
and central build-props scaffolding from F01 is reused unchanged; only the baseline *content*
grows. This feature **starts M2**; unlike F06 (the M1 exit) it carries no packing/milestone
action — the kernel already packs.

## Complexity Tracking

> No unjustified Constitution Check violations. The generic `'change`/`'fact` type parameters
> are the kernel's established domain-neutrality idiom (`Check<'fact>`, `EvidenceGraph<'id>`),
> the order-independent `Fenced`-name combination reuses the F02 reason-combination convention,
> and `route` is a `Contract.ofRules` fold plus a `List.partition` — no entries required here.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
