module FS.GG.Governance.CacheEligibility.Tests.AttributionAndOrderTests

open System
open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility.Tests.Support

// User Story 3 (part) — one attributable verdict per gate, deterministic order (SC-006, L-E1..L-E5). `evaluate`
// returns exactly one verdict per supplied candidate, each attributed to its `GateId`, ordered by
// `gateIdValue` ordinal with a structural tiebreak so the order is independent of supply order; every gate is
// preserved (duplicates kept, none dropped/merged).

/// A stable multiset key for an entry (gate string + a structural rendering of the verdict), so we can compare
/// the produced entries against the expected ones independent of the sort's internal order.
let private entryKey (e: CacheEligibilityEntry) = (gateIdValue e.Gate, sprintf "%A" e.Verdict)

[<Tests>]
let tests =
    testList
        "AttributionAndOrder"
        [ testPropertyWithConfig fscheckConfig "exactly one attributed verdict per candidate, none dropped/merged/duplicated (US3 #1, SC-006, L-E1)"
          <| fun (cs: CandidateGate list) (s: ReuseStore) ->
              let actual = CacheEligibility.entries (CacheEligibility.evaluate cs s)
              // The expected entries: each candidate mapped to its own attributed verdict.
              let expected = cs |> List.map (fun c -> { Gate = c.Gate; Verdict = CacheEligibility.evaluateGate c s })
              // Same count, and the same multiset of attributed verdicts (order checked separately below).
              List.length actual = List.length cs
              && (actual |> List.map entryKey |> List.sort) = (expected |> List.map entryKey |> List.sort)

          test "worked example: z:a, a:b, a:a ⇒ ordered a:a, a:b, z:a (ordinal) (US3 #2, SC-006, L-E2)" {
              let cs = [ candidate (gid "z" "a") baseInputs; candidate (gid "a" "b") baseInputs; candidate (gid "a" "a") baseInputs ]
              let order = CacheEligibility.entries (CacheEligibility.evaluate cs EvidenceReuse.empty) |> List.map (fun e -> gateIdValue e.Gate)
              Expect.equal order [ "a:a"; "a:b"; "z:a" ] "entries are ordered by the GateId ordinal string"
          }

          testPropertyWithConfig fscheckConfig "entries are in non-decreasing GateId ordinal order (US3 #2, SC-006, L-E2)"
          <| fun (cs: CandidateGate list) (s: ReuseStore) ->
              CacheEligibility.entries (CacheEligibility.evaluate cs s)
              |> List.pairwise
              |> List.forall (fun (a, b) -> String.CompareOrdinal(gateIdValue a.Gate, gateIdValue b.Gate) <= 0)

          testPropertyWithConfig fscheckConfig "order-independence: any permutation yields a byte-identical report (US3, SC-006, L-E3)"
          <| fun (cs: CandidateGate list) (s: ReuseStore) ->
              // `List.rev` is a permutation; the report must be identical regardless of supply order.
              CacheEligibility.evaluate (List.rev cs) s = CacheEligibility.evaluate cs s

          test "explicit permutations of the worked example are byte-identical (L-E3)" {
              let mk order = CacheEligibility.evaluate (order |> List.map (fun g -> candidate g baseInputs)) EvidenceReuse.empty
              let a = mk [ gid "z" "a"; gid "a" "b"; gid "a" "a" ]
              let b = mk [ gid "a" "a"; gid "z" "a"; gid "a" "b" ]
              let c = mk [ gid "a" "b"; gid "a" "a"; gid "z" "a" ]
              Expect.equal b a "permutation b equals a"
              Expect.equal c a "permutation c equals a"
          }

          test "duplicate GateId, DIFFERENT inputs ⇒ TWO entries under that gate (US3, Edge, L-E4)" {
              // Both candidates carry gate build:tests but different inputs; against an empty store both are
              // MustRecompute NoPriorEvidence — but they must still be TWO entries, never merged or dropped.
              let store = storeOf [ baseInputs, refA ]
              let cs =
                  [ candidate (gid "build" "tests") baseInputs // exact match ⇒ Reusable refA
                    candidate (gid "build" "tests") { baseInputs with RuleHash = RuleHash "r2" } ] // ⇒ MustRecompute
              let entries = CacheEligibility.entries (CacheEligibility.evaluate cs store)
              Expect.equal (List.length entries) 2 "two candidates under one gate ⇒ two entries"
              Expect.all entries (fun e -> e.Gate = gid "build" "tests") "both entries are attributed to the duplicate gate"
          }

          test "duplicate GateId yielding EQUAL verdicts ⇒ byte-identical report for any supply order (L-E4)" {
              // Two candidates, same gate, different inputs, but BOTH MustRecompute NoPriorEvidence against an
              // empty store ⇒ the two entries are byte-identical, so supply order is immaterial.
              let c1 = candidate (gid "build" "tests") baseInputs
              let c2 = candidate (gid "build" "tests") { baseInputs with Head = Revision "zzz" }
              Expect.equal
                  (CacheEligibility.evaluate [ c1; c2 ] EvidenceReuse.empty)
                  (CacheEligibility.evaluate [ c2; c1 ] EvidenceReuse.empty)
                  "equal-verdict duplicates make the report supply-order-independent"
          }

          test "empty candidate list ⇒ empty report, total not an error (US3, Edge, L-E5)" {
              Expect.equal (CacheEligibility.evaluate [] (storeOf [ baseInputs, refA ])) (CacheEligibilityReport []) "empty input ⇒ empty report"
              Expect.equal (CacheEligibility.entries (CacheEligibility.evaluate [] EvidenceReuse.empty)) [] "entries of the empty report is []"
          } ]
