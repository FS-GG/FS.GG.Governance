# Implementation Plan: Explanation Output, the Drift-Proof Contract & Evidence Freshness — Making the Kernel's Reasoning Legible

**Branch**: `006-explanation-output` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-explanation-output/spec.md`

## Summary

Add the **output layer** (F06) to the existing `FS.GG.Governance.Kernel` assembly — the
three derivations that turn the kernel's in-memory reasoning into stable, portable data and
**complete Milestone M1, the first useful product**:

1. **JSON explanation.** A deterministic, round-trippable JSON serialization of F03's
   `Explanation` proof tree (and of F05's `EvidenceState` / effective-state map). It mirrors
   the proof tree's surface shape, records each atomic probe's met/unmet/unknown `Outcome`,
   carries the rolled-up `Verdict` at every node (root identical to `Check.eval`), and
   executes **no** probe — an `Opaque` node contributes its name and recorded outcome only.
2. **The drift-proof contract.** `Contract.ofRules` folds a catalog of F04 `CheckRule<'fact>`
   into one entry per rule — id, severity, spec source, and a **rendered statement** that
   *is* `Check.render` of the rule's check, not a separately authored string. Because the
   statement is the rendered selector, the contract **cannot drift** from what is enforced.
   It is emittable as readable text and as round-trippable JSON.
3. **Evidence freshness.** A pure predicate — `Freshness.decide` — over *supplied* comparable
   instants: evidence is `Fresh` exactly when it was recorded **at or after** the latest
   change instant of every artifact it covers (boundary inclusive; no covered artifacts ⇒
   `Fresh`), `Stale` otherwise.

The approach adds **no new dependency**: JSON uses `System.Text.Json` (`Utf8JsonWriter` to
emit, `JsonDocument` to parse), which ships in the `net10.0` shared framework — it is a
`System.*` assembly, so the existing V12 dependency-hygiene test (which allows `System.*`)
passes unchanged with **zero** `PackageReference`. The whole feature is **pure and total**:
it performs no I/O, reads no real artifact, and reads no clock — every explanation, rule
catalog, evidence record, and instant is supplied as a value. Discovering real modification
times, dispatching reviews, recording verdicts, and persisting/printing the JSON remain the
**F08** edge interpreter's and **F12** CLI's job (FR-013). All output is **domain-neutral**:
the `Explanation` type is non-generic (names/outcomes/verdicts, never `'fact`); contract and
evidence-map node identity is `RuleId`/a supplied `'id -> string` projection (FR-012).

This feature **decides no new locked roadmap decision**; it consumes F03 (`Explanation`,
`Check.render`, `Check.eval`), F04 (`CheckRule`, `Severity`, `SpecSource`, `RuleId`), and F05
(`EvidenceState`, `effective`), all already merged, and is consumed by F08 and the F12 CLI.
The public surface is three curated `.fsi` contracts —
[`contracts/Freshness.fsi`](./contracts/Freshness.fsi),
[`contracts/Contract.fsi`](./contracts/Contract.fsi),
[`contracts/Json.fsi`](./contracts/Json.fsi) — added to the kernel assembly with the
surface-area baseline re-blessed (FR-014).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new.** The kernel stays BCL + FSharp.Core. JSON uses
`System.Text.Json` (`Utf8JsonWriter`, `JsonDocument`, `JsonEncodedText`), part of the
`net10.0` shared framework — no `PackageReference` is added. `System.Text.Json` is a
`System.*` assembly, so V12 (`name.StartsWith "System."`) keeps passing (research D1, D7).
`Contract` reuses F03 `Check.render`; `Freshness` uses only the standard `comparison`
constraint. Test project only: Expecto + FsCheck, already pinned (F01 D5).

**Storage**: N/A — pure values; no filesystem, network, git, clock, or agent. Serialization
operates on supplied `Explanation`/`Contract`/`EvidenceState`/`Map` values; freshness on
supplied instants (FR-010, FR-013).

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through the built
library / `scripts/prelude.fsx` (Principle I). FsCheck properties for round-trip fidelity
over arbitrary explanations (SC-003), determinism across repeated serialization and
input-permutations that preserve meaning (SC-002), and inclusive-boundary freshness over
arbitrary instant lists (SC-007). Targeted tests for: root-verdict = `Check.eval` across all
six check shapes (SC-001), no-probe-executed + opaque-by-name (SC-004), contract tracks the
selector / cannot drift / total over the empty catalog (SC-005/SC-006), the six distinct
evidence tokens + effective-map round-trip (FR-011), and zero-dependency hygiene (SC-009, via
the existing V11/V12 reflective tests re-blessed for the F06 surface).

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: single library (+ its test project) — additive change to the existing
`FS.GG.Governance.Kernel` — `library`.

