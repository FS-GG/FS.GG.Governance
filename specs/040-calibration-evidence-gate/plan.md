# Implementation Plan: Judge-vs-Human Calibration Evidence — the Beyond-Advisory-Maturity Gate

**Branch**: `040-calibration-evidence-gate` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/040-calibration-evidence-gate/spec.md`

## Summary

Land **Phase 12 (Agent-Reviewed Rule Guardrails)**'s **sixth and final** line — *"Define judge-vs-human calibration
evidence before any agent-reviewed rule can block protected boundaries"* (design `docs/initial-implementation-plan.md`;
`docs/initial-design.md`, *Optional agent-reviewed constraints*, the **calibration-debt** row: *"Agent-reviewed rule
packs need periodic judge-vs-human comparison before they can move beyond advisory maturity"*). F035
(`AgentReviewKey`), F036 (`VerdictReuse`), F037 (`PromptIsolation`), F038 (`ReviewRecord`), and F039
(`AdvisoryPromotion`) landed the phase's first five lines. This row delivers the design's **calibration-evidence
decision primitive** as a pure **decision** core: a typed calibration-decision value and a single total,
deterministic function that decides whether an agent-reviewed rule pack's reviewer has accumulated **enough
judge-vs-human calibration evidence** to move **beyond advisory maturity** — and which **defaults to uncalibrated**
(stays advisory) whenever the evidence is absent or insufficient.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F039 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new packable pure core,
**`FS.GG.Governance.Calibration`** — the **agent-review analogue of F023 `deriveEffectiveSeverity` / F030 `decide` /
F036 `lookup` / F039 `decide`**: a *decision* core (not a record core) that maps supplied facts to a named outcome
and derives **no** byte-stable identity.

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| 3 — prompt isolation (F037) | `PromptIsolation` | *How is the request shaped so the artifact is data, not an instruction?* |
| 4 — review record (F038) | `ReviewRecord` | *What was this completed review, for the audit trail?* |
| 5 — advisory promotion (F039) | `AdvisoryPromotion` | *May **this finding** be promoted from advisory to block-eligible, and on which basis?* |
| **6 — calibration evidence (this row)** | **`Calibration`** | ***Has this agent reviewer accumulated enough judge-vs-human calibration evidence to move beyond advisory maturity at all?*** |

The core makes **no verdict** and runs **no comparison**: the judge verdicts and human verdicts have already been
produced elsewhere; this core consumes their **already-classified agreement** as supplied facts. It invokes **no
model / agent / network**, reads **no clock / filesystem / git / environment**, computes **no hash from raw bytes**,
runs **no actual review** and **no actual human comparison**, makes **no cache lookup / verdict invalidation**
(F035 / F036), builds **no review record** (F038, consumed as input not produced), produces / interprets / re-scores
**no verdict** (FR-007), does not re-implement F039 per-finding eligibility (composes *with* it downstream), derives
**no effective severity / enforcement verdict** (F023 / F024), performs **no persistence / JSON projection**, and adds
**no CLI**. Its sole output is the typed `CalibrationDecision` value.

The core provides (full vocabulary in [data-model.md](./data-model.md); the signatures + laws in
[contracts/calibration-api.md](./contracts/calibration-api.md)):

- **`AgreementClassification`** = `Agreeing | Disagreeing` — the already-classified per-sample fact, the **only**
  thing the core consumes from a comparison sample (FR-002, FR-007, research D4).
- **`JudgeIdentity`** = `{ Model: ModelId; ModelVersion: ModelVersion; PromptHash: ReviewerPromptHash }` — the
  per-judge calibration scope, reusing **F035** `ModelId` / `ModelVersion` / `ReviewerPromptHash` **verbatim**
  (FR-009 SHOULD; the *"calibration is per judge identity, not global"* contract — research D3).
- **`ComparisonSample`** = `{ JudgeVerdict: RecordedVerdict; HumanVerdict: RecordedVerdict; Agreement:
  AgreementClassification }` — one judge-vs-human comparison, reusing **F038** `RecordedVerdict` **verbatim** for the
  two opaque verdicts (FR-009 MAY); the verdicts are never interpreted (FR-007), only the `Agreement` is consumed
  (research D3/D4).
- **`SampleCount`** = `SampleCount of int` and **`AgreementLevel`** = `AgreementLevel of int` — the summarised
  observation newtypes (supplied on the edge's own opaque scale; mirror F039's `ConfirmationCount` /
  `ConfidenceThreshold` discipline — research D4).
- **`CalibrationEvidence`** = `{ Scope: JudgeIdentity; Samples: ComparisonSample list; ObservedAgreement:
  AgreementLevel }` — the collection of comparison samples scoped to one identity. The **sample count is derived**
  from `List.length Samples` (the honest count of evidence actually present); the **agreement level is supplied** (a
  *level* is a scale choice the edge owns — research D4).
- **`CalibrationThresholds`** = `{ MinimumSamples: SampleCount; MinimumAgreement: AgreementLevel }` — the supplied
  policy levers (FR-003).
- **`CalibrationMetrics`** = `{ ObservedSamples: SampleCount; RequiredSamples: SampleCount; ObservedAgreement:
  AgreementLevel; RequiredAgreement: AgreementLevel }` — the satisfied metrics named by a *calibrated* outcome (the
  no-hide record; `RequiredSamples` is the **effective** minimum applied, `max(MinimumSamples, 2)` — research D5/D7).
- **`CalibrationReason`** = `NoCalibrationEvidence | TooFewSamples of SampleCount * SampleCount | AgreementBelowThreshold
  of AgreementLevel * AgreementLevel` — the no-hide attribution carried by an *uncalibrated* outcome (FR-005, research
  D5). **No `Stale` case** — recency is deliberately not modelled here (research D8).
- **`CalibrationDecision`** = `Uncalibrated of CalibrationReason | Calibrated of CalibrationMetrics` — the two-outcome
  gate verdict; *calibrated* carries its satisfied metrics, so a threshold-unmet calibration is **unrepresentable**
  (FR-001, research D6).
- **`Calibration.decide`** — the single total, deterministic decision `CalibrationThresholds -> CalibrationEvidence ->
  CalibrationDecision` (FR-003).
- **`Calibration.calibrationReason` / `calibrationMetrics` / `isCalibrated` / `observedSampleCount` / `sampleCountValue`
  / `agreementValue`** — the small projection / unwrap helpers for audit and tests.

The core reuses **F035** identity vocabulary (`ModelId` / `ModelVersion` / `ReviewerPromptHash` from
`FS.GG.Governance.AgentReviewKey.Model`) for the per-judge scope (FR-009 SHOULD) and **F038** `RecordedVerdict` (from
`FS.GG.Governance.ReviewRecord.Model`) for the per-sample opaque verdicts (FR-009 MAY), introducing only the minimal
new vocabulary the row needs (the agreement classification, the judge-identity scope record, the comparison sample,
the two summarised-observation newtypes, the evidence/thresholds/metrics records, the calibration reason, and the
calibration-decision value). The merged cores and their `surface/*.surface.txt` baselines are **untouched**;
`dotnet build` / `dotnet test` over existing projects stays unchanged, and the new project + its test project are
purely additive (SC-007).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` inherited
from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test project.

**Primary Dependencies**: Two `ProjectReference`s — **`FS.GG.Governance.AgentReviewKey`** (F035, for the reused
`ModelId` / `ModelVersion` / `ReviewerPromptHash` identity vocabulary) and **`FS.GG.Governance.ReviewRecord`** (F038,
for the reused opaque `RecordedVerdict` token). The transitive pure cores (`PromptIsolation`, `SensedMetadata`,
`FreshnessKey`, `Config`) arrive through F038 but are unused by this core (the F039 *"transitive-but-unused"*
pattern). **No new third-party `PackageReference`** (FR-011): the decision is plain pattern matching + integer
comparison + `FSharp.Core` + the two reused tokens. Test frameworks already on the central feed
(`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**,
**YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the decision is an in-value result of supplied data.
The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`Calibration.decide` / projections / unwrappers and
the `Model` types) over real, literally-constructible values (Principle V — every value is a genuine typed token: real
F035 `ModelId` / `ModelVersion` / `ReviewerPromptHash`, real F038 `RecordedVerdict`, literal counts/levels; no mock,
no clock read, no model invoked, no file read, no bytes hashed, no human consulted). Concerns: (1) **uncalibrated by
default** — empty samples ⇒ `Uncalibrated NoCalibrationEvidence`; too-few-samples ⇒ `Uncalibrated (TooFewSamples …)`;
agreement-below-threshold ⇒ `Uncalibrated (AgreementBelowThreshold …)`; self-confidence never calibrates (no input
admits it) (SC-001, US1); (2) **calibrated on sufficient evidence, metrics named** — count ≥ effective-min **and**
agreement ≥ threshold ⇒ `Calibrated` naming the satisfied metrics (SC-002, US2); (3) **inclusive `>=` with the
no-single-sample floor** — the basis is satisfied exactly when `count >= max(min, 2) && observed >= required`,
verified across counts and agreement levels below, equal to, and above their thresholds, and never for a lone sample
(SC-003, edge cases); (4) **totality** — a decision is returned and never throws across the full cross-product of
sample-count (zero, one, many) and agreement-level (below, equal, above) (SC-004, US3); (5) **determinism / purity** —
equal evidence ⇒ equal decision under changed cwd / time / filesystem, no I/O (SC-005, US3); (6)
**necessary-not-sufficient** — a `Calibrated` value carries no blocking action and no enforcement verdict (SC-006,
US3); (7) **calibrated-unrepresentable-without-metrics** — `Calibrated` always carries `CalibrationMetrics` (FR-001);
(8) **surface drift + scope hygiene** — the assembly references only `AgentReviewKey` / `ReviewRecord` (+ allowed
transitive cores) (Principle II, SC-007). Uncalibrated-default, totality, determinism, metrics-named, and the
two-threshold comparator laws are FsCheck properties; the worked examples are pinned to
[contracts/calibration-api.md](./contracts/calibration-api.md), plus the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **uncalibrated-by-default safety, totality, determinism, and the correct
two-threshold logic**, not latency; the decision is a small computation over a handful of supplied facts (Spec
Assumptions: *"Determinism is the contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-006): reads no clock, filesystem, git, environment, or network;
invokes no model / agent; runs no review; performs no human comparison at runtime; computes no hash from raw bytes;
makes no cache-key / verdict-store / lookup / invalidation operation; builds no review record; measures no elapsed
time; spawns no process; persists nothing. Treats each comparison sample's judge and human verdicts as opaque facts —
never produced, interpreted, compared, re-scored, or thresholded (FR-007). A *calibrated* decision is
necessary-not-sufficient: it carries no blocking action and asserts no protected-boundary block (FR-008). Identical
supplied inputs always yield an identical decision. The merged cores and baselines are not modified (FR-009 / SC-007).

**Scale/Scope**: One new `src/` library (`Calibration` — `Model.fsi/fs` + `Calibration.fsi/fs`); one new test
project; one new surface baseline `surface/FS.GG.Governance.Calibration.surface.txt`; two solution entries; a short
`scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer. Zero changes to
existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `Calibration.fsi` and exercised in `scripts/prelude.fsx` (a new F040 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers, and the internal comparator helper stays unexposed by its absence from the `.fsi`. A new `surface/FS.GG.Governance.Calibration.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F039 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | Plain records + single-case newtypes + three small unions; the decision is a short `if/elif` ladder over a derived count and two supplied integers (no SRTP, reflection outside the surface test, custom operators, type providers, or non-trivial CEs). The reused tokens (`ModelId` etc., `RecordedVerdict`) are opened, not re-modeled (research D3). |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — a pure total decision over supplied values. Like F023/F030/F036/F039, this is a pure decision core needing no MVU ceremony. The *actual* review, the *actual* human comparison, counting samples over time, and sensing recency are a later host edge (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (real F035 identity tokens, real F038 `RecordedVerdict`, literal counts/levels); no clock read, no model invoked, no file read, no bytes hashed, no human consulted, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The function is total: no exception, no swallowed failure, no silent drop. Every combination — no evidence, too few samples, a lone sample, agreement below/at/above threshold — is an ordinary named decision (Edge Cases), and the *uncalibrated* outcome always names its reason and the *calibrated* outcome always names its satisfied metrics (the no-hide rule). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F035 identity + F038 `RecordedVerdict` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-011); references only the sibling pure cores `AgentReviewKey` (F035) + `ReviewRecord` (F038) — and their transitive pure cores `PromptIsolation` / `SensedMetadata` / `FreshnessKey` / `Config`, unused here — no git / filesystem scanning / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A (no
stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The two sibling references (research
D3) reuse the F035 identity vocabulary the spec's FR-009 SHOULD names and the F038 `RecordedVerdict` its MAY names,
pull in nothing impure, and are the only cross-core coupling; the leaner *identity-only* footprint was the considered
alternative (research D3).

## Project Structure

### Documentation (this feature)

```text
specs/040-calibration-evidence-gate/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D8 + the uncalibrated-default / two-threshold / no-hide facts
├── data-model.md        # Phase 1 — AgreementClassification, JudgeIdentity, ComparisonSample, SampleCount,
│                        #            AgreementLevel, CalibrationEvidence, CalibrationThresholds, CalibrationMetrics,
│                        #            CalibrationReason, CalibrationDecision (reuses F035 identity + F038 RecordedVerdict)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   └── calibration-api.md   # the public signatures + their laws (uncalibrated-default, metrics-named, comparator) + the scope guard
├── checklists/          # (if present) spec-quality checklist
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.Calibration/                      # NEW — the pure calibration-evidence decision core
├── Model.fsi                                          # NEW — AgreementClassification, JudgeIdentity, ComparisonSample,
│                                                      #       SampleCount, AgreementLevel, CalibrationEvidence,
│                                                      #       CalibrationThresholds, CalibrationMetrics, CalibrationReason,
│                                                      #       CalibrationDecision (sole public surface; reuses F035 + F038 verbatim)
├── Model.fs                                           # NEW — the matching type defns (no access modifiers)
├── Calibration.fsi                                    # NEW — decide / calibrationReason / calibrationMetrics / isCalibrated /
│                                                      #       observedSampleCount / sampleCountValue / agreementValue
├── Calibration.fs                                     # NEW — the pure, total decision body + the comparator helper (private by omission)
└── FS.GG.Governance.Calibration.fsproj                # NEW — packable; references AgentReviewKey + ReviewRecord; BCL + FSharp.Core

tests/FS.GG.Governance.Calibration.Tests/              # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── UncalibratedDefaultTests.fs                         # NEW — US1: no evidence ⇒ NoCalibrationEvidence; too-few ⇒ TooFewSamples;
│                                                      #       agreement-below ⇒ AgreementBelowThreshold; self-confidence never calibrates (SC-001)
├── CalibratedTests.fs                                  # NEW — US2: thresholds met ⇒ Calibrated naming the satisfied metrics (SC-002)
├── ComparatorTests.fs                                  # NEW — SC-003: count vs effective-min + agreement vs threshold below/equal/above + no-single-sample floor
├── TotalityTests.fs                                    # NEW — US3: a decision always returned, never throws, across the cross-product (SC-004)
├── DeterminismTests.fs                                 # NEW — US3: equal evidence ⇒ equal decision under changed cwd/time/fs; no I/O (SC-005)
├── NecessaryNotSufficientTests.fs                      # NEW — US3: Calibrated carries no blocking action / no enforcement verdict (SC-006)
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + AgentReviewKey/ReviewRecord-only scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.Calibration.Tests.fsproj           # NEW — references Calibration (+ AgentReviewKey/ReviewRecord for the tokens); test packages

surface/FS.GG.Governance.Calibration.surface.txt        # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F040 FSI section (design-first proof)
FS.GG.Governance.sln                                    # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.Calibration` (the established
one-new-minimal-core-per-row rhythm, research D1), compiled `Model → Calibration`, referencing the sibling pure cores
`AgentReviewKey` (F035) and `ReviewRecord` (F038) only to reuse the identity vocabulary and the opaque
`RecordedVerdict` token verbatim (research D3). A sibling test project exercises the public surface with real literal
values. The library is additive: no existing `src/`, `surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
