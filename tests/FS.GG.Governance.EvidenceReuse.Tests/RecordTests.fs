module FS.GG.Governance.EvidenceReuse.Tests.RecordTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse.Tests.Support

// US3 — recording is pure, deterministic, and de-duplicating (SC-005). A just-recorded entry is immediately
// reusable; re-recording under matching inputs refreshes (never duplicates) most-recent-wins; non-matching
// inputs leave prior entries independently reusable; `record` does not mutate its input; replay is
// deterministic.

[<Tests>]
let tests =
    testList
        "Record"
        [ test "reflexive reuse: decide i (record i E1 empty) = Reuse E1 (US3 #1, SC-005)" {
              let store = EvidenceReuse.record baseInputs E1 EvidenceReuse.empty

              Expect.equal (EvidenceReuse.decide baseInputs store) (Reuse E1) "a just-recorded entry is reusable"
          }

          test "refresh / de-dup most-recent-wins: record i E2 (record i E1 s) ⇒ Reuse E2, no dup (US3 #2, FR-008)" {
              let once = EvidenceReuse.record baseInputs E1 EvidenceReuse.empty
              let twice = EvidenceReuse.record baseInputs E2 once

              Expect.equal (EvidenceReuse.decide baseInputs twice) (Reuse E2) "the most-recent ref wins"

              Expect.equal
                  (EvidenceReuse.entries twice |> List.length)
                  (EvidenceReuse.entries once |> List.length)
                  "re-recording matching inputs leaves no duplicate (count unchanged)"

              Expect.equal (EvidenceReuse.entries twice |> List.length) 1 "exactly one entry for the matching-input class"
          }

          test "independence: recording non-matching inputs leaves every entry independently reusable (US3 #3, FR-008)" {
              let candidateB = { baseInputs with RuleHash = RuleHash "r2" }

              let store =
                  EvidenceReuse.empty
                  |> EvidenceReuse.record baseInputs E1
                  |> EvidenceReuse.record candidateB E2

              Expect.equal (EvidenceReuse.decide baseInputs store) (Reuse E1) "the first entry is still reusable"
              Expect.equal (EvidenceReuse.decide candidateB store) (Reuse E2) "the new entry is reusable too"
              Expect.equal (EvidenceReuse.entries store |> List.length) 2 "both entries are kept (different worlds)"
          }

          test "no mutation: record does not alter the input store value (FR-007)" {
              let original = EvidenceReuse.record baseInputs E1 EvidenceReuse.empty
              let originalEntries = EvidenceReuse.entries original
              let _ = EvidenceReuse.record baseInputs E2 original

              Expect.equal
                  (EvidenceReuse.entries original)
                  originalEntries
                  "the input store is unchanged after record (a new value is returned)"

              Expect.equal (EvidenceReuse.decide baseInputs original) (Reuse E1) "the original still decides to its own ref"
          }

          testPropertyWithConfig fscheckConfig "replay determinism: same start store + same record sequence ⇒ identical decisions (US3 #4, SC-005)"
          <| fun (start: ReuseStore) (i: FreshnessInputs) (e: EvidenceRef) (c: FreshnessInputs) ->
              let a = EvidenceReuse.record i e start
              let b = EvidenceReuse.record i e start
              EvidenceReuse.decide c a = EvidenceReuse.decide c b ]
