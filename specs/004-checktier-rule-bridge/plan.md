# Implementation Plan: CheckTier & Rule Bridge — Who Decides, and Reproducible Agent Reviews

**Branch**: `004-checktier-rule-bridge` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-checktier-rule-bridge/spec.md`

## Summary

Add the **CheckTier/Rule bridge** (F04) to the existing `FS.GG.Governance.Kernel`
assembly: the arbitration declaration `CheckTier = Deterministic | AgentReviewed |
HumanOnly`, the orthogonal `Severity = Advisory | Blocking`, the `SpecSource` provenance
handle, the judge identity `JudgeId`, the domain-neutral `RuleOutcome` (`Decided` /
`NeedsReview` / `Escalated`) the kernel carries, the authored `CheckRule<'fact>` record,
its smart constructors (`rule` / `blocking` / `asking`), the pure `cacheKey` function
(decision #1), and the `toRule` bridge that turns an authored rule into the kernel's
executable `Rule<'fact>` (F01). The whole point is that a check (F03) gets a **home**: a
declaration of *who is competent to decide it*, and — for an agent-reviewed rule — a
**reproducible content-hash cache key** so a stochastic judge is consulted only when its
inputs actually change.

The approach reuses F03 and F01 wholesale and adds **no new truth tables or hashing
scheme**: `toRule`'s `Deterministic` branch is `Check.eval`; the `AgentReviewed` branch
keys the review with `cacheKey (Check.hash check) (Check.reads check |> content-hash)
question` and short-circuits on a recorded verdict; the `HumanOnly` branch escalates. The
**reified-ness guardrail** (FR-006) lives in the `rule` constructor: it returns
`Error (OpaqueCannotBeDeterministic id)` when `not (Check.isReified check)` and the tier
is `Deterministic`, so an `Opaque` node can never masquerade as machine-decidable. The
cache key folds in the **judge identity** (`JudgeId.ModelId` + `Version` + the
reviewer-prompt hash of the rule's `Question`) on top of `Check.hash` and the artifact
content hashes — **locking decision #1** — which makes the re-review-on-judge-change
policy fall out for free (a changed judge changes the key, so the cache misses).

The bridge is **pure and total**: it performs no agent call and no I/O. It produces a
`RuleOutcome` *as data*; the adapter supplies a `Bridge<'fact>` record (the judge, an
artifact-content-hash lookup over the facts, and `Embed`/`Project` between `RuleOutcome`
and the adapter's `'fact`) — keeping the kernel domain-neutral and deferring the actual
review dispatch/recording to the **F08** effects interpreter. The public surface is the
curated [`contracts/CheckRule.fsi`](./contracts/CheckRule.fsi), added to the kernel
assembly. Zero new dependencies (BCL only; the same `SHA256` F03 already uses for the
structural hash). **Notes decision #2** (single-sample judge noise / aggregation) for F08
without deciding it (see [research.md](./research.md)).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** — BCL only. `toRule` reuses the in-assembly F03
`Check` interpreters and F01 `Rule`/`FactSet`/`RuleId`/`ProvenanceStep`; `cacheKey` uses
`System.Security.Cryptography.SHA256` + `System.Text.Encoding.UTF8` (both `System.*`,
already used by F03's `hash` and allowed by the existing V12 dependency-hygiene test).
Test project only: Expecto + FsCheck, already pinned (F01 D5).

**Storage**: N/A — pure values; no filesystem, network, git, or agent. The artifact
content hashes the cache key needs are read **from the supplied facts** via the
caller-supplied `Bridge.ArtifactHash` (an adapter asserts artifact-content facts), so
`toRule` and `cacheKey` perform no I/O of their own (FR-015).

**Testing**: `dotnet test`; semantic tests exercise the **public** surface through the
built library / `scripts/prelude.fsx` (Principle I). FsCheck properties for cache-key
reproducibility and per-ingredient sensitivity (SC-002), and tests for the reified-ness
refusal (SC-001), cache hit/miss + re-review (SC-003/SC-004), no-drift description
(SC-006), tier/severity orthogonality (SC-008), and totality (SC-007). The reflective
surface-drift test (V11) and dependency-hygiene test (V12) extend to the `CheckRule`
surface for free once re-blessed.

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: single library (+ its test project) — additive change to the existing
`FS.GG.Governance.Kernel` — `library`.

**Performance Goals**: correctness/determinism, not throughput. `toRule` builds a closure;
each `Apply` is one `Check.eval`/`hash`/`reads` fold plus a linear scan of the facts for a
recorded verdict; `cacheKey` is one SHA-256 over a bounded pre-image. No measured hot path.
`toRule`, `cacheKey`, and every bridged `Apply` are **total** (FR-017).

**Constraints**: pure & deterministic; `toRule` performs no agent call and no I/O
(FR-015); the `rule` constructor refuses `Deterministic` for a non-reified check (FR-006,
SC-001); the cache key is reproducible and changes iff any of {check hash, artifact
hashes, judge model id, judge version, reviewer prompt} changes (SC-002); a recorded
verdict under a stale judge is never reused (FR-013, SC-004); a Deterministic verdict is
never coerced (SC-005); the bridged description equals `Check.render` (SC-006); severity
is independent of tier and `HumanOnly` escalates regardless (SC-008); zero heavy
dependencies (SC-009).

**Scale/Scope**: seven new public types (`CheckTier`, `Severity`, `SpecSource`, `JudgeId`,
`ReviewRequest`, `RecordedReview`, `RuleOutcome`) + `CheckRule<'fact>` + `RuleRejection` +
`Bridge<'fact>` + one `CheckRule` module (3 constructors, `cacheKey`, `toRule`), all in the
existing kernel namespace. No new project.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after
Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `contracts/CheckRule.fsi` drafted first; FSI sketch extends `scripts/prelude.fsx` (quickstart); semantic tests against the public surface precede `CheckRule.fs`. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Curated `CheckRule.fsi` is the sole surface; `CheckRule.fs` carries no `private`/`internal`/`public` on top-level bindings; the reflective drift test (V11) re-blessed to include the F04 types + the `CheckRule` module (FR-018). |
| III. Idiomatic simplicity | **PASS (justified)** | Plain unions + records + total functions; `Result` for the one refusal (an allowed CE-free idiom). **No custom operators, no SRTP, no reflection, no type providers, no CEs, no `let rec`** in this feature — strictly simpler than F03. `[<CompilationRepresentation(ModuleSuffix)>]` on the `CheckRule` module is the standard type+companion-module idiom (cf. `Check`/`Verdict`). The one deliberate naming choice — the authored rule is `CheckRule<'fact>`, not `Rule<'fact>` — is forced by the already-shipped kernel `Rule<'fact>` and recorded in research D1. |
| IV. Elmish/MVU boundary | **N/A (functional core of it)** | The bridge is pure: `toRule` does **no** I/O, no agent call, no state machine. It emits a `RuleOutcome` (incl. `NeedsReview`, the review request) **as data** — exactly the "I/O represented as data, interpreted only at the edge" half of Principle IV. The `update`/effect-interpreter that dispatches the review and records the verdict is **F08**; F04 deliberately ships only the pure values it will consume. |
| V. Test evidence mandatory; prefer real | **PASS** | Real `CheckRule` values, real `Check`s, real `Bridge` instances (a tiny in-test adapter `'fact`) throughout; FsCheck for the cache-key reproducibility/sensitivity properties. Cache hit/miss proven by running a bridged rule's `Apply` over facts that do/don't carry a matching `RecordedReview` (real evidence, no mock). No synthetic evidence anticipated. |
| VI. Observability & safe failure | **PASS (scoped)** | No I/O to log in F04; every function is total and returns a result for all inputs incl. an unknown artifact (`ArtifactHash` sentinel) and an empty fact set — no silent failure, no throw from the bridge itself. |
| Change Classification | **Tier 1** | New public API surface (the tier/severity/rule bridge + cache key) + surface-baseline update; full artifact chain (spec, plan, `.fsi`, baseline, tests, docs) (FR-018). |
| Engineering Constraints | **PASS** | `net10.0`; added to `FS.GG.Governance.Kernel`; `.fsi` per public module; surface baseline updated; zero new deps (SHA-256 is BCL, already used by F03); no rendering/domain vocabulary; generic over `'fact`, adapter supplies `Bridge` (the operating rule). Kernel still packs at F06, not here. |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the one
naming choice in Principle III is forced and documented, not waived).**

Decisions locked / touched by this feature (roadmap §F04): **locks decision #1** — the
agent-review cache key = `Check.hash` + artifact content hashes (over `Check.reads`,
de-duplicated + ordinal-sorted) **+ judge model id + judge version + reviewer-prompt
hash**, with the re-review-on-judge-change policy (a changed judge changes the key →
cache miss → fresh review). **Notes decision #2** (single-sample noise — whether to
aggregate N runs / require a confidence threshold before freezing a verdict) for the F08
interpreter; F04's cache-key shape is compatible with either choice and does not decide
it. The actual agent call, the verdict recording, the contract fold, and `Explanation`
serialization are **out of scope** — F08 and F06.

## Project Structure

### Documentation (this feature)

```text
specs/004-checktier-rule-bridge/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D7
├── data-model.md        # Phase 1 — types, toRule/cacheKey rules, invariants
├── quickstart.md        # Phase 1 — FSI sketch + validation scenarios V13–V20
├── contracts/
│   └── CheckRule.fsi    # Phase 1 — the curated public signature contract
├── checklists/
│   └── requirements.md  # spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Kernel/
├── FS.GG.Governance.Kernel.fsproj   # add CheckRule.fsi + CheckRule.fs to Compile (AFTER Check.*)
├── Verdict.fsi / Verdict.fs         # unchanged (F02) — RuleOutcome/cache-hit carry a Verdict
├── Kernel.fsi  / Kernel.fs          # unchanged (F01) — toRule emits a kernel Rule<'fact>
├── Check.fsi   / Check.fs           # unchanged (F03) — toRule reuses eval/hash/reads/render/isReified
├── CheckRule.fsi                    # = contracts/CheckRule.fsi (NEW, compiled after Check.*)
└── CheckRule.fs                     # implementation against the stable signature (NEW)

tests/FS.GG.Governance.Kernel.Tests/
├── FS.GG.Governance.Kernel.Tests.fsproj   # add CheckRuleTests.fs to Compile (before Main.fs)
├── CheckRuleTests.fs                        # NEW: V13–V20 (refusal/cacheKey/hit-miss/re-review/toRule/orthogonality/totality)
├── CheckTests.fs                            # unchanged (F03)
├── VerdictTests.fs                          # unchanged (F02)
├── FixedPointTests.fs                       # unchanged (F01)
├── SurfaceDriftTests.fs                     # unchanged; V11 now also guards the CheckRule surface, V12 still BCL-only
└── Main.fs                                  # unchanged

scripts/prelude.fsx                          # extend with a short CheckRule/toRule sketch (FSI design pass)
surface/FS.GG.Governance.Kernel.surface.txt  # RE-BLESSED to include the F04 types + CheckRule module
```

**Structure Decision**: additive to the single existing kernel library. `CheckRule` is a
new `CheckRule.fsi`/`CheckRule.fs` pair compiled **after** `Check.*` (and therefore after
`Verdict.*`/`Kernel.*`), because it depends on all three — `Check` for the algebra it
bridges and folds, `Verdict` for the verdict a `RuleOutcome` carries, and `Kernel` for the
executable `Rule<'fact>` it produces and the `FactSet`/`ProvenanceStep` it emits into. No
new project: the roadmap (§3) keeps the tier model and bridge *in the kernel* so every
adapter (F09–F11) reuses them and supplies only its `Bridge<'fact>` + probe set, with zero
new dependencies. The `surface/`, `scripts/`, and central build-props scaffolding stood up
at F01 is reused unchanged; only the baseline *content* grows.

## Complexity Tracking

> No unjustified Constitution Check violations. The one naming choice (`CheckRule` vs the
> design doc's `Rule`) is forced by the existing kernel `Rule<'fact>` and documented in
> research D1 — no entries required here.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
