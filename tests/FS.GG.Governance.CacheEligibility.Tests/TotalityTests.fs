module FS.GG.Governance.CacheEligibility.Tests.TotalityTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility.Tests.Support

// User Story 3 (part) — totality (SC-004, L-T1). `evaluate` / `evaluateGate` are defined for every
// `CandidateGate list` × `ReuseStore` (zero/one/many candidates, duplicate GateIds, empty/matching/
// non-matching store), return a well-formed report/verdict, and never throw.

[<Tests>]
let tests =
    testList
        "Totality"
        [ testPropertyWithConfig fscheckConfig "evaluate returns a report and never throws over the full cross-product (SC-004, L-T1)"
          <| fun (cs: CandidateGate list) (s: ReuseStore) ->
              // Forcing the entries proves no exception escapes and every entry is one of the two cases.
              CacheEligibility.entries (CacheEligibility.evaluate cs s)
              |> List.forall (fun e ->
                  match e.Verdict with
                  | Reusable _
                  | MustRecompute _ -> true)

          testPropertyWithConfig fscheckConfig "evaluateGate returns a verdict and never throws over the full cross-product (SC-004, L-T1)"
          <| fun (c: CandidateGate) (s: ReuseStore) ->
              match CacheEligibility.evaluateGate c s with
              | Reusable _
              | MustRecompute _ -> true

          test "the named edge cases all yield ordinary reports (no throw)" {
              let store = storeOf [ baseInputs, refA ]
              let c = candidate (gid "build" "tests") baseInputs
              // No candidates.
              Expect.equal (CacheEligibility.evaluate [] store) (CacheEligibilityReport []) "no candidates ⇒ empty report"
              // One candidate, matching store ⇒ one Reusable entry.
              Expect.equal (CacheEligibility.entries (CacheEligibility.evaluate [ c ] store) |> List.length) 1 "one candidate ⇒ one entry"
              // One candidate, empty store ⇒ one MustRecompute entry.
              Expect.equal
                  (CacheEligibility.evaluate [ c ] EvidenceReuse.empty)
                  (CacheEligibilityReport [ { Gate = gid "build" "tests"; Verdict = MustRecompute NoPriorEvidence } ])
                  "one candidate, empty store ⇒ one MustRecompute NoPriorEvidence entry"
              // Duplicate GateIds against a non-matching store ⇒ two entries, both recompute, no throw.
              let dup = [ c; candidate (gid "build" "tests") { baseInputs with RuleHash = FS.GG.Governance.FreshnessKey.Model.RuleHash "r9" } ]
              Expect.equal (CacheEligibility.entries (CacheEligibility.evaluate dup EvidenceReuse.empty) |> List.length) 2 "duplicate gate ids ⇒ two entries"
          } ]
