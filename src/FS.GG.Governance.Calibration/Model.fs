// Calibration-evidence types for the judge-vs-human calibration-evidence decision core (F040). The public
// surface is fixed by Model.fsi (Principle II); no top-level binding here carries an access modifier. These
// are the supplied evidence + thresholds and named outcomes that `Calibration.decide` works over; they reuse
// the F035 identity tokens (`ModelId` / `ModelVersion` / `ReviewerPromptHash`) and the F038 `RecordedVerdict`
// verbatim rather than redefining them (FR-009).

namespace FS.GG.Governance.Calibration

open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.ReviewRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type AgreementClassification =
        | Agreeing
        | Disagreeing

    type JudgeIdentity =
        { Model: ModelId
          ModelVersion: ModelVersion
          PromptHash: ReviewerPromptHash }

    type ComparisonSample =
        { JudgeVerdict: RecordedVerdict
          HumanVerdict: RecordedVerdict
          Agreement: AgreementClassification }

    type SampleCount = SampleCount of int

    type AgreementLevel = AgreementLevel of int

    type CalibrationEvidence =
        { Scope: JudgeIdentity
          Samples: ComparisonSample list
          ObservedAgreement: AgreementLevel }

    type CalibrationThresholds =
        { MinimumSamples: SampleCount
          MinimumAgreement: AgreementLevel }

    type CalibrationMetrics =
        { ObservedSamples: SampleCount
          RequiredSamples: SampleCount
          ObservedAgreement: AgreementLevel
          RequiredAgreement: AgreementLevel }

    type CalibrationReason =
        | NoCalibrationEvidence
        | TooFewSamples of observed: SampleCount * required: SampleCount
        | AgreementBelowThreshold of observed: AgreementLevel * required: AgreementLevel

    type CalibrationDecision =
        | Uncalibrated of CalibrationReason
        | Calibrated of CalibrationMetrics
