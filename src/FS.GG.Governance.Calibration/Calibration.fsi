// Curated public signature contract for the calibration-evidence operations (F040).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Calibration.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here. The `calibrated` comparator helper is ABSENT from this surface (private by omission).
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Calibration.fs body
// exists (Principle I). `decide` is PURE and TOTAL (FR-003, FR-006): defined for every input, never throwing,
// reading no clock/filesystem/git/environment/network, invoking no model/agent, running no review, performing
// no human comparison, hashing no bytes, making no cache-key/verdict-store/lookup/invalidation, building no
// review record, persisting nothing, and identical for identical input regardless of evaluation time, machine,
// process, or working directory. Its sole output is the typed `CalibrationDecision`.

namespace FS.GG.Governance.Calibration

open FS.GG.Governance.Calibration.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Calibration =

    /// Decide whether an agent-reviewed rule pack has enough judge-vs-human calibration evidence to move beyond
    /// advisory maturity. PURE and TOTAL (FR-003). Let `observed = List.length evidence.Samples`,
    /// `min = sampleCountValue thresholds.MinimumSamples`, `effectiveMin = max min 2`,
    /// `obs = agreementValue evidence.ObservedAgreement`, `req = agreementValue thresholds.MinimumAgreement`.
    /// In precedence order: `observed = 0` ⇒ `Uncalibrated NoCalibrationEvidence` (L-D1); `observed <
    /// effectiveMin` ⇒ `Uncalibrated (TooFewSamples (SampleCount observed, SampleCount effectiveMin))` (L-D2);
    /// `obs < req` ⇒ `Uncalibrated (AgreementBelowThreshold (evidence.ObservedAgreement,
    /// thresholds.MinimumAgreement))` (L-D3); otherwise `Calibrated { ObservedSamples = SampleCount observed;
    /// RequiredSamples = SampleCount effectiveMin; ObservedAgreement = evidence.ObservedAgreement;
    /// RequiredAgreement = thresholds.MinimumAgreement }` (L-D4). Uncalibrated by default (L-D5): whenever the
    /// evidence is absent or short of a threshold the result is `Uncalibrated _` — never `Calibrated`. Calibrated
    /// IFF `observed >= effectiveMin && obs >= req` (L-D6), both gates inclusive (`>=`), with the no-single-sample
    /// floor `max min 2` (L-D7) so a lone sample never calibrates. `Calibrated` names the satisfied metrics
    /// (L-D8); `Uncalibrated` names the unmet reason (L-D9). The model's own self-confidence is not an input
    /// (L-D10); the per-sample verdicts are opaque and never interpreted (L-D11); the effective minimum is
    /// reported truthfully (L-D12). Necessary-not-sufficient (L-D15): a `Calibrated` decision carries no blocking
    /// action, no severity, no enforcement verdict, and no F039 eligibility. Reads no
    /// clock/filesystem/git/environment/network, invokes no model, hashes no bytes, runs no review, performs no
    /// human comparison, makes no cache/verdict-store/lookup/invalidation, builds no review record (FR-006).
    val decide: thresholds: CalibrationThresholds -> evidence: CalibrationEvidence -> CalibrationDecision

    /// The reason named by a decision (projection). TOTAL. `Some r` for `Uncalibrated r` (L-P1); `None` for
    /// `Calibrated _`.
    val calibrationReason: decision: CalibrationDecision -> CalibrationReason option

    /// The satisfied metrics named by a decision (projection). TOTAL. `Some m` for `Calibrated m` (L-P2);
    /// `None` for `Uncalibrated _`.
    val calibrationMetrics: decision: CalibrationDecision -> CalibrationMetrics option

    /// Whether a decision is calibrated (boolean projection). TOTAL. `true` for `Calibrated _`; `false` for
    /// `Uncalibrated _` (L-P3).
    val isCalibrated: decision: CalibrationDecision -> bool

    /// The honest comparison-sample count actually present in the evidence: `SampleCount (List.length
    /// evidence.Samples)` (L-O1). TOTAL.
    val observedSampleCount: evidence: CalibrationEvidence -> SampleCount

    /// Unwrap a `SampleCount` to its int (for audit, messages, tests). TOTAL (L-U1).
    val sampleCountValue: count: SampleCount -> int

    /// Unwrap an `AgreementLevel` to its int (for audit, messages, tests). TOTAL (L-U2).
    val agreementValue: level: AgreementLevel -> int