**Performance Goals**: correctness/determinism, not throughput. Serialization is a single
linear pass over the proof tree / catalog / map; parsing is one `JsonDocument` walk;
freshness is one `max`/compare over the covered instants — each O(size of input). No measured
hot path. All functions are **total** (FR-007, FR-013).

**Constraints**: pure & deterministic; no I/O, no clock, no real-artifact reads (FR-010,
FR-013). JSON is **byte-for-byte deterministic** — fixed object-key and array order via
explicit `Utf8JsonWriter` calls (no `Dictionary` iteration; effective-map keys ordinal-sorted
on the projected id) (FR-003, SC-002) — and **round-trips** to an equal value for
explanations, contracts, and evidence states (FR-004, FR-007, FR-011, SC-003). Serialization
executes **no** probe and emits **no** un-inspectable function — an `Opaque` node serializes
to its declared name + recorded outcome only (FR-002, SC-004). The contract statement IS
`Check.render` (drift-proof, FR-006, SC-005); `Contract.ofRules` is total over the empty
catalog (FR-007, SC-006). Freshness is inclusive at the tie and reports no-covered-artifacts
as fresh (FR-009, SC-007), and is a pure function of the supplied instants (FR-010, SC-008).
All output is domain-neutral and adds zero dependency (FR-012, SC-009).

**Scale/Scope**: three new public modules in the existing kernel namespace —
`Freshness` (one DU `Freshness` + `decide`/`isFresh`), `Contract` (one record `ContractEntry`
+ `ofRules`/`render`), and `Json` (the four serialize/parse pairs:
explanation, contract, evidence state, effective-state map). No new project.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The three `contracts/*.fsi` are drafted first; the FSI sketch extends `scripts/prelude.fsx` (quickstart); semantic tests against the public surface precede the `.fs` bodies. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `Freshness.fsi`/`Contract.fsi`/`Json.fsi` are the sole surfaces; the `.fs` files carry no `private`/`internal`/`public` on top-level bindings; the reflective drift test (V11) is re-blessed to include the F06 types + modules; V12 still BCL/`System.*`-only (FR-014). |
| III. Idiomatic simplicity | **PASS (justified)** | Plain records/DUs + total functions folding existing values. `System.Text.Json`'s low-level `Utf8JsonWriter`/`JsonDocument` is **standard-library**, not a flagged complex feature (no custom operators, no SRTP, no reflection in the kernel, no type providers, no non-trivial CEs). Reflective serialization of F# unions is deliberately **rejected** in favour of explicit writer calls so determinism + the tag-discriminated round-trip are guaranteed (research D1/D4). A `let rec` walk over the recursive `Explanation`/`Check` is the idiomatic tree fold (Principle III endorses `let rec` for tree walks). |
| IV. Elmish/MVU boundary | **N/A (pure derivation)** | No multi-step state, no I/O, no retries, no agent call, no clock, no background work. Every function maps supplied values to a `string`/`Map`/`Freshness`. There is no `Model`/`Msg`/`Cmd` because there is no workflow — exactly the "simple pure function … explanation formatter" the principle exempts. Reading real mtimes, persisting JSON, and printing are the F08/F12 edge's job, modelled there. |
| V. Test evidence mandatory; prefer real | **PASS** | Real `Explanation`/`CheckRule`/`EvidenceGraph` values built from real checks and declared states throughout; FsCheck for the round-trip, determinism, and freshness *properties*. No synthetic evidence anticipated — the inputs ARE real kernel values. |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F06. Serialization is total and deterministic; **parsing** of the kernel's own emitted JSON is total. Malformed externally-supplied JSON fails fast with an explicit exception carrying the offending position (it never silently produces a wrong value) — distinguishing malformed input from a tool defect (Principle VI); round-trip of kernel-emitted JSON never throws. |
| Change Classification | **Tier 1** | New public API surface (the JSON serialization, the contract fold, the freshness predicate) + surface-baseline update; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs) (FR-014). |
| Engineering Constraints | **PASS** | `net10.0`; added to `FS.GG.Governance.Kernel`; `.fsi` per public module; surface baseline updated; **zero new deps** (`System.Text.Json` from the shared framework, no `PackageReference`); no rendering/domain vocabulary; node identity generic / via a supplied projection (the operating rule). This feature is the **M1 exit**: packing the kernel to `~/.local/share/nuget-local/` is the milestone action (Phase 2 task; mechanics are a tasks concern, not a spec requirement). |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the
`Utf8JsonWriter`/`JsonDocument` use is standard-library JSON and the `let rec` tree walk +
`'instant`/`'id : comparison` constraints are ordinary F# idioms, not waived complexity).**

