// Curated public signature contract for the calibration-evidence types (F040).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). These are the supplied evidence + thresholds and the named outcomes the `Calibration.decide`
// operation works over. They REUSE the F035 identity tokens (`ModelId` / `ModelVersion` / `ReviewerPromptHash`,
// opened from `FS.GG.Governance.AgentReviewKey.Model`) and the F038 `RecordedVerdict` (opened from
// `FS.GG.Governance.ReviewRecord.Model`) verbatim, never redefined (FR-009). The only new vocabulary is the
// closed two-value agreement classification, the per-judge scope record, the comparison sample, the two
// summarised-observation newtypes, the evidence + thresholds + metrics records, the no-hide reason union, and
// the two-outcome decision union — the minimal vocabulary the calibration-debt guardrail needs.

namespace FS.GG.Governance.Calibration

open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.ReviewRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The CLOSED two-value outcome of ONE judge-vs-human comparison — the only thing `decide` consumes from a
    /// sample (FR-002, FR-007). The model's own self-reported confidence is NOT a case (FR-002, SC-001):
    /// calibration is human comparison, never self-assessment.
    type AgreementClassification =
        | Agreeing
        | Disagreeing

    /// The per-judge calibration scope (FR-009 SHOULD, research D3). Reuses the F035 identity tokens verbatim.
    /// Calibration is per judge identity, not global: evidence under one model id / version / reviewer-prompt
    /// hash does not calibrate a different identity. This core RECORDS the scope (no-hide / audit) and trusts
    /// the supplied evidence is already filtered to one identity; it does not itself filter.
    type JudgeIdentity =
        { Model: ModelId
          ModelVersion: ModelVersion
          PromptHash: ReviewerPromptHash }

    /// One judge-vs-human comparison (FR-002, research D3/D4). Pairs the agent reviewer's verdict with a
    /// human's verdict on the same item (reusing F038 `RecordedVerdict` for both, opaque — never parsed,
    /// interpreted, compared, re-scored, or dereferenced, FR-007) and carries the already-classified
    /// `Agreement` — the consumed fact.
    type ComparisonSample =
        { JudgeVerdict: RecordedVerdict
          HumanVerdict: RecordedVerdict
          Agreement: AgreementClassification }

    /// A comparison-sample count (observed-derived or threshold-minimum). Single-case newtype preserving
    /// type-distinctness from `AgreementLevel` (a swapped count/level is a compile error). Supplied as data —
    /// never parsed; negative/degenerate values are total inputs (the comparator never throws).
    type SampleCount = SampleCount of int

    /// An agreement level on the edge's own opaque scale (percent, permille, basis points, …). Single-case
    /// newtype preserving type-distinctness from `SampleCount`. This core never parses or interprets the scale
    /// — it only compares with `>=`. Negative/degenerate values are total inputs.
    type AgreementLevel = AgreementLevel of int

    /// The comparison samples for one agent reviewer, scoped to a judge identity, plus the SUPPLIED observed
    /// agreement level (FR-002, research D4). The comparison-sample count is DERIVED from `List.length Samples`
    /// (`observedSampleCount`), not a separate supplied field — the honest count of evidence actually present.
    /// An empty `Samples` list is the ordinary "no calibration evidence" value (Edge Cases), never malformed.
    type CalibrationEvidence =
        { Scope: JudgeIdentity
          Samples: ComparisonSample list
          ObservedAgreement: AgreementLevel }

    /// The two SUPPLIED thresholds the evidence is measured against (FR-003): a minimum comparison-sample count
    /// and a minimum agreement level. Supplied values, not parsed by this core. No freshness window — recency
    /// is not modelled here (research D8).
    type CalibrationThresholds =
        { MinimumSamples: SampleCount
          MinimumAgreement: AgreementLevel }

    /// The no-hide record of exactly what cleared the gate (FR-005, US2 scenario 3). `RequiredSamples` is the
    /// EFFECTIVE minimum applied `max(MinimumSamples, 2)` (research D7), so the named bar is truthful even
    /// under a degenerate supplied minimum.
    type CalibrationMetrics =
        { ObservedSamples: SampleCount
          RequiredSamples: SampleCount
          ObservedAgreement: AgreementLevel
          RequiredAgreement: AgreementLevel }

    /// Why a reviewer stays uncalibrated — the no-hide attribution carried by an *uncalibrated* outcome, always
    /// present (FR-005, Principle VI). `NoCalibrationEvidence`: no comparison samples at all (the design's
    /// default). `TooFewSamples`: a count below the effective minimum — too thin to be meaningful (carries
    /// observed + effective-required). `AgreementBelowThreshold`: enough samples but agreement below the
    /// threshold (carries observed + required). NO `Stale` case — recency deferred (research D8).
    type CalibrationReason =
        | NoCalibrationEvidence
        | TooFewSamples of observed: SampleCount * required: SampleCount
        | AgreementBelowThreshold of observed: AgreementLevel * required: AgreementLevel

    /// The two-outcome gate verdict (FR-001, research D6). `Calibrated` ALWAYS carries its satisfied
    /// `CalibrationMetrics`, so a *calibrated* decision without the thresholds having been met is
    /// UNREPRESENTABLE (FR-001). `Uncalibrated` always carries its `CalibrationReason`. A `Calibrated` value
    /// asserts only beyond-advisory *maturity*: it carries no blocking action and no enforcement verdict
    /// (FR-008) — calibration is necessary, not sufficient.
    type CalibrationDecision =
        | Uncalibrated of CalibrationReason
        | Calibrated of CalibrationMetrics
