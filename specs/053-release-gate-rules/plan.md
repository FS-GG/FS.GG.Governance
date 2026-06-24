# Implementation Plan: Pure Release-Gate Readiness Rules Core

**Branch**: `053-release-gate-rules` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/053-release-gate-rules/spec.md`

## Summary

The evidence-reuse thread (Phase 11, F029–F052) is complete; the next open Governance thread is the
**`fsgg release` gate** (roadmap Phase 13). Following the repo's pure-core-first rhythm, this row delivers
**only the pure rule-evaluation core**: given a set of declared release rules and the typed release facts they
govern (facts **provided as input**, not sensed here), produce exactly one deterministic finding per rule
(satisfied / violated, self-explaining reason, declared severity + maturity attached) and roll those findings
up into a release verdict and exit-code basis — reusing the **existing** enforcement and verdict machinery
verbatim. Sensing the real facts, the `fsgg release` host command, and the `release.json` projection are
**following rows, out of scope here** (spec "Out of Scope").

Every release-shaped primitive already exists in the merged model but is unpopulated: F023 `Enforcement`
already recognizes the `Release` run mode and `Release` profile and derives effective severity; F014 `Config`
already carries `BlockOnRelease` maturity, the `ReleaseSurface` surface class, and the `Release` environment
class; F024 `Ship` already rolls enforced items into a `Verdict` (Pass/Fail), a disjoint Blockers/Warnings/
Passing partition, and a typed `ExitCodeBasis`. What no row has supplied is the **release-specific rule
vocabulary** and the pure evaluation that turns declared release expectations plus their governing facts into
findings the existing machinery can roll up.

The whole feature is **one small new pure library** — `FS.GG.Governance.ReleaseRules` — layered on the merged
thread (the constitution's "heavier capabilities layer on top, not into the core"). It introduces the closed
release rule-kind vocabulary the roadmap names (**version bump, package metadata, template pins, publish plan,
trusted publishing, provenance**), a typed `ReleaseFacts` input, a `ReleaseFinding`, and two pure functions:

1. **`evaluate : rules -> facts -> ReleaseFinding list`** (US1) — compare each declared rule against the
   provided fact for its kind and emit **exactly one** finding per rule, classified `Satisfied` or
   `Violated`, with a self-explaining reason and the rule's declared base severity + maturity carried through.
   A fact that is **absent or unrecoverable** yields a `Violated` finding (fail-safe, FR-005); facts not
   governed by any declared rule are ignored (no fabricated finding). Output is sorted by a stable composite
   key so it is byte-identical across runs (FR-007).
2. **`rollup : findings -> ReleaseDecision`** (US2) — derive each finding's effective severity through F023
   `deriveEffectiveSeverity` **verbatim** under the `Release` run mode and `Release` profile, then partition
   the findings into Blockers/Warnings/Passing and compute `Verdict`/`ExitCodeBasis` by **re-applying the
   exact F024 partition rule** and reusing the F024 `Verdict`/`ExitCodeBasis` result types unchanged.

The one genuinely-new design decision (resolved in [research.md](./research.md), D1) is that the rollup
**cannot call `Ship.rollup`**: that function's input is a `RouteResult` of F018 gates + F017 findings, and a
release rule is **neither** — it has no `GateId` and no unknown-path `FindingId`/`Path`. Synthesizing fake
gate identities to force a `Ship.rollup` call would *redefine* the release primitives FR-009 says to reuse and
couple release rules to F018/F017 identity. So the core reuses F023's per-item severity decision verbatim and
F024's **partition rule and result vocabulary** (`Verdict`/`ExitCodeBasis`) — the same observable three-way
classification F024 itself applies to gates/findings, applied to the release-finding domain. This is reuse of
the rule, not a fork of the frozen rollup (D1, FR-004, FR-009).

The core is **pure, total, and deterministic**: facts in, findings and verdict out, no I/O, no process, no
document (FR-007, FR-008). The committed contracts live in [contracts/](./contracts/); the entities and flow
in [data-model.md](./data-model.md); the build/exercise/test walkthrough in [quickstart.md](./quickstart.md);
the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`,
`WarnOn=3390;1182` from `Directory.Build.props`). This row adds **one** small new pure library
(`FS.GG.Governance.ReleaseRules`) and its test project. No existing project is edited; no command, no schema,
no document.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`** (FR-008,
constitution dependency-minimalism). The new library references exactly the merged cores whose primitives it
reuses: `FS.GG.Governance.Config` (F014 — `Maturity`/`BlockOnRelease`, `SurfaceId`, `SurfaceClass`/
`ReleaseSurface`, `EnvironmentClass`/`Release`), `FS.GG.Governance.Enforcement` (F023 — `Severity`, `RunMode`/
`Release`, `Profile`/`Release`, `EnforcementInput`, `EnforcementDecision`, `deriveEffectiveSeverity`), and
`FS.GG.Governance.Ship` (F024 — the `Verdict` and `ExitCodeBasis` result types reused verbatim). It does
**not** reference `Route`, `Gates`, or `Findings`: release rules are not F018 gates or F017 unknown-path
findings, so pulling those in would be dead weight and would invite the dishonest "synthesize a `RouteResult`"
design D1 rejects. Test frameworks unchanged (Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk,
YoloDev.Expecto.TestSdk).

**Storage**: None. The core reads no file and writes no file (FR-007, FR-008). Facts are supplied as in-memory
typed input; the verdict is an in-memory value. No reuse store, no `release.json`, no schema, no schema-version
bump (sensing, the host command, and the projection are following rows — spec "Out of Scope").

**Testing**: Expecto + FsCheck. New `FS.GG.Governance.ReleaseRules.Tests` drives the public FSI surface of the
packed library: (US1) one finding per declared rule with correct satisfied/violated classification across all
six rule kinds, the absent/unrecoverable-fact ⇒ `Violated` fail-safe, the duplicate-rule and ungoverned-fact
edge cases; (US2) the rollup verdict + exit-code basis + disjoint Blockers/Warnings/Passing partition over a
mixed finding set (blocking violation ⇒ Fail; all-satisfied ⇒ Pass/Clean; advisory violation ⇒ Pass with the
violation visible as a Warning); (US3) byte-identical output over a repeated evaluation (determinism) and the
output rule-kind multiset equals the declared rule-kind multiset (no-hide, no fabrication). A property test
(FsCheck) asserts `|findings| = |rules|` (SC-001) and `|Blockers|+|Warnings|+|Passing| = |findings|` (no-drop)
over random rule/fact sets. **No network, no governed repository, no process spawn** — facts are in-memory
(SC-004); no `Synthetic` literals are needed because the core consumes only its own declared input. New
surface baseline `surface/FS.GG.Governance.ReleaseRules.surface.txt` (generated via `BLESS_SURFACE`).

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. The core is platform-agnostic pure F#
(no OS-specific surface).

**Project Type**: A single pure library — a closed rule-kind vocabulary, a typed facts input, a per-rule
evaluation, and a verdict rollup, all total functions. **Principle IV does not apply**: there is no
multi-step state, no I/O, no retries, no user interaction — exactly the "single rule evaluation / fact store"
shape the constitution names as *not* needing Elmish ceremony. (Sensing, the host command, and the projection
— the rows that *do* carry I/O — are out of scope and will honor Principle IV when they arrive, mirroring the
existing `RouteCommand`/`ShipCommand` MVU boundary.)

**Performance Goals**: N/A. The cost is one F023 `deriveEffectiveSeverity` call and one classification per
declared rule, plus one stable sort — linear in the rule count. No hot path, no measured budget.

**Constraints**: Pure/total/deterministic (FR-007): facts in, findings + verdict out; no I/O, no clock, no
process, no document; byte-identical output for identical input. One-finding-per-rule (FR-001): exactly one
finding per declared rule, never dropped (FR-006) or fabricated. Fail-safe (FR-005): an absent/unrecoverable
fact ⇒ `Violated`, never silently satisfied. Verbatim composition (FR-003, FR-004, FR-009): effective
severity is F023 `deriveEffectiveSeverity` called unchanged under `Release` mode/profile; the verdict reuses
the F024 `Verdict`/`ExitCodeBasis` types and the F024 partition rule re-applied — **no** new severity scheme,
mode, or profile, and **no** edit to F023/F024/F014. Maturity-only relaxation (FR-010): whether a violated
rule blocks or warns is governed solely by its declared maturity/base-severity through the existing
enforcement levers, so relaxing a rule changes its effective severity and the blocker count but never its
satisfied/violated truth or its visibility.

**Scale/Scope**: Additive only. **New**: `src/FS.GG.Governance.ReleaseRules/` (`Model.fsi/.fs`,
`Release.fsi/.fs`, `.fsproj`), `tests/FS.GG.Governance.ReleaseRules.Tests/`,
`surface/FS.GG.Governance.ReleaseRules.surface.txt`, two `.sln` entries, a `scripts/prelude.fsx` F053 section,
the `CLAUDE.md` plan pointer. **Edited**: none of the merged cores. **Untouched (frozen)**: F023
`Enforcement`, F024 `Ship`, F014 `Config`, and every other core/golden/schema.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing the `ReleaseRules` `contracts/Model.fsi` + `contracts/Release.fsi` and a runnable `scripts/prelude.fsx` F053 transcript (evaluate a mixed rule set, show one-finding-per-rule, the fail-safe, and the rollup verdict) **before any `.fs` body**, then writing public-surface semantic tests that fail before implementation and pass after — driving the packed `ReleaseRules` surface, not private helpers. |
| II. Visibility lives in `.fsi` | PASS | Every new public symbol is declared in the curated `Model.fsi` (`ReleaseRuleKind`, `FactState`, `RuleOutcome`, `ReleaseRule`, `ReleaseFacts`, `ReleaseFinding`, `EnforcedReleaseFinding`, `ReleaseDecision`) and `Release.fsi` (`releaseRuleKindToken`, `releaseRuleKindOrdinal`, `factFor`, `evaluate`, `rollup`, `evaluateRelease`). The `.fs` files carry no `private`/`internal`/`public` modifiers (the per-rule classifier, the `EnforcementInput` builder, and the partition helper live unexported, kept off-surface by absence from the `.fsi`). A new `surface/FS.GG.Governance.ReleaseRules.surface.txt` baseline is guarded by the existing reflective drift test. |
| III. Idiomatic Simplicity | PASS | The plainest F#: `evaluate` is a `List.map` of a per-rule `match` on the fact's `FactState` followed by a stable `List.sortBy`; `rollup` is a `List.map` building an `EnforcementInput` + the verbatim `deriveEffectiveSeverity` call, then a `List.partition`-style three-way classification keyed on (base severity, effective severity) and the **same** one-line `Verdict`/`ExitCodeBasis` rule F024 states. No `mutable`, no custom operators, no SRTP, no reflection (outside tests), no type providers, no recursion-for-state, no non-trivial CEs. |
| IV. Elmish/MVU boundary | PASS (not applicable) | The core has no multi-step state, I/O, retries, user interaction, or background work — it is the constitution's named "single rule evaluation / fact store" pure-function case that explicitly does **not** need Elmish ceremony. Facts arrive as typed input; the verdict is a return value. The I/O-bearing rows (sensing, the `fsgg release` host command, the `release.json` projection) are out of scope and will honor Principle IV on the established command MVU boundary when they arrive. |
| V. Test Evidence | PASS | Semantic tests fail before the library exists and pass after, exercising the public FSI surface over real in-memory declared input (no network, no governed repository, no process — SC-004). The determinism guarantee is proven by a genuine repeated evaluation asserting byte-identical findings + verdict (SC-003); the no-hide guarantee by a multiset-equality assertion (SC-001/SC-006); the fail-safe by a fixture whose fact is absent (SC-005). No `Synthetic` evidence is involved — the core consumes only its own typed declared input, which is the real and only evidence it has. |
| VI. Observability & Safe Failure | PASS | The core is total and fails safe by construction: an absent/unrecoverable fact is the explicit `Unrecoverable` `FactState` ⇒ a `Violated` finding with a reason naming the missing expectation (FR-005), never a swallowed exception, a throw, or a silent "satisfied". Every declared rule produces a visible, self-explaining finding (FR-006); a relaxed-to-advisory violation is surfaced as a Warning, never dropped (FR-010). There is no I/O path to fail, so there is nothing to swallow. |

**Change Classification**: **Tier 1 (contracted change)** — adds public API surface (a new `ReleaseRules`
library with a curated `.fsi` pair and a surface baseline). It introduces a new pure library but **no** new
third-party dependency, **no** schema, **no** schema-version bump, and **no** edit to any frozen merged core
or golden baseline. Requires the full artifact chain: spec, plan, `.fsi`, surface baseline, test evidence,
and docs (this plan + the design artifacts).

**Engineering Constraints**: net10.0 ✅; each new public module carries a curated `.fsi` ✅; a surface
baseline is added ✅; no new third-party dependency ✅ (FSharp.Core + the already-merged F014/F023/F024
ProjectReferences); `FS.GG.Governance.*` namespace ✅; the core layers on top of the merged thread, not into a
frozen core ✅ (constitution's "heavier capabilities layer on top, not into the core"); one-way operating
rule unaffected — the core assumes no rendering package IDs, template names, target names, or layout; the
governed identity is a generic F014 `SurfaceId` supplied by the caller ✅. No violations → **Complexity
Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/053-release-gate-rules/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions (D1–D7)
├── data-model.md        # Phase 1 output — entities, the evaluate→rollup flow, the partition rule
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough (a mixed rule-set demo)
├── contracts/
│   ├── Model.fsi          # NEW ReleaseRules surface — the rule/fact/finding/decision vocabulary
│   └── Release.fsi        # NEW ReleaseRules surface — evaluate, rollup, evaluateRelease, tokens
├── checklists/          # (pre-existing) authoring checklists
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.ReleaseRules/                     # NEW (this row) — the pure release-gate core
├── Model.fsi          # curated surface: ReleaseRuleKind, FactState, RuleOutcome, ReleaseRule,
│                       # ReleaseFacts, ReleaseFinding, EnforcedReleaseFinding, ReleaseDecision
│                       # (ReleaseDecision reuses Ship.Model Verdict / ExitCodeBasis verbatim)
├── Model.fs           # the domain declarations (no access modifiers)
├── Release.fsi        # curated surface: releaseRuleKindToken, releaseRuleKindOrdinal, factFor,
│                       # evaluate, rollup, evaluateRelease
├── Release.fs         # evaluate (per-rule classify + stable sort), rollup (verbatim F023
│                       # deriveEffectiveSeverity + the re-applied F024 partition rule), evaluateRelease;
│                       # the EnforcementInput builder and the partition helper live unexported
└── FS.GG.Governance.ReleaseRules.fsproj   # ProjectReferences: Config, Enforcement, Ship;
                                            # compile order Model.fsi→Model.fs→Release.fsi→Release.fs

surface/
└── FS.GG.Governance.ReleaseRules.surface.txt          # NEW reflective baseline (generated via BLESS_SURFACE)

tests/FS.GG.Governance.ReleaseRules.Tests/             # NEW — US1 evaluate, US2 rollup, US3 determinism/no-hide
                                                       #   + an FsCheck property (one-in-one-out, no-drop)

scripts/prelude.fsx                                    # + an F053 release-rules walkthrough section
FS.GG.Governance.sln                                   # + the new ReleaseRules src + test project entries

# Untouched (frozen): F023 Enforcement, F024 Ship, F014 Config, and every other core/golden/schema.
```

**Structure Decision**: Put the new release-gate core in **one small new pure library**
(`FS.GG.Governance.ReleaseRules`), the repo idiom (≈40 focused libraries) and the constitution's "heavier
capabilities layer on top, not into the core." It references **only** the three cores whose primitives it
reuses — F014 `Config` (maturity/surface/environment vocabulary), F023 `Enforcement` (the severity algebra),
and F024 `Ship` (the `Verdict`/`ExitCodeBasis` result types) — and deliberately **not** `Route`/`Gates`/
`Findings`, because a release rule is not an F018 gate or an F017 finding (D1). The impure halves (sensing the
real facts, the `fsgg release` host command, the additive `release.json` projection) are separate following
rows, exactly the cadence the cache thread followed (pure decision core → projection → host wiring), so this
row stays a pure leaf with no MVU surface.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
