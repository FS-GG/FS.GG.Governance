module FS.GG.Governance.CacheEligibility.Tests.NecessaryNotSufficientTests

open Expecto
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility.Tests.Support

// User Story 3 (part) — necessary-not-sufficient + no-hide (SC-007, FR-001/FR-010, L-G6/L-P2/L-P3/L-P4). A
// `CacheEligibilityVerdict` exposes NO skip action, severity, ship verdict, or exit-code basis — proven BY
// CONSTRUCTION by an exhaustive pattern match that compiles (every value is `Reusable _` or `MustRecompute _`
// and nothing more), plus the SurfaceDrift + reference-graph guard (SurfaceDriftTests). The genuine value-level
// assertions here are the no-hide rule and FR-001: every produced verdict names its single outcome and no
// other, so no verdict is an opaque yes/no and a `MustRecompute` always names its cause.

/// The by-construction proof: this total function compiles ONLY because `CacheEligibilityVerdict` has exactly
/// the two cases and carries nothing beyond the F030 evidence/cause payloads — there is no skip action,
/// severity, ship verdict, or exit-code basis to match on or to extract.
let private outcomeName (v: CacheEligibilityVerdict) =
    match v with
    | Reusable _ -> "reusable"
    | MustRecompute _ -> "recompute"

[<Tests>]
let tests =
    testList
        "NecessaryNotSufficient"
        [ test "the verdict is structurally exactly two outcomes — no enforcement member exists (by construction, L-G6)" {
              // If `CacheEligibilityVerdict` grew a third case or an enforcement payload, `outcomeName` would
              // stop compiling or this enumeration would miss a case. Pin both representative values.
              Expect.equal (outcomeName (Reusable refA)) "reusable" "Reusable is one closed outcome"
              Expect.equal (outcomeName (MustRecompute NoPriorEvidence)) "recompute" "MustRecompute is the other"
          }

          testPropertyWithConfig fscheckConfig "every Reusable names its evidence and no cause; every MustRecompute names its cause and no evidence (FR-001, no-hide, L-P2/L-P3/L-P4)"
          <| fun (c: CandidateGate) (s: ReuseStore) ->
              let v = CacheEligibility.evaluateGate c s

              match v with
              | Reusable ref ->
                  CacheEligibility.isReusable v
                  && CacheEligibility.reusableEvidence v = Some ref
                  && CacheEligibility.recomputeCause v = None
              | MustRecompute cause ->
                  not (CacheEligibility.isReusable v)
                  && CacheEligibility.recomputeCause v = Some cause
                  && CacheEligibility.reusableEvidence v = None

          test "every entry of a produced report obeys the no-hide projections (FR-001, no-hide)" {
              // A mixed report: one exact match (Reusable) and one stale candidate (MustRecompute).
              let store = storeOf [ baseInputs, refA ]
              let cs =
                  [ candidate (gid "a" "a") baseInputs
                    candidate (gid "b" "b") { baseInputs with Domain = FS.GG.Governance.Config.Model.DomainId "release" } ]

              for e in CacheEligibility.entries (CacheEligibility.evaluate cs store) do
                  match e.Verdict with
                  | Reusable _ ->
                      Expect.isTrue (CacheEligibility.isReusable e.Verdict) "Reusable ⇒ isReusable"
                      Expect.isSome (CacheEligibility.reusableEvidence e.Verdict) "Reusable names its evidence"
                      Expect.isNone (CacheEligibility.recomputeCause e.Verdict) "Reusable has no cause"
                  | MustRecompute _ ->
                      Expect.isFalse (CacheEligibility.isReusable e.Verdict) "MustRecompute ⇒ not isReusable"
                      Expect.isSome (CacheEligibility.recomputeCause e.Verdict) "MustRecompute always names its cause"
                      Expect.isNone (CacheEligibility.reusableEvidence e.Verdict) "MustRecompute has no evidence"
          } ]