Decisions locked / touched by this feature (roadmap §F06): **decides no new locked
decision.** It consumes F03's `Explanation`/`render`/`eval` (the single-source folds that
make the contract drift-proof and the explanation faithful), F04's `CheckRule`/`Severity`/
`SpecSource`/`RuleId`, and F05's `EvidenceState`/`effective`. It is consumed by F08 (which
emits these outputs at the edge) and the F12 CLI (`explain`/`contract`/evidence-report).
Reading real artifacts/mtimes, dispatching reviews, recording verdicts, and persisting JSON
are **out of scope** — F08 and F12. An absolute max-age/TTL freshness notion is explicitly
**deferred** (spec Assumptions) — this is the simple causal model only.

## Project Structure

### Documentation (this feature)

```text
specs/006-explanation-output/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D8
├── data-model.md        # Phase 1 — types, JSON schema, freshness/contract rules, invariants
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V31–V39
├── contracts/
│   ├── Freshness.fsi    # Phase 1 — the freshness predicate surface
│   ├── Contract.fsi     # Phase 1 — the drift-proof contract fold surface
│   └── Json.fsi         # Phase 1 — the JSON serialize/parse surface
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Kernel/
├── FS.GG.Governance.Kernel.fsproj   # add Freshness.*, Contract.*, Json.* to Compile (AFTER CheckRule.*)
├── Verdict.fsi / Verdict.fs         # unchanged (F02)
├── Kernel.fsi  / Kernel.fs          # unchanged (F01)
├── Evidence.fsi / Evidence.fs       # unchanged (F05) — Freshness/Json reuse EvidenceState
├── Check.fsi   / Check.fs           # unchanged (F03) — Json reuses Explanation; Contract reuses render
├── CheckRule.fsi / CheckRule.fs     # unchanged (F04) — Contract reuses CheckRule/Severity/SpecSource
├── Freshness.fsi                    # = contracts/Freshness.fsi (NEW)
├── Freshness.fs                     # implementation against the stable signature (NEW)
├── Contract.fsi                     # = contracts/Contract.fsi (NEW, compiled after CheckRule.*)
├── Contract.fs                      # implementation (NEW)
├── Json.fsi                         # = contracts/Json.fsi (NEW, compiled LAST)
└── Json.fs                          # implementation (NEW)

tests/FS.GG.Governance.Kernel.Tests/
├── FS.GG.Governance.Kernel.Tests.fsproj   # add F06 test files to Compile (before Main.fs)
├── FreshnessTests.fs                       # NEW: inclusive boundary / empty-covered / purity (V37–V38)
├── ContractTests.fs                        # NEW: per-rule entry / drift-proof / empty-catalog (V35–V36)
├── JsonTests.fs                            # NEW: mirror-shape / determinism / round-trip / opaque / evidence (V31–V34, V39)
├── CheckRuleTests.fs / CheckTests.fs / EvidenceTests.fs / VerdictTests.fs / FixedPointTests.fs  # unchanged
├── SurfaceDriftTests.fs                    # unchanged; V11 now also guards the F06 surface, V12 still BCL/System.*
└── Main.fs                                 # unchanged

scripts/prelude.fsx                          # extend with a short explain→JSON / contract / freshness sketch
surface/FS.GG.Governance.Kernel.surface.txt  # RE-BLESSED to include the F06 types + modules
```

**Structure Decision**: additive to the single existing kernel library. The three F06 modules
compile **after** `CheckRule.*` because `Contract` references `CheckRule`/`Severity`/
`SpecSource` (F04) and `Json` references `Contract`'s `ContractEntry`, F03's `Explanation`,
and F05's `EvidenceState` — so the build order is `Freshness.*`, then `Contract.*`, then
`Json.*` last. `Freshness` depends on nothing but the standard `comparison` constraint but is
grouped with the other F06 output modules at the end of the build. No new project: the
roadmap (§3) keeps the output layer **in the kernel** so the F08 edge and F12 CLI emit these
exact serializations with zero new dependencies. The `surface/`, `scripts/`, and central
build-props scaffolding from F01 is reused unchanged; only the baseline *content* grows.
Completing this feature is the **M1 exit** — a Phase 2 task packs `FS.GG.Governance.Kernel` to
`~/.local/share/nuget-local/` (flipping `IsPackable` for the kernel); the packing mechanics
are a tasks concern, not part of this feature's code surface.

## Complexity Tracking

> No unjustified Constitution Check violations. `System.Text.Json`'s low-level
> writer/document API is standard-library JSON (the spec's chosen "runtime's built-in
> facilities"), and the `let rec` tree walk plus the `'instant : comparison` / `'id :
> comparison` constraints are ordinary F# idioms — no entries required here.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
