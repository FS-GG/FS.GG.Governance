---
description: "Task list for 040-calibration-evidence-gate implementation"
---

# Tasks: Judge-vs-Human Calibration Evidence — the Beyond-Advisory-Maturity Gate

**Feature branch**: `040-calibration-evidence-gate`
**Spec**: `specs/040-calibration-evidence-gate/spec.md`
**Plan**: `specs/040-calibration-evidence-gate/plan.md`

**Input**: Design documents from `/specs/040-calibration-evidence-gate/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/calibration-api.md

**Tests**: Included and mandatory. This repo's Constitution Principle V makes test evidence a gate, and the spec is
itself an uncalibrated-by-default / two-threshold / no-single-sample-floor / inclusive-comparator / no-hide / totality /
determinism contract — the tests *are* the deliverable's proof. Every value is a real, literally-constructible typed
token (real F035 `ModelId` / `ModelVersion` / `ReviewerPromptHash`, real F038 `RecordedVerdict`, literal counts/levels);
no mock, no clock read, no model invoked, no file read, no bytes hashed, no human consulted.

**Organization**: Tasks are grouped by phase (sequential) and tagged by user story (`[US1]`/`[US2]`/`[US3]`) for
traceability. `[P]` marks a task with no dependency on another incomplete task in its phase.

**Tier**: The whole feature is **Tier 1** (new public API surface + new `surface/*.surface.txt` baseline, no new
third-party dependency). No per-task tier annotations needed — all tasks share the feature tier.

**Elmish/MVU**: **Not applicable** — a pure, total, deterministic decision over supplied values; no state, no I/O, no
workflow (plan Constitution Check, Principle IV = N/A). No `Model`/`Msg`/`Effect`/`update`/interpreter tasks. The
*actual* review, the *actual* human comparison, counting samples over time, and sensing recency are a later host edge,
out of scope.

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale on the task line)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (project skeleton, no behavior)

**Purpose**: Create the new library + test project so everything compiles and the solution restores. No semantics yet.

- [X] T001 Create `src/FS.GG.Governance.Calibration/FS.GG.Governance.Calibration.fsproj` — SDK-style,
  `RootNamespace`/`PackageId` `FS.GG.Governance.Calibration`, `Version` `0.1.0`, `IsPackable=true` (override
  `Directory.Build.props` like AdvisoryPromotion/ReviewRecord/AgentReviewKey). `<Compile>` order: `Model.fsi`,
  `Model.fs`, `Calibration.fsi`, `Calibration.fs`. **Two** `<ProjectReference>`s — to
  `../FS.GG.Governance.AgentReviewKey/FS.GG.Governance.AgentReviewKey.fsproj` (F035, provides `ModelId` / `ModelVersion`
  / `ReviewerPromptHash`) and `../FS.GG.Governance.ReviewRecord/FS.GG.Governance.ReviewRecord.fsproj` (F038, provides
  `RecordedVerdict`); the transitive pure cores `PromptIsolation` / `SensedMetadata` / `FreshnessKey` / `Config` arrive
  through F038 but are unused here (the F039 transitive-but-unused pattern; plan D1/D3, FR-009). **No third-party
  `PackageReference`** (FR-011). Add a header comment mirroring the AdvisoryPromotion `.fsproj`: pure total decision
  core; reuses F035 identity vocabulary + F038 `RecordedVerdict` verbatim; no
  VerdictReuse/AdvisoryPromotion/Gates/Snapshot/Enforcement/Findings/host/CLI coupling.
- [X] T002 [P] Create `tests/FS.GG.Governance.Calibration.Tests/FS.GG.Governance.Calibration.Tests.fsproj` —
  `IsPackable=false`, `GenerateProgramFile=false`; `<PackageReference>`s: `Expecto`, `Expecto.FsCheck`, `FsCheck`,
  `Microsoft.NET.Test.Sdk`, `YoloDev.Expecto.TestSdk` (versions from `Directory.Packages.props`, no new package);
  `<ProjectReference>`s to the new core and to `FS.GG.Governance.AgentReviewKey` + `FS.GG.Governance.ReviewRecord` (for
  real `ModelId`/`ModelVersion`/`ReviewerPromptHash` and `RecordedVerdict` literals). `<Compile>` order: `Support.fs`,
  `UncalibratedDefaultTests.fs`, `CalibratedTests.fs`, `ComparatorTests.fs`, `TotalityTests.fs`, `DeterminismTests.fs`,
  `NecessaryNotSufficientTests.fs`, `SurfaceDriftTests.fs`, `Main.fs`.
- [X] T003 Add both projects to `FS.GG.Governance.sln` (the `src` and `tests` solution folders), with fresh GUIDs and
  the standard Debug/Release `GlobalSection` configuration rows, matching the existing entries.

**Checkpoint**: `dotnet restore FS.GG.Governance.sln` succeeds; the solution lists both new projects.

---

## Phase 2: Foundational (the `.fsi` contracts, FSI proof, compiling stubs) — BLOCKS all stories

**Purpose**: Draft the sole public surface (`.fsi`), prove it in FSI (Principle I), and add stub `.fs` bodies so the
library and tests compile and tests can FAIL before implementation. **⚠️ No story work begins until this phase is
complete.**

- [X] T004 Write `src/FS.GG.Governance.Calibration/Model.fsi` — the SOLE public surface for the types (data-model.md):
  `open FS.GG.Governance.AgentReviewKey.Model` (brings `ModelId` / `ModelVersion` / `ReviewerPromptHash`, reused
  verbatim) and `open FS.GG.Governance.ReviewRecord.Model` (brings `RecordedVerdict`, reused verbatim); the closed
  two-case `AgreementClassification = Agreeing | Disagreeing` (FR-002); the per-judge scope record `JudgeIdentity = {
  Model: ModelId; ModelVersion: ModelVersion; PromptHash: ReviewerPromptHash }` (FR-009 SHOULD); the `ComparisonSample =
  { JudgeVerdict: RecordedVerdict; HumanVerdict: RecordedVerdict; Agreement: AgreementClassification }` (FR-009 MAY); the
  two summarised-observation newtypes `SampleCount of int` and `AgreementLevel of int` (FR-004); the `CalibrationEvidence
  = { Scope: JudgeIdentity; Samples: ComparisonSample list; ObservedAgreement: AgreementLevel }` (the sample count is
  derived from `Samples`, the agreement level is supplied); the `CalibrationThresholds = { MinimumSamples: SampleCount;
  MinimumAgreement: AgreementLevel }` (FR-003); the `CalibrationMetrics = { ObservedSamples: SampleCount;
  RequiredSamples: SampleCount; ObservedAgreement: AgreementLevel; RequiredAgreement: AgreementLevel }` (the no-hide
  success record; `RequiredSamples` is the **effective** minimum `max(MinimumSamples, 2)`); the `CalibrationReason =
  NoCalibrationEvidence | TooFewSamples of observed: SampleCount * required: SampleCount | AgreementBelowThreshold of
  observed: AgreementLevel * required: AgreementLevel` (FR-005 — **no `Stale` case**, recency deferred per research D8);
  and the `CalibrationDecision = Uncalibrated of CalibrationReason | Calibrated of CalibrationMetrics` (FR-001 — a
  threshold-unmet calibration is unrepresentable because `Calibrated` always carries metrics). Curated doc comments in
  the AgentReviewKey/ReviewRecord `.fsi` style: `RecordedVerdict` and the identity tokens are opaque supplied facts (no
  validation, no parsing, no dereferencing); the model's own self-confidence is **not** a case (FR-002); the verdicts are
  never produced/interpreted/re-scored — only `Agreement` is consumed (FR-007); calibration is per judge identity, not
  global. No access modifiers will appear in the matching `.fs`.
- [X] T005 Write `src/FS.GG.Governance.Calibration/Calibration.fsi` — the SOLE public surface for the operations
  (contracts/calibration-api.md): `val decide: thresholds: CalibrationThresholds -> evidence: CalibrationEvidence ->
  CalibrationDecision`; `val calibrationReason: decision: CalibrationDecision -> CalibrationReason option`; `val
  calibrationMetrics: decision: CalibrationDecision -> CalibrationMetrics option`; `val isCalibrated: decision:
  CalibrationDecision -> bool`; `val observedSampleCount: evidence: CalibrationEvidence -> SampleCount`; `val
  sampleCountValue: count: SampleCount -> int`; `val agreementValue: level: AgreementLevel -> int`. Doc comments state
  purity/totality/determinism and the laws (`decide` is uncalibrated-by-default and calibrated iff `observed >=
  effectiveMin && obs >= req`, naming the satisfied metrics on success and the reason on hold, L-D1..L-D15; the effective
  minimum is `max(MinimumSamples, 2)` so a lone sample never calibrates; the projections L-P1..L-P3; `observedSampleCount
  = SampleCount (List.length Samples)` L-O1; the unwrappers L-U1/L-U2; reads no clock/filesystem/git/environment/network,
  invokes no model, hashes no bytes, runs no review, performs no human comparison, makes no cache/verdict-store/
  lookup/invalidation, builds no review record). The internal comparator helper is **absent** from the `.fsi` (private by
  omission, Principle II).
- [X] T006 Add `src/FS.GG.Governance.Calibration/Model.fs` and `src/FS.GG.Governance.Calibration/Calibration.fs` — real
  type definitions in `Model.fs` (the two unions, the two newtypes, the four records are plain data, define them fully);
  `decide`, `calibrationReason`, `calibrationMetrics`, `isCalibrated`, `observedSampleCount`, `sampleCountValue`,
  `agreementValue` as `failwith "not implemented"` stubs in `Calibration.fs`. No `private`/`internal`/`public` modifiers
  (Principle II). Confirm `dotnet build src/FS.GG.Governance.Calibration/...` is clean under `TreatWarningsAsErrors`.
- [X] T007 [P] Add the F040 design-first section to `scripts/prelude.fsx` — `#r` the new Debug DLL plus the
  AgentReviewKey and ReviewRecord DLLs; `open FS.GG.Governance.AgentReviewKey.Model`, `open
  FS.GG.Governance.ReviewRecord.Model`, `open FS.GG.Governance.Calibration.Model`, and `open
  FS.GG.Governance.Calibration`; construct the quickstart.md / contract worked examples as literal `CalibrationEvidence`
  / `CalibrationThresholds` and `printfn` the intended `decide` results against `T = { MinimumSamples = SampleCount 3;
  MinimumAgreement = AgreementLevel 80 }`: no samples ⇒ `Uncalibrated NoCalibrationEvidence`; 1 sample / agreement 100 ⇒
  `Uncalibrated (TooFewSamples (SampleCount 1, SampleCount 3))` (lone sample never calibrates); 2 samples / 100 ⇒
  `Uncalibrated (TooFewSamples (SampleCount 2, SampleCount 3))`; 3 samples / 79 ⇒ `Uncalibrated (AgreementBelowThreshold
  (AgreementLevel 79, AgreementLevel 80))`; 3 samples / 80 ⇒ `Calibrated { ObservedSamples = SampleCount 3;
  RequiredSamples = SampleCount 3; … }` (agreement inclusive at the threshold); 5 samples / 95 ⇒ `Calibrated {
  ObservedSamples = SampleCount 5; RequiredSamples = SampleCount 3; … }`; degenerate `MinimumSamples = SampleCount 1`, 1
  sample ⇒ `Uncalibrated (TooFewSamples (SampleCount 1, SampleCount 2))` (the no-single-sample floor `max(1, 2) = 2`).
  This is the Principle-I FSI proof; it documents the shape even while bodies are stubbed.
- [X] T008 Write `tests/FS.GG.Governance.Calibration.Tests/Support.fs` — real, literally-constructible builders
  (Principle V, no mocks): a `judgeId` helper building a `JudgeIdentity` from literal `ModelId`/`ModelVersion`/
  `ReviewerPromptHash`; `agreeingSample` / `disagreeingSample` builders pairing literal `RecordedVerdict`s with an
  `AgreementClassification`; an `evidence` helper assembling `CalibrationEvidence` from a sample list + an
  `AgreementLevel`; a `thresholds` helper; the six worked-example records from contracts/calibration-api.md with their
  expected `decide` results, for example-test oracles; FsCheck generators for `AgreementClassification`, a
  `ComparisonSample` list of arbitrary length (incl. `[]` and singletons), `AgreementLevel` and `SampleCount` over the
  full non-negative **and negative** int range (totality), `RecordedVerdict` strings (incl. empty + multi-byte), a
  `JudgeIdentity`, a full `CalibrationEvidence`, and a full `CalibrationThresholds`; and the `findRepoRoot (DirectoryInfo
  AppContext.BaseDirectory)` / `repoRoot` helper copied from the AdvisoryPromotion/ReviewRecord `Support.fs` precedent.
  No I/O beyond repo-root resolution.
- [X] T009 Write `tests/FS.GG.Governance.Calibration.Tests/Main.fs` — the Expecto entry point (`[<EntryPoint>]
  runTestsInAssemblyWithCLIArgs`), matching the existing test projects.

**Checkpoint**: `dotnet build FS.GG.Governance.sln` is clean; the test project compiles; running tests now FAILS only
because operation bodies are stubs (not because of compile errors).

---

## Phase 3: User Story 1 — An uncalibrated agent reviewer stays advisory by default (Priority: P1) 🎯 MVP

**Goal**: With no calibration evidence, or with evidence falling short of a supplied threshold, `decide` returns
`Uncalibrated` carrying its reason — `NoCalibrationEvidence` when no comparison samples exist, `TooFewSamples (observed,
effectiveMin)` when the count is below the effective minimum, `AgreementBelowThreshold (observed, required)` when there
are enough samples but agreement is too low — and the model's own self-reported confidence never calibrates. This is the
heart of the design's calibration-debt constraint and the phase's exit criterion: an unmeasured agent reviewer can
**never** move beyond advisory maturity.

**Independent Test**: Supply a `CalibrationEvidence` that is empty, or below the required sample count, or with an
agreement level below the supplied threshold; assert `decide` is `Uncalibrated` with a reason naming the unmet
requirement. No model invoked, no human consulted, no I/O.

### Tests for User Story 1 (write first; must FAIL against stubs)

- [X] T010 [P] [US1] `tests/.../UncalibratedDefaultTests.fs` — (1) **no evidence** (SC-001, US1 #1, L-D1): empty
  `Samples` (any `ObservedAgreement`, any thresholds) ⇒ `decide = Uncalibrated NoCalibrationEvidence`; (2) **too few
  samples** (SC-001, US1 #2, L-D2): a sample count `n` with `1 <= n < max(min, 2)` ⇒ `Uncalibrated (TooFewSamples
  (SampleCount n, SampleCount (max min 2)))` — the carried `required` is the **effective** minimum (L-D12); (3)
  **agreement below threshold** (SC-001, US1 #3, L-D3): enough samples (`n >= max(min, 2)`) but `obs < req` ⇒
  `Uncalibrated (AgreementBelowThreshold (evidence.ObservedAgreement, thresholds.MinimumAgreement))`; (4)
  **self-confidence never calibrates** (SC-001, US1 #4, L-D10): this is a **by-construction** fact, not a fail-then-pass
  behavioral test — there is no field by which the model's own confidence enters `CalibrationEvidence` (only
  judge-vs-human `Samples` + the supplied `ObservedAgreement` populate it), and that absence is enforced by the type and
  guarded by the SurfaceDrift baseline (T017), not by a runtime assertion. The behavioral check here only confirms that
  ordinary evidence — built solely from samples + `ObservedAgreement`, with no self-confidence channel available — flows
  through `decide` to an ordinary `Uncalibrated` outcome; name the test (`Synthetic`-free) so it reads as the
  structural/by-construction check it is; (5) **uncalibrated-by-default property** (SC-001, L-D5): an FsCheck property that whenever `observed = 0
  || observed < max(min, 2) || obs < req`, `decide` is `Uncalibrated _` — never `Calibrated`.

### Implementation for User Story 1

- [X] T011 [US1] Implement `decide` (and the private comparator helper), `calibrationReason`, `calibrationMetrics`,
  `isCalibrated`, `observedSampleCount`, `sampleCountValue`, `agreementValue` in `Calibration.fs` per
  contracts/calibration-api.md and data-model.md — `let observed = List.length evidence.Samples`, `let min =
  sampleCountValue thresholds.MinimumSamples`, `let effectiveMin = max min 2`, `let obs = agreementValue
  evidence.ObservedAgreement`, `let req = agreementValue thresholds.MinimumAgreement`; then the precedence ladder: `if
  observed = 0 then Uncalibrated NoCalibrationEvidence elif observed < effectiveMin then Uncalibrated (TooFewSamples
  (SampleCount observed, SampleCount effectiveMin)) elif obs < req then Uncalibrated (AgreementBelowThreshold
  (evidence.ObservedAgreement, thresholds.MinimumAgreement)) else Calibrated { ObservedSamples = SampleCount observed;
  RequiredSamples = SampleCount effectiveMin; ObservedAgreement = evidence.ObservedAgreement; RequiredAgreement =
  thresholds.MinimumAgreement }` (L-D1..L-D4). `calibrationReason` projects (`Uncalibrated r ⇒ Some r`; `Calibrated _ ⇒
  None`, L-P1); `calibrationMetrics` projects (`Calibrated m ⇒ Some m`; `Uncalibrated _ ⇒ None`, L-P2); `isCalibrated`
  (`Calibrated _ ⇒ true`; `Uncalibrated _ ⇒ false`, L-P3); `observedSampleCount evidence = SampleCount (List.length
  evidence.Samples)` (L-O1); the two unwrappers pattern-match their newtype (L-U1/L-U2). Pure pattern matching + integer
  comparison + `FSharp.Core` only; no clock/filesystem/git/environment/network, no model, no byte hashing, no review, no
  human comparison, no cache/verdict operation, no review-record build (L-D11/L-D14, FR-006). This single total function
  serves both US1 (uncalibrated branches) and US2 (calibrated branch). Run T010: green (uncalibrated branches).

**Checkpoint**: US1 is functional — an unmeasured or under-measured agent reviewer stays uncalibrated with a named
reason, by construction. The design's uncalibrated-by-default safety posture holds. MVP reached for the default.

---

## Phase 4: User Story 2 — A reviewer becomes calibrated on sufficient judge-vs-human evidence, and the evidence is named (Priority: P1)

**Goal**: When the supplied thresholds are met — sample count `>= max(min, 2)` **and** observed agreement `>= req` —
`decide` returns `Calibrated` naming the satisfied `CalibrationMetrics` (the no-hide rule), not a bare flag. The
agreement comparison is inclusive (`obs = req` calibrates). Co-P1 with US1 — together they fix the gate's two outcomes.
`decide` is already whole from T011; this phase validates its calibrated branch.

**Independent Test**: Supply a `CalibrationEvidence` whose sample count meets/exceeds the effective minimum **and** whose
agreement level meets/exceeds the supplied threshold ⇒ `Calibrated` naming the satisfied metrics (observed sample count
+ agreement level against their requirements); confirm `decide` is a deterministic function of the supplied evidence.

### Tests for User Story 2 (validate the calibrated branch of the completed `decide` from T011)

- [X] T012 [P] [US2] `tests/.../CalibratedTests.fs` — (1) **calibrated, metrics named** (SC-002, US2 #1/#3, L-D4/L-D8):
  the worked example 5 samples / agreement 95 against `T` ⇒ `Calibrated { ObservedSamples = SampleCount 5; RequiredSamples
  = SampleCount 3; ObservedAgreement = AgreementLevel 95; RequiredAgreement = AgreementLevel 80 }`, and
  `calibrationMetrics (decide …) = Some` that record — assert the metrics name the observed count + level and the
  effective requirements, not a bare flag; (2) **inclusive at the agreement threshold** (SC-002, US2 #2, L-D7): 3 samples
  / agreement exactly 80 against `T` ⇒ `Calibrated { … ObservedAgreement = AgreementLevel 80; RequiredAgreement =
  AgreementLevel 80 }` (meets-or-exceeds); (3) **effective-minimum honesty in metrics** (L-D12): under degenerate
  `MinimumSamples = SampleCount 1`, a calibrated decision still reports `RequiredSamples = SampleCount 2` (the effective
  floor), never the understated supplied `1`; (4) **calibrated-iff-both-gates property** (SC-002, L-D6): an FsCheck
  property that `isCalibrated (decide t e)` ⟺ `observed >= max(min, 2) && obs >= req`, and that every `Calibrated`
  decision's metrics carry exactly `ObservedSamples = SampleCount observed`, `RequiredSamples = SampleCount (max min 2)`,
  `ObservedAgreement = e.ObservedAgreement`, `RequiredAgreement = t.MinimumAgreement`.
- [X] T013 [P] [US2] `tests/.../ComparatorTests.fs` — (SC-003, FR-004, L-D7): the calibration basis is satisfied
  **exactly** when `observed >= max(min, 2) && obs >= req`. **Sample gate**: for `min >= 2`, verified across `observed <
  min`, `observed = min`, `observed > min` (with agreement satisfied); a lone sample (`observed = 1`) never passes for
  any `min` (incl. `min = 1`, where `max(1, 2) = 2 > 1` ⇒ `TooFewSamples (SampleCount 1, SampleCount 2)`); `observed = 0`
  is `NoCalibrationEvidence`, not `TooFewSamples`. **Agreement gate**: across `obs < req`, `obs = req` (inclusive,
  calibrates), `obs > req` with the sample gate satisfied. An FsCheck property over sample-list length and
  `obs`/`req`/`min` straddling the thresholds (incl. negative/degenerate values) confirms `isCalibrated (decide t e)` ⟺
  `List.length e.Samples >= max (sampleCountValue t.MinimumSamples) 2 && agreementValue e.ObservedAgreement >=
  agreementValue t.MinimumAgreement`, and that the precedence (`NoCalibrationEvidence` before `TooFewSamples` before
  `AgreementBelowThreshold`) holds.

**Checkpoint**: US1 + US2 — the gate's two outcomes are both proven: uncalibrated by default, and calibrated-on-
sufficient-evidence naming the satisfied metrics, with the inclusive two-threshold comparator and the no-single-sample
floor. Full decision core functional.

---

## Phase 5: User Story 3 — The decision is total, deterministic, and never blocks on its own (Priority: P2)

**Goal**: `decide` is defined for every `CalibrationThresholds` × `CalibrationEvidence` (any sample list incl. `[]` and
singletons, any `AgreementLevel`, any `SampleCount` incl. zero/negative), never throws, never reads a clock/file/model;
identical inputs ⇒ identical decision; and a `Calibrated` decision is necessary-not-sufficient — it carries no blocking
action, no severity, and no enforcement verdict. Builds on US1–US2, so P2.

**Independent Test**: Exercise `decide` across the full cross-product of sample counts (zero, one, many) and agreement
levels straddling the threshold (below, equal, above) ⇒ always returns a decision, never throws; call it twice on
identical inputs ⇒ equal results; inspect a `Calibrated` value ⇒ it carries only beyond-advisory maturity.

### Tests for User Story 3 (validate totality/determinism/necessary-not-sufficient over the completed `decide` from T011)

- [X] T014 [P] [US3] `tests/.../TotalityTests.fs` — (SC-004, US3 #1, L-D13): an FsCheck property over the full
  cross-product of arbitrary `ComparisonSample` lists (incl. `[]` and singletons), arbitrary `AgreementLevel` (incl. `0`,
  negatives, `Int32.Min/MaxValue`), and arbitrary `CalibrationThresholds` (`SampleCount`/`AgreementLevel` incl.
  negative/degenerate ints) asserts `decide` returns a `CalibrationDecision` and never throws; every combination is an
  ordinary named decision (the Edge Cases — no evidence, one sample, agreement below/at/above threshold).
- [X] T015 [P] [US3] `tests/.../DeterminismTests.fs` — (SC-005, US3 #2, L-D14): `decide t e = decide t e` for example +
  FsCheck-generated inputs; and a purity check mirroring the AdvisoryPromotion/ReviewRecord precedent — the decision is
  structurally identical when computed in different working directories, at different times, and with unrelated
  repository / filesystem state changed between calls; no model invoked, no human consulted, no review run, no
  clock/filesystem/git/environment/network read, no bytes hashed, nothing persisted.
- [X] T016 [P] [US3] `tests/.../NecessaryNotSufficientTests.fs` — (SC-006, US3 #3, L-D15): the FR-008 negatives are
  **by construction**, not fail-then-pass behavioral tests. That `CalibrationDecision` exposes no blocking action, no
  `Severity`, no effective severity, no enforcement verdict, and no F039 eligibility is proven by an **exhaustive
  pattern match that compiles** (every value is `Uncalibrated _` or `Calibrated _` and nothing more) **plus** the
  SurfaceDrift + reference-graph guard (T017), which together pin that no such member or dependency exists; name the
  test so it reads as the structural/by-construction check it is. The type is the calibration-maturity verdict and
  nothing more (necessary, not sufficient — blocking still composes F039 per-finding eligibility, this calibration, and
  the F023/F024 enforcement machinery). The genuine **value-level, fail-then-pass** assertion in this file is the
  no-hide rule (L-D8/L-D9), which also pins FR-001 at the value level: every `Calibrated` produced by `decide` yields
  `calibrationMetrics _ = Some _` and every `Uncalibrated` yields `calibrationReason _ = Some _` (and `Calibrated` is
  unrepresentable without `CalibrationMetrics` — there is no `Calibrated`-without-metrics constructor).

**Checkpoint**: US1 + US2 + US3 — the decision is total over all inputs, deterministic/pure, names its outcome, and is
honestly scoped as calibration maturity (not blocking). All success criteria SC-001..SC-006 are pinned.

---

## Phase 6: Surface governance & polish (Tier-1 baseline, scope hygiene)

**Purpose**: Lock the public surface (Principle II) and prove the assembly's reference graph stays minimal (SC-007).
Bless the baseline only after the surface is final.

- [X] T017 `tests/.../SurfaceDriftTests.fs` — a reflective `SurfaceDrift` test (the F029–F039 precedent): enumerate the
  public surface of `FS.GG.Governance.Calibration` and compare byte-for-byte to
  `surface/FS.GG.Governance.Calibration.surface.txt`, with the `BLESS_SURFACE=1` re-bless path; plus a **scope-hygiene**
  assertion (contracts/calibration-api.md scope guard, SC-007) that the assembly references **only** `FSharp.Core`,
  `FS.GG.Governance.AgentReviewKey`, `FS.GG.Governance.ReviewRecord`, and — transitively —
  `FS.GG.Governance.PromptIsolation`, `FS.GG.Governance.SensedMetadata`, `FS.GG.Governance.FreshnessKey`,
  `FS.GG.Governance.Config` (unused here), plus the BCL — and **not** `Gates`, `Snapshot`, `Route`/`Routing`,
  `Findings`, `Enforcement`, `VerdictReuse`, `AdvisoryPromotion`, any `Adapters.*`, `Host`, `Cli`, `Ship`, or
  `AuditJson`. **Note**: FR-006/FR-007/FR-008's *behavioral* negatives (no verdict produce/interpret/re-score/threshold,
  no human comparison, no cache key / verdict store / lookup / invalidation, no review-record build, no model invocation,
  no byte hashing, no persistence, no JSON projection, no CLI, no blocking action, no effective severity) are satisfied
  **by construction** — the surface holds only the ten types + `decide` / `calibrationReason` / `calibrationMetrics` /
  `isCalibrated` / `observedSampleCount` / `sampleCountValue` / `agreementValue` (no such operation exists to call) — and
  are guarded by this reference-graph + surface-drift check, not by a positive behavioral test.
- [X] T018 Generate and commit `surface/FS.GG.Governance.Calibration.surface.txt` via `BLESS_SURFACE=1 dotnet test
  tests/FS.GG.Governance.Calibration.Tests/...`; review the diff (exactly the two public modules — the `Model` types
  `AgreementClassification` / `JudgeIdentity` / `ComparisonSample` / `SampleCount` / `AgreementLevel` /
  `CalibrationEvidence` / `CalibrationThresholds` / `CalibrationMetrics` / `CalibrationReason` / `CalibrationDecision`,
  and `decide` / `calibrationReason` / `calibrationMetrics` / `isCalibrated` / `observedSampleCount` / `sampleCountValue`
  / `agreementValue`; no comparator-helper leak) and commit it as part of the Tier-1 change. After this, T017 runs green
  without `BLESS_SURFACE`.
- [X] T019 [P] **Verify-only** — `CLAUDE.md`'s SPECKIT plan reference already points at
  `specs/040-calibration-evidence-gate/plan.md` (the active pointer); confirm it and make **no edit** unless it has
  drifted. No other doc changes.
- [X] T020 Run `quickstart.md` validation end-to-end: `dotnet build FS.GG.Governance.sln`, `dotnet fsi
  scripts/prelude.fsx` (the F040 section prints the expected no-evidence / too-few / agreement-below / calibrated /
  no-single-sample-floor results), and `dotnet test tests/FS.GG.Governance.Calibration.Tests/...` — all green under
  `TreatWarningsAsErrors`. Confirm `dotnet test FS.GG.Governance.sln` over the existing projects is unchanged (no
  existing baseline rewritten, no existing test changes outcome — SC-007).

**Checkpoint**: Tier-1 surface is blessed and guarded; the assembly's reference graph is minimal (AgentReviewKey +
ReviewRecord + transitive cores only); the full solution builds and tests green; existing cores untouched. **Phase 12
closes.**

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1. **BLOCKS all stories** — the `.fsi` surface, FSI proof, and compiling
  stubs must exist before any story test can be written and FAIL.
- **Phase 3 (US1)**: depends on Phase 2. The MVP (uncalibrated-by-default). T011 implements the **whole** total `decide`
  (all uncalibrated branches *and* the calibrated branch), since the function must be complete to be total.
- **Phase 4 (US2)**: depends on Phase 2; co-P1 with US1. Its tests (T012/T013) validate the **calibrated** branch and
  the two-threshold comparator of the same `decide` implemented in T011 — there is no separate US2 implementation task;
  the single total function serves both stories. Sequenced after US1 because the calibrated branch is the complement of
  the uncalibrated default.
- **Phase 5 (US3)**: depends on Phase 2 + T011 (asserts totality/determinism/necessary-not-sufficient/no-hide of the
  finished `decide`); P2.
- **Phase 6 (surface/polish)**: last — bless the baseline only after the surface is final (Phase 2 `.fsi` unchanged
  through implementation).

### Within each story

- Tests are written FIRST and must FAIL against the Phase-2 stubs (US1, T010), then pass after T011. US2/US3 tests pass
  against the complete `decide` once T011 lands.
- `Model` type definitions precede the `Calibration` operation bodies that consume them.

### Parallel opportunities

- **Phase 1**: T002 `[P]` (test `.fsproj`) is independent of T001 (library `.fsproj`); T003 (sln) needs both.
- **Phase 2**: T007 `[P]` (prelude FSI section) is independent of the `.fsi`/stub work once the DLL name is fixed by
  T001. T004/T005 (the two `.fsi` files) can be drafted together; T006 needs both; T008/T009 need the compiling stub.
- **Story test files are all `[P]`** relative to each other (distinct files): T010, T012, T013, T014, T015, T016 touch
  different test files. They share `Support.fs` (T008) as a prerequisite.
- **Phase 6**: T019 `[P]` (CLAUDE.md) is independent of the surface test; T017→T018→T020 are sequential.

---

## Task count per user story

- **Setup (Phase 1)**: 3 tasks (T001–T003).
- **Foundational (Phase 2)**: 6 tasks (T004–T009).
- **US1 (Phase 3)**: 2 tasks (T010 test, T011 impl) 🎯 MVP.
- **US2 (Phase 4)**: 2 tasks (T012/T013 tests; impl shared with T011).
- **US3 (Phase 5)**: 3 tasks (T014–T016 tests; totality/determinism/necessary-not-sufficient + no-hide by construction).
- **Surface & polish (Phase 6)**: 4 tasks (T017–T020).
- **Total**: 20 tasks.

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2)** — the project skeleton, the `.fsi` surface + FSI proof, and the
single total `decide` proven on both outcomes. US1 and US2 are **co-P1** (the spec: together they fix the gate's two
outcomes — uncalibrated by default, and calibrated on sufficient judge-vs-human evidence with the satisfied metrics
named), so the smallest honest MVP delivers both: an unmeasured reviewer that stays uncalibrated *and* a sufficiently
measured one that becomes calibrated naming exactly which metrics cleared the gate. US3 (totality/determinism/
necessary-not-sufficient, P2) pins the trust guarantees over the same finished function; Phase 6 locks the Tier-1
surface and reference-graph hygiene.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- `[Story]` label maps a task to its user story for traceability.
- Verify US1's tests FAIL against the Phase-2 stubs before implementing T011, then pass after; US2/US3 tests pass once
  T011 lands.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document.
- Commit after each task or logical group; keep existing `src/`, `surface/`, and merged test projects untouched
  (SC-007). F035 identity vocabulary and F038 `RecordedVerdict` are consumed verbatim, never modified (FR-009).
</content>
</invoke>
