module FS.GG.Governance.AdvisoryPromotion.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model

// Shared REAL-input builders + FsCheck generators for the F039 tests (Principle V — every value below is a
// real, literally-constructible typed value: a real F030 `EvidenceRef`, literal `ConfirmationCount`/
// `ConfidenceThreshold`, a literal `SignOff`, never a mock; the decision is pure so no upstream chain is
// needed, no clock read, no model invoked, no file read, no bytes hashed). No I/O beyond repo-root resolution.

// ── Scalar builders (real literals, no mocks) ──

let evidence (s: string) : EvidenceRef = EvidenceRef s
let signOff (s: string) : SignOff = SignOff s
let confirmations (n: int) : ConfirmationCount = ConfirmationCount n
let threshold (n: int) : ConfidenceThreshold = ConfidenceThreshold n

/// Assemble a `PromotionFacts` from supplied levers — the sole input to `decide`.
let facts (e: EvidenceRef option) (c: int) (t: int) (s: SignOff option) : PromotionFacts =
    { BackingEvidence = e
      Confirmations = ConfirmationCount c
      ConfidenceThreshold = ConfidenceThreshold t
      SignOff = s }

// ── The seven worked examples (contracts/advisory-promotion-api.md) with their expected `decide` results,
//    as example-test oracles. Each value is a real literal. ──

let workedExamples: (PromotionFacts * PromotionDecision) list =
    [ facts None 0 3 None, StaysAdvisory NoPermittedBasis
      facts None 2 3 None, StaysAdvisory(ConfidenceBelowThreshold(ConfirmationCount 2, ConfidenceThreshold 3))
      facts None 1 1 None, StaysAdvisory(ConfidenceBelowThreshold(ConfirmationCount 1, ConfidenceThreshold 1))
      facts (Some(EvidenceRef "e")) 0 3 None, EligibleToBlock(DeterministicBackingEvidence, [])
      facts None 3 3 None, EligibleToBlock(RepeatedReviewConfidence, [])
      facts None 0 3 (Some(SignOff "u")), EligibleToBlock(HumanSignOff, [])
      facts (Some(EvidenceRef "e")) 5 3 (Some(SignOff "u")),
      EligibleToBlock(DeterministicBackingEvidence, [ RepeatedReviewConfidence; HumanSignOff ]) ]

// ── Oracle: the bases satisfied by facts, in the fixed order (distinct from the implementation under test) ──

/// Independently compute the bases a `PromotionFacts` justifies, in the fixed order
/// *DeterministicBackingEvidence, RepeatedReviewConfidence, HumanSignOff*. The repeated-review basis holds
/// exactly at the inclusive floor with the no-single-sample guard (`c >= t && c >= 2`).
let expectedBases (f: PromotionFacts) : PromotionBasis list =
    let (ConfirmationCount c) = f.Confirmations
    let (ConfidenceThreshold t) = f.ConfidenceThreshold

    [ if f.BackingEvidence.IsSome then
          DeterministicBackingEvidence
      if c >= t && c >= 2 then
          RepeatedReviewConfidence
      if f.SignOff.IsSome then
          HumanSignOff ]

// ── FsCheck generators (real values, no mocks) ──

// Strings include empty, multi-byte, and structural values — every one a literal supplied token.
let private scalarGen: Gen<string> =
    Gen.elements [ ""; "e"; "u"; "evid-1"; "sha256:abc"; "héllo"; "日本語"; ";;;"; "\n" ]

let private genEvidence: Gen<EvidenceRef> = scalarGen |> Gen.map EvidenceRef
let private genSignOff: Gen<SignOff> = scalarGen |> Gen.map SignOff

let private optionOf (g: Gen<'a>) : Gen<'a option> =
    Gen.oneof [ Gen.constant None; g |> Gen.map Some ]

let private genEvidenceOpt: Gen<EvidenceRef option> = optionOf genEvidence
let private genSignOffOpt: Gen<SignOff option> = optionOf genSignOff

// Counts/thresholds span the full non-negative AND negative int range, including the degenerate extremes, so
// totality and the comparator law are exercised across, at, below, and above the threshold (and a lone review).
let private genInt: Gen<int> =
    Gen.oneof
        [ Gen.elements [ -3; -1; 0; 1; 2; 3; 4; 5; 10 ]
          Gen.choose (-1000, 1000)
          Gen.elements [ Int32.MinValue; Int32.MaxValue ] ]

let private genConfirmationCount: Gen<ConfirmationCount> = genInt |> Gen.map ConfirmationCount
let private genConfidenceThreshold: Gen<ConfidenceThreshold> = genInt |> Gen.map ConfidenceThreshold

let private genFacts: Gen<PromotionFacts> =
    gen {
        let! e = genEvidenceOpt
        let! c = genConfirmationCount
        let! t = genConfidenceThreshold
        let! s = genSignOffOpt

        return
            { BackingEvidence = e
              Confirmations = c
              ConfidenceThreshold = t
              SignOff = s }
    }

type Generators =
    static member EvidenceRef() : Arbitrary<EvidenceRef> = Arb.fromGen genEvidence
    static member SignOff() : Arbitrary<SignOff> = Arb.fromGen genSignOff
    static member ConfirmationCount() : Arbitrary<ConfirmationCount> = Arb.fromGen genConfirmationCount
    static member ConfidenceThreshold() : Arbitrary<ConfidenceThreshold> = Arb.fromGen genConfidenceThreshold
    static member PromotionFacts() : Arbitrary<PromotionFacts> = Arb.fromGen genFacts

/// FsCheck config registering the real F039 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
