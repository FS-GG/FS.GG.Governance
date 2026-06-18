# Implementation Plan: Check — The Reified, Inspectable Rule Algebra

**Branch**: `003-check-algebra` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-check-algebra/spec.md`

## Summary

Add the reified `Check` algebra (F03) to the existing `FS.GG.Governance.Kernel`
assembly: the domain-neutral support types (`ArtifactRef`, `Outcome`, `ProbeArg`,
`Probe<'fact>`), the closed combinator union
(`Check<'fact> = Atom | All | Any | Not | Implies | Opaque`), the readable smart
constructors / operators (`probe`, `allOf`, `anyOf`, `not'`, `==>`, `.&`, `.|`), the
proof-tree value `Explanation`, and the **six interpreters** that fold a check from a
single source: `eval`, `render`, `hash`, `explain`, `reads`, `isReified`. The whole
point is that a rule's check is *one value* — not an opaque `FactSet -> Verdict`
lambda — so the same value can be evaluated into a verdict, rendered into a contract
sentence, hashed into a cache key, and explained as a proof tree, with none of those
able to drift from what is actually enforced (see [research.md](./research.md)).

The approach: `eval` reuses the F02 `Verdict.all` / `Verdict.any` / `Verdict.negate`
combinators directly, so the Kleene three-valued semantics and order-independent
reason aggregation are **inherited, not re-implemented** (`Uncertain` survives unless a
dominating result is present). `Implies (a, b)` is *desugared* to `Any [Not a; b]` for
`eval`, but kept **positional** for `render`/`hash`. `hash` closes the
commutative-node confluence hazard (Hazard 3 of
`docs/governance-design/theory-and-composition.md`): the children of `All`/`Any` are
canonicalized by ordinal-sorting their sub-hashes before combining, while positional
structure (`Implies` sides, a probe's ordered `Args`/`Reads`) is preserved. The
algebra is deliberately **applicative, never monadic** (no `bind`) so `render`, `hash`,
`reads`, and `isReified` fold the structure **without ever executing** a probe's `Eval`
— the inspectability guarantee the entire design rests on.

The public surface is the curated [`contracts/Check.fsi`](./contracts/Check.fsi),
added to the kernel assembly. Zero new dependencies (BCL only; `SHA256` from
`System.Security.Cryptography` for the structural hash). `Check` is the **keystone**:
F04 builds the `CheckTier`/`Rule` bridge on `isReified`/`hash`/`reads`, and F06
serializes `Explanation` and folds `render` into the published contract.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** — BCL only. `eval` reuses the in-assembly F02
`Verdict` module; `hash` uses `System.Security.Cryptography.SHA256` +
`System.Text.Encoding.UTF8` (both `System.*`, allowed by the existing V12
dependency-hygiene test). Test project only: Expecto + FsCheck, already pinned (F01 D5).

**Storage**: N/A — pure values; no filesystem, network, git, or rendering library
(FR-015). `eval`/`explain` take a `FactSet<'fact>` argument but perform no I/O of their
own; only a probe's caller-supplied `Eval` touches facts, and never during
`render`/`hash`/`reads`/`isReified`.

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through the
built library / `scripts/prelude.fsx` (Principle I). FsCheck properties for hash
commutative-canonicalization, eval ↔ Kleene agreement, and the cross-fold invariant
(`explain` verdict = `eval` verdict). The reflective surface-drift test (V11) and
dependency-hygiene test (V12) extend to the `Check` surface for free once re-blessed.

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: single library (+ its test project) — additive change to the
existing `FS.GG.Governance.Kernel` — `library`.

**Performance Goals**: correctness/determinism, not throughput. Every interpreter is a
single O(n) fold over the check tree (O(n log n) where a commutative node sorts its
child hashes); no measured hot path. All six interpreters are **total** (FR-013).

**Constraints**: pure & deterministic; `render`/`hash`/`reads`/`isReified` MUST never
execute a probe `Eval` and MUST NOT require a fact set (SC-001); commutative-node hash
is permutation-invariant while positional structure is preserved (SC-002); `eval` ↔
Kleene with `Uncertain` never silently coerced (SC-003); `explain` verdict = `eval`
verdict, 100% (SC-004); `isReified` false iff an `Opaque` is present (SC-005); every
interpreter total (SC-006); zero heavy dependencies (SC-008).

**Scale/Scope**: five new public types (`ArtifactRef`, `Outcome`, `ProbeArg`,
`Probe<'fact>`, `Check<'fact>`) + the `Explanation` proof-tree type + one `Check`
module (7 builders/operators, 6 interpreters) + an `Explanation.verdict` accessor, all
in the existing kernel namespace. No new project.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after
Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `contracts/Check.fsi` drafted first; FSI sketch extends `scripts/prelude.fsx` (quickstart); semantic tests against the public surface precede `Check.fs`. `tasks.md` (F03) will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `Check.fsi` is the sole surface; `Check.fs` carries no `private`/`internal`/`public` on top-level bindings; the reflective drift test (V11) re-blessed to include the F03 types + the `Check`/`Explanation` modules (FR-016). |
| III. Idiomatic simplicity | **PASS (justified)** | Plain unions + records + total folds. **Two flagged features used, justified here:** (a) the custom operators `==>`, `.&`, `.|` — the readable rule-authoring surface that is the entire premise of the eDSL (`docs/governance-design/rule-edsl.md`); they are thin aliases for `Implies`/`All`/`Any`, add no semantics, and read like the sentences a rule enforces (FR-005). (b) `let rec` over the `Check` tree in every interpreter — genuine recursion over branching structure, which Principle III explicitly endorses ("recursion is for branching structure"), not state-hiding. `[<CompilationRepresentation(ModuleSuffix)>]` on the `Check` module is the standard type+companion-module idiom (cf. `List`/`Option`, and F02's `Verdict`). No SRTP, reflection, type providers, or CEs in the kernel. |
| IV. Elmish/MVU boundary | **N/A** | Pure, applicative value algebra — **no `bind`, no data-dependent sequencing** (FR-012), no state machine, I/O, retries, or user interaction. The MVU boundary first appears at F08 (the agent-review effects edge). |
| V. Test evidence mandatory; prefer real | **PASS** | Real `Check` values and real probes throughout; FsCheck properties for the headline order-independence and cross-fold guarantees. The "never executes" guarantee is proved with a real probe whose `Eval` throws — `render`/`hash` succeed, `eval` throws (real evidence, no mock). No synthetic evidence anticipated (no `// SYNTHETIC:` disclosures expected). |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F03; every interpreter is total and returns a result for all inputs incl. empty `All`/`Any` (FR-013) — no silent failure, no partial results, no throw from the algebra itself. |
| Change Classification | **Tier 1** | New public API surface (the `Check` algebra + six interpreters) + surface-baseline update; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs) (FR-016). |
| Engineering Constraints | **PASS** | `net10.0`; added to `FS.GG.Governance.Kernel`; `.fsi` per public module; surface baseline updated; zero new deps (SHA256 is BCL); no domain vocabulary / I/O (FR-015). Kernel still packs at F06, not here. |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the
two Principle III items are justified above, not waived).**

