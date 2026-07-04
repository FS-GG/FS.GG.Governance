module FS.GG.Governance.Calibration.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model

// Shared REAL-input builders + FsCheck generators for the F040 tests (Principle V — every value below is a
// real, literally-constructible typed value: real F035 `ModelId`/`ModelVersion`/`ReviewerPromptHash`, real
// F038 `RecordedVerdict`, literal `SampleCount`/`AgreementLevel`; never a mock; the decision is pure so no
// upstream chain is needed, no clock read, no model invoked, no file read, no bytes hashed, no human
// consulted). No I/O beyond repo-root resolution.

// ── Scalar builders (real literals, no mocks) ──

/// Build a per-judge calibration scope from literal F035 identity tokens.
let judgeId (m: string) (v: string) (h: string) : JudgeIdentity =
    { Model = ModelId m
      ModelVersion = ModelVersion v
      PromptHash = ReviewerPromptHash h }

/// A default scope reused where the identity is immaterial to the decision (calibration is per identity, but
/// `decide` trusts the evidence is pre-filtered to one identity — research D3).
let defaultJudge: JudgeIdentity = judgeId "gpt" "1" "h"

/// One judge-vs-human comparison sample pairing literal F038 verdicts (opaque — `decide` counts samples and
/// reads the evidence-level ObservedAgreement, never a per-sample field).
let sample (judge: string) (human: string) : ComparisonSample =
    { JudgeVerdict = RecordedVerdict judge
      HumanVerdict = RecordedVerdict human }

/// A sample where the judge and human reached the same verdict.
let agreeingSample: ComparisonSample = sample "v" "v"

/// A sample where the judge and human differed.
let disagreeingSample: ComparisonSample = sample "j" "h"

/// Assemble `CalibrationEvidence` from a sample list + a supplied observed agreement level, under the default
/// scope.
let evidence (samples: ComparisonSample list) (agreement: int) : CalibrationEvidence =
    { Scope = defaultJudge
      Samples = samples
      ObservedAgreement = AgreementLevel agreement }

/// `n` agreeing samples + a supplied observed agreement level — the worked-example shape.
let evidenceOf (n: int) (agreement: int) : CalibrationEvidence =
    evidence (List.replicate (max n 0) agreeingSample) agreement

/// Assemble `CalibrationThresholds` from a minimum sample count + a minimum agreement level.
let thresholds (minSamples: int) (minAgreement: int) : CalibrationThresholds =
    { MinimumSamples = SampleCount minSamples
      MinimumAgreement = AgreementLevel minAgreement }

// ── The six worked examples (contracts/calibration-api.md) with their expected `decide` results, as
//    example-test oracles. T = { MinimumSamples = 3; MinimumAgreement = 80 }. Each value is a real literal. ──

let T: CalibrationThresholds = thresholds 3 80

let workedExamples: (CalibrationThresholds * CalibrationEvidence * CalibrationDecision) list =
    [ T, evidenceOf 0 95, Uncalibrated NoCalibrationEvidence
      T, evidenceOf 1 100, Uncalibrated(TooFewSamples(SampleCount 1, SampleCount 3))
      T, evidenceOf 2 100, Uncalibrated(TooFewSamples(SampleCount 2, SampleCount 3))
      T, evidenceOf 3 79, Uncalibrated(AgreementBelowThreshold(AgreementLevel 79, AgreementLevel 80))
      T,
      evidenceOf 3 80,
      Calibrated
          { ObservedSamples = SampleCount 3
            RequiredSamples = SampleCount 3
            ObservedAgreement = AgreementLevel 80
            RequiredAgreement = AgreementLevel 80 }
      T,
      evidenceOf 5 95,
      Calibrated
          { ObservedSamples = SampleCount 5
            RequiredSamples = SampleCount 3
            ObservedAgreement = AgreementLevel 95
            RequiredAgreement = AgreementLevel 80 } ]

// ── Oracle: the calibration basis recomputed independently of the implementation under test ──

/// Independently decide whether the supplied evidence calibrates against the thresholds: the sample gate is
/// `List.length Samples >= max(min, 2)` and the agreement gate is `obs >= req`, both inclusive.
let expectedCalibrated (t: CalibrationThresholds) (e: CalibrationEvidence) : bool =
    let observed = List.length e.Samples
    let (SampleCount min) = t.MinimumSamples
    let effectiveMin = max min 2
    let (AgreementLevel obs) = e.ObservedAgreement
    let (AgreementLevel req) = t.MinimumAgreement
    observed >= effectiveMin && obs >= req

// ── FsCheck generators (real values, no mocks) ──

// Verdict strings include empty, multi-byte, and structural values — every one a literal supplied token; the
// verdicts are opaque and never interpreted, so any string is a valid sample.
let private genVerdictString: Gen<string> =
    Gen.elements [ ""; "v"; "pass"; "fail"; "sha256:abc"; "héllo"; "日本語"; ";;;"; "\n" ]

let private genSample: Gen<ComparisonSample> =
    gen {
        let! j = genVerdictString
        let! h = genVerdictString

        return
            { JudgeVerdict = RecordedVerdict j
              HumanVerdict = RecordedVerdict h }
    }

// Sample lists span the empty list, singletons, and arbitrary length (totality + the no-single-sample floor).
let private genSamples: Gen<ComparisonSample list> =
    Gen.oneof
        [ Gen.constant []
          genSample |> Gen.map List.singleton
          Gen.listOf genSample ]

// Counts/levels span the full non-negative AND negative int range, including the degenerate extremes, so
// totality and the comparator law are exercised across, at, below, and above the threshold (and a lone sample).
let private genInt: Gen<int> =
    Gen.oneof
        [ Gen.elements [ -3; -1; 0; 1; 2; 3; 4; 5; 10 ]
          Gen.choose (-1000, 1000)
          Gen.elements [ Int32.MinValue; Int32.MaxValue ] ]

let private genSampleCount: Gen<SampleCount> = genInt |> Gen.map SampleCount
let private genAgreementLevel: Gen<AgreementLevel> = genInt |> Gen.map AgreementLevel

let private genJudgeIdentity: Gen<JudgeIdentity> =
    gen {
        let! m = genVerdictString
        let! v = genVerdictString
        let! h = genVerdictString

        return
            { Model = ModelId m
              ModelVersion = ModelVersion v
              PromptHash = ReviewerPromptHash h }
    }

let private genEvidence: Gen<CalibrationEvidence> =
    gen {
        let! scope = genJudgeIdentity
        let! samples = genSamples
        let! agreement = genAgreementLevel

        return
            { Scope = scope
              Samples = samples
              ObservedAgreement = agreement }
    }

let private genThresholds: Gen<CalibrationThresholds> =
    gen {
        let! minSamples = genSampleCount
        let! minAgreement = genAgreementLevel

        return
            { MinimumSamples = minSamples
              MinimumAgreement = minAgreement }
    }

type Generators =
    static member ComparisonSample() : Arbitrary<ComparisonSample> = Arb.fromGen genSample
    static member SampleCount() : Arbitrary<SampleCount> = Arb.fromGen genSampleCount
    static member AgreementLevel() : Arbitrary<AgreementLevel> = Arb.fromGen genAgreementLevel
    static member JudgeIdentity() : Arbitrary<JudgeIdentity> = Arb.fromGen genJudgeIdentity
    static member CalibrationEvidence() : Arbitrary<CalibrationEvidence> = Arb.fromGen genEvidence
    static member CalibrationThresholds() : Arbitrary<CalibrationThresholds> = Arb.fromGen genThresholds

/// FsCheck config registering the real F040 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
