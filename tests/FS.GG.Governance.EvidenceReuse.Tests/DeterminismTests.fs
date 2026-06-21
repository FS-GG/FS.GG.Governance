module FS.GG.Governance.EvidenceReuse.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse.Tests.Support

// Determinism + covered-artifact SET semantics (SC-002). The decision is a deterministic function of the
// candidate and the store; reordering/duplicating covered artifacts in either changes nothing (inherited
// from F029 `matches`/`diff`).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fscheckConfig "decide c s is identical every time (SC-002)"
          <| fun (c: FreshnessInputs) (s: ReuseStore) -> EvidenceReuse.decide c s = EvidenceReuse.decide c s

          test "reordering/duplicating CoveredArtifacts in the CANDIDATE never changes the decision (SC-002)" {
              let store = storeOf [ baseInputs, E1 ]
              // base has [h2; h1; h1]; same set, different order + extra duplicate.
              let shuffled =
                  { baseInputs with CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }

              Expect.equal
                  (EvidenceReuse.decide shuffled store)
                  (EvidenceReuse.decide baseInputs store)
                  "covered-artifact order/dup in the candidate must not change the decision"
          }

          test "reordering/duplicating CoveredArtifacts in a STORED entry never changes the decision (SC-002)" {
              let storeOrdered = storeOf [ baseInputs, E1 ]

              let entryShuffled =
                  { baseInputs with CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }

              let storeShuffled = storeOf [ entryShuffled, E1 ]

              Expect.equal
                  (EvidenceReuse.decide baseInputs storeShuffled)
                  (EvidenceReuse.decide baseInputs storeOrdered)
                  "covered-artifact order/dup in a stored entry must not change the decision"
          }

          testPropertyWithConfig fscheckConfig "any same-set permutation of the candidate's covered artifacts ⇒ identical decision (SC-002)"
          <| fun (c: FreshnessInputs) (s: ReuseStore) ->
              let perm = FsCheck.Gen.sample 0 1 (samePermutationOf c.CoveredArtifacts) |> List.head
              let permuted = { c with CoveredArtifacts = perm }
              EvidenceReuse.decide permuted s = EvidenceReuse.decide c s ]