Decisions locked / touched by this feature (roadmap §F03, design decision #4 / Hazard
3): pins the **commutative-node hash canonicalization** (ordinal-sort child sub-hashes
for `All`/`Any`; positional for `Implies` and probe `Args`/`Reads`) and confirms `Not`
over an *evaluated* sub-verdict is total and order-free (it reuses F02 `negate`). The
`CheckTier`/`Rule` bridge, the agent-review cache machinery, `Explanation`
serialization, and the contract fold are **out of scope** — F04 and F06.

## Project Structure

### Documentation (this feature)

```text
specs/003-check-algebra/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D7
├── data-model.md        # Phase 1 — types, eval/hash/render/explain rules, invariants
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V1–V12
├── contracts/
│   └── Check.fsi        # Phase 1 — the curated public signature contract
├── checklists/
│   └── requirements.md  # spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Kernel/
├── FS.GG.Governance.Kernel.fsproj   # add Check.fsi + Check.fs to the Compile list (AFTER Kernel.*)
├── Verdict.fsi / Verdict.fs         # unchanged (F02) — eval/explain reuse Verdict.all/any/negate
├── Kernel.fsi  / Kernel.fs          # unchanged (F01) — Check reuses FactSet<'fact>
├── Check.fsi                        # = contracts/Check.fsi (NEW, compiled after Kernel.*)
└── Check.fs                         # implementation against the stable signature (NEW)

tests/FS.GG.Governance.Kernel.Tests/
├── FS.GG.Governance.Kernel.Tests.fsproj   # add CheckTests.fs to the Compile list (before Main.fs)
├── CheckTests.fs                            # NEW: V1–V11 (eval/hash/render/explain/reads/isReified/never-executes)
├── VerdictTests.fs                          # unchanged (F02)
├── FixedPointTests.fs                       # unchanged (F01)
├── SurfaceDriftTests.fs                     # unchanged; V11 now also guards the Check surface, V12 still BCL-only
└── Main.fs                                  # unchanged

scripts/prelude.fsx                          # extend with a short Check sketch (FSI design pass)
surface/FS.GG.Governance.Kernel.surface.txt  # RE-BLESSED to include the F03 types + Check/Explanation modules
```

**Structure Decision**: additive to the single existing kernel library. `Check` is a
new `Check.fsi`/`Check.fs` pair compiled **after** `Verdict.*` and `Kernel.*`, because
it depends on both — `Verdict` for the verdict it folds to, and `Kernel.FactSet<'fact>`
for the facts a probe's `Eval` consumes. No new project: the roadmap (§3) keeps the
algebra and its interpreters *in the kernel* so every adapter (F09–F11) reuses them and
supplies only its probe set, with zero new dependencies — exactly the kernel's contract
(FR-015, SC-008). The `surface/`, `scripts/`, and central build-props scaffolding stood
up at F01 is reused unchanged; only the baseline *content* grows.

## Complexity Tracking

> No unjustified Constitution Check violations. The two Principle III features (custom
> operators; `let rec` tree folds) are justified inline in the Constitution Check, not
> waived — no entries required here.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
